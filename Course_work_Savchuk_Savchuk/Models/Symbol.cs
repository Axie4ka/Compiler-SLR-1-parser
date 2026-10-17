namespace Translator.Models
{
    public class Symbol
    {
        // Номер записи в таблице
        public int Number { get; set; }

        // Код: i1, i2... или c1, c2...
        public string Code { get; set; }

        // Имя идентификатора или значение константы
        public string Name { get; set; }

        // Тип: Идентификатор или Константа
        public string Type { get; set; }

        public Symbol(
            int number,
            string code,
            string name,
            string type)
        {
            Number = number;
            Code = code;
            Name = name;
            Type = type;
        }
    }
}