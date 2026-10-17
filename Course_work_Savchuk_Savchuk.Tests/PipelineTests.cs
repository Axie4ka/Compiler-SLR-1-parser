using Translator.Lexing;
using Translator.Models;
using Translator.Parsing;
using Translator.Semantics;

namespace Course_work_Savchuk_Savchuk.Tests;

// Примеры программ из Program.cs, пройденные через все этапы
public class PipelineTests
{
    [Fact]
    public void CorrectProgram_IsCompiled()
    {
        const string source =
            "VAR A, B, RESULT : LOGICAL;\r\n" +
            "BEGIN\r\n" +
            "    READ(A);\r\n" +
            "    B = .NOT. A;\r\n" +
            "    RESULT = A .AND. B;\r\n" +
            "    WRITE(RESULT);\r\n" +
            "END\r\n";

        Assert.Equal(
            "A READ B A .NOT. = RESULT A B .AND. = RESULT WRITE",
            Compiler.PostfixText(source));

        string assembly = Compiler.Assembly(source);

        Assert.Contains("call ReadBit", assembly);
        Assert.Contains("and al, bl", assembly);
        Assert.Contains("mov V_RESULT, al", assembly);
        Assert.Contains("call WriteBit", assembly);
    }

    [Fact]
    public void ProgramWithConditional_IsCompiled()
    {
        const string source =
            "VAR A, B : LOGICAL;\n" +
            "BEGIN\n" +
            "    READ(A);\n" +
            "    IF A .EQU. 1 THEN\n" +
            "        B = 0;\n" +
            "    ELSE\n" +
            "        B = ( A .OR. 1 ) .AND. .NOT. A;\n" +
            "    END_IF;\n" +
            "    WRITE(B);\n" +
            "END";

        Assert.Empty(Compiler.Analyze(source).Warnings);

        Assert.Equal(
            "A READ A 1 .EQU. M1 УПЛ B 0 = M2 БП " +
            "M1: B A 1 .OR. A .NOT. .AND. = M2: B WRITE",
            Compiler.PostfixText(source));

        string assembly = Compiler.Assembly(source);

        Assert.Contains("\r\nM1:\r\n", assembly);
        Assert.Contains("\r\nM2:\r\n", assembly);
    }

    [Fact]
    public void LexicalErrors_OnlyFirstIsReported()
    {
        Lexer lexer = Compiler.Lex(
            "VAR A, B, RESULT, LONGIDENTIFIER, @ : LOGICAL;\n" +
            "BEGIN\n" +
            "    READ(A);\n" +
            "    B = 0.XOR.A;\n" +
            "    RESULT = A.AND.B;\n" +
            "    WRITE(RESULT);\n" +
            "END");

        Diagnostic error = Assert.Single(lexer.Errors);

        Assert.Contains("\"LONGIDENTIFIER\" превышает", error.Message);
        Assert.Equal((1, 19), (error.Line, error.Column));
    }

    [Fact]
    public void SyntaxError_IsReportedByParser()
    {
        LrParser parser = Compiler.Parse(
            "VAR A, B : LOGICAL;\n" +
            "BEGIN\n" +
            "    READ(A)\n" +
            "    B = A;\n" +
            "END");

        Diagnostic error = Assert.Single(parser.Errors);

        Assert.Contains("ожидалось: ;", error.Message);
        Assert.Equal(3, error.Line);
    }

    [Fact]
    public void SemanticErrors_OnlyFirstIsReported()
    {
        SemanticAnalyzer semantic = Compiler.Analyze(
            "VAR A, B, A : LOGICAL;\n" +
            "BEGIN\n" +
            "     READ(A);\n" +
            "B = C;\n" +
            "END");

        Diagnostic error = Assert.Single(semantic.Errors);

        Assert.Contains("\"A\" объявлена повторно", error.Message);
    }
}
