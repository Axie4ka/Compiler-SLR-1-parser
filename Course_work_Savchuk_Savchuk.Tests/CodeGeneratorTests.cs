using Translator.Generation;
using Translator.Models;

namespace Course_work_Savchuk_Savchuk.Tests;

public class CodeGeneratorTests
{
    // Команды основной части программы (от start: до выхода в DOS)
    // без комментариев и отступов
    private static List<string> Commands(string assembly)
    {
        return assembly
            .Split("\r\n")
            .SkipWhile(line => line != "start:")
            .Skip(1)
            .Select(line => line.Split(';')[0].Trim())
            .TakeWhile(line => line != "mov ax, 4C00h")
            .Where(line => line.Length > 0)
            // Настройка сегмента данных
            .Skip(2)
            .ToList();
    }

    // Команды, в которые переводятся операторы тела программы
    private static List<string> Translate(string body)
    {
        return Commands(
            Compiler.Assembly(
                Compiler.Program("A, B, R", "READ(A, B);\n" + body)))
            // READ(A, B)
            .Skip(6)
            .ToList();
    }

    [Fact]
    public void Program_HasSegmentsEntryPointAndExit()
    {
        string assembly = Compiler.Assembly(Compiler.Program("A", "READ(A);"));

        string[] lines = assembly.Split("\r\n");

        Assert.Contains(".model small", lines);
        Assert.Contains(".stack 100h", lines);
        Assert.Contains(".data", lines);
        Assert.Contains(".code", lines);
        Assert.Contains("start:", lines);
        Assert.Contains("    mov ax, @data", lines);
        Assert.Contains("    mov ds, ax", lines);
        Assert.Contains("    int 21h", lines);
        Assert.Contains("ReadBit proc", lines);
        Assert.Contains("WriteBit proc", lines);
        Assert.Contains("PrintNewLine proc", lines);

        Assert.Equal("end start", lines[^2]);
        Assert.Equal("", lines[^1]);
    }

    [Fact]
    public void Lines_EndWithCrLf()
    {
        string assembly = Compiler.Assembly(Compiler.Program("A", "READ(A);"));

        Assert.DoesNotContain("\n", assembly.Replace("\r\n", ""));
        Assert.DoesNotContain("\r", assembly.Replace("\r\n", ""));
    }

    // Русский текст допустим только в комментариях
    [Fact]
    public void CodeOutsideComments_IsAscii()
    {
        string assembly = Compiler.Assembly(
            Compiler.Program(
                "A, B",
                "READ(A);\nIF A THEN B = 0; ELSE B = 1; END_IF;\nWRITE(B);"));

        Assert.All(
            assembly.Split("\r\n"),
            line => Assert.All(
                line.Split(';')[0],
                c => Assert.InRange(c, ' ', '~')));
    }

    [Fact]
    public void EachVariable_GetsValueAndTitle()
    {
        string assembly = Compiler.Assembly(
            Compiler.Program("A, RESULT", "READ(A);\nRESULT = A;"));

        Assert.Matches(@"(?m)^V_A +db 0\r$", assembly);
        Assert.Matches(@"(?m)^N_A +db 'A = \$'\r$", assembly);
        Assert.Matches(@"(?m)^V_RESULT +db 0\r$", assembly);
        Assert.Matches(@"(?m)^N_RESULT +db 'RESULT = \$'\r$", assembly);
    }

    // Ассемблер регистр не различает, а исходный язык различает
    [Fact]
    public void VariablesDifferingByCase_GetDifferentNames()
    {
        string assembly = new CodeGenerator().Generate(
            new[]
            {
                new PostfixItem(1, PostfixKind.Address, "a"),
                new PostfixItem(2, PostfixKind.Read, "READ"),
                new PostfixItem(3, PostfixKind.Address, "A"),
                new PostfixItem(4, PostfixKind.Variable, "a"),
                new PostfixItem(5, PostfixKind.Operation, "=")
            },
            new[] { "a", "A" });

        Assert.Matches(@"(?m)^V_a +db 0\r$", assembly);
        Assert.Matches(@"(?m)^V_A_2 +db 0\r$", assembly);

        // Выводится имя из исходной программы
        Assert.Matches(@"(?m)^N_A_2 +db 'A = \$'\r$", assembly);

        Assert.Equal(
            new[]
            {
                "mov dx, offset N_a",
                "call ReadBit",
                "mov V_a, al",
                "mov al, V_a",
                "mov V_A_2, al"
            },
            Commands(assembly));
    }

    [Fact]
    public void Read_CallsReadBitAndStoresResult()
    {
        Assert.Equal(
            new[]
            {
                "mov dx, offset N_A",
                "call ReadBit",
                "mov V_A, al"
            },
            Commands(Compiler.Assembly(Compiler.Program("A", "READ(A);"))));
    }

    [Fact]
    public void Write_CallsWriteBitWithValueAndTitle()
    {
        Assert.Equal(
            new[]
            {
                "mov al, V_A",
                "mov dx, offset N_A",
                "call WriteBit"
            },
            Translate("WRITE(A);"));
    }

    // Пара push ax / pop ax между вычислением и сохранением удаляется
    [Theory]
    [InlineData("R = A;", "mov al, V_A")]
    [InlineData("R = 1;", "mov al, 1")]
    [InlineData("R = 0;", "mov al, 0")]
    public void SimpleAssignment_GoesThroughAlWithoutStack(
        string body,
        string load)
    {
        Assert.Equal(new[] { load, "mov V_R, al" }, Translate(body));
    }

    [Fact]
    public void Not_InvertsLowBit()
    {
        Assert.Equal(
            new[] { "mov al, V_A", "xor al, 1", "mov V_R, al" },
            Translate("R = .NOT. A;"));
    }

    [Theory]
    [InlineData(".AND.", "and al, bl")]
    [InlineData(".OR.", "or al, bl")]
    public void BinaryOperation_TakesOperandsFromStack(
        string operation,
        string command)
    {
        Assert.Equal(
            new[]
            {
                "mov al, V_A",
                "push ax",
                "mov al, V_B",
                "push ax",
                "pop bx",
                "pop ax",
                command,
                "mov V_R, al"
            },
            Translate($"R = A {operation} B;"));
    }

    [Fact]
    public void Equivalence_IsInvertedXor()
    {
        Assert.Equal(
            new[]
            {
                "mov al, V_A",
                "push ax",
                "mov al, V_B",
                "push ax",
                "pop bx",
                "pop ax",
                "xor al, bl",
                "xor al, 1",
                "mov V_R, al"
            },
            Translate("R = A .EQU. B;"));
    }

    [Fact]
    public void Conditional_JumpsOverBranches()
    {
        Assert.Equal(
            new[]
            {
                "mov al, V_A",
                "or al, al",
                "jnz S1",
                "jmp M1",
                "S1:",
                "mov al, 0",
                "mov V_R, al",
                "jmp M2",
                "M1:",
                "mov al, 1",
                "mov V_R, al",
                "M2:"
            },
            Translate("IF A THEN R = 0; ELSE R = 1; END_IF;"));
    }

    [Fact]
    public void EachConditional_GetsOwnSkipLabel()
    {
        List<string> commands = Translate(
            "IF A THEN R = 0; ELSE R = 1; END_IF;\n" +
            "IF B THEN R = 0; ELSE R = 1; END_IF;");

        Assert.Equal(
            new[] { "S1:", "M1:", "M2:", "S2:", "M3:", "M4:" },
            commands.Where(c => c.EndsWith(':')));

        Assert.Contains("jnz S2", commands);
        Assert.Contains("jmp M3", commands);
        Assert.Contains("jmp M4", commands);
    }

    // На метку может прийти переход, поэтому пара не удаляется
    [Fact]
    public void PushPopSeparatedByLabel_IsKept()
    {
        string assembly = new CodeGenerator().Generate(
            new[]
            {
                new PostfixItem(1, PostfixKind.Address, "A"),
                new PostfixItem(2, PostfixKind.Constant, "1"),
                new PostfixItem(3, PostfixKind.Label, "L"),
                new PostfixItem(4, PostfixKind.Operation, "=")
            },
            new[] { "A" });

        Assert.Equal(
            new[] { "mov al, 1", "push ax", "L:", "pop ax", "mov V_A, al" },
            Commands(assembly));
    }

    // Комментарий удалённой pop ax переходит к следующей команде
    [Fact]
    public void CommentOfRemovedPop_MovesToNextCommand()
    {
        string assembly = Compiler.Assembly(
            Compiler.Program("A, R", "READ(A);\nR = .NOT. A;"));

        Assert.Matches(@"(?m)^    mov al, V_A +; A\r$", assembly);
        Assert.Matches(@"(?m)^    xor al, 1 +; \.NOT\.\r$", assembly);
        Assert.Matches(@"(?m)^    mov V_R, al +; R =\r$", assembly);
    }

    [Fact]
    public void UnknownOperation_Throws()
    {
        var generator = new CodeGenerator();

        Assert.Throws<InvalidOperationException>(
            () => generator.Generate(
                new[] { new PostfixItem(1, PostfixKind.Operation, ".XOR.") },
                new[] { "A" }));
    }

    [Fact]
    public void Generate_StartsAnewOnEachCall()
    {
        var generator = new CodeGenerator();

        var postfix = Compiler.Postfix(
            Compiler.Program("A", "IF 1 THEN A = 0; ELSE A = 1; END_IF;"));

        string first = generator.Generate(postfix, new[] { "A" });
        string second = generator.Generate(postfix, new[] { "A" });

        Assert.Equal(first, second);

        string other = generator.Generate(
            Compiler.Postfix(Compiler.Program("B", "READ(B);")),
            new[] { "B" });

        Assert.DoesNotContain("V_A", other);
        Assert.Contains("V_B", other);
    }
}
