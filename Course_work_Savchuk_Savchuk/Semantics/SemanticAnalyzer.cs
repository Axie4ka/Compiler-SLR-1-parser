using System.Collections.Generic;
using Translator.Lexing;
using Translator.Models;
using Translator.Parsing;

namespace Translator.Semantics
{
    // Семантический анализ по дереву разбора.
    // Запускается после успешного синтаксического анализа.
    //
    // Ошибки (анализ останавливается на первой):
    //   - идентификатор совпадает с ключевым словом (без учёта регистра);
    //   - переменная объявлена повторно;
    //   - переменная не объявлена;
    //   - переменная используется до того, как получила значение.
    //
    // Предупреждения (работу не останавливают):
    //   - переменная объявлена, но нигде не используется.
    public class SemanticAnalyzer
    {
        // Анализ прекращается на первой ошибке,
        // поэтому в списке не более одной записи
        public List<Diagnostic> Errors { get; } =
            new List<Diagnostic>();

        public List<Diagnostic> Warnings { get; } =
            new List<Diagnostic>();

        // Таблица символов: имя -> лексема, где переменная объявлена
        public Dictionary<string, Lexeme> Declared { get; } =
            new Dictionary<string, Lexeme>();

        // Переменные, встретившиеся в разделе вычислений
        private readonly HashSet<string> referenced = new HashSet<string>();

        public bool Analyze(SyntaxNode tree)
        {
            Errors.Clear();
            Warnings.Clear();
            Declared.Clear();
            referenced.Clear();

            if (!Visit(tree, new HashSet<string>()))
                return false;

            foreach (Lexeme declaration in Declared.Values)
            {
                if (!referenced.Contains(declaration.Text))
                {
                    Warnings.Add(
                        new Diagnostic(
                            declaration.Line,
                            declaration.Column,
                            $"Переменная \"{declaration.Text}\" объявлена, " +
                            "но не используется."));
                }
            }

            return true;
        }

        // Обход узла дерева. initialized — переменные, которые к этому
        // месту программы гарантированно получили значение.
        // Возвращает false, если найдена ошибка.
        private bool Visit(SyntaxNode node, HashSet<string> initialized)
        {
            switch (node.Symbol)
            {
                // VAR СписокПеременных : LOGICAL ;
                case Nodes.Declaration:
                    foreach (Lexeme id in node.Children[1].Identifiers())
                    {
                        if (!Declare(id))
                            return false;
                    }

                    return true;

                // id = Выражение ;
                case Nodes.Assignment:
                    // Сначала вычисляется правая часть: A = .NOT. A
                    // при A без значения — ошибка
                    if (!CheckValues(node.Children[2], initialized))
                        return false;

                    return Assign(node.Children[0].Lexeme!, initialized);

                // READ ( СписокПеременных ) ;
                case Nodes.Input:
                    foreach (Lexeme id in node.Children[2].Identifiers())
                    {
                        if (!Assign(id, initialized))
                            return false;
                    }

                    return true;

                // WRITE ( СписокПеременных ) ;
                case Nodes.Output:
                    return CheckValues(node.Children[2], initialized);

                // IF Выражение THEN СписокПрисваиваний
                // ELSE СписокПрисваиваний END_IF ;
                case Nodes.Conditional:
                    if (!CheckValues(node.Children[1], initialized))
                        return false;

                    var afterThen = new HashSet<string>(initialized);
                    var afterElse = new HashSet<string>(initialized);

                    if (!Visit(node.Children[3], afterThen) ||
                        !Visit(node.Children[5], afterElse))
                    {
                        return false;
                    }

                    // После IF значение гарантировано только у тех
                    // переменных, которые получили его в обеих ветках
                    afterThen.IntersectWith(afterElse);
                    initialized.UnionWith(afterThen);

                    return true;

                default:
                    foreach (SyntaxNode child in node.Children)
                    {
                        if (!Visit(child, initialized))
                            return false;
                    }

                    return true;
            }
        }

        private bool Declare(Lexeme id)
        {
            if (Lexer.TryGetKeyword(id.Text, out string keyword))
            {
                return Fail(
                    id,
                    $"Идентификатор \"{id.Text}\" совпадает с ключевым " +
                    $"словом {keyword} (отличается только регистром).");
            }

            if (Declared.TryGetValue(id.Text, out Lexeme? first))
            {
                return Fail(
                    id,
                    $"Переменная \"{id.Text}\" объявлена повторно " +
                    $"(первое объявление: строка {first.Line}, " +
                    $"столбец {first.Column}).");
            }

            Declared[id.Text] = id;

            return true;
        }

        // Переменная получает значение (присваивание или READ)
        private bool Assign(Lexeme id, HashSet<string> initialized)
        {
            if (!CheckDeclared(id))
                return false;

            initialized.Add(id.Text);

            return true;
        }

        // Все переменные поддерева должны быть объявлены и иметь значение
        private bool CheckValues(SyntaxNode node, HashSet<string> initialized)
        {
            foreach (Lexeme id in node.Identifiers())
            {
                if (!CheckDeclared(id))
                    return false;

                if (!initialized.Contains(id.Text))
                {
                    return Fail(
                        id,
                        $"Переменная \"{id.Text}\" используется до того, " +
                        "как получила значение.");
                }
            }

            return true;
        }

        private bool CheckDeclared(Lexeme id)
        {
            if (!Declared.ContainsKey(id.Text))
                return Fail(id, $"Переменная \"{id.Text}\" не объявлена.");

            referenced.Add(id.Text);

            return true;
        }

        // Регистрация ошибки; анализ после неё не продолжается
        private bool Fail(Lexeme lexeme, string message)
        {
            Errors.Add(new Diagnostic(lexeme.Line, lexeme.Column, message));

            return false;
        }
    }
}
