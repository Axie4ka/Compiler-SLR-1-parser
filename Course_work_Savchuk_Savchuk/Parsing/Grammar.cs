using System;
using System.Collections.Generic;

namespace Translator.Parsing
{
    public class Grammar
    {
        public const string EndMarker = "$";
        public const string AugmentedStart = "S'";

        public List<Production> Productions { get; } = new List<Production>();
        public HashSet<string> NonTerminals { get; } = new HashSet<string>();
        public HashSet<string> Terminals { get; } = new HashSet<string>();

        // Правила задаются строками вида "A -> x B | y".
        // Символы разделяются пробелами. Пустых правил в грамматике нет.
        public Grammar(string startSymbol, IEnumerable<string> rules)
        {
            // Пополненное правило S' -> <начальный символ>
            Productions.Add(
                new Production(0, AugmentedStart, new[] { startSymbol }));
            NonTerminals.Add(AugmentedStart);

            var parsed = new List<KeyValuePair<string, string[]>>();

            foreach (string rule in rules)
            {
                int arrow = rule.IndexOf("->", StringComparison.Ordinal);
                if (arrow < 0)
                    throw new ArgumentException("Нет '->' в правиле: " + rule);

                string left = rule.Substring(0, arrow).Trim();
                NonTerminals.Add(left);

                foreach (string alt in rule.Substring(arrow + 2).Split('|'))
                {
                    string[] right = alt.Split(
                        new[] { ' ', '\t' },
                        StringSplitOptions.RemoveEmptyEntries);

                    if (right.Length == 0)
                        throw new ArgumentException(
                            "Пустая правая часть в правиле: " + rule);

                    parsed.Add(new KeyValuePair<string, string[]>(left, right));
                }
            }

            foreach (var p in parsed)
            {
                Productions.Add(
                    new Production(Productions.Count, p.Key, p.Value));
            }

            // Терминалы — всё, что не является левой частью правила
            foreach (Production p in Productions)
            {
                foreach (string s in p.Right)
                {
                    if (!NonTerminals.Contains(s))
                        Terminals.Add(s);
                }
            }

            Terminals.Add(EndMarker);
        }

        // Грамматика из файла «Грамматика_языка.docx».
        //
        // Правила <Идент>, <Буква> и <Конст> разбираются лексером,
        // поэтому в синтаксическом анализаторе они представлены
        // терминалами id и const. Благодаря этому правило
        // <Идент> ::= <Буква> | <Буква><Идент> не порождает конфликтов
        // «перенос-свёртка» (после буквы нельзя решить, свёртывать
        // идентификатор или читать следующую букву).
        public static Grammar CreateDefault()
        {
            return new Grammar("Программа", new[]
            {
                $"Программа -> {Nodes.Declaration} ОписаниеВычислений",
                $"{Nodes.Declaration} -> VAR СписокПеременных : LOGICAL ;",
                $"СписокПеременных -> {Nodes.Id} | {Nodes.Id} , СписокПеременных",
                "ОписаниеВычислений -> BEGIN СписокОператоров END",
                "СписокОператоров -> Оператор | СписокОператоров Оператор",
                $"Оператор -> {Nodes.Assignment} | {Nodes.Input} | " +
                    $"{Nodes.Output} | {Nodes.Conditional}",
                $"{Nodes.Assignment} -> {Nodes.Id} = Выражение ;",
                $"{Nodes.Input} -> READ ( СписокПеременных ) ;",
                $"{Nodes.Output} -> WRITE ( СписокПеременных ) ;",
                $"{Nodes.Conditional} -> IF Выражение THEN СписокПрисваиваний " +
                    "ELSE СписокПрисваиваний END_IF ;",
                $"СписокПрисваиваний -> {Nodes.Assignment} | " +
                    $"{Nodes.Assignment} СписокПрисваиваний",
                "Выражение -> Выражение .EQU. Дизъюнкция | Дизъюнкция",
                "Дизъюнкция -> Дизъюнкция .OR. Конъюнкция | Конъюнкция",
                "Конъюнкция -> Конъюнкция .AND. УнарноеВыражение | УнарноеВыражение",
                "УнарноеВыражение -> .NOT. УнарноеВыражение | Подвыражение",
                "Подвыражение -> ( Выражение ) | Операнд",
                $"Операнд -> {Nodes.Id} | {Nodes.Const}"
            });
        }
    }
}