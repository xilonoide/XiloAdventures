# 🎮 XiloAdventures

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF-0d5a8f?logo=windows&logoColor=white)
![C#](https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white)
![Estado](https://img.shields.io/badge/Estado-Activo-success)
<!-- Añade aquí tu badge de licencia cuando la definas -->

**XiloAdventures** es un motor de aventuras de texto / ficción interactiva en C# (.NET 8) con su propio **editor visual de mundos en WPF** y soporte opcional de LLM en local.
El objetivo es poder diseñar mundos complejos (salas, objetos, NPCs, misiones, reglas, eventos…) de forma cómoda, visual y extensible.

---

## ✨ ¿Qué es XiloAdventures?

XiloAdventures es un **ecosistema completo** para crear y jugar aventuras conversacionales:

- Un **motor** (`AdventureEngine`) que gestiona:
  - Mundos, salas, salidas, puertas, llaves, objetos, NPCs, etc.
  - Música e imágenes embebidas en el propio archivo de mundo.
  - Guardado/carga de partidas y mundos con **compresión + cifrado**.
- Un **editor WPF** (`XiloAdventures.Wpf`) con:
  - Mapa visual editable.
  - Árbol de contenido (salas, puertas, objetos, llaves…).
  - Propiedades contextuales.
  - Undo/Redo.
- Un **cliente de juego WPF** con:
  - Interfaz de partida con descripción, imagen, inventario, estados del jugador.
  - Parser de comandos.
  - Integración opcional con un **LLM local** para comandos no entendidos.

Todo está pensado para ser **auto-contenido**: tus mundos se guardan en un único archivo `.xaw` que incluye la definición, la música y las imágenes embebidas.

---

## 🧱 Estructura de la solución

La solución principal es `XiloAdventures.sln` e incluye:

### ⚙️ Proyecto `AdventureEngine`

Núcleo del motor:

- `Models`
  - `WorldModel`, `Room`, `Exit`, `Door`, `KeyDefinition`, `PlayerStats`, etc.
  - Soporte para:
    - Música del mundo y por sala (ids/base64).
    - Imágenes de sala embebidas (base64).
    - Puertas asociadas a salidas, llaves, estados (abierta/cerrada/bloqueada), etc.
- `Engine`
  - `WorldLoader.cs`  
    - Carga y guarda mundos en archivos `.xaw` (zip + cifrado).
    - Serialización/deserialización de `WorldModel`.
    - Manejo de música/imágenes embebidas.
  - `SoundManager.cs`  
    - Basado en NAudio.
    - Música de mundo y de sala en bucle.
    - Volúmenes independientes (master, música, efectos).
    - Transiciones de música entre salas/mundos (con fades).
  - `CryptoUtil.cs`, `SaveManager.cs`, etc.
- `Logic`
  - Lógica de puertas, llaves y reglas de juego (por ejemplo `DoorService.cs`).

---

### 🖥️ Proyecto `XiloAdventures.Wpf`

Frontend WPF (editor + juego):

#### 🗺️ Editor de mundos (`WorldEditorWindow`)

- Panel izquierdo: **árbol** del mundo (salas, puertas, llaves, objetos…).
- Panel central: **mapa** con `MapPanel`:
  - Cajas para salas.
  - Líneas para salidas.
  - Iconos de puertas y llaves.
  - Zoom, drag, selección, multi-selección.
  - Undo/Redo de cambios (posiciones, conexiones, etc.).
- Panel derecho: **propiedades** con `PropertyEditor`:
  - Edita propiedades de sala, puerta, objeto, etc.
- Otras características:
  - Ventanas auxiliares para añadir salidas, puertas, elegir direcciones, renombrar salas…
  - Integración con el sistema de guardado/carga de mundos (`WorldLoader`).

#### 🎮 Cliente de juego (`MainWindow` + `StartupWindow`)

- `StartupWindow`:
  - Selector de mundo.
  - Botones: *Nueva partida*, *Editar mundo*, *Opciones*, *Salir*.
  - Manejo de errores (por ejemplo, intentar nueva partida sin mundo seleccionado).
- `MainWindow`:
  - Muestra:
    - Descripción de la sala.
    - Imagen de la sala (ajustada a su área visible).
    - Inventario del jugador.
    - Estados y stats.
    - Histórico de mensajes y resultados de comandos.
  - Caja de texto con:
    - Historial de comandos (navegable con ↑ y ↓).
    - Integración con el parser.
  - Integración con `SoundManager`:
    - Música de mundo vs. música de sala.
    - Mute y volúmenes.

#### ⚙️ Opciones de usuario (`OptionsWindow` + `UiSettings`)

- Ajustes por mundo (`UiSettings`):
  - ✅ Sonido activado/desactivado.
  - 🔊 Volumen master / música / efectos.
  - 🔠 Tamaño de fuente.
  - 🧠 Uso de LLM para comandos no entendidos.
- Se guardan por mundo mediante `UiSettingsManager`.
- Los cambios se aplican sobre la marcha a través de callbacks (por ejemplo para el sonido o el LLM).

---

## 🧠 Integración con LLM en local (Docker)

XiloAdventures puede apoyarse en un modelo de lenguaje local (por ejemplo, **llama3** con Ollama u otro stack) cuando el parser **no entiende un comando**.

- En el juego, dentro de las **opciones de partida**, hay un check:
  - `Usar LLM para comandos no entendidos`.
- Cuando está activado:
  - El cliente hace una petición HTTP al LLM si el parser no sabe interpretar el comando.
  - Si el LLM no está disponible, se muestra un mensaje de error al usuario indicando que:
    - Debe tener **Docker Desktop** instalado y en marcha.
    - Es necesario levantar los servicios con el `docker-compose.yml` incluido.

### ▶️ Cómo levantar el LLM

1. Instala **Docker Desktop**.
2. Abre una terminal en la carpeta del proyecto donde está `docker-compose.yml`.
3. Ejecuta:

   ```bash
   docker-compose up -d
   ```

4. Inicia XiloAdventures, carga una partida y en las opciones marca  
   **“Usar LLM para comandos no entendidos”**.

---

## 🚀 Puesta en marcha del proyecto

### Requisitos

- 🧩 **.NET 8 SDK**
- 🪟 Windows 10/11
- 🧱 Visual Studio 2022 (o similar) con soporte para:
  - .NET Desktop Development
- 🎧 NAudio (referenciado por el proyecto)
- (Opcional) 🐳 Docker Desktop (para el LLM)

### Pasos

1. Clona el repositorio:

   ```bash
   git clone https://github.com/tu-usuario/XiloAdventures.git
   cd XiloAdventures
   ```

2. Abre `XiloAdventures.sln` con Visual Studio.

3. Restaura paquetes y compila la solución.

4. El proyecto WPF principal es `XiloAdventures.Wpf`.

5. Selecciona la configuración que prefieras:
   - `Debug` para desarrollo.
   - `Release` para ejecutables de distribución.

---

## 🕹️ Cómo usar el editor

1. Arranca la aplicación y, desde la ventana de inicio, elige **Editar mundo**.
2. Carga un mundo existente (`.xaw`) o crea uno nuevo.
3. Usa el **árbol** para navegar entre salas, puertas, llaves, etc.
4. Modifica el mapa:
   - Arrastra las salas en el panel central.
   - Crea salidas, puertas y asocia llaves.
   - Usa Undo/Redo (Ctrl+Z / Ctrl+Shift+Z) para deshacer/rehacer cambios.
5. Ajusta propiedades:
   - Música del mundo / salas (dejando vacío el campo se elimina el base64 del JSON al guardar).
   - Imágenes de salas (igual: campo vacío ⇒ se borra la imagen embebida).
6. Guarda el mundo; se generará/actualizará el `.xaw` con toda la información, cifrada y comprimida.

---

## 🎧 Sonido y medios

- La música y los efectos se gestionan con `SoundManager`.
- El editor y el cliente soportan:
  - Música de fondo global del mundo.
  - Música específica por sala.
  - Transición con fade entre pistas al cambiar de sala, cuando corresponde.
- En la ventana de **opciones** puedes:
  - Activar/desactivar sonido globalmente.
  - Ajustar volúmenes de:
    - Master
    - Música
    - Efectos

Los cambios de volumen afectan al sonido **al instante**, de forma suave para el usuario.

---

## 🤝 Cómo colaborar

¡Las contribuciones son bienvenidas! 💜

### 1. Issues

- Usa el apartado de **Issues** para:
  - Reportar bugs.
  - Proponer nuevas funcionalidades.
  - Sugerir mejoras en UX, rendimiento o arquitectura.

Al abrir un issue, intenta incluir:

- Pasos para reproducir (si es un bug).
- Mundo de ejemplo (`.xaw`) si aplica.
- Capturas de pantalla del editor/juego si ayudan a entender el problema.

### 2. Flujo de trabajo recomendado

1. Haz un **fork** del repositorio.
2. Crea una rama para tu cambio:

   ```bash
   git checkout -b feature/mi-mejora
   ```

3. Aplica tus cambios en el engine, el editor o el cliente de juego.
4. Asegúrate de que la solución:
   - Compila sin warnings importantes.
   - Respeta el estilo de código C# existente.
5. Añade o adapta mundos de ejemplo si tu cambio lo requiere.
6. Haz commit y push a tu fork:

   ```bash
   git commit -m "Añade XYZ al editor"
   git push origin feature/mi-mejora
   ```

7. Abre un **Pull Request** describiendo:
   - Qué hace el cambio.
   - Cómo probarlo.
   - Si afecta a formatos de archivo (por ejemplo, cambios en `WorldModel`).

### 3. Estilo de código

- C# moderno (.NET 8, C# 12).
- Nullable Reference Types activado.
- Nombres claros y en inglés para clases/métodos/propiedades.
- Comentarios sólo cuando aporten contexto, no para repetir lo obvio.

---

## 📁 Estructura de archivos destacada

- `AdventureEngine/`
  - `Models/Models.cs` → Entidades del mundo.
  - `Engine/WorldLoader.cs` → Carga/guardado de `.xaw`.
  - `Engine/SoundManager.cs` → Gestión de audio.
- `XiloAdventures.Wpf/`
  - `Windows/StartupWindow.xaml` → Pantalla inicial.
  - `Windows/MainWindow.xaml` → Juego.
  - `Windows/WorldEditorWindow.xaml` → Editor de mundos.
  - `Controls/MapPanel.*` → Lógica del mapa visual.
  - `Controls/PropertyEditor.*` → Panel de propiedades.
  - `Ui/UiSettings.cs` → Preferencias de usuario por mundo.
  - `docker-compose.yml` → Stack del LLM local.

---

## 📜 Licencia

Este proyecto se distribuye bajo la licencia indicada en el archivo `LICENSE` del repositorio.

Consulta ese archivo para conocer los **términos de uso**, las condiciones de **atribución** y cómo puedes reutilizar o ampliar XiloAdventures en tus propios proyectos.

---
