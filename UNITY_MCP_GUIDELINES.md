# UNITY_MCP_GUIDELINES.md

> **Guía de contexto y reglas de sistema para OpenCode al trabajar con el proyecto Unity `Oopsmeinfecto`.**
> Este documento es la fuente de verdad para la interacción con el Editor mediante el servidor MCP de Unity.
> Si una instrucción del usuario entra en conflicto con estas reglas, se debe avisar antes de actuar.

| Campo | Valor |
|---|---|
| Proyecto | `Oopsmeinfecto` |
| Ruta local | `A:\Proyectos\Unity\Oopsmeinfecto` |
| Versión de Unity | `6000.6.7f1` |
| Render Pipeline | Universal Render Pipeline (2D) |
| Escena principal | `Assets/Scenes/SampleScene.unity` |
| Endpoint MCP | `http://127.0.0.1:8080/mcp` |
| Paquete MCP | `com.coplaydev.unity-mcp` (MCPForUnity) |
| Última actualización | 2026-10-29 |

---

## 1. Configuración del Servidor MCP

### 1.1 Configuración de OpenCode

El servidor MCP de Unity se registra como servidor remoto en la configuración de OpenCode:

```json
{
  "mcp": {
    "unityMCP": {
      "type": "remote",
      "url": "http://127.0.0.1:8080/mcp",
      "enabled": true
    }
  }
}
```

### 1.2 Requisitos de conexión

1. El **Unity Editor debe estar abierto** con el proyecto `Oopsmeinfecto` cargado.
2. El paquete **MCPForUnity** debe estar instalado y el puente MCP activo.
3. El servidor debe escuchar en `127.0.0.1:8080` (transporte HTTP/SSE).
4. Debe existir **una sola instancia de Unity** conectada, o fijarse explícitamente la instancia activa.

> [!IMPORTANT]
> Si hay varias instancias de Unity abiertas, el servidor puede rechazar las llamadas.
> Usa `set_active_instance` con el identificador `Nombre@hash` antes de cualquier operación.

### 1.3 Recursos de estado (solo lectura)

Los recursos se leen por **URI exacta**, no por nombre. El contenido viene envuelto en `data`.

| Recurso | Uso |
|---|---|
| `mcpforunity://editor/state` | Estado del Editor: Play Mode, escena activa, compilación, `ready_for_tools`. |
| `mcpforunity://project/info` | Ruta, nombre y versión del proyecto. |
| `mcpforunity://custom-tools` | Herramientas personalizadas disponibles en el proyecto. |
| `mcpforunity://instances` | Instancias de Unity conectadas. |
| `mcpforunity://tests` | Estado de las pruebas. |

### 1.4 Verificación de conexión

Antes de cualquier cambio:

1. Leer `mcpforunity://editor/state`.
2. Confirmar `data.advice.ready_for_tools == true`.
3. Confirmar `data.editor.active_scene.path`.
4. Confirmar que `data.editor.play_mode.is_playing == false` (salvo que se esté probando).

> [!WARNING]
> No ejecutar cambios estructurales mientras `is_compiling`, `is_updating` o `is_domain_reload_pending` sean `true`.
> Esperar a que el Editor esté en estado `idle`.

---

## 2. Capacidades del MCP de Unity (Mapeo de Herramientas)

### 2.1 Alcance

El agente puede interactuar directamente con el Editor de Unity mediante el servidor MCP en
`http://127.0.0.1:8080/mcp`. El MCP es la **fuente de verdad** del estado del proyecto: la escena,
los componentes y los assets del Editor mandan sobre cualquier suposición del agente.

### 2.2 Acciones permitidas

- **Inspeccionar** la jerarquía de la escena y los componentes de cada GameObject.
- **Crear** GameObjects, componentes, Prefabs, materiales, texturas y assets.
- **Modificar** propiedades visibles en el Inspector (transform, componentes, referencias serializadas).
- **Eliminar** objetos, componentes o assets innecesarios o duplicados.
- **Gestionar** la estructura de la escena: padres, posiciones, capas, tags, orden de ejecución.
- **Guardar** la escena y refrescar/compilar el proyecto.
- **Ejecutar** código C# de Editor de forma controlada (`execute_code`) cuando no exista una herramienta estructurada.
- **Controlar** Play Mode para verificar el resultado.
- **Leer** la consola para detectar errores y warnings.

### 2.3 Tabla de herramientas principales

| Herramienta | Categoría | Uso recomendado |
|---|---|---|
| `manage_scene` | Escena | `get_hierarchy`, `get_active`, `create`, `load`, `save`, `validate`, `scene_view_frame`. |
| `find_gameobjects` | Búsqueda | Localizar por nombre, tag, layer, componente, ruta o instance ID. |
| `manage_gameobject` | GameObjects | `create`, `modify`, `delete`, `duplicate`, `move_relative`, `look_at`. |
| `manage_components` | Componentes | `add`, `remove`, `set_property` (incluye referencias serializadas). |
| `manage_prefabs` | Prefabs | Crear desde GameObject, inspeccionar jerarquía, modificar contenido, instanciar. |
| `manage_asset` | Assets | Importar, crear, modificar, mover, renombrar, borrar y buscar assets. |
| `manage_editor` | Editor | `play`, `pause`, `stop`, tags, layers, `undo`, `redo`. |
| `manage_camera` | Cámara | Configuración de cámara y Cinemachine. |
| `manage_material` | Materiales | Shaders, colores y propiedades de materiales. |
| `manage_physics` | Física | Ajustes globales, matrices de colisión, joints y consultas. |
| `manage_animation` | Animación | Animator y creación de AnimationClips. |
| `manage_vfx` | VFX | ParticleSystem, VisualEffect, LineRenderer, TrailRenderer. |
| `manage_graphics` | Render | Volúmenes, post-procesado, light baking, pipeline. |
| `manage_ui` | UI Toolkit | UXML, USS y `UIDocument`. **Solo capa de UI/UX.** |
| `manage_texture` | Texturas | Texturas procedurales (fills, patrones, checkerboard). |
| `manage_shader` | Shaders | Crear, leer y modificar shaders. |
| `manage_scriptable_object` | Datos | Crear y modificar ScriptableObjects por `SerializedObject`. |
| `manage_script` | Scripts | Router de compatibilidad para operaciones de scripts. |
| `create_script` | Scripts | Crear un script C# nuevo. |
| `apply_text_edits` | Scripts | Ediciones de texto exactas y acotadas. |
| `script_apply_edits` | Scripts | Ediciones estructuradas por método/clase. |
| `validate_script` | Scripts | Diagnóstico de compilación de un script. |
| `read_console` | Diagnóstico | Leer o limpiar errores, warnings y logs. |
| `refresh_unity` | Diagnóstico | Refrescar assets y solicitar compilación. |
| `run_tests` / `get_test_job` | Testing | Ejecutar pruebas y consultar el job asíncrono. |
| `execute_code` | Avanzado | Ejecutar C# de Editor como cuerpo de método. **Alto riesgo.** |
| `batch_execute` | Avanzado | Agrupar varios comandos MCP en una sola llamada. |
| `unity_docs` | Referencia | Consultar documentación oficial de Unity. |
| `unity_reflect` | Referencia | Inspeccionar la API viva de Unity por reflexión. |
| `set_active_instance` | Sesión | Fijar la instancia de Unity activa cuando hay varias. |

### 2.4 Herramientas de alto riesgo

> [!CAUTION]
> `execute_code`, `batch_execute`, `manage_asset` con acción `delete` y `manage_gameobject` con acción `delete`
> pueden modificar o destruir estado de forma irreversible. Úsalos solo después de inspeccionar y con un objetivo claro.

Reglas para `execute_code`:

- Preferir siempre las herramientas estructuradas (`manage_gameobject`, `manage_components`, etc.).
- Usarlo solo para operaciones que no tengan herramienta dedicada (por ejemplo, asignar varias referencias serializadas de forma atómica).
- No usarlo para crear la escena final si puede hacerse con herramientas estructuradas.
- Guardar la escena y revisar la consola inmediatamente después.

### 2.5 Recursos vs. herramientas

| Necesidad | Usar |
|---|---|
| Leer estado del Editor | **Recurso** `mcpforunity://editor/state` |
| Leer información del proyecto | **Recurso** `mcpforunity://project/info` |
| Inspeccionar jerarquía | **Herramienta** `manage_scene` (`get_hierarchy`) |
| Crear o modificar objetos | **Herramienta** `manage_gameobject` / `manage_components` |
| Guardar cambios | **Herramienta** `manage_scene` (`save`) |
| Verificar compilación | **Herramienta** `read_console` / `refresh_unity` |

---

## 3. Reglas Fundamentales de Desarrollo (Strict Rules)

### Regla 1 — Persistencia y visibilidad en el Editor

> [!IMPORTANT]
> **Todos los GameObjects, estructuras y elementos del juego DEBEN ser creados e instanciados directamente en el Editor de la escena.**

- Está **PROHIBIDO** generar elementos exclusivamente por código en tiempo de ejecución (`Instantiate` en Play Mode).
- El usuario debe poder **ver, seleccionar, mover y modificar manualmente** cualquier objeto desde la interfaz del Editor **sin presionar Play**.
- Las referencias entre objetos deben quedar **serializadas en la escena** (`[SerializeField]`, `SerializedObject`), no resueltas solo por búsquedas en runtime.
- Los objetos principales deben existir como raíces o hijos persistentes de la escena, no como resultado de un bootstrap de código.

**Excepciones permitidas (únicas):**

| Excepción | Condición |
|---|---|
| Proyectiles | Objetos dinámicos que se crean y destruyen durante el juego. |
| Efectos temporales | Partículas, destellos, daño flotante, VFX de corta duración. |
| Contenido procedural de nivel | Tablero, entidades y barra de energía generados al cargar cada nivel, siempre que sus raíces persistentes estén en la escena. |

> [!NOTE]
> La excepción de contenido procedural es **específica de este proyecto** y está acotada a los hijos de
> `Game/Level Content`. No puede usarse para justificar la creación de objetos principales, cámaras,
> canvases, sistemas o menús en runtime.

> [!WARNING]
> `GameBootstrap` existe únicamente como red de seguridad para escenas sin jerarquía.
> No es la vía principal de creación. La escena `SampleScene` ya contiene la jerarquía real.

**Ejemplo correcto:** crear el objeto `Game` con sus componentes en la escena y asignar sus referencias serializadas.

**Ejemplo incorrecto:** dejar la escena vacía y crear `Game`, `HUD Canvas` y `Defeat Canvas` desde `Awake()` o `[RuntimeInitializeOnLoadMethod]`.

### Regla 2 — Arquitectura tecnológica

> [!IMPORTANT]
> **La lógica de juego y el renderizado deben ser 100 % nativos en C# y componentes de Unity.**

- Usar `MonoBehaviour`, `GameObject`, `Transform`, `ScriptableObject`, `SpriteRenderer`, `Camera`, `Canvas`, `Rigidbody2D`, etc.
- Queda **prohibido** usar HTML/CSS/JS para la lógica central o el renderizado del juego.
- Las tecnologías web/HTML quedan restringidas **exclusivamente** a la capa de UI/UX o menús.
- **UI Toolkit** (`manage_ui`, UXML/USS) está permitido **solo** para UI/UX.
- **uGUI** (`Canvas`, `Image`, `Text`, `CanvasScaler`) es UI nativa de Unity y está permitido para HUD y menús.
- No mezclar lógica de gameplay dentro de la capa de UI.

**Separación obligatoria:**

| Capa | Tecnología permitida |
|---|---|
| Gameplay | C# + componentes nativos de Unity |
| Renderizado del mundo | Sprites, SpriteRenderer, Camera, URP 2D |
| Datos y tuning | ScriptableObjects (`GameTuning`) |
| HUD / menús | uGUI nativo o UI Toolkit |
| Lógica de UI | C# (nunca JS) |

### Regla 3 — Mapeo y verificación

> [!IMPORTANT]
> **Antes de ejecutar cualquier cambio masivo, la IA debe inspeccionar la escena actual mediante el MCP para evitar duplicar objetos o romper la jerarquía existente.**

Secuencia obligatoria:

1. Leer `mcpforunity://editor/state`.
2. Obtener la jerarquía con `manage_scene` → `get_hierarchy`.
3. Buscar duplicados con `find_gameobjects` (por nombre, componente o ruta).
4. Revisar la consola con `read_console` antes de tocar nada.
5. Aplicar cambios de forma incremental.
6. Guardar la escena con `manage_scene` → `save`.
7. Refrescar/compilar si se modificaron scripts (`refresh_unity`).
8. Verificar de nuevo jerarquía, referencias y consola.
9. Probar en Play Mode solo después de que la escena esté guardada y limpia.

### 3.4 Prohibiciones absolutas

- ❌ Crear la jerarquía principal del juego solo desde código en runtime.
- ❌ Duplicar `Game`, `HUD Canvas`, `Defeat Canvas` o `EventSystem`.
- ❌ Dejar referencias serializadas en `null` cuando el objeto existe en la escena.
- ❌ Usar HTML/CSS/JS para gameplay o renderizado del mundo.
- ❌ Ejecutar cambios masivos sin inspeccionar la escena.
- ❌ Guardar una escena con errores de compilación.
- ❌ Añadir assets o carpetas fuera de `Assets/` sin justificación.
- ❌ Modificar `Library/`, `Temp/` o `ProjectSettings/` manualmente sin necesidad.

### 3.5 Excepciones permitidas

- ✅ Proyectiles y VFX temporales creados en runtime.
- ✅ Contenido procedural de nivel bajo una raíz persistente (`Game/Level Content`).
- ✅ Fallbacks defensivos en runtime si falta una referencia de escena (deben registrar warning).
- ✅ Herramientas de Editor (`Assets/Editor/`) para validación o setup reproducible.

---

## 4. Contexto del Proyecto

### 4.1 Stack técnico

| Elemento | Valor |
|---|---|
| Unity | `6000.6.7f1` |
| URP | `17.6.0` (2D Renderer) |
| Input | Input System `1.20.1` |
| UI | uGUI `2.6.0` + UI Toolkit disponible |
| Testing | Unity Test Framework `1.8.0` |
| MCP | MCPForUnity `3.4.11` |

### 4.2 Escena activa: `Assets/Scenes/SampleScene.unity`

Jerarquía persistente esperada:

```text
SampleScene
├── Main Camera          (tag MainCamera, ortográfica, AudioListener, URP 2D)
├── Global Light 2D      (Light2D)
├── Game                 (GameFlowController, LevelRenderer, HUDController,
│   │                     DefeatPresentationController, TurnManager)
│   └── Level Content    (raíz persistente del contenido procedural)
├── HUD Canvas           (Canvas + CanvasScaler + GraphicRaycaster, orden 100)
├── Defeat Canvas        (Canvas + CanvasScaler + GraphicRaycaster + CanvasGroup,
│                         orden 300, inactivo hasta derrota)
└── EventSystem          (EventSystem + InputSystemUIInputModule)
```

> [!NOTE]
> `HUD Canvas` y `Defeat Canvas` están vacíos en el Editor porque sus widgets se construyen en `Awake()`.
> Los **Canvas raíz** son persistentes y visibles; los widgets son capa de UI y pueden generarse en runtime.

### 4.3 Scripts clave

| Script | Responsabilidad |
|---|---|
| `Assets/Scripts/Unity/GameFlowController.cs` | Ciclo de vida del nivel, victoria, derrota, reinicio y siguiente nivel. |
| `Assets/Scripts/Unity/LevelRenderer.cs` | Construcción visual del tablero, entidades y cámara. |
| `Assets/Scripts/Unity/HUDController.cs` | HUD uGUI: nivel, estado, historial e indicador de carga. |
| `Assets/Scripts/Unity/DefeatPresentationController.cs` | Overlay de derrota con fade y callback de reinicio. |
| `Assets/Scripts/Unity/TurnManager.cs` | Entrada de teclado y turnos automáticos. |
| `Assets/Scripts/Unity/SegmentedEnergyBar.cs` | Barra de energía segmentada que sigue al jugador. |
| `Assets/Scripts/Unity/GameTuning.cs` | ScriptableObject de tuning (generación, tiempos, colores). |
| `Assets/Scripts/Unity/PixelArtFactory.cs` | Sprites pixel-art generados en runtime. |
| `Assets/Scripts/Unity/GameBootstrap.cs` | Red de seguridad para escenas sin `GameFlowController`. |
| `Assets/Editor/ProjectValidation.cs` | Validación de generación con 10 semillas. |

### 4.4 Contenido procedural permitido

Bajo `Game/Level Content`, el `LevelRenderer` crea al cargar cada nivel:

- `Board` con los tiles del nivel.
- `Player` y `Zombie`.
- `Energy Bar` con sus segmentos.

Esto es **contenido de nivel**, no la jerarquía principal. La raíz `Level Content` es persistente y visible en el Editor.
No convertir el tablero completo en objetos de escena editables salvo que el usuario lo pida explícitamente.

### 4.5 Assets relevantes

| Ruta | Uso |
|---|---|
| `Assets/Resources/GameTuning.asset` | Configuración de gameplay y colores. |
| `Assets/Resources/BackGrounds/` | Fondos del juego. |
| `Assets/Resources/Sounds/` | Música y efectos. |
| `Assets/Scenes/SampleScene.unity` | Escena principal. |
| `Assets/Settings/` | URP, Input System y volumen global. |

### 4.6 Convenciones

- Namespace del juego: `Oopsmeinfecto`.
- Scripts de runtime en `Assets/Scripts/Unity/`.
- Lógica pura en `Assets/Scripts/Core/`.
- Herramientas de Editor en `Assets/Editor/`.
- Nombres de clases y métodos en PascalCase; campos privados con `_camelCase`.
- Comentarios y UI en español; código en inglés cuando sea consistente con el archivo.

---

## 5. Flujo de Trabajo Obligatorio

### 5.1 Antes de cambiar

- [ ] Confirmar que el Editor está listo (`ready_for_tools`).
- [ ] Leer la jerarquía actual.
- [ ] Buscar duplicados y objetos huérfanos.
- [ ] Revisar la consola.
- [ ] Identificar las referencias serializadas afectadas.
- [ ] Confirmar que la escena está guardada o que hay un punto de retorno.

### 5.2 Durante el cambio

- [ ] Usar herramientas estructuradas siempre que sea posible.
- [ ] Crear o modificar objetos en la escena, no solo en runtime.
- [ ] Mantener una única instancia de `Game`, `HUD Canvas`, `Defeat Canvas` y `EventSystem`.
- [ ] Asignar referencias serializadas después de crear los objetos.
- [ ] No dejar componentes duplicados del mismo tipo en el mismo GameObject.
- [ ] Aplicar cambios pequeños y verificables.

### 5.3 Después del cambio

- [ ] Guardar la escena.
- [ ] Refrescar/compilar si se tocaron scripts.
- [ ] Leer la consola y corregir errores/warnings propios.
- [ ] Verificar la jerarquía final.
- [ ] Verificar que las referencias serializadas no sean `null`.
- [ ] Probar en Play Mode y salir.
- [ ] Confirmar que la escena quedó limpia tras salir de Play Mode.

### 5.4 Ejemplos de operaciones MCP

**Inspeccionar la escena:**

```text
manage_scene { action: "get_hierarchy", include_transform: true, max_depth: 10 }
```

**Buscar duplicados:**

```text
find_gameobjects { search_term: "GameFlowController", search_method: "by_component" }
find_gameobjects { search_term: "HUD Canvas", search_method: "by_name" }
```

**Crear un GameObject persistente:**

```text
manage_gameobject {
  action: "create",
  name: "Game",
  components_to_add: ["GameFlowController", "LevelRenderer", "HUDController",
                      "DefeatPresentationController", "TurnManager"]
}
```

**Asignar una referencia serializada:**

```text
manage_components {
  action: "set_property",
  target: "Game",
  component_type: "HUDController",
  property: "_sceneCanvas",
  value: <instanceID de HUD Canvas>
}
```

**Guardar la escena:**

```text
manage_scene { action: "save" }
```

**Verificar compilación:**

```text
refresh_unity { mode: "force", scope: "scripts", compile: "request", wait_for_ready: true }
read_console { action: "get", types: ["error", "warning"], count: 50, format: "detailed" }
```

**Probar en Play Mode:**

```text
manage_editor { action: "play" }
manage_editor { action: "stop" }
```

> [!CAUTION]
> Nunca dejes el Editor en Play Mode al terminar una tarea, salvo que el usuario lo pida.
> Al salir de Play Mode, verifica que la escena no haya quedado sucia con objetos temporales.

---

## 6. Diagnóstico y Solución de Problemas

| Síntoma | Causa probable | Acción |
|---|---|---|
| El MCP no responde | Unity cerrado o servidor caído | Abrir Unity, verificar el puente MCP en `127.0.0.1:8080`. |
| Error de múltiples instancias | Varios Unity abiertos | `set_active_instance` con `Nombre@hash`. |
| `ready_for_tools = false` | Compilación o refresh en curso | Esperar y volver a leer `mcpforunity://editor/state`. |
| Objetos duplicados | Bootstrap + escena, o setup repetido | `find_gameobjects`, eliminar duplicados, guardar. |
| Referencias `null` en Inspector | Campo no serializado o asignado solo en runtime | Añadir `[SerializeField]` y asignar con `manage_components`. |
| HUD invisible en Play Mode | Canvas sin cámara, orden incorrecto o script no ejecutado | Revisar `Canvas`, `sortingOrder`, `HUDController.Build`. |
| Errores de compilación | Scripts modificados | `read_console` + `validate_script`, corregir y recompilar. |
| Escena sucia tras Play Mode | Objetos creados sin raíz persistente | Revisar `Clear()`/`OnDestroy` y guardar la escena limpia. |
| La generación falla | Tuning o validador | Ejecutar `Oopsmeinfecto/Validate generation (10 seeds)`. |

---

## 7. Contrato de Respuesta del Agente

Toda intervención debe reportar, como mínimo:

1. **Inspección realizada:** escena, jerarquía y consola.
2. **Hallazgos:** objetos faltantes, duplicados o referencias rotas.
3. **Cambios aplicados:** objetos, componentes y referencias modificadas.
4. **Verificación:** compilación, consola y prueba en Play Mode.
5. **Estado final:** escena guardada, Play Mode detenido y pendientes (si los hay).

> [!NOTE]
> Si no se puede verificar algo (por ejemplo, una prueba visual), decirlo explícitamente.
> No afirmar que algo funciona sin haberlo comprobado en el Editor.

---

## 8. Registro de Cambios

| Fecha | Cambio |
|---|---|
| 2026-10-29 | Creación de la guía y consolidación de la jerarquía persistente de `SampleScene`. |
| 2026-10-29 | `GameFlowController`, `HUDController`, `DefeatPresentationController` y `LevelRenderer` usan referencias serializadas de escena. |
| 2026-10-29 | `GameBootstrap` queda como red de seguridad, no como vía principal de creación. |

---

## 9. Resumen Ejecutivo

- **MCP primero:** inspeccionar antes de tocar.
- **Escena primero:** los objetos principales viven en el Editor.
- **C# nativo:** nada de HTML/CSS/JS en gameplay.
- **UI aparte:** uGUI o UI Toolkit solo para HUD/menús.
- **Guardar y verificar:** escena guardada, consola limpia, Play Mode probado y detenido.
