using Translator.Models;
using Translator.Parsing;

namespace Course_work_Savchuk_Savchuk.Tests;

public class LrTableBuilderTests
{
    [Fact]
    public void SimplestGrammar_GivesShiftReduceAndAccept()
    {
        LrTable table = LrTableBuilder.Build(
            new Grammar("S", new[] { "S -> a" }));

        Assert.Equal(3, table.StateCount);
        Assert.Empty(table.Conflicts);

        LrAction shift = table.Action[0]["a"];

        Assert.Equal(ActionKind.Shift, shift.Kind);

        Assert.Equal(
            new LrAction(ActionKind.Reduce, 1),
            table.Action[shift.Value][Grammar.EndMarker]);

        Assert.Equal(
            ActionKind.Accept,
            table.Action[table.Goto[0]["S"]][Grammar.EndMarker].Kind);
    }

    [Fact]
    public void Tables_HaveRowForEveryState()
    {
        LrTable table = Compiler.Table;

        Assert.True(table.StateCount > 0);
        Assert.Equal(table.StateCount, table.Action.Count);
        Assert.Equal(table.StateCount, table.Goto.Count);
    }

    [Fact]
    public void DefaultGrammar_HasNoConflicts()
    {
        Assert.Empty(Compiler.Table.Conflicts);
    }

    [Fact]
    public void DefaultGrammar_StartsOnlyWithVar()
    {
        Assert.Equal(new[] { "VAR" }, Compiler.Table.Action[0].Keys);
        Assert.Equal(ActionKind.Shift, Compiler.Table.Action[0]["VAR"].Kind);
    }

    [Fact]
    public void DefaultGrammar_ActionsReferToExistingStatesAndRules()
    {
        LrTable table = Compiler.Table;

        foreach (LrAction action in table.Action.SelectMany(row => row.Values))
        {
            if (action.Kind == ActionKind.Shift)
                Assert.InRange(action.Value, 0, table.StateCount - 1);

            if (action.Kind == ActionKind.Reduce)
                Assert.InRange(action.Value, 1, table.Grammar.Productions.Count - 1);
        }

        Assert.All(
            table.Goto.SelectMany(row => row.Values),
            state => Assert.InRange(state, 0, table.StateCount - 1));
    }

    [Fact]
    public void DefaultGrammar_KeysAreSplitBetweenActionAndGoto()
    {
        LrTable table = Compiler.Table;

        Assert.All(
            table.Action.SelectMany(row => row.Keys),
            key => Assert.Contains(key, table.Grammar.Terminals));

        Assert.All(
            table.Goto.SelectMany(row => row.Keys),
            key => Assert.Contains(key, table.Grammar.NonTerminals));
    }

    [Fact]
    public void ShiftReduceConflict_IsResolvedByShift()
    {
        LrTable table = LrTableBuilder.Build(
            new Grammar("E", new[] { "E -> E + E | id" }));

        string conflict = Assert.Single(table.Conflicts);

        Assert.Contains("перенос/свёртка", conflict);
        Assert.Contains("символ '+'", conflict);
        Assert.Contains("выбран перенос", conflict);

        // Перенос даёт правую ассоциативность: a + (b + c)
        var parser = new LrParser(table);

        Assert.True(parser.Parse(new[]
        {
            new Lexeme(1, "i1", "a", "", 1, 1),
            new Lexeme(2, "t1", "+", "", 1, 3),
            new Lexeme(3, "i2", "b", "", 1, 5),
            new Lexeme(4, "t1", "+", "", 1, 7),
            new Lexeme(5, "i3", "c", "", 1, 9)
        }));

        SyntaxNode root = parser.Tree!;

        Assert.Single(root.Children[0].Children);
        Assert.Equal(3, root.Children[2].Children.Count);
    }

    [Fact]
    public void ReduceReduceConflict_IsResolvedByLowerRule()
    {
        LrTable table = LrTableBuilder.Build(
            new Grammar("S", new[] { "S -> A | B", "A -> x", "B -> x" }));

        string conflict = Assert.Single(table.Conflicts);

        Assert.Contains("свёртка/свёртка", conflict);
        Assert.Contains("выбрано правило 3", conflict);

        int afterX = table.Action[0]["x"].Value;

        Assert.Equal(
            new LrAction(ActionKind.Reduce, 3),
            table.Action[afterX][Grammar.EndMarker]);
    }
}
