<p align="center">
  <img src="Resources/logo.png" alt="XiloAdventures logo" width="360">
</p>

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF-0d5a8f?logo=windows&logoColor=white)
![C#](https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white)
![Estado](https://img.shields.io/badge/Estado-Activo-success)

XiloAdventures es un ecosistema completo para crear y jugar aventuras conversacionales en C# (.NET 8) con editor visual WPF y cliente de juego. Todo el contenido (salas, objetos, musica, imagenes) viaja dentro de un unico archivo `.xaw` cifrado y comprimido.

---

## Qué incluye

- **Engine (`XiloAdventures.Engine`)**: modelos de mundo, guardado/carga `.xaw` (zip + cifrado), logica de puertas/llaves, audio con NAudio (musica de mundo y de sala, volmenes master/musica/efectos/voz).
- **Editor WPF (`XiloAdventures.Wpf`)**:
  - Mapa visual con zoom, drag, seleccion multiple, iconos de puertas/llaves, tooltip de imagen de sala.
  - Arbol de contenido (salas, salidas, puertas, llaves, objetos, NPCs, misiones).
  - Panel de propiedades en espanol, con textboxes multilinea, checkboxes y radio centrados.
  - Boton *Play* para probar el mundo; muestra overlay de progreso mientras prepara la partida.
  - Guardado/carga de mundos `.xaw`, undo/redo, busqueda.
- **Cliente de juego WPF (`MainWindow`, `StartupWindow`, `XiloAdventures.Wpf.Player`)**:
  - Pantalla inicial con selector de mundos, checks de sonido/IA e overlay de progreso al cargar/iniciar.
  - Ventana de partida con historial, inventario, estados, imagen de sala, musica integrada.
  - Al cerrar la partida pregunta si guardar y confirma la salida (popups oscuros).
- **LLM opcional**: si el parser no entiende un comando y la opcion esta activa, consulta un modelo local (requiere Docker Desktop).
- **TTS (voz)**: generacion y precarga de voz de las descripciones de salas.

---

## Wiki
- Pagina principal: [General](https://github.com/xilonoide/XiloAdventures/wiki/general)
- Editor: [Editor](https://github.com/xilonoide/XiloAdventures/wiki/editor)
- Cliente/Player: [Player](https://github.com/xilonoide/XiloAdventures/wiki/player)

---

## Estructura rapida

- `XiloAdventures.Engine/`
  - `Models/Models.cs`, `Engine/WorldLoader.cs`, `Engine/SoundManager.cs`, `Engine/CryptoUtil.cs`, `Engine/SaveManager.cs`
- `XiloAdventures.Wpf/`
  - `Windows/StartupWindow.xaml` (inicio con overlay de carga)
  - `Windows/MainWindow.xaml` (cliente de juego, salida con confirmacion/guardar)
  - `Windows/WorldEditorWindow.xaml` (editor visual con overlay de play)
  - `Controls/MapPanel.*`, `Controls/PropertyEditor.*`
  - `Ui/UiSettings.cs` (preferencias por mundo)
- `XiloAdventures.Wpf.Player/` cliente standalone de juego

---

## Formato y carpetas
- Mundos `.xaw`: JSON comprimido en ZIP (`world.json`), Base64 y cifrado AES CBC (clave vacia = sin cifrar, 8 chars).
- Partidas `.xas`: estado del juego cifrado (`GameState`), incluye salas/objetos/NPCs, progreso de misiones, tiempo/clima, inventario.
- Carpetas de ejecucion: `worlds/` para mundos y `saves/` para partidas (se crean al arrancar la app, ver `AppPaths`).

---

## Requisitos

- .NET 8 SDK
- Windows 10/11 con soporte WPF
- (Opcional) Docker Desktop para la IA/voz

---

## Uso basico

```bash
dotnet build XiloAdventures.sln
```

Editor + juego (proyecto WPF principal):

```bash
dotnet run --project XiloAdventures.Wpf
```

Solo cliente player:

```bash
dotnet run --project XiloAdventures.Wpf.Player
```

Tests (proyecto `XiloAdventures.Tests`, xUnit):

```bash
dotnet test
```

---

## Flujo de trabajo

1. Abre `XiloAdventures.sln` (VS 2022 recomendado).
2. En la pantalla inicial: crea/carga mundo, configura sonido/IA.
3. Usa el editor para colocar salas, salidas, puertas, objetos, NPCs. Las propiedades estan en espanol y la musica/imagenes se embeben en el `.xaw`.
4. Pulsa **Play** en el editor para probar; veras un overlay de progreso mientras se prepara.
5. En el juego, cierra con confirmacion y opcion de guardar desde el propio popup.

---

## Licencia

Consulta el archivo `LICENSE` para terminos y condiciones.
