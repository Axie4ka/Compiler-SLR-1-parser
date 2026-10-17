using System.Collections.Generic;
using Translator.Models;
using Translator.Parsing;

namespace Translator.Generation
{
    // Построение постфиксной записи (ПОЛИЗ) по дереву разбора.
    // Запускается после успешного семантического анализа.
    //
    //   B = выражение;         ->  B <выражение> =
    //   X .AND. Y              ->  <X> <Y> .AND.      (так же .OR., .EQU.)
    //   .NOT. X                ->  <X> .NOT.
    //   READ(A, B);            ->  A READ B READ
    //   WRITE(A, B);           ->  A WRITE B WRITE
    //   IF c THEN s1 ELSE s2 END_IF;
    //                          ->  <c> M1 УПЛ <s1> M2 БП M1: <s2> M2:
    //
    // Старшинство операций уже учтено грамматикой, поэтому
    // порядок операций берётся прямо из структуры дерева.
    public class PostfixGenerator
    {
        public const string JumpIfFalseText = "УПЛ";
        public const string JumpText = "БП";

        private readonly List<PostfixItem> Items = new List<PostfixItem>();

        private int labelCount;

        public List<PostfixItem> Generate(SyntaxNode tree)
        {
            Items.Clear();
            labelCount = 0;

            Visit(tree);

            return Items;
        }

        private void Visit(SyntaxNode node)
        {
            switch (node.Symbol)
            {
                // Объявления в постфиксную запись не попадают
                case Nodes.Declaration:
                    return;

                // id = Выражение ;
                case Nodes.Assignment:
                    Add(PostfixKind.Address, node.Children[0].Text!);
                    Expression(node.Children[2]);
                    Add(PostfixKind.Operation, "=");
                    return;

                // READ ( СписокПеременных ) ;  и  WRITE ( СписокПеременных ) ;
                case Nodes.Input:
                case Nodes.Output:
                    bool input = node.Symbol == Nodes.Input;

                    foreach (Lexeme id in node.Children[2].Identifiers())
                    {
                        Add(PostfixKind.Address, id.Text);
                        Add(
                            input ? PostfixKind.Read : PostfixKind.Write,
                            input ? "READ" : "WRITE");
                    }

                    return;

                // IF Выражение THEN СписокПрисваиваний
                // ELSE СписокПрисваиваний END_IF ;
                case Nodes.Conditional:
                    string elseLabel = NewLabel();
                    string endLabel = NewLabel();

                    Expression(node.Children[1]);
                    Add(PostfixKind.LabelRef, elseLabel);
                    Add(PostfixKind.JumpIfFalse, JumpIfFalseText);

                    Visit(node.Children[3]);
                    Add(PostfixKind.LabelRef, endLabel);
                    Add(PostfixKind.Jump, JumpText);

                    Add(PostfixKind.Label, elseLabel);
                    Visit(node.Children[5]);

                    Add(PostfixKind.Label, endLabel);
                    return;

                default:
                    foreach (SyntaxNode child in node.Children)
                        Visit(child);

                    return;
            }
        }

        // Выражение: сначала операнды, затем операция
        private void Expression(SyntaxNode node)
        {
            if (node.Symbol == Nodes.Id)
            {
                Add(PostfixKind.Variable, node.Text!);
                return;
            }

            if (node.Symbol == Nodes.Const)
            {
                Add(PostfixKind.Constant, node.Text!);
                return;
            }

            switch (node.Children.Count)
            {
                // Цепное правило: Выражение -> Дизъюнкция и т. п.
                case 1:
                    Expression(node.Children[0]);
                    return;

                // .NOT. УнарноеВыражение
                case 2:
                    Expression(node.Children[1]);
                    Add(PostfixKind.Operation, node.Children[0].Symbol);
                    return;

                default:
                    // ( Выражение ): скобки в постфиксной записи не нужны
                    if (node.Children[0].Symbol == "(")
                    {
                        Expression(node.Children[1]);
                        return;
                    }

                    // Левый операнд, правый операнд, операция
                    Expression(node.Children[0]);
                    Expression(node.Children[2]);
                    Add(PostfixKind.Operation, node.Children[1].Symbol);
                    return;
            }
        }

        private void Add(PostfixKind kind, string text)
        {
            Items.Add(new PostfixItem(Items.Count + 1, kind, text));
        }

        private string NewLabel()
        {
            labelCount++;

            return "M" + labelCount;
        }
    }
}
