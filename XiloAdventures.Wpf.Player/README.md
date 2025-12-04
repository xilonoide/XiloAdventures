# XiloAdventures.Wpf.Player

## Descripción

**XiloAdventures.Wpf.Player** es la aplicación cliente para jugar aventuras conversacionales creadas con el editor de **XiloAdventures.Wpf**. Es una versión simplificada que incluye únicamente la funcionalidad de juego, sin herramientas de edición.

## Características

### Funcionalidades Principales
- **Seleccionar y jugar mundos**: Carga y reproduce aventuras conversacionales (.xaw)
- **Guardar y cargar partidas**: Persiste el progreso del juego
- **Sistema de IA opcional**: Integración con Docker para usar LLM local (Ollama) y síntesis de voz (Coqui TTS)
- **Gestor de inventario**: Muestra estados del jugador, hora del juego e inventario
- **Visualización de imágenes**: Renderiza imágenes de las salas si están disponibles
- **Historial de comandos**: Navegación con flechas arriba/abajo en el terminal

### Diferencias con XiloAdventures.Wpf
- ❌ **Sin editor de mundos**: No incluye herramientas de edición
- ❌ **Sin botón de editor**: La pantalla de inicio no tiene acceso al editor
- ✅ **Interfaz simplificada**: Foco total en la experiencia de juego
- ✅ **Misma funcionalidad de juego**: Todo el motor de juego y características de reproducción

## Estructura del Proyecto

```
XiloAdventures.Wpf.Player/
├── App.xaml                 # Configuración de la aplicación
├── App.xaml.cs              # Lógica de inicio de la aplicación
├── XiloAdventures.Wpf.Player.csproj  # Configuración del proyecto
├── Ui/
│   ├── AppPaths.cs         # Rutas de carpetas del juego
│   └── UiSettings.cs       # Configuración de UI (sonido, fuente, etc)
├── Services/
│   ├── DockerService.cs    # Gestión de contenedores Docker
│   └── DockerShutdownHelper.cs  # Cierre de Docker al salir
└── Windows/
    ├── StartupWindow.xaml/cs       # Pantalla inicial (seleccionar mundo)
    ├── MainWindow.xaml/cs          # Ventana principal de juego
    ├── OptionsWindow.xaml/cs       # Configuración de sonido, fuente, IA
    ├── AboutWindow.xaml/cs         # Acerca de
    ├── AlertWindow.xaml/cs         # Ventanas de diálogo
    ├── ConfirmWindow.xaml/cs       # Ventanas de confirmación
    └── DockerProgressWindow.xaml/cs # Progreso de inicialización de Docker
```

## Dependencias

- **.NET 8.0 Windows** (net8.0-windows)
- **XiloAdventures.Engine** (proyecto interno)
- **WPF** (System.Windows)
- **Docker Desktop** (opcional, para usar IA y síntesis de voz)

## Uso

### Ejecutar la aplicación

```bash
dotnet run --project XiloAdventures.Wpf.Player
```

### Compilar

```bash
dotnet build XiloAdventures.Wpf.Player -c Debug
dotnet build XiloAdventures.Wpf.Player -c Release
```

## Características Principales

### Pantalla de Inicio
- Lista de mundos disponibles en la carpeta `worlds/`
- Crear nueva partida
- Cargar partida guardada
- Eliminar mundos
- Opciones de sonido global e IA

### Ventana de Juego
- **Área de descripción**: Muestra el texto de la aventura
- **Imagen de sala**: Visualiza el gráfico de la habitación actual
- **Panel de estado**: Información de jugador, hora y inventario
- **Entrada de comandos**: Terminal para escribir comandos
- **Menú**: Guardar, cargar, opciones, salir
- **Historial de comandos**: Usa flechas ↑↓ para navegar

### Opciones
- Activar/desactivar sonido
- Ajustar volumen (música, efectos, voz, maestro)
- Tamaño de fuente personalizable
- Activar IA local (requiere Docker)

## Configuración de Docker (opcional)

Para usar las características de IA y síntesis de voz:

1. Instala Docker Desktop desde https://www.docker.com/products/docker-desktop
2. Abre Docker Desktop
3. Activa el checkbox "Usar IA" en las opciones del juego
4. La primera vez descargará contenedores (~2GB) y modelos de IA

### Contenedores utilizados
- **Ollama** (port 11434): Motor de LLM local
- **Coqui TTS** (port 5002): Síntesis de voz en español

## Rutas de Archivos

Por defecto, la aplicación espera los siguientes directorios:

```
./worlds/          # Archivos de mundos (.xaw)
./saves/           # Partidas guardadas (.xas)
./sound/           # Efectos de sonido
./images/          # Imágenes adicionales
./config.xac       # Configuración global
./config_*.xac     # Configuración por mundo
```

## Personalizaciones

### Modificar las ventanas

Todos los XAML pueden personalizarse. Las ventanas principales son:
- `StartupWindow.xaml`: Diseño de la pantalla inicial
- `MainWindow.xaml`: Diseño del juego
- `OptionsWindow.xaml`: Panel de configuración

### Cambiar idioma

El proyecto actualmente está en español. Para otros idiomas, modifica:
- Strings en `*.xaml` y `*.xaml.cs`
- Mensajes en servicios y motores

## Solución de problemas

### "Docker no disponible"
- Asegúrate de que Docker Desktop está instalado y corriendo
- Verifica que puedes ejecutar `docker info` en PowerShell

### "Mundo no encontrado"
- Coloca archivos `.xaw` en la carpeta `worlds/`
- Recarga la lista de mundos cerrando y abriendo la ventana

### "Sonido no funciona"
- Comprueba que tienes archivos de sonido en `sound/`
- Verifica el volumen en Opciones

## Diferencias clave con XiloAdventures.Wpf

| Aspecto | Player | Wpf (Editor) |
|--------|--------|--------------|
| **Propósito** | Jugar mundos | Crear y jugar mundos |
| **Editor incluido** | ❌ No | ✅ Sí |
| **Pantalla inicio** | Simplificada | Con botón de Editor |
| **Código Editor** | ❌ Sin | ✅ Con WorldEditorWindow |
| **Referencias** | Solo a XiloAdventures.Engine | A XiloAdventures.Engine + Logic |

## Construcción y Distribución

### Build Release

```bash
dotnet publish XiloAdventures.Wpf.Player -c Release -r win-x64 --self-contained
```

Esto genera un ejecutable independiente en `bin/Release/net8.0-windows/win-x64/publish/`

### Empaquetar

Copia el directorio publicado junto con las carpetas:
- `worlds/` (con tus mundos)
- `sound/` (si tienes efectos)
- `images/` (si tienes imágenes)

## Licencia

Ver `LICENSE` en la raíz del repositorio.

---

**Versión**: 1.0
**Compatibilidad**: .NET 8.0+, Windows 7+
**Autor**: Proyecto XiloAdventures
