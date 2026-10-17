using Translator.Generation;
using Translator.Models;

namespace Course_work_Savchuk_Savchuk.Tests;

public class PostfixGeneratorTests
{
    // Постфиксная запись операторов тела программы
    private static string Postfix(string body)
    {
        return Compiler.PostfixText(Compiler.Program("A, B, C, R", body));
    }

    [Theory]
    [InlineData("R = A;", "R A =")]
    [InlineData("R = 1;", "R 1 =")]
    [InlineData("R = .NOT. A;", "R A .NOT. =")]
    [InlineData("R = A .AND. B;", "R A B .AND. =")]
    [InlineData("R = A .OR. B;", "R A B .OR. =")]
    [InlineData("R = A .EQU. B;", "R A B .EQU. =")]
    public void Assignment_PutsOperationAfterOperands(string body, string expected)
    {
        Assert.Equal(expected, Postfix(body));
    }

    // Старшинство по убыванию: .NOT., .AND., .OR., .EQU.
    [Theory]
    [InlineData("R = A .OR. B .AND. C;", "R A B C .AND. .OR. =")]
    [InlineData("R = A .AND. B .OR. C;", "R A B .AND. C .OR. =")]
    [InlineData("R = A .EQU. B .OR. C;", "R A B C .OR. .EQU. =")]
    [InlineData("R = A .OR. B .EQU. C;", "R A B .OR. C .EQU. =")]
    [InlineData("R = .NOT. A .AND. B;", "R A .NOT. B .AND. =")]
    [InlineData("R = A .AND. .NOT. B;", "R A B .NOT. .AND. =")]
    [InlineData(
        "R = A .EQU. B .OR. C .AND. .NOT. 1;",
        "R A B C 1 .NOT. .AND. .OR. .EQU. =")]
    public void Expression_RespectsPrecedence(string body, string expected)
    {
        Assert.Equal(expected, Postfix(body));
    }

    [Theory]
    [InlineData("R = A .OR. B .OR. C;", "R A B .OR. C .OR. =")]
    [InlineData("R = A .AND. B .AND. C;", "R A B .AND. C .AND. =")]
    [InlineData("R = A .EQU. B .EQU. C;", "R A B .EQU. C .EQU. =")]
    public void BinaryOperations_AreLeftAssociative(string body, string expected)
    {
        Assert.Equal(expected, Postfix(body));
    }

    [Theory]
    [InlineData("R = ( A .OR. B ) .AND. C;", "R A B .OR. C .AND. =")]
    [InlineData("R = A .AND. ( B .OR. C );", "R A B C .OR. .AND. =")]
    [InlineData("R = .NOT. ( A .AND. B );", "R A B .AND. .NOT. =")]
    [InlineData("R = ( ( A ) );", "R A =")]
    [InlineData("R = .NOT. .NOT. A;", "R A .NOT. .NOT. =")]
    public void Parentheses_ChangeOrderAndDisappear(string body, string expected)
    {
        Assert.Equal(expected, Postfix(body));
    }

    [Fact]
    public void Read_IsGeneratedForEachVariable()
    {
        Assert.Equal("A READ B READ", Postfix("READ(A, B);"));
    }

    [Fact]
    public void Write_IsGeneratedForEachVariable()
    {
        Assert.Equal("A WRITE B WRITE C WRITE", Postfix("WRITE(A, B, C);"));
    }

    [Fact]
    public void Statements_FollowInSourceOrder()
    {
        Assert.Equal(
            "A READ B A .NOT. = B WRITE",
            Postfix("READ(A);\nB = .NOT. A;\nWRITE(B);"));
    }

    [Fact]
    public void Conditional_UsesJumpsAndLabels()
    {
        Assert.Equal(
            "A 1 .EQU. M1 УПЛ B 0 = M2 БП M1: B 1 = C A = M2:",
            Postfix("IF A .EQU. 1 THEN B = 0; ELSE B = 1; C = A; END_IF;"));
    }

    [Fact]
    public void EachConditional_GetsOwnLabels()
    {
        Assert.Equal(
            "A M1 УПЛ B 0 = M2 БП M1: B 1 = M2: " +
            "B M3 УПЛ C 0 = M4 БП M3: C 1 = M4:",
            Postfix(
                "IF A THEN B = 0; ELSE B = 1; END_IF;\n" +
                "IF B THEN C = 0; ELSE C = 1; END_IF;"));
    }

    [Fact]
    public void Items_HaveKinds()
    {
        List<PostfixItem> items = Compiler.Postfix(
            Compiler.Program(
                "A, B",
                "READ(A);\n" +
                "IF A THEN B = .NOT. 1; ELSE B = A; END_IF;\n" +
                "WRITE(B);"));

        Assert.Equal(
            new[]
            {
                PostfixKind.Address, PostfixKind.Read,
                PostfixKind.Variable,
                PostfixKind.LabelRef, PostfixKind.JumpIfFalse,
                PostfixKind.Address, PostfixKind.Constant,
                PostfixKind.Operation, PostfixKind.Operation,
                PostfixKind.LabelRef, PostfixKind.Jump,
                PostfixKind.Label,
                PostfixKind.Address, PostfixKind.Variable,
                PostfixKind.Operation,
                PostfixKind.Label,
                PostfixKind.Address, PostfixKind.Write
            },
            items.Select(i => i.Kind));
    }

    [Fact]
    public void Items_AreNumberedFromOne()
    {
        List<PostfixItem> items = Compiler.Postfix(
            Compiler.Program("A", "READ(A);\nA = .NOT. A;"));

        Assert.Equal(
            Enumerable.Range(1, items.Count),
            items.Select(i => i.Number));
    }

    [Fact]
    public void Generate_StartsAnewOnEachCall()
    {
        var generator = new PostfixGenerator();

        var tree = Compiler.Tree(
            Compiler.Program("A", "IF 1 THEN A = 0; ELSE A = 1; END_IF;"));

        string first = string.Join(" ", generator.Generate(tree));
        string second = string.Join(" ", generator.Generate(tree));

        Assert.Equal("1 M1 УПЛ A 0 = M2 БП M1: A 1 = M2:", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void PostfixItem_PrintsLabelDefinitionWithColon()
    {
        Assert.Equal("M1:", new PostfixItem(1, PostfixKind.Label, "M1").ToString());
        Assert.Equal("M1", new PostfixItem(1, PostfixKind.LabelRef, "M1").ToString());
        Assert.Equal(".AND.", new PostfixItem(1, PostfixKind.Operation, ".AND.").ToString());
    }
}
