namespace Translator.Models
{
    public class Lexeme
    {
        // Порядковый номер лексемы в исходном тексте
        public int Number { get; set; }

        // Код лексемы: t1, i1, c1 и т.д.
        public string Code { get; set; }

        // Сам текст лексемы
        public string Text { get; set; }

        // Тип лексемы
        public string Type { get; set; }

        // Строка
        public int Line { get; set; }

        // Столбец
        public int Column { get; set; }

        public Lexeme(
            int number,
            string code,
            string text,
            string type,
            int line,
            int column)
        {
            Number = number;
            Code = code;
            Text = text;
            Type = type;
            Line = line;
            Column = column;
        }
    }
}