using System;
using System.Collections.Generic;

namespace XiloAdventures.Engine.Models;

/// <summary>
/// Fase actual del combate por turnos.
/// </summary>
public enum CombatPhase
{
    /// <summary>Determinando orden de turnos (tirada de iniciativa).</summary>
    Initiative,
    /// <summary>Esperando acción del jugador.</summary>
    PlayerAction,
    /// <summary>Jugador tirando dados de ataque/defensa.</summary>
    PlayerRoll,
    /// <summary>NPC eligiendo acción.</summary>
    NpcAction,
    /// <summary>NPC tirando dados.</summary>
    NpcRoll,
    /// <summary>Resolviendo daño del turno.</summary>
    Resolution,
    /// <summary>Fin de ronda (verificar victoria/derrota).</summary>
    RoundEnd,
    /// <summary>Jugador ganó el combate.</summary>
    Victory,
    /// <summary>Jugador perdió el combate.</summary>
    Defeat
}

/// <summary>
/// Acción que un combatiente puede realizar en su turno.
/// </summary>
public enum CombatAction
{
    /// <summary>Sin acción seleccionada.</summary>
    None,
    /// <summary>Atacar al enemigo.</summary>
    Attack,
    /// <summary>Postura defensiva (+5 defensa este turno).</summary>
    Defend,
    /// <summary>Intentar huir del combate.</summary>
    Flee,
    /// <summary>Usar un objeto del inventario.</summary>
    UseItem,
    /// <summary>Usar una habilidad especial (consume maná).</summary>
    UseAbility
}

/// <summary>
/// Estado de un combate activo.
/// </summary>
public class CombatState
{
    /// <summary>Indica si hay combate activo.</summary>
    public bool IsActive { get; set; }

    /// <summary>ID del NPC enemigo.</summary>
    public string EnemyNpcId { get; set; } = string.Empty;

    /// <summary>Fase actual del combate.</summary>
    public CombatPhase Phase { get; set; } = CombatPhase.Initiative;

    /// <summary>De quién es el turno actual (true = jugador, false = NPC).</summary>
    public bool IsPlayerTurn { get; set; }

    /// <summary>Número de ronda actual (empieza en 1).</summary>
    public int RoundNumber { get; set; } = 1;

    /// <summary>Acción seleccionada por el jugador para este turno.</summary>
    public CombatAction PlayerAction { get; set; } = CombatAction.None;

    /// <summary>ID del objeto seleccionado para usar (si PlayerAction = UseItem).</summary>
    public string? SelectedItemId { get; set; }

    /// <summary>ID de la habilidad seleccionada (si PlayerAction = UseAbility).</summary>
    public string? SelectedAbilityId { get; set; }

    /// <summary>Si el jugador está en postura defensiva este turno (+5 defensa).</summary>
    public bool PlayerDefending { get; set; }

    /// <summary>Resultado de la última tirada del jugador.</summary>
    public DiceRollResult? LastPlayerRoll { get; set; }

    /// <summary>Resultado de la última tirada del NPC.</summary>
    public DiceRollResult? LastNpcRoll { get; set; }

    /// <summary>Historial del combate para mostrar en UI.</summary>
    public List<CombatLogEntry> CombatLog { get; set; } = new();
}

/// <summary>
/// Resultado de una tirada de dado D20.
/// </summary>
public class DiceRollResult
{
    /// <summary>Valor del dado (1-20).</summary>
    public int DiceValue { get; set; }

    /// <summary>Bonus de estadística (Fuerza, Destreza, etc. / 5).</summary>
    public int StatBonus { get; set; }

    /// <summary>Bonus de equipamiento (arma o armadura).</summary>
    public int EquipmentBonus { get; set; }

    /// <summary>Bonus adicional (defender, ventaja, etc.).</summary>
    public int AdditionalBonus { get; set; }

    /// <summary>Total final de la tirada.</summary>
    public int Total => DiceValue + StatBonus + EquipmentBonus + AdditionalBonus;

    /// <summary>Natural 20 - Golpe crítico (daño x2).</summary>
    public bool IsCritical => DiceValue == 20;

    /// <summary>Natural 1 - Fallo automático.</summary>
    public bool IsFumble => DiceValue == 1;

    /// <summary>Descripción del desglose de la tirada.</summary>
    public string Breakdown => $"{DiceValue} + {StatBonus} (estado) + {EquipmentBonus} (equipo)" +
        (AdditionalBonus != 0 ? $" + {AdditionalBonus} (bonus)" : "") +
        $" = {Total}";
}

/// <summary>
/// Entrada en el registro de combate.
/// </summary>
public class CombatLogEntry
{
    /// <summary>Mensaje a mostrar.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>True si la acción la realizó el jugador, false si fue del NPC.</summary>
    public bool IsPlayerAction { get; set; }

    /// <summary>Momento en que ocurrió.</summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>Tipo de entrada para formateo visual.</summary>
    public CombatLogType LogType { get; set; } = CombatLogType.Normal;
}

/// <summary>
/// Tipo de entrada en el log de combate para formateo.
/// </summary>
public enum CombatLogType
{
    /// <summary>Mensaje normal.</summary>
    Normal,
    /// <summary>Ataque exitoso.</summary>
    Hit,
    /// <summary>Ataque fallido.</summary>
    Miss,
    /// <summary>Golpe crítico.</summary>
    Critical,
    /// <summary>Fallo épico.</summary>
    Fumble,
    /// <summary>Victoria.</summary>
    Victory,
    /// <summary>Derrota.</summary>
    Defeat,
    /// <summary>Huida exitosa.</summary>
    Fled,
    /// <summary>Información del sistema.</summary>
    System
}

/// <summary>
/// Tipo de habilidad de combate.
/// </summary>
public enum AbilityType
{
    /// <summary>Habilidad de ataque mágico.</summary>
    Attack,
    /// <summary>Habilidad de defensa mágica.</summary>
    Defense
}

/// <summary>
/// Definición de una habilidad de combate.
/// </summary>
public class CombatAbility
{
    /// <summary>Identificador único de la habilidad.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Nombre de la habilidad.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Descripción de la habilidad.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Tipo de habilidad (ataque o defensa).</summary>
    public AbilityType AbilityType { get; set; } = AbilityType.Attack;

    /// <summary>Coste de maná para usar la habilidad.</summary>
    public int ManaCost { get; set; }

    /// <summary>Bonus de ataque mágico (se suma a la tirada de ataque).</summary>
    public int AttackValue { get; set; }

    /// <summary>Bonus de defensa mágica (se suma a la tirada de defensa).</summary>
    public int DefenseValue { get; set; }

    /// <summary>Daño base que causa (0 si no hace daño).</summary>
    public int Damage { get; set; }

    /// <summary>Curación que proporciona (0 si no cura).</summary>
    public int Healing { get; set; }

    /// <summary>Tipo de daño de la habilidad.</summary>
    public DamageType DamageType { get; set; } = DamageType.Magical;

    /// <summary>Efecto de estado que aplica (null = ninguno).</summary>
    public string? StatusEffect { get; set; }

    /// <summary>Duración del efecto de estado en turnos.</summary>
    public int StatusEffectDuration { get; set; }

    /// <summary>Si la habilidad afecta al usuario en vez de al enemigo.</summary>
    public bool TargetsSelf { get; set; }
}

/// <summary>
/// Resultado del cálculo de daño en combate.
/// </summary>
public class DamageResult
{
    /// <summary>Tirada de ataque del atacante.</summary>
    public DiceRollResult AttackRoll { get; set; } = new();

    /// <summary>Tirada de defensa del defensor.</summary>
    public DiceRollResult DefenseRoll { get; set; } = new();

    /// <summary>True si el ataque impactó.</summary>
    public bool Hit => !AttackRoll.IsFumble && (AttackRoll.IsCritical || AttackRoll.Total > DefenseRoll.Total);

    /// <summary>Daño base calculado.</summary>
    public int BaseDamage { get; set; }

    /// <summary>Daño final aplicado (tras críticos y reducciones).</summary>
    public int FinalDamage { get; set; }

    /// <summary>True si fue golpe crítico.</summary>
    public bool WasCritical => AttackRoll.IsCritical;

    /// <summary>True si fue fallo épico.</summary>
    public bool WasFumble => AttackRoll.IsFumble;
}
