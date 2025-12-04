using System;
using System.Collections.Generic;
using System.Text.Json;
using XiloAdventures.Engine;
using Xunit;

namespace XiloAdventures.Tests;

public class ParserTests
{
    [Fact]
    public void Parse_SingleDirection_DefaultsToGoVerb()
    {
        var parsed = Parser.Parse("n");

        Assert.Equal("go", parsed.Verb);
        Assert.Equal("n", parsed.DirectObject);
        Assert.Null(parsed.IndirectObject);
        Assert.Equal(PrepositionKind.None, parsed.Preposition);
    }

    [Fact]
    public void Parse_KnownVerbSynonym_NormalizesVerbAndNoun()
    {
        var parsed = Parser.Parse("examinar la espada!");

        Assert.Equal("look", parsed.Verb);
        Assert.Equal("espada", parsed.DirectObject);
        Assert.Null(parsed.IndirectObject);
    }

    [Fact]
    public void Parse_GoWithPreposition_MovesTargetToDirectObject()
    {
        var parsed = Parser.Parse("ve al norte");

        Assert.Equal("go", parsed.Verb);
        Assert.Equal("norte", parsed.DirectObject);
        Assert.Null(parsed.IndirectObject);
        Assert.Equal(PrepositionKind.To, parsed.Preposition);
    }

    [Fact]
    public void Parse_TalkWithNpc_MovesNpcToDirectObject()
    {
        var parsed = Parser.Parse("hablar con el enano");

        Assert.Equal("talk", parsed.Verb);
        Assert.Equal("enano", parsed.DirectObject);
        Assert.Null(parsed.IndirectObject);
        Assert.Equal(PrepositionKind.With, parsed.Preposition);
    }

    [Fact]
    public void Parse_UseKeepsPreposition_WhenBothObjectsPresent()
    {
        var parsed = Parser.Parse("usar llave con puerta oxidada");

        Assert.Equal("use", parsed.Verb);
        Assert.Equal("llave", parsed.DirectObject);
        Assert.Equal("puerta oxidada", parsed.IndirectObject);
        Assert.Equal(PrepositionKind.With, parsed.Preposition);
    }

    [Fact]
    public void Parse_WorldDictionaryOverridesGlobalAliases()
    {
        var dict = new
        {
            verbs = new Dictionary<string, string[]?>
            {
                ["pescar"] = new[] { "fish" }
            },
            nouns = new Dictionary<string, string[]?>
            {
                ["trucha"] = new[] { "trout" }
            }
        };

        var json = JsonSerializer.Serialize(dict);

        try
        {
            Parser.SetWorldDictionary(json);
            var parsed = Parser.Parse("fish la trout");

            Assert.Equal("pescar", parsed.Verb);
            Assert.Equal("trucha", parsed.DirectObject);
        }
        finally
        {
            Parser.SetWorldDictionary(null);
        }
    }
}
