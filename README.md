<p align="center">
  <img src="XiloAdventures.Wpf.Common/Assets/logo.png" alt="XiloAdventures logo" width="360">
</p>

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF-0d5a8f?logo=windows&logoColor=white)
![C#](https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white)
![Estado](https://img.shields.io/badge/Estado-Activo-success)
![Tests](https://img.shields.io/badge/Tests-45%20passing-brightgreen)

XiloAdventures es un ecosistema completo para crear y jugar aventuras conversacionales en C# (.NET 8) con editor visual WPF y cliente de juego. Todo el contenido (salas, objetos, música, imágenes) viaja dentro de un único archivo `.xaw` cifrado y comprimido.

---

## Qué incluye

- **Engine (`XiloAdventures.Engine`)**: modelos de mundo, guardado/carga `.xaw` (zip + cifrado), lógica de puertas/llaves, audio con NAudio (música de mundo y de sala, volúmenes master/música/efectos/voz).
- **Editor WPF (`XiloAdventures.Wpf`)**:
  - Mapa visual con zoom, drag, selección múltiple, iconos de puertas/llaves, tooltip de imagen de sala.
  - Árbol de contenido (salas, salidas, puertas, llaves, objetos, NPCs, misiones).
  - Panel de propiedades en español, con textboxes multilínea, checkboxes y radio centrados.
  - Botón *Play* para probar el mundo; muestra overlay de progreso mientras prepara la partida.
  - Guardado/carga de mundos `.xaw`, undo/redo, búsqueda.
  - **"¡Crea tu aventura!"**: opción especial en la lista de mundos para empezar desde cero.
- **Cliente de juego WPF (`MainWindow`, `StartupWindow`)**:
  - Pantalla inicial con selector de mundos, checks de sonido/IA e overlay de progreso al cargar/iniciar.
  - Ventana de partida con historial, inventario, estados, imagen de sala, música integrada.
  - Al cerrar la partida pregunta si guardar y confirma la salida (pop-ups oscuros).
  - **Autoguardado** automático después de cada comando.
- **LLM opcional**: si el parser no entiende un comando y la opción está activa, consulta un modelo local (requiere Docker Desktop).
  - Confirmación antes de activar la IA con información sobre Docker y descarga de modelos.
- **TTS (voz)**: generación y precarga de voz de las descripciones de salas.
- **Player independiente (`XiloAdventures.Wpf.Player`)**: ejecutable standalone para distribuir juegos sin el editor.

---

## Wiki

- Página principal: [General](https://github.com/xilonoide/XiloAdventures/wiki/general)
- Editor: [Editor](https://github.com/xilonoide/XiloAdventures/wiki/editor)
- Cliente/Player: [Player](https://github.com/xilonoide/XiloAdventures/wiki/player)

---

## Estructura del proyecto

| Proyecto | Descripción |
|----------|-------------|
| `XiloAdventures.Engine` | Core del motor: modelos, parser, guardado/carga, audio |
| `XiloAdventures.Wpf` | Editor visual y pantalla de inicio |
| `XiloAdventures.Wpf.Common` | Componentes UI compartidos (ventanas, estilos) |
| `XiloAdventures.Wpf.Player` | Player standalone para distribución |
| `XiloAdventures.Tests` | Tests unitarios (xUnit) |

### Archivos principales

- `Engine/GameEngine.cs` - Motor de juego principal
- `Engine/Parser.cs` - Parser de comandos del jugador
- `Engine/WorldLoader.cs` - Carga/guardado de mundos `.xaw`
- `Engine/SaveManager.cs` - Guardado/carga de partidas `.xas`
- `Engine/DoorService.cs` - Lógica de puertas y llaves
- `Models/Models.cs` - Modelos de datos (Room, GameObject, Npc, etc.)

---

## Formato de archivos

- **Mundos `.xaw`**: JSON comprimido en ZIP (`world.json`), Base64 y cifrado AES CBC.
  - Clave vacía = sin cifrar; clave de 8 caracteres = cifrado.
- **Partidas `.xas`**: estado del juego cifrado (`GameState`), incluye salas/objetos/NPCs, progreso de misiones, tiempo/clima, inventario.
- **Carpetas de ejecución**: `worlds/` para mundos y `saves/` para partidas.

---

## Requisitos

- .NET 8 SDK
- Windows 10/11 con soporte WPF
- (Opcional) Docker Desktop para la IA/voz

---

## Uso básico

```bash
# Compilar todo
dotnet build XiloAdventures.sln

# Ejecutar editor + juego
dotnet run --project XiloAdventures.Wpf

# Ejecutar tests
dotnet test
```

---

## Tests

El proyecto incluye **45 tests unitarios** cubriendo:

| Componente | Tests |
|------------|-------|
| GameEngine | 18 |
| Parser | 6 |
| DoorService | 6 |
| CryptoUtil | 2 |
| SaveManager | 2 |
| WorldLoader | 2 |
| UiSettingsManager | 2 |
| SoundManager | 3 |
| AppPaths | 2 |
| WorldEditorHelpers | 2 |

```bash
dotnet test --verbosity normal
```

---

## Flujo de trabajo

1. Abre `XiloAdventures.sln` (VS 2022 recomendado).
2. En la pantalla inicial: selecciona "¡Crea tu aventura!" para un mundo nuevo o elige uno existente.
3. Usa el editor para colocar salas, salidas, puertas, objetos, NPCs.
4. Pulsa **Play** en el editor para probar; verás un overlay de progreso mientras se prepara.
5. En el juego, cierra con confirmación y opción de guardar.

---

## Arquitectura y optimizaciones

### Componentes principales
- **Parser optimizado**: Usa regex compilados para mejor rendimiento en análisis de comandos
- **GameEngine**: Incluye métodos auxiliares para búsquedas case-insensitive optimizadas
- **SoundManager**: Cálculo consolidado de volúmenes efectivos (master × música × canal)
- **Cifrado**: Sistema AES-CBC con claves de 8 caracteres para archivos `.xaw` y `.xas`

### Exportación de ejecutables
El editor permite exportar mundos como ejecutables `.exe` standalone (~80-100 MB):
- Incluye runtime de .NET 8 embebido (no requiere instalación)
- El mundo `.xaw` va empaquetado como recurso embebido
- Usa `appicon.ico` como icono del ejecutable
- Configuración de IA opcional via archivo `config.xac` junto al `.exe`

### Popups y UI
- Todos los popups usan tema oscuro consistente (#1E1E1E fondo, #F5F5F5 texto)
- Popups de un solo botón se cierran con clic o ESC (sin botón visible)
- Checkbox "Usar IA" centrado verticalmente con su label
- Interrogantes de ayuda (?) en labels: Parser Dictionary, Imagen de Sala (3.5:1 @ 1400×400)

### Gestión de audio
- Música global + música por sala con fade
- Volúmenes independientes: Master, Música, Efectos, Voz
- Precarga de voces de salas adyacentes para transiciones fluidas
- TTS opcional con modelos locales (Piper via Docker)

---

## Licencia

Consulta el archivo `LICENSE` para términos y condiciones.
