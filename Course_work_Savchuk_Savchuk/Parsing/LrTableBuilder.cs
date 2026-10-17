using System;
using System.Collections.Generic;
using System.Linq;

namespace Translator.Parsing
{
    // Построение SLR(1)-таблицы разбора.
    //
    // Конфликты разрешаются заранее, на этапе построения таблицы:
    //   перенос/свёртка  -> выбирается ПЕРЕНОС
    //                       (жадное чтение, правая ассоциативность);
    //   свёртка/свёртка  -> выбирается правило с меньшим номером.
    // Каждое принятое решение записывается в LrTable.Conflicts.
    public static class LrTableBuilder
    {
        private readonly record struct Item(int Production, int Dot);

        public static LrTable Build(Grammar g)
        {
            var first = ComputeFirst(g);
            var follow = ComputeFollow(g, first);

            var table = new LrTable { Grammar = g };

            var states = new List<List<Item>>();
            var index = new Dictionary<string, int>();
            var transitions = new List<Dictionary<string, int>>();

            // Начальное состояние: замыкание пункта S' -> . Программа
            AddState(
                Closure(g, new[] { new Item(0, 0) }),
                states, index, transitions);

            for (int i = 0; i < states.Count; i++)
            {
                var symbols = new List<string>();

                foreach (Item it in states[i])
                {
                    string[] rhs = g.Productions[it.Production].Right;

                    if (it.Dot < rhs.Length && !symbols.Contains(rhs[it.Dot]))
                        symbols.Add(rhs[it.Dot]);
                }

                foreach (string x in symbols)
                {
                    var kernel = new List<Item>();

                    foreach (Item it in states[i])
                    {
                        string[] rhs = g.Productions[it.Production].Right;

                        if (it.Dot < rhs.Length && rhs[it.Dot] == x)
                            kernel.Add(new Item(it.Production, it.Dot + 1));
                    }

                    int target = AddState(
                        Closure(g, kernel),
                        states, index, transitions);

                    transitions[i][x] = target;
                }
            }

            // Заполнение таблиц Action и Goto
            for (int i = 0; i < states.Count; i++)
            {
                table.Action.Add(new Dictionary<string, LrAction>());
                table.Goto.Add(new Dictionary<string, int>());
            }

            for (int i = 0; i < states.Count; i++)
            {
                foreach (var tr in transitions[i])
                {
                    if (g.NonTerminals.Contains(tr.Key))
                        table.Goto[i][tr.Key] = tr.Value;
                }

                foreach (Item it in states[i])
                {
                    Production p = g.Productions[it.Production];

                    if (it.Dot < p.Right.Length)
                    {
                        string sym = p.Right[it.Dot];

                        if (g.Terminals.Contains(sym))
                        {
                            SetAction(
                                table, i, sym,
                                new LrAction(ActionKind.Shift, transitions[i][sym]));
                        }
                    }
                    else if (p.Index == 0)
                    {
                        SetAction(
                            table, i, Grammar.EndMarker,
                            new LrAction(ActionKind.Accept, 0));
                    }
                    else
                    {
                        foreach (string a in follow[p.Left])
                        {
                            SetAction(
                                table, i, a,
                                new LrAction(ActionKind.Reduce, p.Index));
                        }
                    }
                }
            }

            return table;
        }

        // Занесение действия в таблицу с разрешением конфликтов
        private static void SetAction(
            LrTable table,
            int state,
            string symbol,
            LrAction next)
        {
            var row = table.Action[state];
            Grammar g = table.Grammar;

            if (!row.TryGetValue(symbol, out LrAction? old))
            {
                row[symbol] = next;
                return;
            }

            if (old == next)
                return;

            if (old.Kind == ActionKind.Accept || next.Kind == ActionKind.Accept)
            {
                row[symbol] = new LrAction(ActionKind.Accept, 0);
                return;
            }

            if (old.Kind == ActionKind.Shift && next.Kind == ActionKind.Reduce)
            {
                table.Conflicts.Add(
                    $"Состояние {state}, символ '{symbol}': перенос/свёртка " +
                    $"(правило {next.Value}: {g.Productions[next.Value]}) " +
                    "— выбран перенос.");
                return;
            }

            if (old.Kind == ActionKind.Reduce && next.Kind == ActionKind.Shift)
            {
                table.Conflicts.Add(
                    $"Состояние {state}, символ '{symbol}': перенос/свёртка " +
                    $"(правило {old.Value}: {g.Productions[old.Value]}) " +
                    "— выбран перенос.");
                row[symbol] = next;
                return;
            }

            // свёртка/свёртка
            int keep = Math.Min(old.Value, next.Value);
            int drop = Math.Max(old.Value, next.Value);

            table.Conflicts.Add(
                $"Состояние {state}, символ '{symbol}': свёртка/свёртка " +
                $"(правила {keep} и {drop}) — выбрано правило {keep}.");

            row[symbol] = new LrAction(ActionKind.Reduce, keep);
        }

        private static int AddState(
            List<Item> items,
            List<List<Item>> states,
            Dictionary<string, int> index,
            List<Dictionary<string, int>> transitions)
        {
            string key = string.Join(
                ";",
                items
                    .OrderBy(it => it.Production)
                    .ThenBy(it => it.Dot)
                    .Select(it => it.Production + "." + it.Dot));

            if (index.TryGetValue(key, out int existing))
                return existing;

            states.Add(items);
            transitions.Add(new Dictionary<string, int>());
            index[key] = states.Count - 1;

            return states.Count - 1;
        }

        private static List<Item> Closure(Grammar g, IEnumerable<Item> kernel)
        {
            var result = new List<Item>(kernel);
            var seen = new HashSet<Item>(result);

            for (int i = 0; i < result.Count; i++)
            {
                Item it = result[i];
                string[] rhs = g.Productions[it.Production].Right;

                if (it.Dot < rhs.Length && g.NonTerminals.Contains(rhs[it.Dot]))
                {
                    foreach (Production p in g.Productions)
                    {
                        if (p.Left != rhs[it.Dot])
                            continue;

                        var added = new Item(p.Index, 0);

                        if (seen.Add(added))
                            result.Add(added);
                    }
                }
            }

            return result;
        }

        // FIRST для нетерминалов (пустых правил в грамматике нет)
        private static Dictionary<string, HashSet<string>> ComputeFirst(Grammar g)
        {
            var first = new Dictionary<string, HashSet<string>>();

            foreach (string n in g.NonTerminals)
                first[n] = new HashSet<string>();

            bool changed;

            do
            {
                changed = false;

                foreach (Production p in g.Productions)
                {
                    string s = p.Right[0];

                    List<string> add = g.NonTerminals.Contains(s)
                        ? first[s].ToList()
                        : new List<string> { s };

                    foreach (string t in add)
                    {
                        if (first[p.Left].Add(t))
                            changed = true;
                    }
                }
            }
            while (changed);

            return first;
        }

        private static Dictionary<string, HashSet<string>> ComputeFollow(
            Grammar g,
            Dictionary<string, HashSet<string>> first)
        {
            var follow = new Dictionary<string, HashSet<string>>();

            foreach (string n in g.NonTerminals)
                follow[n] = new HashSet<string>();

            follow[Grammar.AugmentedStart].Add(Grammar.EndMarker);

            bool changed;

            do
            {
                changed = false;

                foreach (Production p in g.Productions)
                {
                    for (int i = 0; i < p.Right.Length; i++)
                    {
                        string sym = p.Right[i];

                        if (!g.NonTerminals.Contains(sym))
                            continue;

                        List<string> add;

                        if (i + 1 < p.Right.Length)
                        {
                            string next = p.Right[i + 1];

                            add = g.NonTerminals.Contains(next)
                                ? first[next].ToList()
                                : new List<string> { next };
                        }
                        else
                        {
                            add = follow[p.Left].ToList();
                        }

                        foreach (string t in add)
                        {
                            if (follow[sym].Add(t))
                                changed = true;
                        }
                    }
                }
            }
            while (changed);

            return follow;
        }
    }
}
