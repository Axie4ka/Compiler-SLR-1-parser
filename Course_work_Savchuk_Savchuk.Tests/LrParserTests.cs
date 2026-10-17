using Translator.Models;
using Translator.Parsing;

namespace Course_work_Savchuk_Savchuk.Tests;

public class LrParserTests
{
    private const string Valid =
        "VAR A, B : LOGICAL;\n" +
        "BEGIN\n" +
        "    READ(A);\n" +
        "    B = .NOT. A;\n" +
        "    WRITE(A, B);\n" +
        "END";

    // Разбор с ошибкой: возвращает единственное сообщение
    private static Diagnostic Error(string source)
    {
        LrParser parser = Compiler.Parse(source);

        Assert.Null(parser.Tree);

        return Assert.Single(parser.Errors);
    }

    [Theory]
    [InlineData("i1", "A", "id")]
    [InlineData("i25", "RESULT", "id")]
    [InlineData("c1", "0", "const")]
    [InlineData("t1", "VAR", "VAR")]
    [InlineData("t17", ".AND.", ".AND.")]
    [InlineData("t12", ";", ";")]
    public void ToTerminal_MapsLexemeToGrammarSymbol(
        string code,
        string text,
        string terminal)
    {
        var lexeme = new Lexeme(1, code, text, "", 1, 1);

        Assert.Equal(terminal, LrParser.ToTerminal(lexeme));
    }

    [Fact]
    public void ValidProgram_IsAccepted()
    {
        LrParser parser = Compiler.Parse(Valid);

        Assert.Empty(parser.Errors);
        Assert.NotNull(parser.Tree);
        Assert.Equal("Программа", parser.Tree!.Symbol);
        Assert.Equal("Допуск", parser.Steps[^1].Action);
    }

    [Fact]
    public void Tree_KeepsLexemesInSourceOrder()
    {
        SyntaxNode tree = Compiler.Tree(Valid);

        Assert.Equal(
            new[] { "A", "B", "A", "B", "A", "A", "B" },
            tree.Identifiers().Select(l => l.Text));

        Assert.Equal(2, tree.Children.Count);
        Assert.Equal(Nodes.Declaration, tree.Children[0].Symbol);
    }

    [Fact]
    public void Steps_AreNumberedAndStartFromInitialState()
    {
        LrParser parser = Compiler.Parse(Valid);

        Assert.Equal(
            Enumerable.Range(1, parser.Steps.Count),
            parser.Steps.Select(s => s.Number));

        ParseStep first = parser.Steps[0];

        Assert.Equal("0", first.Stack);
        Assert.StartsWith("VAR id , id", first.Input);
        Assert.EndsWith("...", first.Input);
        Assert.StartsWith("Перенос", first.Action);

        Assert.Equal("$", parser.Steps[^1].Input);
    }

    [Theory]
    [InlineData("B = A .AND. B;")]
    [InlineData("B = A .OR. B .AND. .NOT. A .EQU. 1;")]
    [InlineData("B = ( ( A ) );")]
    [InlineData("B = .NOT. .NOT. ( A .OR. 0 );")]
    [InlineData("IF A THEN B = 0; ELSE B = 1; END_IF;")]
    [InlineData("IF A .EQU. 1 THEN B = 0; A = 1; ELSE B = 1; A = 0; END_IF;")]
    [InlineData("WRITE(A); READ(B);")]
    public void ValidStatement_IsAccepted(string statement)
    {
        LrParser parser = Compiler.Parse(
            Compiler.Program("A, B", "READ(A);\n" + statement));

        Assert.Empty(parser.Errors);
    }

    [Theory]
    // Пустой раздел вычислений
    [InlineData("")]
    // Нет ветки ELSE
    [InlineData("IF A THEN B = 0; END_IF;")]
    // В ветках допустимы только присваивания
    [InlineData("IF A THEN READ(B); ELSE B = 1; END_IF;")]
    [InlineData("IF A THEN IF A THEN B = 0; ELSE B = 1; END_IF; ELSE B = 1; END_IF;")]
    // Пустая ветка
    [InlineData("IF A THEN ELSE B = 1; END_IF;")]
    [InlineData("B = ;")]
    [InlineData("B = A .AND. ;")]
    [InlineData("B = ( A ;")]
    [InlineData("B = A B;")]
    [InlineData("READ();")]
    [InlineData("READ(A,);")]
    [InlineData("READ(1);")]
    [InlineData("WRITE(A .AND. B);")]
    [InlineData("1 = A;")]
    public void InvalidStatement_IsRejected(string statement)
    {
        LrParser parser = Compiler.Parse(Compiler.Program("A, B", statement));

        Assert.Single(parser.Errors);
        Assert.Null(parser.Tree);
        Assert.Equal("Ошибка", parser.Steps[^1].Action);
    }

    [Fact]
    public void EmptyInput_ExpectsVar()
    {
        Diagnostic error = Error("");

        Assert.Equal(
            "Неожиданный конец программы. Ожидалось: VAR.",
            error.Message);

        Assert.Equal((1, 1), (error.Line, error.Column));
    }

    [Fact]
    public void UnexpectedEnd_ListsPossibleContinuations()
    {
        Diagnostic error = Error("VAR A : LOGICAL;\nBEGIN\nREAD(A);");

        Assert.Equal(
            "Неожиданный конец программы. " +
            "Ожидалось: END, IF, READ, WRITE, идентификатор.",
            error.Message);

        // Сразу за последней лексемой
        Assert.Equal((3, 9), (error.Line, error.Column));
    }

    [Fact]
    public void MissingSemicolonAtEndOfLine_PointsToPlaceOfOmission()
    {
        Diagnostic error = Error(
            "VAR A, B : LOGICAL;\n" +
            "BEGIN\n" +
            "    READ(A)\n" +
            "    B = A;\n" +
            "END");

        Assert.Equal(
            "После \")\" ожидалось: ;. Найдено: \"B\".",
            error.Message);

        Assert.Equal((3, 12), (error.Line, error.Column));
    }

    [Fact]
    public void UnexpectedSymbolInsideLine_PointsToIt()
    {
        Diagnostic error = Error("VAR A B : LOGICAL;");

        Assert.Equal(
            "Неожиданный символ \"B\". Ожидалось: ,, :.",
            error.Message);

        Assert.Equal((1, 7), (error.Line, error.Column));
    }

    [Fact]
    public void KeywordInPlaceOfIdentifier_IsExplained()
    {
        Diagnostic error = Error("VAR BEGIN : LOGICAL;");

        Assert.Equal(
            "Неожиданный символ \"BEGIN\". Ожидалось: идентификатор. " +
            "Ключевое слово нельзя использовать как идентификатор.",
            error.Message);
    }

    [Fact]
    public void ExpressionError_OffersOperands()
    {
        Diagnostic error = Error(Compiler.Program("A", "A = ;"));

        Assert.Contains("идентификатор", error.Message);
        Assert.Contains("константа (0 или 1)", error.Message);
        Assert.Contains(".NOT.", error.Message);
        Assert.Contains("(", error.Message);
    }

    [Fact]
    public void TextAfterEnd_IsRejected()
    {
        Diagnostic error = Error(Compiler.Program("A", "READ(A);") + " END");

        Assert.Contains("конец программы", error.Message);
    }

    [Fact]
    public void Parse_ClearsPreviousResults()
    {
        var parser = new LrParser(Compiler.Table);

        Assert.True(parser.Parse(Compiler.Lex(Valid).Lexemes));
        Assert.False(parser.Parse(Compiler.Lex("VAR").Lexemes));

        Assert.Null(parser.Tree);
        Assert.Single(parser.Errors);
        Assert.Equal(2, parser.Steps.Count);

        Assert.True(parser.Parse(Compiler.Lex(Valid).Lexemes));

        Assert.Empty(parser.Errors);
        Assert.NotNull(parser.Tree);
    }
}
