using Translator.Generation;
using Translator.Lexing;
using Translator.Models;
using Translator.Parsing;
using Translator.Semantics;

namespace Course_work_Savchuk_Savchuk.Tests;

// Запуск этапов компилятора над исходным текстом.
// Каждый метод требует, чтобы предыдущие этапы прошли без ошибок.
internal static class Compiler
{
    // Таблица разбора строится один раз: она зависит только от грамматики
    public static readonly LrTable Table =
        LrTableBuilder.Build(Grammar.CreateDefault());

    // Программа с заданными объявлениями и телом
    public static string Program(string variables, string body)
    {
        return $"VAR {variables} : LOGICAL;\nBEGIN\n{body}\nEND";
    }

    public static Lexer Lex(string source)
    {
        var lexer = new Lexer();

        lexer.Analyze(source);

        return lexer;
    }

    public static LrParser Parse(string source)
    {
        Lexer lexer = Lex(source);

        Assert.Empty(lexer.Errors);

        var parser = new LrParser(Table);

        parser.Parse(lexer.Lexemes);

        return parser;
    }

    public static SyntaxNode Tree(string source)
    {
        LrParser parser = Parse(source);

        Assert.Empty(parser.Errors);

        return parser.Tree!;
    }

    public static SemanticAnalyzer Analyze(string source)
    {
        var semantic = new SemanticAnalyzer();

        semantic.Analyze(Tree(source));

        return semantic;
    }

    public static List<PostfixItem> Postfix(string source)
    {
        return new PostfixGenerator().Generate(Tree(source));
    }

    // Постфиксная запись одной строкой
    public static string PostfixText(string source)
    {
        return string.Join(" ", Postfix(source));
    }

    public static string Assembly(string source)
    {
        SyntaxNode tree = Tree(source);

        var semantic = new SemanticAnalyzer();

        Assert.True(semantic.Analyze(tree));

        return new CodeGenerator().Generate(
            new PostfixGenerator().Generate(tree),
            semantic.Declared.Keys);
    }
}
