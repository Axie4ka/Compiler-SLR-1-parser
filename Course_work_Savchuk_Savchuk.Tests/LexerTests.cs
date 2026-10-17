using Translator.Lexing;
using Translator.Models;

namespace Course_work_Savchuk_Savchuk.Tests;

public class LexerTests
{
    [Theory]
    [InlineData("VAR", "t1")]
    [InlineData("BEGIN", "t2")]
    [InlineData("END", "t3")]
    [InlineData("LOGICAL", "t4")]
    [InlineData("READ", "t5")]
    [InlineData("WRITE", "t6")]
    [InlineData("IF", "t7")]
    [InlineData("THEN", "t8")]
    [InlineData("ELSE", "t9")]
    [InlineData("END_IF", "t20")]
    public void Keyword_GetsItsCode(string text, string code)
    {
        Lexer lexer = Compiler.Lex(text);

        Lexeme lexeme = Assert.Single(lexer.Lexemes);

        Assert.Equal(code, lexeme.Code);
        Assert.Equal(text, lexeme.Text);
        Assert.Equal("Ключевое слово", lexeme.Type);
        Assert.Empty(lexer.Identifiers);
    }

    [Theory]
    [InlineData("=", "t10")]
    [InlineData(":", "t11")]
    [InlineData(";", "t12")]
    [InlineData(",", "t13")]
    [InlineData("(", "t14")]
    [InlineData(")", "t15")]
    [InlineData(".NOT.", "t16")]
    [InlineData(".AND.", "t17")]
    [InlineData(".OR.", "t18")]
    [InlineData(".EQU.", "t19")]
    public void Operator_GetsItsCode(string text, string code)
    {
        Lexer lexer = Compiler.Lex(text);

        Lexeme lexeme = Assert.Single(lexer.Lexemes);

        Assert.Equal(code, lexeme.Code);
        Assert.Equal(text, lexeme.Text);
    }

    [Fact]
    public void EmptySource_GivesNoLexemesAndNoErrors()
    {
        var lexer = new Lexer();

        Assert.True(lexer.Analyze(""));
        Assert.Empty(lexer.Lexemes);
        Assert.Empty(lexer.Errors);
    }

    [Fact]
    public void WhitespaceOnly_GivesNoLexemes()
    {
        var lexer = new Lexer();

        Assert.True(lexer.Analyze(" \t\r\n  \n"));
        Assert.Empty(lexer.Lexemes);
    }

    // Ключевые слова пишутся только прописными буквами
    [Fact]
    public void KeywordInLowerCase_IsIdentifier()
    {
        Lexer lexer = Compiler.Lex("var");

        Lexeme lexeme = Assert.Single(lexer.Lexemes);

        Assert.Equal("i1", lexeme.Code);
        Assert.Equal("Идентификатор", lexeme.Type);
    }

    [Fact]
    public void Identifiers_AreNumberedInOrderOfAppearance()
    {
        Lexer lexer = Compiler.Lex("A B RESULT");

        Assert.Equal(
            new[] { "i1", "i2", "i3" },
            lexer.Lexemes.Select(l => l.Code));

        Assert.Equal(
            new[] { "A", "B", "RESULT" },
            lexer.Identifiers.Select(s => s.Name));

        Assert.Equal(
            new[] { 1, 2, 3 },
            lexer.Identifiers.Select(s => s.Number));
    }

    [Fact]
    public void RepeatedIdentifier_ReusesCode()
    {
        Lexer lexer = Compiler.Lex("A B A");

        Assert.Equal(
            new[] { "i1", "i2", "i1" },
            lexer.Lexemes.Select(l => l.Code));

        Assert.Equal(2, lexer.Identifiers.Count);
    }

    [Fact]
    public void Identifiers_AreCaseSensitive()
    {
        Lexer lexer = Compiler.Lex("a A");

        Assert.Equal(
            new[] { "i1", "i2" },
            lexer.Lexemes.Select(l => l.Code));
    }

    [Fact]
    public void Constants_AreNumberedAndReused()
    {
        Lexer lexer = Compiler.Lex("1 0 1");

        Assert.Equal(
            new[] { "c1", "c2", "c1" },
            lexer.Lexemes.Select(l => l.Code));

        Assert.Equal(
            new[] { "1", "0" },
            lexer.Constants.Select(s => s.Name));

        Assert.All(lexer.Lexemes, l => Assert.Equal("Константа", l.Type));
    }

    [Fact]
    public void Lexemes_AreNumberedFromOne()
    {
        Lexer lexer = Compiler.Lex("VAR A : LOGICAL ;");

        Assert.Equal(
            new[] { 1, 2, 3, 4, 5 },
            lexer.Lexemes.Select(l => l.Number));
    }

    [Fact]
    public void OperatorsWithoutSpaces_AreSeparated()
    {
        Lexer lexer = Compiler.Lex("RESULT=A.AND.B;");

        Assert.Empty(lexer.Errors);

        Assert.Equal(
            new[] { "RESULT", "=", "A", ".AND.", "B", ";" },
            lexer.Lexemes.Select(l => l.Text));
    }

    [Fact]
    public void Positions_AreTrackedAcrossLines()
    {
        Lexer lexer = Compiler.Lex("VAR A\r\n  B\n\tC");

        Assert.Equal(
            new[] { (1, 1), (1, 5), (2, 3), (3, 2) },
            lexer.Lexemes.Select(l => (l.Line, l.Column)));
    }

    [Fact]
    public void IdentifierOfMaximumLength_IsAccepted()
    {
        var lexer = new Lexer();

        Assert.True(lexer.Analyze("ABCDEFGHI"));
        Assert.Equal("ABCDEFGHI", Assert.Single(lexer.Identifiers).Name);
    }

    [Fact]
    public void TooLongIdentifier_IsError()
    {
        var lexer = new Lexer();

        Assert.False(lexer.Analyze("ABCDEFGHIJ"));

        Diagnostic error = Assert.Single(lexer.Errors);

        Assert.Contains("превышает", error.Message);
        Assert.Contains("\"ABCDEFGHIJ\"", error.Message);
        Assert.Empty(lexer.Identifiers);
    }

    [Theory]
    [InlineData("A1")]
    [InlineData("A_B")]
    [InlineData("END_")]
    [InlineData("END_IFX")]
    public void IdentifierWithNonLetters_IsError(string text)
    {
        var lexer = new Lexer();

        Assert.False(lexer.Analyze(text));

        Diagnostic error = Assert.Single(lexer.Errors);

        Assert.Contains("только из букв", error.Message);
        Assert.Empty(lexer.Lexemes);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("10")]
    [InlineData("01")]
    [InlineData("1A")]
    public void InvalidConstant_IsError(string text)
    {
        var lexer = new Lexer();

        Assert.False(lexer.Analyze(text));

        Diagnostic error = Assert.Single(lexer.Errors);

        Assert.Contains($"Недопустимая константа \"{text}\"", error.Message);
        Assert.Empty(lexer.Constants);
    }

    [Theory]
    [InlineData(".XOR.")]
    [InlineData(".and.")]
    [InlineData("..")]
    public void UnknownDotOperator_IsError(string text)
    {
        var lexer = new Lexer();

        Assert.False(lexer.Analyze(text));

        Assert.Contains(
            $"Неизвестный оператор \"{text}\"",
            Assert.Single(lexer.Errors).Message);
    }

    [Theory]
    [InlineData(".")]
    [InlineData(".AND")]
    [InlineData(".AND B")]
    [InlineData(".A1.")]
    public void UnclosedDotOperator_IsError(string text)
    {
        var lexer = new Lexer();

        Assert.False(lexer.Analyze(text));

        Assert.Contains(
            "Ожидался оператор",
            Assert.Single(lexer.Errors).Message);
    }

    [Theory]
    [InlineData("@")]
    [InlineData("+")]
    [InlineData("_")]
    [InlineData("Я")]
    public void UnknownCharacter_IsError(string text)
    {
        var lexer = new Lexer();

        Assert.False(lexer.Analyze(text));

        Assert.Contains(
            $"Недопустимый символ \"{text}\"",
            Assert.Single(lexer.Errors).Message);
    }

    [Fact]
    public void Error_PointsToStartOfBadLexeme()
    {
        Lexer lexer = Compiler.Lex("VAR A;\n  B = 0.XOR.A;");

        Diagnostic error = Assert.Single(lexer.Errors);

        Assert.Equal(2, error.Line);
        Assert.Equal(8, error.Column);
    }

    [Fact]
    public void Analysis_StopsAtFirstError()
    {
        Lexer lexer = Compiler.Lex("VAR A, @ # LONGIDENTIFIER");

        Diagnostic error = Assert.Single(lexer.Errors);

        Assert.Contains("\"@\"", error.Message);

        // Лексемы до ошибки сохраняются
        Assert.Equal(
            new[] { "VAR", "A", "," },
            lexer.Lexemes.Select(l => l.Text));
    }

    [Fact]
    public void Analyze_ClearsPreviousResults()
    {
        var lexer = new Lexer();

        lexer.Analyze("A B 1 @");

        Assert.True(lexer.Analyze("C 0"));

        Assert.Equal(2, lexer.Lexemes.Count);
        Assert.Equal("C", Assert.Single(lexer.Identifiers).Name);
        Assert.Equal("0", Assert.Single(lexer.Constants).Name);
        Assert.Empty(lexer.Errors);
    }

    [Theory]
    [InlineData("VAR", "VAR")]
    [InlineData("var", "VAR")]
    [InlineData("Begin", "BEGIN")]
    [InlineData("end_if", "END_IF")]
    public void TryGetKeyword_IgnoresCase(string word, string keyword)
    {
        Assert.True(Lexer.TryGetKeyword(word, out string found));
        Assert.Equal(keyword, found);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("ENDIF")]
    [InlineData("")]
    public void TryGetKeyword_RejectsOtherWords(string word)
    {
        Assert.False(Lexer.TryGetKeyword(word, out string found));
        Assert.Equal("", found);
    }
}
