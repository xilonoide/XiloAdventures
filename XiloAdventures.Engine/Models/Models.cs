using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace XiloAdventures.Engine.Models;

public class WorldModel
{
    public GameInfo Game { get; set; } = new();
    public List<Room> Rooms { get; set; } = new();
    public List<GameObject> Objects { get; set; } = new();
    public List<Npc> Npcs { get; set; } = new();
    public List<QuestDefinition> Quests { get; set; } = new();
    public List<UseRule> UseRules { get; set; } = new();
    public List<TradeRule> TradeRules { get; set; } = new();
    public List<EventRule> Events { get; set; } = new();

    public List<Door> Doors { get; set; } = new();
    public List<KeyDefinition> Keys { get; set; } = new();

    /// <summary>
    /// Biblioteca de música del mundo (archivos compartidos entre salas).
    /// </summary>
    public List<MusicAsset> Musics { get; set; } = new();

    /// <summary>
    /// Biblioteca de efectos de sonido del mundo.
    /// </summary>
    public List<FxAsset> Fxs { get; set; } = new();

    /// <summary>
    /// Posiciones del mapa para cada sala (coordenadas lógicas X/Y) usadas por el editor.
    /// </summary>
    public Dictionary<string, MapPosition> RoomPositions { get; set; } = new();
}

public enum WeatherType
{
    Despejado,
    Lluvioso,
    Nublado,
    Tormenta
}

public class GameInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = "Aventura sin título";
    public string StartRoomId { get; set; } = string.Empty;
    public string? WorldMusicId { get; set; }

    /// <summary>
    /// Música por defecto del mundo en Base64 (se guarda dentro del JSON del mundo).
    /// Si es null o vacío, no sonará música global.
    /// </summary>
    [Browsable(false)]
    public string? WorldMusicBase64 { get; set; }

    /// <summary>
    /// Clave de cifrado para las partidas guardadas de los jugadores.
    /// Debe tener 8 caracteres.
    /// </summary>
    public string? EncryptionKey { get; set; }

    /// <summary>Hora inicial de la partida (0-23).</summary>
    public int StartHour { get; set; } = 9;

    /// <summary>Clima inicial del mundo.</summary>
    public WeatherType StartWeather { get; set; } = WeatherType.Despejado;

    /// <summary>Minutos reales que equivalen a 1 hora de juego (1-10).</summary>
    public int MinutesPerGameHour { get; set; } = 6;

    /// <summary>Diccionario de sinónimos por mundo para el parser (JSON).</summary>
    public string? ParserDictionaryJson { get; set; }
}

public class Room
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Sala sin nombre";
    public string Description { get; set; } = string.Empty;

    public string? ImageId { get; set; }

    /// <summary>
    /// Contenido de la imagen de la sala en Base64 (se guarda dentro del JSON del mundo).
    /// Si es null o vacío, no se mostrará imagen.
    /// </summary>
    [Browsable(false)]
    public string? ImageBase64 { get; set; }

    public string? MusicId { get; set; }

    /// <summary>
    /// Música específica de la sala en Base64 (se guarda dentro del JSON del mundo).
    /// Si es null o vacío, se usará la música global del mundo (si la hay).
    /// </summary>
    [Browsable(false)]
    public string? MusicBase64 { get; set; }
    public bool IsInterior { get; set; } = false;
    public bool IsIlluminated { get; set; } = true;

    public List<Exit> Exits { get; set; } = new();

    [Browsable(false)]
    public List<string> ObjectIds { get; set; } = new();

    [Browsable(false)]
    public List<string> NpcIds { get; set; } = new();

    public string? RequiredQuestId { get; set; }
    public QuestStatus? RequiredQuestStatus { get; set; }

    public List<string> Tags { get; set; } = new();
}

public class Exit
{
    public string Direction { get; set; } = string.Empty;
    public string TargetRoomId { get; set; } = string.Empty;

    public bool IsLocked { get; set; }
    public string? LockId { get; set; }

    /// <summary>
    /// Si esta salida está asociada a una puerta física del mundo, su Id.
    /// Si es null, la salida funciona como hasta ahora (solo con IsLocked/LockId).
    /// </summary>
    public string? DoorId { get; set; }

    public string? RequiredQuestId { get; set; }
    public QuestStatus? RequiredQuestStatus { get; set; }

    public List<string> Tags { get; set; } = new();
}

public class GameObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Objeto sin nombre";
    public string Description { get; set; } = string.Empty;

    public bool CanTake { get; set; }
    public bool IsContainer { get; set; }
    public List<string> ContainedObjectIds { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    public int BaseValue { get; set; }
    public int Quality { get; set; }

    /// <summary>Sala inicial donde se encuentra el objeto.</summary>
    public string? RoomId { get; set; }

    /// <summary>Controla si el jugador puede ver / interactuar con el objeto en la sala.</summary>
    public bool Visible { get; set; } = true;
}

public class Npc
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "NPC sin nombre";
    public string Description { get; set; } = string.Empty;

    /// <summary>Sala inicial donde aparece el NPC.</summary>
    public string? RoomId { get; set; }

    /// <summary>Diálogo simple para el NPC.</summary>
    public List<DialogueLine> Dialogue { get; set; } = new();

    /// <summary>Inventario del NPC.</summary>
    public List<string> InventoryObjectIds { get; set; } = new();

    /// <summary>Estadísticas de combate del NPC.</summary>
    public CombatStats Stats { get; set; } = new();

    /// <summary>Texto libre para describir el comportamiento.</summary>
    public string? Behavior { get; set; }

    /// <summary>Tags arbitrarios para lógica de eventos, etc.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Controla si el jugador puede ver / interactuar con el NPC en la sala.</summary>
    public bool Visible { get; set; } = true;
}

public class DialogueLine
{
    public int Index { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class CombatStats
{
    public int Level { get; set; } = 1;
    public int Strength { get; set; } = 5;
    public int Dexterity { get; set; } = 5;
    public int Intelligence { get; set; } = 5;

    public int MaxHealth { get; set; } = 10;
    public int CurrentHealth { get; set; } = 10;

    public int Gold { get; set; }
}

public class QuestDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = "Misión sin nombre";
    public string Description { get; set; } = string.Empty;
    public string? StartRoomId { get; set; }
    public List<string> Objectives { get; set; } = new();
}

public class QuestState
{
    public string QuestId { get; set; } = string.Empty;
    public QuestStatus Status { get; set; } = QuestStatus.NotStarted;
    public int CurrentObjectiveIndex { get; set; }
}

public enum QuestStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed
}

public class UseRule
{
    public string Id { get; set; } = string.Empty;
    public string? ObjectId { get; set; }
    public string? TargetObjectId { get; set; }
    public string? RequiredQuestId { get; set; }
    public QuestStatus? RequiredQuestStatus { get; set; }
    public string ResultText { get; set; } = string.Empty;
    public string? SoundEffectId { get; set; }
}

public class TradeRule
{
    public string Id { get; set; } = string.Empty;
    public string? NpcId { get; set; }
    public string? OfferedObjectId { get; set; }
    public string? RequestedObjectId { get; set; }
    public int? Price { get; set; }
    public string ResultText { get; set; } = string.Empty;
    public string? SoundEffectId { get; set; }
}

public class EventRule
{
    public string Id { get; set; } = string.Empty;
    public string? TriggerFlag { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? SoundEffectId { get; set; }
}

public class PlayerStats
{
    public string ClassName { get; set; } = "Aventurero";
    public int Strength { get; set; } = 5;
    public int Dexterity { get; set; } = 5;
    public int Intelligence { get; set; } = 5;

    public int MaxHealth { get; set; } = 20;
    public int CurrentHealth { get; set; } = 20;

    public int Gold { get; set; } = 0;
    public int Level { get; set; } = 1;
    public int Experience { get; set; } = 0;
}

public class GameState
{
    public string WorldId { get; set; } = string.Empty;
    public string? WorldMusicId { get; set; }
    
    // Clave copiada del GameInfo para persistir en la sesión de juego
    public string? WorldEncryptionKey { get; set; }
    
    public string CurrentRoomId { get; set; } = string.Empty;

    public PlayerStats Player { get; set; } = new();

    public List<Room> Rooms { get; set; } = new();
    public List<GameObject> Objects { get; set; } = new();
    public List<Npc> Npcs { get; set; } = new();

    public Dictionary<string, QuestState> Quests { get; set; } = new();
    public List<UseRule> UseRules { get; set; } = new();
    public List<TradeRule> TradeRules { get; set; } = new();
    public List<EventRule> Events { get; set; } = new();

    public List<Door> Doors { get; set; } = new();
    public List<KeyDefinition> Keys { get; set; } = new();

    public Dictionary<string, bool> Flags { get; set; } = new();
    public List<string> InventoryObjectIds { get; set; } = new();

    public DateTime GameTime { get; set; } = default;
    public int TurnCounter { get; set; }
    public string TimeOfDay { get; set; } = "día";
    public WeatherType Weather { get; set; } = WeatherType.Despejado;
}

public class MapPosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class MusicAsset
{
    /// <summary>
    /// Nombre del archivo de música (ej: "theme.mp3").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Contenido del archivo de música en Base64.
    /// </summary>
    [Browsable(false)]
    public string Base64 { get; set; } = string.Empty;

    /// <summary>
    /// Tamaño del archivo en bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Duración del archivo en segundos.
    /// </summary>
    public double DurationSeconds { get; set; }
}

public class FxAsset
{
    /// <summary>
    /// Nombre del archivo de FX (ej: "explosion.wav").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Contenido del archivo de FX en Base64.
    /// </summary>
    [Browsable(false)]
    public string Base64 { get; set; } = string.Empty;

    /// <summary>
    /// Tamaño del archivo en bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Duración del archivo en segundos.
    /// </summary>
    public double DurationSeconds { get; set; }
}

