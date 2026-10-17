namespace Translator.Models
{
    // Сообщение анализатора (ошибка или предупреждение)
    // с привязкой к месту в исходном тексте
    public class Diagnostic
    {
        // Строка
        public int Line { get; set; }

        // Столбец
        public int Column { get; set; }

        // Текст сообщения
        public string Message { get; set; }

        public Diagnostic(
            int line,
            int column,
            string message)
        {
            Line = line;
            Column = column;
            Message = message;
        }
    }
}
