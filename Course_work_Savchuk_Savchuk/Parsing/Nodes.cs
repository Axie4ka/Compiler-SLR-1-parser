namespace Translator.Parsing
{
    // Имена символов грамматики, по которым узлы дерева разбора
    // распознаются при семантическом анализе и генерации.
    // Правила грамматики строятся из этих же констант.
    public static class Nodes
    {
        public const string Declaration = "ОбъявлениеПеременных";
        public const string Assignment = "Присваивание";
        public const string Input = "Ввод";
        public const string Output = "Вывод";
        public const string Conditional = "УсловныйОператор";

        // Терминалы, которыми в грамматике представлены
        // идентификаторы и константы
        public const string Id = "id";
        public const string Const = "const";
    }
}
