using System;
using System.Collections.Generic;
using System.Linq;
using Translator.Lexing;
using Translator.Models;

namespace Translator.Parsing
{
    // Восходящий (LR) синтаксический анализатор
    public class LrParser
    {
        private readonly LrTable table;

        public List<ParseStep> Steps { get; } = new List<ParseStep>();
        public List<Diagnostic> Errors { get; } = new List<Diagnostic>();

        public SyntaxNode? Tree { get; private set; }

        public LrParser(LrTable table)
        {
            this.table = table;
        }

        // Лексема -> терминал грамматики
        public static string ToTerminal(Lexeme lexeme)
        {
            if (lexeme.Code[0] == 'i')
                return Nodes.Id;

            if (lexeme.Code[0] == 'c')
                return Nodes.Const;

            return lexeme.Text;
        }

        public bool Parse(IReadOnlyList<Lexeme> lexemes)
        {
            Steps.Clear();
            Errors.Clear();
            Tree = null;

            var input = new List<string>();

            foreach (Lexeme l in lexemes)
                input.Add(ToTerminal(l));

            input.Add(Grammar.EndMarker);

            var states = new List<int> { 0 };
            var symbols = new List<string>();
            var nodes = new List<SyntaxNode>();

            // Стек состояний на момент последнего переноса (для диагностики)
            var lastShift = new List<int>(states);

            int pos = 0;

            while (true)
            {
                int state = states[states.Count - 1];
                string a = input[pos];

                if (!table.Action[state].TryGetValue(a, out LrAction? action))
                {
                    ReportError(lexemes, pos, lastShift);

                    AddStep(states, symbols, input, pos, "Ошибка");

                    return false;
                }

                if (action.Kind == ActionKind.Shift)
                {
                    AddStep(
                        states, symbols, input, pos,
                        $"Перенос -> {action.Value}");

                    states.Add(action.Value);
                    symbols.Add(a);
                    nodes.Add(new SyntaxNode(a, lexemes[pos]));
                    pos++;

                    lastShift = new List<int>(states);
                }
                else if (action.Kind == ActionKind.Reduce)
                {
                    Production p = table.Grammar.Productions[action.Value];
                    int n = p.Right.Length;

                    AddStep(
                        states, symbols, input, pos,
                        $"Свёртка {p.Index}: {p.Left}");

                    var node = new SyntaxNode(p.Left);
                    node.Children.AddRange(nodes.GetRange(nodes.Count - n, n));

                    nodes.RemoveRange(nodes.Count - n, n);
                    symbols.RemoveRange(symbols.Count - n, n);
                    states.RemoveRange(states.Count - n, n);

                    int top = states[states.Count - 1];

                    states.Add(table.Goto[top][p.Left]);
                    symbols.Add(p.Left);
                    nodes.Add(node);
                }
                else
                {
                    AddStep(states, symbols, input, pos, "Допуск");

                    Tree = nodes[0];

                    return true;
                }
            }
        }

        private void ReportError(
            IReadOnlyList<Lexeme> lexemes,
            int pos,
            List<int> states)
        {
            // SLR-анализатор до обнаружения ошибки успевает выполнить свёртки
            // по FOLLOW-множеству, и в текущем состоянии часть допустимых
            // продолжений уже потеряна. Поэтому берём стек на момент
            // последнего переноса и оставляем терминалы, которые
            // действительно ведут от него к переносу.
            List<string> viable = table.Grammar.Terminals
                .Where(t => IsViable(states, t))
                .ToList();

            string expected = string.Join(
                ", ",
                viable
                    .Select(Describe)
                    .OrderBy(s => s, StringComparer.Ordinal));

            Lexeme? previous = pos > 0 ? lexemes[pos - 1] : null;

            int line = 1;
            int column = 1;
            string message;

            if (pos >= lexemes.Count)
            {
                if (previous != null)
                {
                    line = previous.Line;
                    column = previous.Column + previous.Text.Length;
                }

                message = $"Неожиданный конец программы. Ожидалось: {expected}.";
            }
            else if (previous != null && lexemes[pos].Line > previous.Line)
            {
                // Чего-то не хватает в конце предыдущей строки
                // (например, ';'): указываем на место пропуска.
                line = previous.Line;
                column = previous.Column + previous.Text.Length;

                message =
                    $"После \"{previous.Text}\" ожидалось: {expected}. " +
                    $"Найдено: \"{lexemes[pos].Text}\".";
            }
            else
            {
                line = lexemes[pos].Line;
                column = lexemes[pos].Column;

                message =
                    $"Неожиданный символ \"{lexemes[pos].Text}\". " +
                    $"Ожидалось: {expected}.";
            }

            // На месте, где возможен только идентификатор, стоит ключевое слово
            if (pos < lexemes.Count &&
                Lexer.TryGetKeyword(lexemes[pos].Text, out _) &&
                viable.Count == 1 && viable[0] == Nodes.Id)
            {
                message +=
                    " Ключевое слово нельзя использовать как идентификатор.";
            }

            Errors.Add(new Diagnostic(line, column, message));
        }

        // Приведёт ли терминал к переносу (или допуску) при данном стеке:
        // выполняем свёртки на копии стека, пока не дойдём до переноса или ошибки
        private bool IsViable(List<int> states, string terminal)
        {
            var stack = new List<int>(states);

            while (true)
            {
                int state = stack[stack.Count - 1];

                if (!table.Action[state].TryGetValue(terminal, out LrAction? action))
                    return false;

                if (action.Kind != ActionKind.Reduce)
                    return true;

                Production p = table.Grammar.Productions[action.Value];

                stack.RemoveRange(stack.Count - p.Right.Length, p.Right.Length);
                stack.Add(table.Goto[stack[stack.Count - 1]][p.Left]);
            }
        }

        private static string Describe(string terminal)
        {
            switch (terminal)
            {
                case Nodes.Id: return "идентификатор";
                case Nodes.Const: return "константа (0 или 1)";
                case Grammar.EndMarker: return "конец программы";
                default: return terminal;
            }
        }

        private void AddStep(
            List<int> states,
            List<string> symbols,
            List<string> input,
            int pos,
            string action)
        {
            var stack = new System.Text.StringBuilder();
            stack.Append(states[0]);

            for (int i = 0; i < symbols.Count; i++)
            {
                stack.Append(' ').Append(symbols[i]);
                stack.Append(' ').Append(states[i + 1]);
            }

            string rest = string.Join(" ", input.Skip(pos).Take(8));

            if (input.Count - pos > 8)
                rest += " ...";

            Steps.Add(
                new ParseStep(Steps.Count + 1, stack.ToString(), rest, action));
        }
    }
}