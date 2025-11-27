using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using XiloAdventures.Engine.Models;

namespace XiloAdventures.Engine;

public enum PrepositionKind
{
    None,
    To,
    With,
    From,
    In
}

public record ParsedCommand(string Verb, string? DirectObject, string? IndirectObject, PrepositionKind Preposition);

public static class Parser
{
private static readonly Dictionary<string, string> VerbAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Mirar / examinar
        ["mirar"] = "look",
        ["examinar"] = "look",
        ["x"] = "look",

        // Movimiento (solo verbo, las direcciones se tratan como comando directo)
        ["ir"] = "go",
        ["ve"] = "go",

        // Inventario
        ["inventario"] = "inventory",
        ["i"] = "inventory",

        // Coger / soltar
        ["coger"] = "take",
        ["toma"] = "take",
        ["coge"] = "take",
        ["soltar"] = "drop",
        ["dejar"] = "drop",

        // Hablar
        ["hablar"] = "talk",
        ["decir"] = "say",
        ["opcion"] = "option",
        ["opción"] = "option",

        // Usar / dar
        ["usar"] = "use",
        ["dar"] = "give",

        // Misiones
        ["misiones"] = "quests",

        // Guardar / cargar
        ["guardar"] = "save",
        ["cargar"] = "load",

        // Ayuda
        ["ayuda"] = "help"
    };

    private static readonly Dictionary<string, PrepositionKind> Prepositions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = PrepositionKind.To,
        ["al"] = PrepositionKind.To,
        ["con"] = PrepositionKind.With,
        ["de"] = PrepositionKind.From,
        ["en"] = PrepositionKind.In
    };

    public static ParsedCommand Parse(string input)
    {
        input = input.Trim();

        if (string.IsNullOrWhiteSpace(input))
            return new ParsedCommand(string.Empty, null, null, PrepositionKind.None);

        input = Regex.Replace(input, @"\s+", " ");
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var verbToken = parts[0];
        var rest = parts.Length > 1 ? parts[1] : string.Empty;

        // Dirección sola: "n", "norte", "arriba", etc.
        if (!VerbAliases.ContainsKey(verbToken) && IsDirection(verbToken))
        {
            return new ParsedCommand("go", verbToken, null, PrepositionKind.None);
        }

        var canonicalVerb = VerbAliases.TryGetValue(verbToken, out var mapped) ? mapped : verbToken.ToLowerInvariant();

        if (string.IsNullOrEmpty(rest))
            return new ParsedCommand(canonicalVerb, null, null, PrepositionKind.None);

        // Buscar preposición principal
        string? direct = null;
        string? indirect = null;
        var prepKind = PrepositionKind.None;

        var tokens = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        int prepIndex = -1;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (Prepositions.TryGetValue(tokens[i], out var pk))
            {
                prepIndex = i;
                prepKind = pk;
                break;
            }
        }

        if (prepIndex == -1)
        {
            direct = rest;
        }
        else
        {
            direct = string.Join(' ', tokens.Take(prepIndex));
            indirect = string.Join(' ', tokens.Skip(prepIndex + 1));
        }

        return new ParsedCommand(canonicalVerb,
            string.IsNullOrWhiteSpace(direct) ? null : direct,
            string.IsNullOrWhiteSpace(indirect) ? null : indirect,
            prepKind);
    }

    private static bool IsDirection(string token)
    {
        token = token.ToLowerInvariant();
        return token is "n" or "s" or "e" or "o" or "ne" or "no" or "se" or "so"
            or "norte" or "sur" or "este" or "oeste"
            or "arriba" or "abajo" or "subir" or "bajar";
    }
}
