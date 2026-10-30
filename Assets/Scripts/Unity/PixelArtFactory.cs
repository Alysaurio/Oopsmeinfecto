using System;
using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Runtime-generated provisional pixel art. Everything uses point filtering and a 1-unit
    /// world scale so the grid logic never depends on sprite resolution.
    /// </summary>
    public static class PixelArtFactory
    {
        private static Sprite _white;
        private static Sprite _floorA;
        private static Sprite _floorB;
        private static Sprite _wall;
        private static Sprite _damage;
        private static Sprite _switch;
        private static Sprite _player;
        private static Sprite _zombie;
        private static Sprite _arrow;
        private static Material _spriteMaterial;

        // IMPORTANT: these caches must use Unity's overloaded == null check, not ??=.
        // The project runs with "Enter Play Mode Options" + Disable Domain Reload, so the
        // static fields survive between Play sessions while the runtime sprites are destroyed
        // when Play mode stops. A plain reference check (??=) would keep returning the
        // destroyed sprite and every SpriteRenderer would render nothing (world invisible).
        public static Sprite WhiteSprite
        {
            get { if (_white == null) _white = CreateSolidSprite(1, Color.white); return _white; }
        }

        public static Sprite FloorA
        {
            get { if (_floorA == null) _floorA = CreateFloor("FloorA", 255, 220, 240); return _floorA; }
        }

        public static Sprite FloorB
        {
            get { if (_floorB == null) _floorB = CreateFloor("FloorB", 235, 205, 225); return _floorB; }
        }

        public static Sprite WallSprite
        {
            get { if (_wall == null) _wall = CreateWall(); return _wall; }
        }

        public static Sprite DamageSprite
        {
            get { if (_damage == null) _damage = CreateDamage(); return _damage; }
        }

        public static Sprite SwitchSprite
        {
            get { if (_switch == null) _switch = CreateSwitch(); return _switch; }
        }

        public static Sprite PlayerSprite
        {
            get { if (_player == null) _player = CreatePlayer(); return _player; }
        }

        public static Sprite ZombieSprite
        {
            get { if (_zombie == null) _zombie = CreateZombie(); return _zombie; }
        }

        public static Sprite ArrowSprite
        {
            get { if (_arrow == null) _arrow = CreateArrow(); return _arrow; }
        }

        public static Material SpriteMaterial
        {
            get
            {
                if (_spriteMaterial != null) return _spriteMaterial;
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader != null)
                {
                    _spriteMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                }
                return _spriteMaterial;
            }
        }

        /// <summary>
        /// Clears the caches at the start of every Play session. With domain reload disabled the
        /// statics keep pointing at objects destroyed when the previous session ended.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCaches()
        {
            _white = null;
            _floorA = null;
            _floorB = null;
            _wall = null;
            _damage = null;
            _switch = null;
            _player = null;
            _zombie = null;
            _arrow = null;
            _spriteMaterial = null;
        }

        private static Sprite CreateSolidSprite(int size, Color color)
        {
            var pixels = new Color32[size * size];
            var c = (Color32)color;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
            return MakeSprite("Solid", size, pixels);
        }

        private static Sprite CreateFloor(string name, byte baseValue, byte border, byte dot)
        {
            const int size = 16;
            var p = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte v = baseValue;
                    if (x == 0 || y == 0 || x == size - 1 || y == size - 1) v = border;
                    if ((x == 7 || x == 8) && (y == 7 || y == 8)) v = dot;
                    p[y * size + x] = new Color32(v, v, v, 255);
                }
            }
            return MakeSprite(name, size, p);
        }

        private static Sprite CreateWall()
        {
            const int size = 16;
            var p = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte v = 150;
                    int row = y / 4;
                    int offset = (row % 2) * 4;
                    if (y % 4 == 0) v = 95;
                    if ((x + offset) % 8 == 0) v = 105;
                    if (y == size - 1 || x == 0) v = 180;
                    p[y * size + x] = new Color32(v, v, v, 255);
                }
            }
            return MakeSprite("Wall", size, p);
        }

        private static Sprite CreateDamage()
        {
            const int size = 16;
            var p = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wave = Mathf.Sin((x + y) * 0.9f) * 0.5f + 0.5f;
                    byte v = (byte)(120 + wave * 80);
                    p[y * size + x] = new Color32((byte)(v * 0.45f), v, (byte)(v * 0.35f), 255);
                }
            }
            DrawDisc(p, size, 4, 4, 2, C(170, 255, 150));
            DrawDisc(p, size, 11, 10, 2, C(150, 255, 130));
            DrawDisc(p, size, 7, 12, 1, C(200, 255, 180));
            return MakeSprite("Damage", size, p);
        }

        private static Sprite CreateSwitch()
        {
            const int size = 16;
            var p = new Color32[size * size];
            FillRect(p, size, 3, 2, 10, 11, C(80, 90, 100));
            FillRect(p, size, 4, 3, 8, 9, C(120, 135, 145));
            FillRect(p, size, 5, 5, 6, 5, C(40, 220, 210));
            FillRect(p, size, 5, 10, 6, 1, C(60, 180, 175));
            FillRect(p, size, 7, 13, 2, 2, C(90, 100, 110));
            FillRect(p, size, 7, 15, 2, 1, C(255, 220, 90));
            FillRect(p, size, 2, 1, 12, 1, C(55, 60, 70));
            return MakeSprite("Switch", size, p);
        }

        private static Sprite CreatePlayer()
        {
            const int size = 16;
            var p = new Color32[size * size];
            // hair / head
            FillRect(p, size, 5, 11, 6, 4, C(235, 195, 145));
            FillRect(p, size, 5, 14, 6, 1, C(65, 45, 35));
            FillRect(p, size, 5, 12, 1, 2, C(65, 45, 35));
            FillRect(p, size, 10, 12, 1, 2, C(65, 45, 35));
            SetP(p, size, 6, 12, C(25, 30, 40));
            SetP(p, size, 9, 12, C(25, 30, 40));
            // shirt
            FillRect(p, size, 5, 6, 6, 5, C(45, 105, 200));
            FillRect(p, size, 3, 6, 2, 4, C(235, 195, 145));
            FillRect(p, size, 11, 6, 2, 4, C(235, 195, 145));
            // trousers / shoes
            FillRect(p, size, 5, 2, 2, 4, C(45, 50, 70));
            FillRect(p, size, 9, 2, 2, 4, C(45, 50, 70));
            FillRect(p, size, 4, 1, 3, 1, C(25, 25, 30));
            FillRect(p, size, 9, 1, 3, 1, C(25, 25, 30));
            return MakeSprite("Player", size, p);
        }

        private static Sprite CreateZombie()
        {
            const int size = 16;
            var p = new Color32[size * size];
            // head
            FillRect(p, size, 5, 11, 6, 4, C(105, 175, 90));
            FillRect(p, size, 5, 14, 6, 1, C(45, 70, 40));
            SetP(p, size, 6, 12, C(255, 70, 50));
            SetP(p, size, 9, 12, C(255, 70, 50));
            FillRect(p, size, 6, 11, 4, 1, C(50, 60, 35));
            // torn shirt
            FillRect(p, size, 5, 6, 6, 5, C(90, 65, 100));
            FillRect(p, size, 4, 7, 1, 3, C(70, 50, 80));
            FillRect(p, size, 11, 7, 1, 3, C(70, 50, 80));
            // arms reaching out
            FillRect(p, size, 1, 8, 4, 2, C(105, 175, 90));
            FillRect(p, size, 11, 8, 4, 2, C(105, 175, 90));
            // legs
            FillRect(p, size, 5, 2, 2, 4, C(55, 50, 60));
            FillRect(p, size, 9, 2, 2, 4, C(55, 50, 60));
            FillRect(p, size, 4, 1, 3, 1, C(35, 35, 40));
            FillRect(p, size, 9, 1, 3, 1, C(35, 35, 40));
            return MakeSprite("Zombie", size, p);
        }

        private static Sprite CreateArrow()
        {
            const int size = 16;
            var p = new Color32[size * size];
            var white = C(255, 255, 255);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Upward arrow: triangular head and a 3px stem.
                    bool head = y >= 9 && y <= 14 && Mathf.Abs(x - 7.5f) <= (14 - y) * 0.85f;
                    bool stem = y >= 2 && y <= 9 && x >= 6 && x <= 9;
                    if (head || stem) p[y * size + x] = white;
                }
            }
            return MakeSprite("Arrow", size, p);
        }

        private static Sprite MakeSprite(string name, int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static Color32 C(int r, int g, int b, int a = 255)
        {
            return new Color32((byte)r, (byte)g, (byte)b, (byte)a);
        }

        private static void SetP(Color32[] pixels, int size, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            pixels[y * size + x] = color;
        }

        private static void FillRect(Color32[] pixels, int size, int x, int y, int w, int h, Color32 color)
        {
            for (int yy = y; yy < y + h; yy++)
            {
                for (int xx = x; xx < x + w; xx++)
                {
                    SetP(pixels, size, xx, yy, color);
                }
            }
        }

        private static void DrawDisc(Color32[] pixels, int size, int cx, int cy, int radius, Color32 color)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius) SetP(pixels, size, cx + x, cy + y, color);
                }
            }
        }
    }
}
