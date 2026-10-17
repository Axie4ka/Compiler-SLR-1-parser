using System;
using System.Collections.Generic;
using System.Text;
using Translator.Models;

namespace Translator.Generation
{
    // Генерация программы на ассемблере x86 (16 бит, DOS, синтаксис
    // MASM/TASM) по постфиксной записи.
    //
    // Постфиксная запись выполняется как программа стековой машины:
    // значения 0/1 находятся в AL и передаются через аппаратный стек.
    // Адреса переменных и ссылки на метки в машинный стек не кладутся —
    // генератор хранит их в собственном стеке до команды, которая их использует.
    //
    // Каждый элемент записи переводится отдельно, поэтому значение часто
    // кладётся в стек и следующей же командой снимается. Такие пары
    // push ax / pop ax удаляются из готового текста отдельным проходом.
    //
    // Русский текст есть только в комментариях: имена, метки и строки
    // для вывода состоят из символов ASCII, поэтому кодировка файла
    // на работу ассемблера и программы в DOS не влияет.
    public class CodeGenerator
    {
        private const string NewLine = "\r\n";

        private const string Indent = "    ";

        // Строка программы: текст и необязательный комментарий
        private sealed record AsmLine(string Text, string? Comment = null);

        private readonly List<AsmLine> lines = new List<AsmLine>();

        // Имя переменной исходной программы -> суффикс имени в ассемблере
        private readonly Dictionary<string, string> asmNames =
            new Dictionary<string, string>();

        // Отложенные операнды: адреса переменных и ссылки на метки
        private readonly Stack<PostfixItem> pending = new Stack<PostfixItem>();

        private int skipCount;

        public string Generate(
            IReadOnlyList<PostfixItem> postfix,
            IEnumerable<string> variables)
        {
            lines.Clear();
            asmNames.Clear();
            pending.Clear();
            skipCount = 0;

            AssignNames(variables);

            Line("; Программа создана компилятором (вариант 8)");
            Line("; Целевая платформа: x86, 16 бит, DOS (MASM/TASM)");
            Line();
            Line(".model small");
            Line(".stack 100h");
            Line();
            Line(".data");

            // V_имя — значение переменной, N_имя — её имя для ввода-вывода
            foreach (var pair in asmNames)
            {
                Line($"V_{pair.Value,-12} db 0");
                Line($"N_{pair.Value,-12} db '{pair.Key} = $'");
            }

            Line();
            Line(".code");
            Line("start:");
            Command("mov ax, @data");
            Command("mov ds, ax");

            foreach (PostfixItem item in postfix)
                Translate(item);

            Line();
            Command("mov ax, 4C00h", "выход в DOS");
            Command("int 21h");
            Line();

            Procedures();

            Line("end start");

            RemovePushPop();

            return Render();
        }

        // Ассемблер не различает регистр букв, а исходный язык различает:
        // переменным a и A нужны разные имена
        private void AssignNames(IEnumerable<string> variables)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string name in variables)
            {
                string asmName = name;
                int number = 2;

                while (!used.Add(asmName))
                {
                    asmName = name + "_" + number;
                    number++;
                }

                asmNames[name] = asmName;
            }
        }

        private void Translate(PostfixItem item)
        {
            switch (item.Kind)
            {
                case PostfixKind.Address:
                case PostfixKind.LabelRef:
                    pending.Push(item);
                    break;

                case PostfixKind.Variable:
                    Command($"mov al, {Value(item.Text)}", item.Text);
                    Command("push ax");
                    break;

                case PostfixKind.Constant:
                    Command($"mov al, {item.Text}", item.Text);
                    Command("push ax");
                    break;

                case PostfixKind.Operation:
                    Operation(item.Text);
                    break;

                case PostfixKind.Read:
                {
                    string name = pending.Pop().Text;

                    Command($"mov dx, offset {Title(name)}", $"{name} READ");
                    Command("call ReadBit");
                    Command($"mov {Value(name)}, al");
                    break;
                }

                case PostfixKind.Write:
                {
                    string name = pending.Pop().Text;

                    Command($"mov al, {Value(name)}", $"{name} WRITE");
                    Command($"mov dx, offset {Title(name)}");
                    Command("call WriteBit");
                    break;
                }

                // Короткий условный переход достаёт только на 127 байт,
                // поэтому на метку ведёт длинный jmp
                case PostfixKind.JumpIfFalse:
                {
                    string label = pending.Pop().Text;
                    string skip = "S" + (++skipCount);

                    Command("pop ax", $"{label} {PostfixGenerator.JumpIfFalseText} (переход по лжи)");
                    Command("or al, al");
                    Command($"jnz {skip}");
                    Command($"jmp {label}");
                    Line($"{skip}:");
                    break;
                }

                case PostfixKind.Jump:
                {
                    string label = pending.Pop().Text;

                    Command($"jmp {label}", $"{label} {PostfixGenerator.JumpText} (безусловный переход)");
                    break;
                }

                case PostfixKind.Label:
                    Line($"{item.Text}:");
                    break;
            }
        }

        private void Operation(string operation)
        {
            switch (operation)
            {
                case "=":
                {
                    string name = pending.Pop().Text;

                    Command("pop ax", $"{name} =");
                    Command($"mov {Value(name)}, al");
                    break;
                }

                case ".NOT.":
                    Command("pop ax", ".NOT.");
                    Command("xor al, 1");
                    Command("push ax");
                    break;

                case ".AND.":
                    Command("pop bx", ".AND.");
                    Command("pop ax");
                    Command("and al, bl");
                    Command("push ax");
                    break;

                case ".OR.":
                    Command("pop bx", ".OR.");
                    Command("pop ax");
                    Command("or al, bl");
                    Command("push ax");
                    break;

                // Эквивалентность: 1, если операнды равны
                case ".EQU.":
                    Command("pop bx", ".EQU.");
                    Command("pop ax");
                    Command("xor al, bl");
                    Command("xor al, 1");
                    Command("push ax");
                    break;

                default:
                    throw new InvalidOperationException(
                        "Неизвестная операция: " + operation);
            }
        }

        // Подпрограммы ввода-вывода (int 21h)
        private void Procedures()
        {
            Line("; Ввод: DX = адрес приглашения, результат AL = 0 или 1");
            Line("ReadBit proc");
            Line("RB_ask:");
            Command("mov ah, 09h", "вывод приглашения");
            Command("int 21h");
            Command("mov ah, 01h", "чтение символа");
            Command("int 21h");
            Command("mov bl, al");
            Command("call PrintNewLine");
            Command("cmp bl, '0'");
            Command("je RB_ok");
            Command("cmp bl, '1'");
            Command("jne RB_ask", "допустимы только 0 и 1");
            Line("RB_ok:");
            Command("mov al, bl");
            Command("sub al, '0'");
            Command("ret");
            Line("ReadBit endp");
            Line();

            Line("; Вывод: DX = адрес имени, AL = значение (0 или 1)");
            Line("WriteBit proc");
            Command("push ax");
            Command("mov ah, 09h", "вывод имени");
            Command("int 21h");
            Command("pop ax");
            Command("mov dl, al");
            Command("add dl, '0'");
            Command("mov ah, 02h", "вывод цифры");
            Command("int 21h");
            Command("call PrintNewLine");
            Command("ret");
            Line("WriteBit endp");
            Line();

            Line("PrintNewLine proc");
            Command("push ax");
            Command("push dx");
            Command("mov ah, 02h");
            Command("mov dl, 13");
            Command("int 21h");
            Command("mov dl, 10");
            Command("int 21h");
            Command("pop dx");
            Command("pop ax");
            Command("ret");
            Line("PrintNewLine endp");
            Line();
        }

        private string Value(string variable)
        {
            return "V_" + asmNames[variable];
        }

        private string Title(string variable)
        {
            return "N_" + asmNames[variable];
        }

        private void Command(string text, string? comment = null)
        {
            lines.Add(new AsmLine(Indent + text, comment));
        }

        private void Line(string text = "")
        {
            lines.Add(new AsmLine(text));
        }

        // Удаление пар соседних команд push ax / pop ax: значение и так
        // остаётся в AX, а стек после такой пары не меняется.
        // Пара, разделённая меткой, не трогается: на метку может прийти
        // переход. Комментарий удалённой pop ax переходит к следующей команде.
        private void RemovePushPop()
        {
            int i = 0;

            while (i + 1 < lines.Count)
            {
                if (lines[i].Text != Indent + "push ax" ||
                    lines[i + 1].Text != Indent + "pop ax")
                {
                    i++;
                    continue;
                }

                string? comment = lines[i + 1].Comment;

                lines.RemoveRange(i, 2);

                if (comment != null &&
                    i < lines.Count &&
                    lines[i].Comment == null &&
                    lines[i].Text.StartsWith(Indent))
                {
                    lines[i] = lines[i] with { Comment = comment };
                }
            }
        }

        private string Render()
        {
            var code = new StringBuilder();

            foreach (AsmLine line in lines)
            {
                if (line.Comment == null)
                    code.Append(line.Text);
                else
                    code.Append($"{line.Text,-28}; {line.Comment}");

                code.Append(NewLine);
            }

            return code.ToString();
        }
    }
}
