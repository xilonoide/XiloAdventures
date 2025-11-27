# 🎮 XiloAdventures

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF-0d5a8f?logo=windows&logoColor=white)
![C#](https://img.shields.io/badge/Language-C%23-239120?logo=csharp&logoColor=white)
![Estado](https://img.shields.io/badge/Estado-Activo-success)
<!-- Añade aquí tu badge de licencia cuando la definas -->

**XiloAdventures** es un motor de aventuras de texto / ficción interactiva en C# (.NET 8) con su propio **editor visual de mundos en WPF**.  
El objetivo es poder diseñar mundos complejos (salas, objetos, NPCs, misiones, reglas, eventos…) de forma cómoda, visual y extensible.

---

## ✨ Características principales

### 🧠 Motor (`AdventureEngine`)

- 📦 **Modelo de mundo tipado** (`WorldModel`):
  - `Rooms`, `GameObject`, `Npc`, `QuestDefinition`, `UseRule`, `TradeRule`, `EventRule`, etc.
- 📜 **Carga de mundos en JSON** mediante `WorldLoader`.
- 🔐 Utilidades de **cifrado** (`CryptoUtil`) para partidas/archivos (AES simétrico).
- 🧮 Lógica de juego centralizada en `GameEngine`:
  - Manejo de estado del jugador.
  - Gestión de inventario, uso de objetos, comercio, eventos, quests, etc.
- 🎵 Soporte básico para **sonido y música** (`SoundManager`).

### 🗺️ Editor de mundos WPF (`XiloAdventures.Wpf`)

- 🌳 **Panel izquierdo**: Árbol de mundo
  - Listado jerárquico de salas, objetos y NPCs.
  - Doble click en:
    - 🧍‍♂️ **NPC** → centra el mapa en la sala donde está.
    - 🎒 **Objeto** → centra el mapa en la sala donde está.
    - 🚪 **Sala** → centra el mapa en esa sala.
- 🗺️ **Panel central**: Mapa visual
  - Salas representadas como cajas con conectores por dirección.
  - Líneas entre salas para representar salidas.
  - Arrastrar salas para reorganizar el mapa.
  - Sistema de **zoom** y desplazamiento.
  - Colisiones y márgenes entre salas para evitar solapamientos.
  - Las salas no pueden salir de la zona central del mapa.
- 🧩 **Selección y edición**
  - Selección de una o varias salas / elementos del mapa.
  - Selección mediante rectángulo de arrastre (marquee).
  - Salidas seleccionables como entidades independientes.
  - Las salidas pueden eliminarse con la tecla **Supr/Delete**.
- ✂️ **Edición avanzada**
  - Cortar, copiar y pegar elementos del mapa (salas, etc.).
  - Las salidas seleccionadas se pueden borrar pero no se incluyen en cortar/pegar.
  - Menú **Editar** con comandos estándar y atajos:
    - `Ctrl + X` → Cortar
    - `Ctrl + C` → Copiar
    - `Ctrl + V` → Pegar
  - Al pegar:
    - Los nuevos elementos aparecen desplazados respecto a los originales para verlos fácilmente.
- ↩️ **Undo / Redo**
  - Historial de cambios del editor:
    - Movimiento de salas.
    - Creación/eliminación de conexiones.
    - Cambios de propiedades, etc.
  - Atajos:
    - `Ctrl + Z` → Deshacer
    - `Ctrl + Shift + Z` → Rehacer

- 🎨 **UI oscura y moderna**
  - Ventana del editor a pantalla completa por defecto.
  - Menú con iconos emoji:
    - 🆕 Nuevo
    - 📂 Abrir
    - 💾 Guardar
    - 📝 Guardar como…
    - ✖ Cerrar

---

## 🧱 Estructura del proyecto

```text
XiloAdventures/
├─ AdventureEngine/              # Motor de juego (lógica y modelos)
│  ├─ AdventureEngine.csproj
│  ├─ Engine/
│  │  ├─ GameEngine.cs          # Lógica principal de juego
│  │  ├─ WorldLoader.cs         # Carga de mundos desde JSON
│  │  ├─ SaveManager.cs         # Gestión de partidas
│  │  ├─ SoundManager.cs        # Sonido y música
│  │  └─ CryptoUtil.cs          # Utilidades de cifrado
│  └─ Models/
│     └─ Models.cs              # Definición de WorldModel, Room, GameObject, Npc, etc.
│
└─ XiloAdventures.Wpf/          # Editor visual de mundos
   ├─ XiloAdventures.Wpf.csproj
   ├─ App.xaml / App.xaml.cs    # Arranque de la app WPF
   ├─ Ui/
   │  ├─ AppPaths.cs
   │  └─ UiSettings.cs
   ├─ Controls/
   │  ├─ MapPanel.cs            # Lógica principal y API del mapa
   │  ├─ MapPanel.Input.cs      # Eventos de ratón, teclado, selección, delete, etc.
   │  ├─ MapPanel.Rendering.cs  # Dibujo de salas, líneas y conectores
   │  ├─ PropertyEditor.xaml    # Editor de propiedades a la derecha
   │  └─ PropertyEditor.xaml.cs
   ├─ Windows/
   │  ├─ MainWindow.xaml        # Ventana principal (launcher)
   │  ├─ MainWindow.xaml.cs
   │  ├─ WorldEditorWindow.xaml # Editor de mundos
   │  └─ WorldEditorWindow.xaml.cs
   └─ worlds/
      └─ la_forja_de_earendur.json  # Mundo de ejemplo
```

---

## 🚀 Empezar a usarlo

### ✅ Requisitos

- 🧩 **.NET 8 SDK**
- 🪟 **Windows 10/11**
- 💻 Visual Studio 2022 (o superior) con soporte para:
  - .NET Desktop Development
  - WPF

### 🔧 Compilar y ejecutar

1. Clona el repositorio:

   ```bash
   git clone https://github.com/TU_USUARIO/TU_REPO_XiloAdventures.git
   cd TU_REPO_XiloAdventures/XiloAdventures
   ```

2. Abre la solución:

   - Abre `XiloAdventures.sln` con Visual Studio.

3. Selecciona el proyecto de inicio:

   - Establece **`XiloAdventures.Wpf`** como *Startup Project*.

4. Ejecuta:

   - Pulsa **F5** o **Ctrl + F5** para lanzar el editor de mundos.

---

## 🌍 Mundos y archivos

- Los mundos se almacenan como **JSON**.
- Carpeta por defecto en el proyecto WPF:

  ```text
  XiloAdventures/XiloAdventures.Wpf/worlds/
  ```

- Ejemplo incluido:
  - `la_forja_de_earendur.json`

Puedes usar el editor para:

- Crear un mundo nuevo desde el menú **Archivo → Nuevo**.
- Abrir un mundo existente con **Archivo → Abrir…**.
- Guardar cambios con **Archivo → Guardar / Guardar como…**.

---

## 🎛️ Uso del editor (vista rápida)

- 🌳 **Árbol (izquierda)**
  - Selecciona salas, objetos y NPCs.
  - Doble click:
    - Sala → centra el mapa en esa sala.
    - Objeto → centra en la sala donde está el objeto (permite elegirla si no la tiene).
    - NPC → idem para el NPC.

- 🗺️ **Mapa (centro)**
  - Arrastra salas para reorganizar el layout.
  - Usa la rueda del ratón (y/o teclas configuradas) para hacer zoom.
  - Conectores en los bordes de cada sala para crear salidas.
  - Las líneas (salidas) se pueden seleccionar y borrar con **Supr/Delete**.

- ⚙️ **Propiedades (derecha)**
  - Editor de propiedades contextual: según lo seleccionado (sala, objeto, NPC, etc.) muestra sus campos editables.

- ⌨️ **Atajos de teclado principales**
  - `Ctrl + Z` → Deshacer
  - `Ctrl + Shift + Z` → Rehacer
  - `Ctrl + X` → Cortar
  - `Ctrl + C` → Copiar
  - `Ctrl + V` → Pegar
  - `Supr` → Eliminar salidas seleccionadas (y otros elementos donde aplique)

---

## 🧭 Roadmap (ideas futuras)

- 🧑‍💻 Cliente “intérprete” de aventuras usando `AdventureEngine` (jugador standalone).
- 🧪 Sistema de scripting/extensiones para reglas avanzadas de juego.
- 🌐 Exportar mundos a otros formatos (por ejemplo, compatibilidad parcial con otros motores).
- 📸 Mejoras en la visualización del mapa (mini-mapa, grid opcional, snapping, etc.).
- 🧷 Favoritos / marcadores de salas para saltar rápidamente en mapas muy grandes.

---

## 🤝 Contribuir

1. Haz un fork del repositorio.
2. Crea una rama para tu feature o fix:

   ```bash
   git checkout -b feature/mi-mejora
   ```

3. Realiza tus cambios con commits pequeños y claros.
4. Asegúrate de que compila sin errores y que el editor funciona.
5. Abre un Pull Request describiendo:
   - Qué has cambiado.
   - Por qué.
   - Cómo probarlo.

---

## 🌟 Notas finales

XiloAdventures está pensado para crecer poco a poco:  
primero como **editor cómodo de mundos**, y después como plataforma completa para crear y jugar aventuras de texto modernas.
