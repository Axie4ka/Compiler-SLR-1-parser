using Translator.Parsing;

namespace Course_work_Savchuk_Savchuk.Tests;

public class GrammarTests
{
    [Fact]
    public void Constructor_AddsAugmentedProduction()
    {
        var grammar = new Grammar("S", new[] { "S -> a" });

        Production first = grammar.Productions[0];

        Assert.Equal(Grammar.AugmentedStart, first.Left);
        Assert.Equal(new[] { "S" }, first.Right);
    }

    [Fact]
    public void Constructor_SplitsAlternatives()
    {
        var grammar = new Grammar("S", new[] { "S -> a S | b", "A ->  c\td " });

        Assert.Equal(
            new[] { "S' -> S", "S -> a S", "S -> b", "A -> c d" },
            grammar.Productions.Select(p => p.ToString()));

        Assert.Equal(
            new[] { 0, 1, 2, 3 },
            grammar.Productions.Select(p => p.Index));
    }

    [Fact]
    public void Constructor_SeparatesTerminalsAndNonTerminals()
    {
        var grammar = new Grammar("S", new[] { "S -> a A | b", "A -> c" });

        Assert.Equal(
            new[] { "A", "S", "S'" },
            grammar.NonTerminals.OrderBy(s => s, StringComparer.Ordinal));

        Assert.Equal(
            new[] { "$", "a", "b", "c" },
            grammar.Terminals.OrderBy(s => s, StringComparer.Ordinal));
    }

    [Fact]
    public void RuleWithoutArrow_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new Grammar("S", new[] { "S a b" }));
    }

    [Theory]
    [InlineData("S ->")]
    [InlineData("S -> a |")]
    [InlineData("S -> | a")]
    public void EmptyRightSide_Throws(string rule)
    {
        Assert.Throws<ArgumentException>(
            () => new Grammar("S", new[] { rule }));
    }

    [Fact]
    public void DefaultGrammar_HasExpectedSymbols()
    {
        Grammar grammar = Grammar.CreateDefault();

        Assert.Equal(new[] { "Программа" }, grammar.Productions[0].Right);
        Assert.Equal(30, grammar.Productions.Count);

        Assert.Equal(
            new[]
            {
                "$", "(", ")", ",", ".AND.", ".EQU.", ".NOT.", ".OR.",
                ":", ";", "=", "BEGIN", "ELSE", "END", "END_IF", "IF",
                "LOGICAL", "READ", "THEN", "VAR", "WRITE", "const", "id"
            },
            grammar.Terminals.OrderBy(s => s, StringComparer.Ordinal));

        Assert.Contains(Nodes.Declaration, grammar.NonTerminals);
        Assert.Contains(Nodes.Assignment, grammar.NonTerminals);
        Assert.Contains(Nodes.Input, grammar.NonTerminals);
        Assert.Contains(Nodes.Output, grammar.NonTerminals);
        Assert.Contains(Nodes.Conditional, grammar.NonTerminals);
    }

    // Каждый нетерминал правой части должен иметь своё правило
    [Fact]
    public void DefaultGrammar_HasNoLowercaseTerminalsExceptIdAndConst()
    {
        Grammar grammar = Grammar.CreateDefault();

        Assert.DoesNotContain(
            grammar.Terminals,
            t => t.Any(c => c >= 'А' && c <= 'я'));
    }
}
