using Translator.Models;
using Translator.Semantics;

namespace Course_work_Savchuk_Savchuk.Tests;

public class SemanticAnalyzerTests
{
    // Анализ с ошибкой: возвращает единственное сообщение
    private static Diagnostic Error(string source)
    {
        SemanticAnalyzer semantic = Compiler.Analyze(source);

        return Assert.Single(semantic.Errors);
    }

    private static void AssertValid(string source)
    {
        SemanticAnalyzer semantic = Compiler.Analyze(source);

        Assert.Empty(semantic.Errors);
        Assert.Empty(semantic.Warnings);
    }

    [Fact]
    public void ValidProgram_HasNoErrorsAndFillsSymbolTable()
    {
        SemanticAnalyzer semantic = Compiler.Analyze(
            Compiler.Program(
                "A, B, RESULT",
                "READ(A);\nB = .NOT. A;\nRESULT = A .AND. B;\nWRITE(RESULT);"));

        Assert.Empty(semantic.Errors);
        Assert.Empty(semantic.Warnings);
        Assert.Equal(new[] { "A", "B", "RESULT" }, semantic.Declared.Keys);

        // В таблице символов — место объявления
        Assert.Equal(1, semantic.Declared["B"].Line);
        Assert.Equal(8, semantic.Declared["B"].Column);
    }

    [Fact]
    public void Analyze_ReturnsTrueOnlyWithoutErrors()
    {
        var semantic = new SemanticAnalyzer();

        Assert.True(semantic.Analyze(
            Compiler.Tree(Compiler.Program("A", "A = 1;"))));

        Assert.False(semantic.Analyze(
            Compiler.Tree(Compiler.Program("A", "B = 1;"))));
    }

    [Fact]
    public void Redeclaration_IsError()
    {
        Diagnostic error = Error(
            "VAR A, B, A : LOGICAL;\nBEGIN\nREAD(A);\nEND");

        Assert.Equal(
            "Переменная \"A\" объявлена повторно " +
            "(первое объявление: строка 1, столбец 5).",
            error.Message);

        Assert.Equal((1, 11), (error.Line, error.Column));
    }

    [Theory]
    [InlineData("begin", "BEGIN")]
    [InlineData("Var", "VAR")]
    [InlineData("Logical", "LOGICAL")]
    public void IdentifierDifferingFromKeywordOnlyByCase_IsError(
        string name,
        string keyword)
    {
        Diagnostic error = Error(
            Compiler.Program(name, $"READ({name});"));

        Assert.Equal(
            $"Идентификатор \"{name}\" совпадает с ключевым " +
            $"словом {keyword} (отличается только регистром).",
            error.Message);
    }

    [Theory]
    [InlineData("A = C;")]
    [InlineData("C = A;")]
    [InlineData("READ(C);")]
    [InlineData("WRITE(C);")]
    [InlineData("IF C THEN A = 0; ELSE A = 1; END_IF;")]
    [InlineData("IF A THEN C = 0; ELSE A = 1; END_IF;")]
    [InlineData("IF A THEN A = 0; ELSE A = C; END_IF;")]
    public void UndeclaredVariable_IsError(string statement)
    {
        Diagnostic error = Error(
            Compiler.Program("A", "READ(A);\n" + statement));

        Assert.Equal("Переменная \"C\" не объявлена.", error.Message);
        Assert.Equal(4, error.Line);
    }

    [Theory]
    [InlineData("A = B;")]
    [InlineData("A = 1 .AND. ( 0 .OR. .NOT. B );")]
    [InlineData("B = .NOT. B;")]
    [InlineData("WRITE(B);")]
    [InlineData("WRITE(A, B);")]
    [InlineData("IF B THEN A = 0; ELSE A = 1; END_IF;")]
    [InlineData("IF A THEN A = B; ELSE A = 1; END_IF;")]
    public void VariableUsedBeforeAssignment_IsError(string statement)
    {
        Diagnostic error = Error(
            Compiler.Program("A, B", "READ(A);\n" + statement));

        Assert.Equal(
            "Переменная \"B\" используется до того, как получила значение.",
            error.Message);
    }

    [Theory]
    [InlineData("B = 1;\nA = B;")]
    [InlineData("READ(A, B);\nA = B;")]
    [InlineData("B = A;\nB = .NOT. B;")]
    public void AssignmentAndRead_GiveVariableValue(string statements)
    {
        AssertValid(Compiler.Program("A, B", "READ(A);\n" + statements));
    }

    [Fact]
    public void VariableAssignedInBothBranches_HasValueAfterIf()
    {
        AssertValid(
            Compiler.Program(
                "A, B",
                "READ(A);\n" +
                "IF A THEN B = 0; ELSE B = 1; END_IF;\n" +
                "WRITE(B);"));
    }

    [Theory]
    [InlineData("IF A THEN B = 0; ELSE A = 1; END_IF;")]
    [InlineData("IF A THEN A = 0; ELSE B = 1; END_IF;")]
    public void VariableAssignedInOneBranch_HasNoValueAfterIf(string conditional)
    {
        Diagnostic error = Error(
            Compiler.Program(
                "A, B",
                "READ(A);\n" + conditional + "\nWRITE(B);"));

        Assert.Contains("\"B\" используется до того", error.Message);
        Assert.Equal(5, error.Line);
    }

    // Значение, полученное в ветке THEN, в ветке ELSE недоступно
    [Fact]
    public void BranchesOfIf_DoNotShareAssignments()
    {
        Diagnostic error = Error(
            Compiler.Program(
                "A, B",
                "READ(A);\nIF A THEN B = 0; ELSE A = B; END_IF;"));

        Assert.Contains("\"B\" используется до того", error.Message);
    }

    [Fact]
    public void AssignmentInsideBranch_IsVisibleLaterInSameBranch()
    {
        AssertValid(
            Compiler.Program(
                "A, B",
                "READ(A);\nIF A THEN B = 0; A = B; ELSE B = 1; END_IF;"));
    }

    [Fact]
    public void Variables_AreCaseSensitive()
    {
        Diagnostic error = Error(Compiler.Program("A", "READ(a);"));

        Assert.Equal("Переменная \"a\" не объявлена.", error.Message);

        AssertValid(Compiler.Program("a, A", "READ(a);\nA = a;"));
    }

    [Fact]
    public void Analysis_StopsAtFirstError()
    {
        SemanticAnalyzer semantic = Compiler.Analyze(
            Compiler.Program("A, UNUSED", "A = C;\nA = D;\nWRITE(E);"));

        Assert.Contains("\"C\"", Assert.Single(semantic.Errors).Message);

        // Предупреждения выдаются только для программы без ошибок
        Assert.Empty(semantic.Warnings);
    }

    [Fact]
    public void UnusedVariable_IsWarningNotError()
    {
        SemanticAnalyzer semantic = Compiler.Analyze(
            Compiler.Program("A, B, C", "READ(A);"));

        Assert.Empty(semantic.Errors);

        Assert.Equal(
            new[]
            {
                "Переменная \"B\" объявлена, но не используется.",
                "Переменная \"C\" объявлена, но не используется."
            },
            semantic.Warnings.Select(w => w.Message));

        Assert.Equal(
            new[] { (1, 8), (1, 11) },
            semantic.Warnings.Select(w => (w.Line, w.Column)));
    }

    [Theory]
    [InlineData("B = 1;")]
    [InlineData("READ(B);")]
    public void VariableThatOnlyReceivesValue_IsConsideredUsed(string statement)
    {
        AssertValid(Compiler.Program("B", statement));
    }

    [Fact]
    public void Analyze_ClearsPreviousResults()
    {
        var semantic = new SemanticAnalyzer();

        semantic.Analyze(Compiler.Tree(Compiler.Program("A, B", "READ(A);")));
        semantic.Analyze(Compiler.Tree(Compiler.Program("X", "X = Y;")));

        Assert.Single(semantic.Errors);
        Assert.Empty(semantic.Warnings);
        Assert.Equal(new[] { "X" }, semantic.Declared.Keys);

        semantic.Analyze(Compiler.Tree(Compiler.Program("Z", "READ(Z);")));

        Assert.Empty(semantic.Errors);
        Assert.Empty(semantic.Warnings);
        Assert.Equal(new[] { "Z" }, semantic.Declared.Keys);
    }
}
