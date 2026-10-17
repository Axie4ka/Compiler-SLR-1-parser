namespace Translator.Parsing
{
    // Правило грамматики: Left -> Right[0] Right[1] ...
    public class Production
    {
        public int Index { get; }
        public string Left { get; }
        public string[] Right { get; }

        public Production(int index, string left, string[] right)
        {
            Index = index;
            Left = left;
            Right = right;
        }

        public override string ToString()
        {
            return Left + " -> " + string.Join(" ", Right);
        }
    }
}
