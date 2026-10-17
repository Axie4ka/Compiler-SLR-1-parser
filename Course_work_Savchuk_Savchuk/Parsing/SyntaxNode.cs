using System.Collections.Generic;
using Translator.Models;

namespace Translator.Parsing
{
    // Узел дерева разбора
    public class SyntaxNode
    {
        public string Symbol { get; }

        // Лексема исходного текста (только у листьев дерева)
        public Lexeme? Lexeme { get; }

        public string? Text
        {
            get { return Lexeme?.Text; }
        }

        public List<SyntaxNode> Children { get; } = new List<SyntaxNode>();

        public SyntaxNode(string symbol, Lexeme? lexeme = null)
        {
            Symbol = symbol;
            Lexeme = lexeme;
        }

        // Идентификаторы поддерева в порядке их следования в тексте
        public IEnumerable<Lexeme> Identifiers()
        {
            if (Symbol == Nodes.Id)
                yield return Lexeme!;

            foreach (SyntaxNode child in Children)
            {
                foreach (Lexeme id in child.Identifiers())
                    yield return id;
            }
        }
    }
}
