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

        Assert.Equal("examine", parsed.Verb);
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

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyCommand()
    {
        var parsed = Parser.Parse("");
        Assert.Equal(string.Empty, parsed.Verb);
        Assert.Null(parsed.DirectObject);
    }

    [Fact]
    public void Parse_WhitespaceOnlyInput_ReturnsEmptyCommand()
    {
        var parsed = Parser.Parse("   ");
        Assert.Equal(string.Empty, parsed.Verb);
        Assert.Null(parsed.DirectObject);
    }

    [Theory]
    [InlineData("norte", "n")]
    [InlineData("sur", "s")]
    [InlineData("este", "e")]
    [InlineData("oeste", "o")]
    [InlineData("arriba", "up")]
    [InlineData("abajo", "down")]
    [InlineData("subir", "up")]
    [InlineData("bajar", "down")]
    [InlineData("noreste", "ne")]
    [InlineData("noroeste", "no")]
    [InlineData("sureste", "se")]
    [InlineData("suroeste", "so")]
    public void Parse_DirectionWords_NormalizedCorrectly(string input, string expectedDirection)
    {
        var parsed = Parser.Parse(input);

        Assert.Equal("go", parsed.Verb);
        Assert.Equal(expectedDirection, parsed.DirectObject);
    }

    [Fact]
    public void Parse_OpenDoor_ParsesCorrectly()
    {
        var parsed = Parser.Parse("abrir puerta");

        Assert.Equal("open", parsed.Verb);
        Assert.Equal("puerta", parsed.DirectObject);
    }

    [Fact]
    public void Parse_CloseDoor_ParsesCorrectly()
    {
        var parsed = Parser.Parse("cerrar puerta");

        Assert.Equal("close", parsed.Verb);
        Assert.Equal("puerta", parsed.DirectObject);
    }

    [Fact]
    public void Parse_TakeObject_ParsesCorrectly()
    {
        var parsed = Parser.Parse("coger espada");

        Assert.Equal("take", parsed.Verb);
        Assert.Equal("espada", parsed.DirectObject);
    }

    [Theory]
    [InlineData("coger")]
    [InlineData("toma")]
    [InlineData("coge")]
    [InlineData("tomar")]
    [InlineData("agarrar")]
    [InlineData("recoger")]
    public void Parse_TakeSynonyms_AllMapToTake(string verb)
    {
        var parsed = Parser.Parse($"{verb} objeto");

        Assert.Equal("take", parsed.Verb);
        Assert.Equal("objeto", parsed.DirectObject);
    }

    [Fact]
    public void Parse_DropObject_ParsesCorrectly()
    {
        var parsed = Parser.Parse("soltar espada");

        Assert.Equal("drop", parsed.Verb);
        Assert.Equal("espada", parsed.DirectObject);
    }

    [Theory]
    [InlineData("inventario")]
    [InlineData("inv")]
    [InlineData("i")]
    public void Parse_InventorySynonyms_AllMapToInventory(string verb)
    {
        var parsed = Parser.Parse(verb);

        Assert.Equal("inventory", parsed.Verb);
    }

    [Fact]
    public void Parse_ArticlesStripped_FromDirectObject()
    {
        var parsed = Parser.Parse("coger la espada");

        Assert.Equal("take", parsed.Verb);
        Assert.Equal("espada", parsed.DirectObject);
    }

    [Fact]
    public void Parse_MultipleArticlesStripped_FromDirectObject()
    {
        var parsed = Parser.Parse("examinar el viejo libro");

        Assert.Equal("examine", parsed.Verb);
        Assert.Equal("viejo libro", parsed.DirectObject);
    }

    [Fact]
    public void Parse_PunctuationStripped_FromInput()
    {
        var parsed = Parser.Parse("examinar la espada!");

        Assert.Equal("examine", parsed.Verb);
        Assert.Equal("espada", parsed.DirectObject);
    }

    [Fact]
    public void Parse_MultipleSpaces_Normalized()
    {
        var parsed = Parser.Parse("coger    la    espada");

        Assert.Equal("take", parsed.Verb);
        Assert.Equal("espada", parsed.DirectObject);
    }

    [Fact]
    public void Parse_PutObjectIn_ParsesCorrectly()
    {
        var parsed = Parser.Parse("meter espada en cofre");

        Assert.Equal("put", parsed.Verb);
        Assert.Equal("espada", parsed.DirectObject);
        Assert.Equal("cofre", parsed.IndirectObject);
        Assert.Equal(PrepositionKind.In, parsed.Preposition);
    }

    [Fact]
    public void Parse_GetObjectFrom_ParsesCorrectly()
    {
        var parsed = Parser.Parse("sacar espada de cofre");

        Assert.Equal("get_from", parsed.Verb);
        Assert.Equal("espada", parsed.DirectObject);
        Assert.Equal("cofre", parsed.IndirectObject);
        Assert.Equal(PrepositionKind.From, parsed.Preposition);
    }

    [Fact]
    public void Parse_GiveObjectTo_ParsesCorrectly()
    {
        var parsed = Parser.Parse("dar espada a enano");

        Assert.Equal("give", parsed.Verb);
        Assert.Equal("espada", parsed.DirectObject);
        Assert.Equal("enano", parsed.IndirectObject);
        Assert.Equal(PrepositionKind.To, parsed.Preposition);
    }

    [Fact]
    public void Parse_HelpCommand_ParsesCorrectly()
    {
        var parsed = Parser.Parse("ayuda");

        Assert.Equal("help", parsed.Verb);
        Assert.Null(parsed.DirectObject);
    }

    [Fact]
    public void Parse_UnknownVerb_PreservedAsIs()
    {
        var parsed = Parser.Parse("bailar");

        Assert.Equal("bailar", parsed.Verb);
        Assert.Null(parsed.DirectObject);
    }
}
