using Translator.Models;
using System;
using System.Collections.Generic;

namespace Translator.Lexing
{
    public class Lexer
    {

        public List<Lexeme> Lexemes { get; private set; }

        public List<Symbol> Identifiers { get; private set; }

        public List<Symbol> Constants { get; private set; }

        // Анализ прекращается на первой ошибке,
        // поэтому в списке не более одной записи
        public List<Diagnostic> Errors { get; private set; }



        // Зарезервированные слова и их коды
        private static readonly Dictionary<string, string> keywordCodes =
            new Dictionary<string, string>
            {
                { "VAR",     "t1" },
                { "BEGIN",   "t2" },
                { "END",     "t3" },
                { "LOGICAL", "t4" },
                { "READ",    "t5" },
                { "WRITE",   "t6" },
                { "IF",      "t7" },
                { "THEN",    "t8" },
                { "ELSE",    "t9" },
                { "END_IF",  "t20" }
            };



        // Операторы и специальные символы
        private static readonly Dictionary<string, string> operatorCodes =
            new Dictionary<string, string>
            {
                { "=",     "t10" },
                { ":",     "t11" },
                { ";",     "t12" },
                { ",",     "t13" },
                { "(",     "t14" },
                { ")",     "t15" },

                { ".NOT.",  "t16" },
                { ".AND.",  "t17" },
                { ".OR.",   "t18" },
                { ".EQU.",  "t19" }
            };

        // Максимальная длина идентификатора
        private const int MaxIdentifierLength = 9;

        // Конструктор
        public Lexer()
        {
            Lexemes = new List<Lexeme>();
            Identifiers = new List<Symbol>();
            Constants = new List<Symbol>();
            Errors = new List<Diagnostic>();
        }

        // Является ли слово ключевым без учёта регистра.
        // keyword — ключевое слово в том виде, как оно записано в языке.
        public static bool TryGetKeyword(string word, out string keyword)
        {
            foreach (string candidate in keywordCodes.Keys)
            {
                if (string.Equals(
                        candidate,
                        word,
                        StringComparison.OrdinalIgnoreCase))
                {
                    keyword = candidate;
                    return true;
                }
            }

            keyword = "";
            return false;
        }

        // Основной метод анализа.
        // Возвращает false, если встречена лексическая ошибка:
        // анализ останавливается на первой же ошибке.
        public bool Analyze(string source)
        {
            // Очищаем результаты предыдущего анализа
            Lexemes.Clear();
            Identifiers.Clear();
            Constants.Clear();
            Errors.Clear();

            int position = 0;
            int line = 1;
            int column = 1;

            while (position < source.Length)
            {
                char current = source[position];

                // Пробелы
                if (current == ' ' || current == '\t' || current == '\r')
                {
                    position++;
                    column++;
                    continue;
                }

                // Перевод строки
                if (current == '\n')
                {
                    position++;
                    line++;
                    column = 1;
                    continue;
                }

                int startColumn = column;

                // Идентификатор или ключевое слово
                if (IsLetter(current))
                {
                    string word = ReadLettersAndDigits(
                        source,
                        ref position,
                        ref column);

                    // Сначала проверяем, является ли слово зарезервированным
                    if (keywordCodes.TryGetValue(word, out string? keywordCode))
                    {
                        AddLexeme(
                            keywordCode,
                            word,
                            "Ключевое слово",
                            line,
                            startColumn);

                        continue;
                    }

                    // Иначе это идентификатор: он состоит только из букв
                    if (!ContainsOnlyLetters(word))
                    {
                        return Fail(
                            line,
                            startColumn,
                            $"Недопустимый идентификатор \"{word}\": " +
                            "идентификатор может состоять только из букв.");
                    }

                    if (word.Length > MaxIdentifierLength)
                    {
                        return Fail(
                            line,
                            startColumn,
                            $"Идентификатор \"{word}\" превышает " +
                            $"максимальную длину {MaxIdentifierLength} символов.");
                    }

                    AddLexeme(
                        GetOrAdd(Identifiers, "i", word, "Идентификатор"),
                        word,
                        "Идентификатор",
                        line,
                        startColumn);

                    continue;
                }

                // Константа
                if (IsDigit(current))
                {
                    string number = ReadLettersAndDigits(
                        source,
                        ref position,
                        ref column);

                    // По нашей грамматике допустимы только 0 и 1
                    if (number != "0" && number != "1")
                    {
                        return Fail(
                            line,
                            startColumn,
                            $"Недопустимая константа \"{number}\". " +
                            "Разрешены только 0 и 1.");
                    }

                    AddLexeme(
                        GetOrAdd(Constants, "c", number, "Константа"),
                        number,
                        "Константа",
                        line,
                        startColumn);

                    continue;
                }

                // Операторы вида .AND., .OR., .NOT., .EQU.
                if (current == '.')
                {
                    string? dotOperator = ReadDotOperator(
                        source,
                        ref position,
                        ref column);

                    if (dotOperator == null)
                    {
                        return Fail(
                            line,
                            startColumn,
                            "Ожидался оператор вида .AND., .OR., .NOT. или .EQU.");
                    }

                    if (!operatorCodes.TryGetValue(
                            dotOperator,
                            out string? operatorCode))
                    {
                        return Fail(
                            line,
                            startColumn,
                            $"Неизвестный оператор \"{dotOperator}\".");
                    }

                    AddLexeme(
                        operatorCode,
                        dotOperator,
                        "Оператор",
                        line,
                        startColumn);

                    continue;
                }

                // Односимвольные операторы и разделители
                string currentSymbol = current.ToString();

                if (operatorCodes.TryGetValue(
                        currentSymbol,
                        out string? symbolCode))
                {
                    AddLexeme(
                        symbolCode,
                        currentSymbol,
                        "Оператор/разделитель",
                        line,
                        startColumn);

                    position++;
                    column++;

                    continue;
                }

                // Неизвестный символ
                return Fail(
                    line,
                    column,
                    $"Недопустимый символ \"{current}\".");
            }

            return true;
        }

        // Регистрация ошибки; анализ после неё не продолжается
        private bool Fail(int line, int column, string message)
        {
            Errors.Add(new Diagnostic(line, column, message));

            return false;
        }

        // Буквы языка — только латинские
        private static bool IsLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private static bool ContainsOnlyLetters(string text)
        {
            foreach (char c in text)
            {
                if (!IsLetter(c))
                    return false;
            }

            return true;
        }

        // Чтение слитной последовательности букв, цифр и '_'.
        // Читается целиком, чтобы "A1" или "1A" не распадались
        // на идентификатор и константу, а давали лексическую ошибку.
        // Знак '_' нужен только для ключевого слова END_IF.
        private string ReadLettersAndDigits(
            string source,
            ref int position,
            ref int column)
        {
            int startPosition = position;

            while (
                position < source.Length &&
                (IsLetter(source[position]) ||
                 IsDigit(source[position]) ||
                 source[position] == '_'))
            {
                position++;
                column++;
            }

            return source.Substring(
                startPosition,
                position - startPosition);
        }

        // Чтение оператора .AND. / .OR. / .NOT. / .EQU.
        // Возвращает null, если после букв нет закрывающей точки.
        private string? ReadDotOperator(
            string source,
            ref int position,
            ref int column)
        {
            // Между точками допустимы только буквы
            int endPosition = position + 1;

            while (
                endPosition < source.Length &&
                IsLetter(source[endPosition]))
            {
                endPosition++;
            }

            if (endPosition >= source.Length || source[endPosition] != '.')
            {
                return null;
            }

            string result = source.Substring(
                position,
                endPosition - position + 1);

            // Перемещаем позицию после оператора
            position = endPosition + 1;
            column += result.Length;

            return result;
        }

        // Добавление лексемы
        private void AddLexeme(
            string code,
            string text,
            string type,
            int line,
            int column)
        {
            int number = Lexemes.Count + 1;

            Lexemes.Add(
                new Lexeme(
                    number,
                    code,
                    text,
                    type,
                    line,
                    column)
            );
        }

        // Добавление / поиск идентификатора или константы:
        // одинаковые записи получают один и тот же код (i1, c1, ...)
        private static string GetOrAdd(
            List<Symbol> symbols,
            string codePrefix,
            string name,
            string type)
        {
            foreach (Symbol symbol in symbols)
            {
                if (symbol.Name == name)
                    return symbol.Code;
            }

            int number = symbols.Count + 1;
            string code = codePrefix + number;

            symbols.Add(new Symbol(number, code, name, type));

            return code;
        }
    }
}
