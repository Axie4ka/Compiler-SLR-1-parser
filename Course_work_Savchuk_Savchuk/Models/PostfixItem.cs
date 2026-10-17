namespace Translator.Models
{
    // Вид элемента постфиксной записи
    public enum PostfixKind
    {
        // Значение переменной
        Variable,

        // Сама переменная, а не её значение
        // (левая часть присваивания, аргумент READ и WRITE)
        Address,

        // Константа 0 или 1
        Constant,

        // Операция: .NOT. .AND. .OR. .EQU. =
        Operation,

        // Ввод значения переменной
        Read,

        // Вывод значения
        Write,

        // Ссылка на метку (операнд перехода)
        LabelRef,

        // УПЛ — условный переход по лжи
        JumpIfFalse,

        // БП — безусловный переход
        Jump,

        // Определение метки
        Label
    }

    public class PostfixItem
    {
        // Порядковый номер элемента в постфиксной записи
        public int Number { get; set; }

        // Вид элемента
        public PostfixKind Kind { get; set; }

        // Имя переменной, значение константы, знак операции или имя метки
        public string Text { get; set; }

        public PostfixItem(
            int number,
            PostfixKind kind,
            string text)
        {
            Number = number;
            Kind = kind;
            Text = text;
        }

        // Определение метки записывается с двоеточием: "M1:"
        public override string ToString()
        {
            return Kind == PostfixKind.Label ? Text + ":" : Text;
        }
    }
}
