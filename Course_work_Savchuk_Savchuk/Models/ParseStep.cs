namespace Translator.Models
{
    // Одна строка протокола разбора
    public class ParseStep
    {
        public int Number { get; set; }

        // Содержимое стека: состояния и символы
        public string Stack { get; set; }

        // Оставшаяся часть входной цепочки
        public string Input { get; set; }

        // Выполненное действие
        public string Action { get; set; }

        public ParseStep(
            int number,
            string stack,
            string input,
            string action)
        {
            Number = number;
            Stack = stack;
            Input = input;
            Action = action;
        }
    }
}