using Translator.Generation;
using Translator.Lexing;
using Translator.Models;
using Translator.Parsing;
using Translator.Semantics;
using System;


// Пример корректной программы:
//
// VAR A, B, RESULT : LOGICAL;
// BEGIN
//     READ(A);
//     B = .NOT. A;
//     RESULT = A .AND. B;
//     WRITE(RESULT);
// END
//
// Пример с условным оператором:
//
// VAR A, B : LOGICAL;
// BEGIN
//     READ(A);
//     IF A .EQU. 1 THEN
//         B = 0;
//     ELSE
//         B = ( A .OR. 1 ) .AND. .NOT. A;
//     END_IF;
//     WRITE(B);
// END
//
// Пример с лексическими ошибками (сообщается только о первой):
//
// VAR A, B, RESULT, LONGIDENTIFIER, @ : LOGICAL;
// BEGIN
//     READ(A);
//     B = 0.XOR.A;
//     RESULT = A.AND.B;
//     WRITE(RESULT);
// END
//
// Пример с семантическими ошибками (повторное объявление A, не объявлена C;
// сообщается только о первой):
//
//VAR A, B, A : LOGICAL;
//BEGIN
//     READ(A);
//B = C;
//END
//
// Пример с синтаксической ошибкой (нет ';' после READ(A)):
//
// VAR A, B : LOGICAL;
// BEGIN
//     READ(A)
//     B = A;
// END

namespace Translator
{
    internal class Program
    {
        // Файл по умолчанию: лежит в проекте и при сборке
        // копируется в папку с программой
        private const string DefaultFileName = "program.txt";

        // Ширина колонок протокола разбора
        private const int StackWidth = 45;
        private const int InputWidth = 30;

        // Файл, из которого прочитан исходный текст
        // (null — текст введён с клавиатуры)
        private static string? sourcePath;

        // Указан ключ /run
        private static bool runRequested;

        // Источник текста выбран в меню: о запуске в DOSBox можно спросить
        private static bool interactive;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine(" 24ВП2 Савчук Д., Савчук М., Вариант 8");
            Console.WriteLine(" КОМПИЛЯТОР");

            Console.WriteLine();

            // Ключ /run — собрать программу и запустить её в DOSBox;
            // остальные аргументы — путь к файлу с исходным текстом
            var files = new List<string>();

            foreach (string argument in args)
            {
                if (IsRunOption(argument))
                    runRequested = true;
                else
                    files.Add(argument);
            }

            string? source = ReadSource(files.ToArray());

            if (source == null)
            {
                WaitForKey();
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Выполняется лексический анализ...");

            Console.WriteLine();

            Lexer lexer = new Lexer();

            lexer.Analyze(source);

            PrintLexemes(lexer.Lexemes);

            PrintSymbols(
                " ТАБЛИЦА ИДЕНТИФИКАТОРОВ",
                "Имя",
                "Идентификаторы отсутствуют.",
                lexer.Identifiers);

            PrintSymbols(
                " ТАБЛИЦА КОНСТАНТ",
                "Значение",
                "Константы отсутствуют.",
                lexer.Constants);

            Console.WriteLine();
            Console.WriteLine(" ЛЕКСИЧЕСКИЕ ОШИБКИ");

            if (lexer.Errors.Count == 0)
                Console.WriteLine("Лексических ошибок не обнаружено.");
            else
                PrintDiagnostics(lexer.Errors);

            Console.WriteLine();
            Console.WriteLine("Лексический анализ завершён.");

            Console.WriteLine();
            Console.WriteLine(" СИНТАКСИЧЕСКИЙ АНАЛИЗ (SLR(1), восходящий разбор)");
            Console.WriteLine();

            if (lexer.Errors.Count > 0)
            {
                Console.WriteLine(
                    "Синтаксический анализ не выполняется: " +
                    "в тексте есть лексические ошибки.");
            }
            else
            {
                RunSyntaxAnalysis(lexer);
            }

            Console.WriteLine();
            Console.WriteLine("Работа завершена.");

            WaitForKey();
        }

        private static void PrintLexemes(IEnumerable<Lexeme> lexemes)
        {
            Console.WriteLine(" ТАБЛИЦА ЛЕКСЕМ");

            Console.WriteLine(
                "{0,-5} {1,-8} {2,-15} {3,-25} {4}",
                "№",
                "Код",
                "Лексема",
                "Тип",
                "Позиция");

            Console.WriteLine(new string('-', 75));

            foreach (Lexeme lexeme in lexemes)
            {
                Console.WriteLine(
                    "{0,-5} {1,-8} {2,-15} {3,-25} {4}:{5}",
                    lexeme.Number,
                    lexeme.Code,
                    lexeme.Text,
                    lexeme.Type,
                    lexeme.Line,
                    lexeme.Column);
            }
        }

        // Таблица идентификаторов или констант
        private static void PrintSymbols(
            string title,
            string nameColumn,
            string emptyMessage,
            IReadOnlyCollection<Symbol> symbols)
        {
            Console.WriteLine();
            Console.WriteLine(title);

            if (symbols.Count == 0)
            {
                Console.WriteLine(emptyMessage);
                return;
            }

            Console.WriteLine("{0,-5} {1,-8} {2,-15}", "№", "Код", nameColumn);
            Console.WriteLine(new string('-', 35));

            foreach (Symbol symbol in symbols)
            {
                Console.WriteLine(
                    "{0,-5} {1,-8} {2,-15}",
                    symbol.Number,
                    symbol.Code,
                    symbol.Name);
            }
        }

        // Ошибки и предупреждения любого из анализаторов
        private static void PrintDiagnostics(IEnumerable<Diagnostic> diagnostics)
        {
            foreach (Diagnostic diagnostic in diagnostics)
            {
                Console.WriteLine(
                    "Строка {0}, столбец {1}: {2}",
                    diagnostic.Line,
                    diagnostic.Column,
                    diagnostic.Message);
            }
        }

        private static bool IsRunOption(string argument)
        {
            return string.Equals(argument, "/run", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "-run", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "--run", StringComparison.OrdinalIgnoreCase);
        }

        // Получение исходного текста:
        //   - путь к файлу в аргументе командной строки;
        //   - иначе выбор в меню: ввод с клавиатуры или файл
        //     (пустой путь — файл по умолчанию).
        // Возвращает null, если файл из аргумента прочитать не удалось.
        private static string? ReadSource(string[] args)
        {
            if (args.Length > 0)
            {
                string? text = ReadFile(args[0]);

                if (text != null)
                {
                    Console.WriteLine("Исходный текст прочитан из файла: {0}", args[0]);

                    sourcePath = args[0];
                }

                return text;
            }

            // Ввод перенаправлен (program.exe < файл): меню показывать некому
            if (Console.IsInputRedirected)
                return ReadFromKeyboard();

            interactive = true;

            while (true)
            {
                Console.WriteLine("Источник исходного текста программы:");
                Console.WriteLine("  1 - ввести с клавиатуры");
                Console.WriteLine("  2 - прочитать из файла");
                Console.Write("Ваш выбор: ");

                string? choice = Console.ReadLine();

                // Конец входного потока
                if (choice == null)
                    return "";

                choice = choice.Trim();

                if (choice == "1")
                {
                    Console.WriteLine();
                    Console.WriteLine("Введите исходный текст программы.");
                    Console.WriteLine(
                        "Для завершения ввода введите две пустые строки подряд.");
                    Console.WriteLine();

                    return ReadFromKeyboard();
                }

                if (choice == "2")
                {
                    Console.Write(
                        "Путь к файлу (пустая строка - {0} рядом с программой): ",
                        DefaultFileName);

                    // Кавычки появляются при перетаскивании файла в окно консоли
                    string path = (Console.ReadLine() ?? "").Trim().Trim('"');

                    if (path.Length == 0)
                    {
                        path = System.IO.Path.Combine(
                            AppContext.BaseDirectory,
                            DefaultFileName);
                    }

                    string? text = ReadFile(path);

                    if (text != null)
                    {
                        Console.WriteLine(
                            "Исходный текст прочитан из файла: {0}", path);

                        sourcePath = path;

                        return text;
                    }
                }
                else
                {
                    Console.WriteLine("Введите 1 или 2.");
                }

                Console.WriteLine();
            }
        }

        private static string ReadFromKeyboard()
        {
            string source = "";

            // Одиночная пустая строка — часть программы (иначе текст
            // молча обрезался бы), ввод завершают две пустые подряд
            // или конец входного потока.
            bool previousEmpty = false;

            while (true)
            {
                string? line = Console.ReadLine();

                if (line == null)
                    break;

                if (line.Length == 0)
                {
                    if (previousEmpty)
                        break;

                    previousEmpty = true;
                }
                else
                {
                    previousEmpty = false;
                }

                source += line + Environment.NewLine;
            }

            return source;
        }

        // Чтение файла; при неудаче выводит сообщение и возвращает null
        private static string? ReadFile(string path)
        {
            try
            {
                return System.IO.File.ReadAllText(path);
            }
            catch (Exception e) when (
                e is System.IO.IOException ||
                e is UnauthorizedAccessException ||
                e is ArgumentException ||
                e is NotSupportedException)
            {
                Console.WriteLine(
                    "Не удалось прочитать файл \"{0}\": {1}",
                    path,
                    DescribeFileError(path, e));

                return null;
            }
        }

        // Причина ошибки чтения на русском языке
        // (текст исключения выдаётся системой на английском)
        private static string DescribeFileError(string path, Exception e)
        {
            if (System.IO.Directory.Exists(path))
                return "указана папка, а не файл.";

            // Символы вроде < > | ? * система отвергает общей ошибкой ввода-вывода
            if (System.IO.Path.GetFileName(path).IndexOfAny(
                    System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                return "в имени файла есть недопустимые символы.";
            }

            switch (e)
            {
                case System.IO.FileNotFoundException:
                    return "файл не найден.";

                case System.IO.DirectoryNotFoundException:
                    return "папка, указанная в пути, не найдена.";

                case System.IO.PathTooLongException:
                    return "слишком длинный путь.";

                case UnauthorizedAccessException:
                    return "нет доступа к файлу.";

                case ArgumentException:
                case NotSupportedException:
                    return "недопустимый путь.";

                default:
                    return "файл занят другой программой " +
                           "или произошла ошибка ввода-вывода.";
            }
        }

        private static void WaitForKey()
        {
            // При вводе из файла клавиатуры нет, ReadKey бросил бы исключение
            if (!Console.IsInputRedirected)
                Console.ReadKey();
        }

        private static void RunSyntaxAnalysis(Lexer lexer)
        {
            Grammar grammar = Grammar.CreateDefault();
            LrTable table = LrTableBuilder.Build(grammar);

            // Правила грамматики
            Console.WriteLine(" ПРАВИЛА ГРАММАТИКИ");

            foreach (Production p in grammar.Productions)
            {
                Console.WriteLine("{0,3}: {1}", p.Index, p);
            }

            // Конфликты, разрешённые при построении таблицы
            Console.WriteLine();
            Console.WriteLine(" КОНФЛИКТЫ ТАБЛИЦЫ РАЗБОРА");
            Console.WriteLine("Состояний автомата: {0}", table.StateCount);

            if (table.Conflicts.Count == 0)
            {
                Console.WriteLine(
                    "Конфликтов перенос/свёртка и свёртка/свёртка нет.");
            }
            else
            {
                Console.WriteLine(
                    "Конфликты разрешены заранее " +
                    "(перенос приоритетнее свёртки):");

                foreach (string c in table.Conflicts)
                    Console.WriteLine("  " + c);
            }

            // Разбор
            LrParser parser = new LrParser(table);

            bool ok = parser.Parse(lexer.Lexemes);

            Console.WriteLine();
            Console.WriteLine(" ПРОТОКОЛ РАЗБОРА");
            Console.WriteLine(
                "(стек показан своей верхней частью, номера правил — см. выше)");

            Console.WriteLine(
                "{0,-4} {1,-" + StackWidth + "} {2,-" + InputWidth + "} {3}",
                "Шаг",
                "Стек",
                "Вход",
                "Действие");

            Console.WriteLine(
                new string('-', 4 + StackWidth + InputWidth + 35));

            foreach (var step in parser.Steps)
            {
                Console.WriteLine(
                    "{0,-4} {1,-" + StackWidth + "} {2,-" + InputWidth + "} {3}",
                    step.Number,
                    Tail(step.Stack, StackWidth),
                    Head(step.Input, InputWidth),
                    step.Action);
            }

            Console.WriteLine();
            Console.WriteLine(" СИНТАКСИЧЕСКИЕ ОШИБКИ");

            if (ok)
            {
                Console.WriteLine("Синтаксических ошибок не обнаружено.");

                Console.WriteLine();
                Console.WriteLine(" ДЕРЕВО РАЗБОРА");
                PrintTree(parser.Tree!, 0);

                RunSemanticAnalysis(parser.Tree!);
            }
            else
            {
                PrintDiagnostics(parser.Errors);
            }
        }

        private static void RunSemanticAnalysis(SyntaxNode tree)
        {
            Console.WriteLine();
            Console.WriteLine(" СЕМАНТИЧЕСКИЕ ОШИБКИ");

            SemanticAnalyzer semantic = new SemanticAnalyzer();

            semantic.Analyze(tree);

            if (semantic.Errors.Count == 0)
            {
                Console.WriteLine("Семантических ошибок не обнаружено.");
            }
            else
            {
                PrintDiagnostics(semantic.Errors);
            }

            // Предупреждения работу не останавливают
            if (semantic.Warnings.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine(" ПРЕДУПРЕЖДЕНИЯ");

                PrintDiagnostics(semantic.Warnings);
            }

            if (semantic.Errors.Count == 0)
                RunCompilation(tree, semantic);
        }

        // Компиляция: постфиксная запись и программа на ассемблере
        private static void RunCompilation(
            SyntaxNode tree,
            SemanticAnalyzer semantic)
        {
            PostfixGenerator postfixGenerator = new PostfixGenerator();

            var postfix = postfixGenerator.Generate(tree);

            Console.WriteLine();
            Console.WriteLine(" ПОСТФИКСНАЯ ЗАПИСЬ (ПОЛИЗ)");
            Console.WriteLine(
                "({0} - условный переход по лжи, {1} - безусловный переход)",
                PostfixGenerator.JumpIfFalseText,
                PostfixGenerator.JumpText);

            Console.WriteLine("{0,-5} {1,-10} {2}", "№", "Элемент", "Вид");
            Console.WriteLine(new string('-', 45));

            foreach (var item in postfix)
            {
                Console.WriteLine(
                    "{0,-5} {1,-10} {2}",
                    item.Number,
                    item,
                    DescribeKind(item.Kind));
            }

            Console.WriteLine();
            Console.WriteLine(string.Join(" ", postfix));

            CodeGenerator codeGenerator = new CodeGenerator();

            string assembly = codeGenerator.Generate(
                postfix,
                semantic.Declared.Keys);

            Console.WriteLine();
            Console.WriteLine(" АССЕМБЛЕРНЫЙ КОД (x86, 16 бит, DOS)");
            Console.WriteLine(assembly);

            string? asmPath = SaveAssembly(assembly);

            if (asmPath != null)
                BuildAndRun(asmPath);
        }

        // Сборка программы (ml, link16) и запуск в DOSBox:
        // по ключу /run или по ответу пользователя
        private static void BuildAndRun(string asmPath)
        {
            if (!runRequested)
            {
                if (!interactive)
                    return;

                Console.WriteLine();
                Console.Write("Собрать программу и запустить в DOSBox? (д/н): ");

                string answer = (Console.ReadLine() ?? "").Trim().ToLower();

                if (answer != "д" && answer != "да" &&
                    answer != "y" && answer != "yes")
                {
                    return;
                }
            }

            Console.WriteLine();
            Console.WriteLine(" СБОРКА И ЗАПУСК");

            DosRunner runner = new DosRunner();

            runner.LoadSettings(
                System.IO.Path.Combine(
                    AppContext.BaseDirectory,
                    DosRunner.SettingsFileName));

            if (!runner.Build(asmPath))
            {
                Console.WriteLine(runner.Error);
                return;
            }

            Console.WriteLine("Программа собрана: {0}", runner.ExecutablePath);

            if (!runner.Run())
            {
                Console.WriteLine(runner.Error);
                return;
            }

            Console.WriteLine("Программа запущена в окне DOSBox.");
        }

        private static string DescribeKind(PostfixKind kind)
        {
            switch (kind)
            {
                case PostfixKind.Variable: return "значение переменной";
                case PostfixKind.Address: return "адрес переменной";
                case PostfixKind.Constant: return "константа";
                case PostfixKind.Operation: return "операция";
                case PostfixKind.Read: return "ввод";
                case PostfixKind.Write: return "вывод";
                case PostfixKind.LabelRef: return "ссылка на метку";
                case PostfixKind.JumpIfFalse: return "условный переход по лжи";
                case PostfixKind.Jump: return "безусловный переход";
                default: return "метка";
            }
        }

        // Программа на ассемблере сохраняется рядом с исходным файлом
        // под тем же именем, а при вводе с клавиатуры — рядом с программой
        // Возвращает путь к сохранённому файлу или null при неудаче
        private static string? SaveAssembly(string assembly)
        {
            string path = sourcePath != null
                ? System.IO.Path.ChangeExtension(sourcePath, ".asm")
                : System.IO.Path.Combine(AppContext.BaseDirectory, "program.asm");

            // Исходный текст сам лежит в файле .asm: не затираем его
            if (sourcePath != null &&
                string.Equals(
                    System.IO.Path.GetFullPath(path),
                    System.IO.Path.GetFullPath(sourcePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                path = sourcePath + ".out.asm";
            }

            try
            {
                // UTF-8 без метки порядка байтов: с ней ассемблер
                // не разберёт первую строку файла
                System.IO.File.WriteAllText(
                    path,
                    assembly,
                    new System.Text.UTF8Encoding(false));

                Console.WriteLine(
                    "Программа на ассемблере сохранена в файл: {0}",
                    System.IO.Path.GetFullPath(path));

                return path;
            }
            catch (Exception e) when (
                e is System.IO.IOException ||
                e is UnauthorizedAccessException ||
                e is ArgumentException ||
                e is NotSupportedException)
            {
                Console.WriteLine(
                    "Не удалось сохранить файл \"{0}\": {1}",
                    path,
                    DescribeFileError(path, e));

                return null;
            }
        }

        // Обрезка слева: остаётся верхушка стека
        private static string Tail(string text, int width)
        {
            if (text.Length <= width)
                return text;

            int start = text.Length - (width - 4);
            int space = text.IndexOf(' ', start);

            return space < 0
                ? "..." + text.Substring(start)
                : "..." + text.Substring(space);
        }

        // Обрезка справа: остаётся начало входной цепочки
        private static string Head(string text, int width)
        {
            if (text.Length <= width)
                return text;

            return text.Substring(0, width - 3) + "...";
        }

        private static void PrintTree(SyntaxNode node, int depth)
        {
            string indent = new string(' ', depth * 2);

            if (node.Text != null)
                Console.WriteLine("{0}{1} '{2}'", indent, node.Symbol, node.Text);
            else
                Console.WriteLine("{0}{1}", indent, node.Symbol);

            foreach (SyntaxNode child in node.Children)
                PrintTree(child, depth + 1);
        }
    }
}