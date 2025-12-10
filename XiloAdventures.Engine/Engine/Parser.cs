using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Engine;

/// <summary>
/// Types of prepositions recognized by the parser.
/// </summary>
public enum PrepositionKind
{
    None,
    To,
    With,
    From,
    In
}

/// <summary>
/// Compiled regex patterns for performance optimization.
/// </summary>
file static class ParserRegex
{
    public static readonly Regex MultiSpace = new(@"\s+", RegexOptions.Compiled);
    public static readonly Regex Punctuation = new("[.,;:!?¡¿\"'()]", RegexOptions.Compiled);
}

/// <summary>
/// Represents a parsed player command with verb, objects, and preposition.
/// </summary>
public readonly struct ParsedCommand
{
    /// <summary>The canonical verb (e.g., "go", "take", "look").</summary>
    public string Verb { get; }

    /// <summary>The direct object of the command (e.g., "sword", "north").</summary>
    public string? DirectObject { get; }

    /// <summary>The indirect object (e.g., "with key" -> "key").</summary>
    public string? IndirectObject { get; }

    /// <summary>The preposition used in the command.</summary>
    public PrepositionKind Preposition { get; }

    public ParsedCommand(string verb, string? directObject, string? indirectObject, PrepositionKind preposition)
    {
        Verb = verb;
        DirectObject = directObject;
        IndirectObject = indirectObject;
        Preposition = preposition;
    }
}

internal sealed class ParserDictionaryDto
{
    public Dictionary<string, string[]?>? verbs { get; set; }
    public Dictionary<string, string[]?>? nouns { get; set; }
}

/// <summary>
/// Natural language parser for adventure game commands.
/// Supports Spanish input with verb/noun aliases and direction shortcuts.
/// </summary>
/// <remarks>
/// The parser normalizes player input by:
/// - Converting verb synonyms to canonical forms (e.g., "mirar" -> "look")
/// - Handling prepositions (a, con, de, en)
/// - Recognizing direction shortcuts (n, s, e, o, etc.)
/// - Supporting per-world custom dictionaries
/// </remarks>
public static class Parser
{
    // Diccionarios globales (base + recursos embebidos)
    private static readonly Dictionary<string, string> GlobalVerbAliases = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> GlobalNounAliases = new(StringComparer.OrdinalIgnoreCase);

    // Diccionarios específicos del mundo actual (se rellenan con GameInfo.ParserDictionaryJson)
    private static readonly Dictionary<string, string> WorldVerbAliases = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> WorldNounAliases = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, PrepositionKind> Prepositions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = PrepositionKind.To,
        ["al"] = PrepositionKind.To,
        ["hacia"] = PrepositionKind.To,
        ["con"] = PrepositionKind.With,
        ["de"] = PrepositionKind.From,
        ["desde"] = PrepositionKind.From,
        ["en"] = PrepositionKind.In,
        ["sobre"] = PrepositionKind.In
    };

    private static readonly HashSet<string> IgnoredNounPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "el","la","los","las",
        "un","una","unos","unas",
        "al","del",
        "mi","mis","tu","tus","su","sus",
        "este","esta","estos","estas",
        "ese","esa","esos","esas",
        "aquel","aquella","aquellos","aquellas"
    };

    static Parser()
    {
        InitializeDictionaries();
    }

    private static void InitializeDictionaries()
    {
        // Verbos base en castellano -> verbos canónicos
        AddVerbAlias("look", "mirar", "mira", "examinar", "ver", "observa", "x");
        AddVerbAlias("go", "ir", "ve", "andar", "caminar");
        AddVerbAlias("inventory", "inventario", "inv", "i");
        AddVerbAlias("take", "coger", "toma", "coge", "tomar", "agarrar", "recoger");
        AddVerbAlias("drop", "soltar", "dejar", "tirar");
        AddVerbAlias("open", "abrir", "abre");
        AddVerbAlias("close", "cerrar", "cierra");
        AddVerbAlias("unlock", "desbloquear", "abrir con llave", "abrir con");
        AddVerbAlias("lock", "bloquear", "cerrar con llave", "cerrar con");
        AddVerbAlias("put", "meter", "poner", "colocar", "guardar");
        AddVerbAlias("get_from", "sacar", "quitar", "extraer");
        AddVerbAlias("look_in", "mirar en", "mirar dentro", "ver en", "ver dentro", "examinar en");
        AddVerbAlias("talk", "hablar", "habla", "charlar", "conversar");
        AddVerbAlias("say", "decir", "di");
        AddVerbAlias("option", "opcion", "opción");
        AddVerbAlias("use", "usar", "utilizar", "emplear");
        AddVerbAlias("give", "dar", "entregar");
        AddVerbAlias("quests", "misiones", "mision", "misión", "quest");
        AddVerbAlias("save", "guardar", "salvar");
        AddVerbAlias("load", "cargar");
        AddVerbAlias("help", "ayuda");

        // Algunos sinónimos globales de nombres
        AddNounAlias("espada", "hoja", "sable", "mandoble");
        AddNounAlias("enano", "enano borracho", "minero", "barbudo");
        AddNounAlias("posada", "taberna", "mesón", "meson");
        AddNounAlias("oro", "moneda", "monedas", "dinero");

        // Intentar cargar ParserDictionary.json embebido (si existe)
        try
        {
            var asm = typeof(Parser).Assembly;
            var resourceName = asm
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("ParserDictionary.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = asm.GetManifestResourceStream(resourceName);

                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var json = reader.ReadToEnd();
                    var dto = JsonSerializer.Deserialize<ParserDictionaryDto>(json);
                    ApplyDtoToAliases(dto, GlobalVerbAliases, GlobalNounAliases);
                }
            }
        }
        catch
        {
            // Si falla el recurso embebido, seguimos con los diccionarios base.
        }
    }

    private static void AddVerbAlias(string canonical, params string[] synonyms)
    {
        if (string.IsNullOrWhiteSpace(canonical))
            return;

        GlobalVerbAliases[canonical] = canonical;
        foreach (var syn in synonyms)
        {
            var s = syn?.Trim();
            if (string.IsNullOrEmpty(s)) continue;
            GlobalVerbAliases[s] = canonical;
        }
    }

    private static void AddNounAlias(string canonical, params string[] synonyms)
    {
        if (string.IsNullOrWhiteSpace(canonical))
            return;

        canonical = canonical.Trim().ToLowerInvariant();
        GlobalNounAliases[canonical] = canonical;

        foreach (var syn in synonyms)
        {
            var s = syn?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(s)) continue;
            GlobalNounAliases[s] = canonical;
        }
    }

    private static void ApplyDtoToAliases(
        ParserDictionaryDto? dto,
        Dictionary<string, string> verbAliases,
        Dictionary<string, string> nounAliases)
    {
        if (dto == null) return;

        if (dto.verbs != null)
        {
            foreach (var kvp in dto.verbs)
            {
                var canonical = (kvp.Key ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(canonical))
                    continue;

                verbAliases[canonical] = canonical;

                if (kvp.Value == null) continue;
                foreach (var rawSyn in kvp.Value)
                {
                    var syn = (rawSyn ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(syn)) continue;
                    verbAliases[syn] = canonical;
                }
            }
        }

        if (dto.nouns != null)
        {
            foreach (var kvp in dto.nouns)
            {
                var canonical = (kvp.Key ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(canonical))
                    continue;

                nounAliases[canonical] = canonical;

                if (kvp.Value == null) continue;
                foreach (var rawSyn in kvp.Value)
                {
                    var syn = (rawSyn ?? string.Empty).Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(syn)) continue;
                    nounAliases[syn] = canonical;
                }
            }
        }
    }

    /// <summary>
    /// Sets a world-specific dictionary for verb and noun aliases.
    /// </summary>
    /// <param name="json">JSON string containing verb/noun mappings, or null to clear.</param>
    public static void SetWorldDictionary(string? json)
    {
        WorldVerbAliases.Clear();
        WorldNounAliases.Clear();

        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            var dto = JsonSerializer.Deserialize<ParserDictionaryDto>(json);
            ApplyDtoToAliases(dto, WorldVerbAliases, WorldNounAliases);
        }
        catch
        {
            // Si el JSON está mal, ignoramos el diccionario del mundo.
        }
    }

    /// <summary>
    /// Parses a player command string into structured components.
    /// </summary>
    /// <param name="input">The raw command string from the player.</param>
    /// <returns>A ParsedCommand with verb, direct/indirect objects, and preposition.</returns>
    public static ParsedCommand Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new ParsedCommand(string.Empty, null, null, PrepositionKind.None);

        input = input.Trim();

        // Normalizar espacios
        input = ParserRegex.MultiSpace.Replace(input, " ");

        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var verbToken = parts[0];
        var rest = parts.Length > 1 ? parts[1] : string.Empty;

        // Dirección sola: "n", "norte", "arriba", etc.
        if (!HasVerbAlias(verbToken) && IsDirection(verbToken))
        {
            var dirToken = NormalizeDirectionToken(verbToken);
            return new ParsedCommand("go", dirToken, null, PrepositionKind.None);
        }

        var canonicalVerb = NormalizeVerb(verbToken);

        if (string.IsNullOrEmpty(rest))
            return new ParsedCommand(canonicalVerb, null, null, PrepositionKind.None);

        // Partir objeto directo / indirecto según preposición
        string? direct = null;
        string? indirect = null;
        var prepKind = PrepositionKind.None;

        var tokens = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var prepIndex = -1;
        PrepositionKind foundPrepKind = PrepositionKind.None;

        for (int i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (Prepositions.TryGetValue(t, out var pk))
            {
                prepIndex = i;
                foundPrepKind = pk;
                break;
            }
        }

        if (prepIndex >= 0)
        {
            direct = string.Join(' ', tokens.Take(prepIndex));
            indirect = string.Join(' ', tokens.Skip(prepIndex + 1));
            prepKind = foundPrepKind;
        }
        else
        {
            direct = rest;
        }

        direct = NormalizeNoun(direct);
        indirect = NormalizeNoun(indirect);

        // Heurísticas específicas por verbo

        // "ir al norte", "ve a la puerta"
        if (canonicalVerb == "go" && string.IsNullOrEmpty(direct) && !string.IsNullOrEmpty(indirect))
        {
            direct = indirect;
            indirect = null;
        }

        // "hablar con el enano", "decir al guardia"
        if ((canonicalVerb == "talk" || canonicalVerb == "say" || canonicalVerb == "option")
            && string.IsNullOrEmpty(direct) && !string.IsNullOrEmpty(indirect))
        {
            direct = indirect;
            indirect = null;
        }

        return new ParsedCommand(
            canonicalVerb,
            string.IsNullOrWhiteSpace(direct) ? null : direct,
            string.IsNullOrWhiteSpace(indirect) ? null : indirect,
            prepKind);
    }

    private static bool HasVerbAlias(string token)
    {
        return WorldVerbAliases.ContainsKey(token) || GlobalVerbAliases.ContainsKey(token);
    }

    private static string NormalizeVerb(string verb)
    {
        var token = NormalizeToken(verb);
        if (string.IsNullOrEmpty(token))
            return string.Empty;

        if (WorldVerbAliases.TryGetValue(token, out var vWorld))
            return vWorld;

        if (GlobalVerbAliases.TryGetValue(token, out var vGlobal))
            return vGlobal;

        return token.ToLowerInvariant();
    }

    private static string? NormalizeNoun(string? noun)
    {
        if (string.IsNullOrWhiteSpace(noun))
            return null;

        // minúsculas
        var s = noun.ToLowerInvariant();

        // quitar puntuación sencilla
        s = ParserRegex.Punctuation.Replace(s, "");

        // normalizar espacios
        s = ParserRegex.MultiSpace.Replace(s, " ").Trim();
        if (string.IsNullOrEmpty(s))
            return null;

        // eliminar artículos / determinantes iniciales
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (parts.Count > 0 && IgnoredNounPrefixes.Contains(parts[0]))
        {
            parts.RemoveAt(0);
        }
        s = string.Join(' ', parts);
        if (string.IsNullOrWhiteSpace(s))
            return null;

        // Intentar mapear por diccionario de mundo
        if (WorldNounAliases.TryGetValue(s, out var nWorld))
            return nWorld;

        // Intentar mapear por diccionario global
        if (GlobalNounAliases.TryGetValue(s, out var nGlobal))
            return nGlobal;

        return s;
    }

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        token = token.Trim();
        token = ParserRegex.Punctuation.Replace(token, "");
        return token.ToLowerInvariant();
    }

    private static bool IsDirection(string token)
    {
        token = token.ToLowerInvariant();
        return token is "n" or "s" or "e" or "o" or "ne" or "no" or "se" or "so"
            or "norte" or "sur" or "este" or "oeste"
            or "noreste" or "noroeste" or "sureste" or "suroeste"
            or "arriba" or "abajo" or "subir" or "bajar";
    }

    private static string NormalizeDirectionToken(string dir)
    {
        dir = dir.ToLowerInvariant();
        return dir switch
        {
            "norte" or "n" => "n",
            "sur" or "s" => "s",
            "este" or "e" => "e",
            "oeste" or "o" => "o",
            "noreste" or "ne" => "ne",
            "noroeste" or "no" => "no",
            "sureste" or "se" => "se",
            "suroeste" or "so" => "so",
            "arriba" or "subir" => "up",
            "abajo" or "bajar" => "down",
            _ => dir
        };
    }
}
