// Custom/RetroLitSprite
// 2.5D grid lighting for the tile layers and characters of Oopsmeinfecto.
// Purely visual: it never affects game logic. Data is fed globally by GridLightManager.
//
// _LitMode:
//   0 = Floor     (baldosa*, DG*, grieta*, moho*): point lights + wall occlusion (ray vs wall AABB grid,
//                  22.5 degree soft penumbra) + elliptical shadows cast by Player / Zombie.
//   1 = Wall top  (paredarriba*): only the lower 15 px front face (68% of the 22 px useful height)
//                  receives light, only from lights below it and with a clear line of sight.
//                  The upper 7 px (roof, 32%) receives 0 light. Computed in world space.
//   2 = Unlit     (paredabajo*, paredizquierda*, paredderecha*, esquinaizquierda*, esquinaderecha*):
//                  light multiplier = 0, keeps its dark base color.
//   3 = Entity    (Player / Zombie): per-renderer light computed on the CPU (directional Y rule).
Shader "Custom/RetroLitSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _LitMode ("Lit Mode (0 Floor, 1 WallTop, 2 Unlit, 3 Entity)", Float) = 0
        _LightGain ("Light Gain", Float) = 1.5
        _WallUsefulBottom ("Wall Useful Bottom (cell-local, 0 px)", Float) = 0
        _WallUsefulTop ("Wall Useful Top (cell-local, 22/32 px)", Float) = 0.6875
        _WallFrontRatio ("Wall Front Ratio (15/22 px)", Float) = 0.6818
        [PerRendererData] _EntityLight ("Entity Light", Color) = (0,0,0,0)
        [PerRendererData] _EntityDarken ("Entity Darken", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define RL_MAX_LIGHTS 16
            #define RL_MAX_SHADOWS 8
            #define RL_MAX_STEPS 8

            sampler2D _MainTex;
            float4 _Color;
            float _LitMode;
            float _LightGain;
            float _WallUsefulBottom;
            float _WallUsefulTop;
            float _WallFrontRatio;
            float4 _EntityLight;
            float _EntityDarken;

            // ---- Globals written by GridLightManager ----
            float _RL_Enabled;
            float _RL_LightCount;
            float4 _RL_LightPos[RL_MAX_LIGHTS];    // xy = world position, z = radius, w = intensity
            float4 _RL_LightColor[RL_MAX_LIGHTS];  // rgb = color
            float _RL_ShadowCount;
            float4 _RL_ShadowA[RL_MAX_SHADOWS];    // xy = shadow origin (feet), zw = direction away from light
            float4 _RL_ShadowB[RL_MAX_SHADOWS];    // x = length, y = half width, z = strength, w = light index
            sampler2D _RL_OccluderTex;             // 1 texel per cell, r > 0.5 = wall (occluder AABB)
            float4 _RL_OccluderSize;               // x = width, y = height, z = 1/width, w = 1/height
            float4 _RL_GridOrigin;                 // xy = world position of the centre of cell (0,0)

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            // Grid space: cell (i, j) covers [i, i+1) x [j, j+1). Cells outside the board are walls.
            float2 ToGrid(float2 world)
            {
                return world - _RL_GridOrigin.xy + 0.5;
            }

            float IsOccluder(float2 cell)
            {
                float inBounds = step(0.0, cell.x) * step(0.0, cell.y) *
                                 step(cell.x, _RL_OccluderSize.x - 0.5) * step(cell.y, _RL_OccluderSize.y - 0.5);
                float2 uv = clamp((cell + 0.5) * _RL_OccluderSize.zw, 0.0, 1.0);
                float sampleOcc = step(0.5, tex2Dlod(_RL_OccluderTex, float4(uv, 0.0, 0.0)).r);
                return lerp(1.0, sampleOcc, inBounds);
            }

            // Exact segment vs. wall-AABB test: walks every unit cell (axis aligned box) crossed by the
            // segment p -> l (Amanatides-Woo traversal). Branchless & unrolled for GPU driver / TDR safety.
            float RayVisible(float2 p, float2 l)
            {
                float2 d = l - p;
                float2 cell = floor(p);
                float2 endCell = floor(l);
                float2 stepDir = float2(d.x >= 0.0 ? 1.0 : -1.0, d.y >= 0.0 ? 1.0 : -1.0);
                float2 tDelta = 1.0 / max(abs(d), float2(1e-4, 1e-4));
                float2 f = p - cell;
                float2 tMax = float2(stepDir.x > 0.0 ? 1.0 - f.x : f.x,
                                     stepDir.y > 0.0 ? 1.0 - f.y : f.y) * tDelta;

                float vis = 1.0;
                float active = 1.0;

                [unroll]
                for (int i = 0; i < RL_MAX_STEPS; i++)
                {
                    float stepX = step(tMax.x, tMax.y);
                    float stepY = 1.0 - stepX;
                    float t = stepX * tMax.x + stepY * tMax.y;
                    tMax += float2(stepX, stepY) * tDelta;
                    cell += float2(stepX, stepY) * stepDir;

                    float reachedEnd = step(1.0, t) + step(abs(cell.x - endCell.x) + abs(cell.y - endCell.y), 0.25);
                    active *= step(reachedEnd, 0.5);

                    float occ = IsOccluder(cell);
                    vis *= (1.0 - active * occ);
                    active *= (1.0 - occ);
                }
                return vis;
            }

            // Soft shadow: 3 rays spread over a 22.5 degree cone (+-11.25 deg) around the direction to the
            // light. Wall corners therefore cast diagonal shadows with a smooth 20-25 degree penumbra.
            float SoftVisibility(float2 p, float2 l)
            {
                float2 d = l - p;
                if (dot(d, d) < 1e-6) return 1.0;

                const float ca = 0.9807853; // cos(11.25 deg)
                const float sa = 0.1950903; // sin(11.25 deg)

                float v = 0.40 * RayVisible(p, l);
                v += 0.30 * RayVisible(p, p + float2(d.x * ca - d.y * sa, d.x * sa + d.y * ca));
                v += 0.30 * RayVisible(p, p + float2(d.x * ca + d.y * sa, -d.x * sa + d.y * ca));
                return v;
            }

            float Attenuation(float dist, float radius)
            {
                // Smooth falloff: 1 at the centre, 0 at the radius.
                return 1.0 - smoothstep(0.0, radius, dist);
            }

            float EllipseShadow(float2 world, float4 a, float4 b)
            {
                float2 dir = a.zw;
                float2 perp = float2(-dir.y, dir.x);
                float2 rel = world - a.xy;
                float halfLen = max(b.x * 0.5, 1e-3);
                float2 q = float2((dot(rel, dir) - halfLen) / halfLen, dot(rel, perp) / max(b.y, 1e-3));
                return (1.0 - smoothstep(0.45, 1.0, length(q))) * b.z;
            }

            // Floor: returns rgb = accumulated light, a = darkening from entity shadows.
            float4 FloorLight(float2 world)
            {
                float2 g = ToGrid(world);
                float3 acc = 0.0;
                float shadowDark = 0.0;
                int lightCount = clamp((int)_RL_LightCount, 0, RL_MAX_LIGHTS);
                int shadowCount = clamp((int)_RL_ShadowCount, 0, RL_MAX_SHADOWS);

                for (int i = 0; i < RL_MAX_LIGHTS; i++)
                {
                    if (i >= lightCount) break;
                    float4 lp = _RL_LightPos[i];
                    float dist = distance(world, lp.xy);
                    if (dist >= lp.z) continue;

                    float vis = SoftVisibility(g, ToGrid(lp.xy));
                    if (vis <= 0.0) continue;

                    float att = Attenuation(dist, lp.z) * vis;

                    float sh = 0.0;
                    for (int j = 0; j < RL_MAX_SHADOWS; j++)
                    {
                        if (j >= shadowCount) break;
                        float4 sb = _RL_ShadowB[j];
                        if (abs(sb.w - (float)i) < 0.5)
                            sh = max(sh, EllipseShadow(world, _RL_ShadowA[j], sb));
                    }

                    acc += _RL_LightColor[i].rgb * (lp.w * att * (1.0 - sh));
                    shadowDark = max(shadowDark, sh * att);
                }
                return float4(acc, shadowDark);
            }

            // Front face of paredarriba*: lit only on its lower 15 px, by lights located below it.
            float3 WallTopLight(float2 world)
            {
                float2 g = ToGrid(world);
                float2 cell = floor(g);
                float localY = g.y - cell.y; // 0 = bottom of the 32 px sprite, 1 = top

                float frontTop = _WallUsefulBottom + (_WallUsefulTop - _WallUsefulBottom) * _WallFrontRatio;
                if (localY < _WallUsefulBottom || localY > frontTop) return 0.0; // roof strip: 0 light

                // The vertical face stands on the bottom edge of the wall cell: rays start from that foot
                // line (world X continuous) so adjacent walls are lit as one continuous surface.
                float2 foot = float2(g.x, cell.y - 0.001);
                float2 footWorld = foot + _RL_GridOrigin.xy - 0.5;

                float3 acc = 0.0;
                int lightCount = clamp((int)_RL_LightCount, 0, RL_MAX_LIGHTS);
                for (int i = 0; i < RL_MAX_LIGHTS; i++)
                {
                    if (i >= lightCount) break;
                    float4 lp = _RL_LightPos[i];
                    float2 lg = ToGrid(lp.xy);
                    if (lg.y >= cell.y) continue; // light must come from below

                    float dist = distance(footWorld, lp.xy);
                    if (dist >= lp.z) continue;

                    float2 dir = normalize(lg - foot);
                    float facing = saturate(-dir.y * 1.3);
                    if (facing <= 0.0) continue;

                    float vis = SoftVisibility(foot, lg);
                    acc += _RL_LightColor[i].rgb * (lp.w * Attenuation(dist, lp.z) * vis * facing);
                }
                return acc;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                if (_RL_Enabled < 0.5 || _RL_OccluderSize.x < 1.0 || _RL_OccluderSize.y < 1.0 || c.a <= 0.001) return c;

                if (_LitMode < 0.5)
                {
                    float4 l = FloorLight(i.world);
                    c.rgb = c.rgb * (1.0 + l.rgb * _LightGain) * (1.0 - l.a * 0.35);
                }
                else if (_LitMode < 1.5)
                {
                    c.rgb = c.rgb * (1.0 + WallTopLight(i.world) * _LightGain);
                }
                else if (_LitMode < 2.5)
                {
                    // Never lit: light multiplier = 0.
                }
                else
                {
                    c.rgb = c.rgb * _EntityDarken * (1.0 + _EntityLight.rgb * _LightGain);
                }
                return c;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
