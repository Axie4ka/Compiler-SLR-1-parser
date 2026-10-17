using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Translator.Generation
{
    // Сборка программы на ассемблере и её запуск в DOSBox:
    //   ml      — ассемблирование (.asm -> .obj);
    //   link16  — компоновка 16-разрядной программы (.obj -> .exe);
    //   DOSBox  — запуск .exe.
    // Пути к инструментам читаются из файла настроек рядом с программой.
    public class DosRunner
    {
        public const string SettingsFileName = "tools.txt";

        public string Assembler { get; private set; } = @"C:\masm32\bin\ml.exe";

        public string Linker { get; private set; } = @"C:\masm32\bin\link16.exe";

        public string DosBox { get; private set; } =
            @"C:\Program Files (x86)\DOSBox-0.74-3\DOSBox.exe";

        // Сообщение о причине неудачи последней операции
        public string Error { get; private set; } = "";

        // Собранная программа
        public string ExecutablePath { get; private set; } = "";

        // Строки файла настроек: "имя = путь", после ';' — комментарий.
        // Отсутствие файла не ошибка: остаются пути по умолчанию.
        public void LoadSettings(string path)
        {
            string[] lines;

            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException)
            {
                return;
            }

            foreach (string line in lines)
            {
                string text = line.Trim();
                int equals = text.IndexOf('=');

                if (text.StartsWith(";") || equals < 0)
                    continue;

                string name = text.Substring(0, equals).Trim().ToLowerInvariant();
                string value = text.Substring(equals + 1).Trim().Trim('"');

                if (value.Length == 0)
                    continue;

                switch (name)
                {
                    case "ml": Assembler = value; break;
                    case "link16": Linker = value; break;
                    case "dosbox": DosBox = value; break;
                }
            }
        }

        // Ассемблирование и компоновка. Программа создаётся рядом с .asm.
        public bool Build(string asmPath)
        {
            if (!CheckTool(Assembler, "ассемблер", "ml") ||
                !CheckTool(Linker, "компоновщик", "link16"))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(asmPath);
            string directory = Path.GetDirectoryName(fullPath)!;
            string asmName = Path.GetFileName(fullPath);

            // В DOS имя файла — не более 8 латинских букв и цифр
            string name = Path.GetFileNameWithoutExtension(fullPath);

            if (!IsDosName(name))
                name = "program";

            string objName = name + ".obj";
            string objPath = Path.Combine(directory, objName);

            // link16 понимает только объектные файлы формата OMF. Старые версии
            // ml (MASM32) создают их сами и ключа /omf не знают, а новые
            // (Visual Studio) без этого ключа 16-разрядную программу
            // не собирают. Поэтому сначала пробуем без ключа, затем с ним.
            bool assembled =
                Assemble(directory, objName, asmName, omfOption: false) &&
                IsOmf(objPath);

            if (!assembled)
            {
                string firstError = Error;

                Error = "";

                bool retried =
                    Assemble(directory, objName, asmName, omfOption: true);

                if (!retried || !IsOmf(objPath))
                {
                    if (retried || Error.Length == 0)
                    {
                        Error = firstError.Length > 0
                            ? firstError
                            : "Ассемблер не создал объектный файл формата OMF.";
                    }
                    else if (firstError.Length > 0)
                    {
                        // Не удались обе попытки. Старый ml не знает ключа /omf,
                        // и тогда настоящая ошибка — только в первом сообщении
                        Error = firstError + Environment.NewLine + Error;
                    }

                    return false;
                }
            }

            // Объектный файл, программа, без карты и без библиотек
            if (!RunTool(
                    Linker,
                    directory,
                    "Компоновщик сообщил об ошибке",
                    $"{objName},{name}.exe,nul,,nul;"))
            {
                return false;
            }

            ExecutablePath = Path.Combine(directory, name + ".exe");

            return true;
        }

        // Запуск собранной программы в DOSBox.
        // Окно DOSBox остаётся открытым, завершения не ждём.
        public bool Run()
        {
            if (!CheckTool(DosBox, "DOSBox", "dosbox"))
                return false;

            string directory = Path.GetDirectoryName(ExecutablePath)!;

            var start = new ProcessStartInfo(DosBox)
            {
                UseShellExecute = false,

                // С ключом -noconsole DOSBox пишет свои stdout.txt и
                // stderr.txt в текущую папку: не засоряем ими папку программы
                WorkingDirectory = Path.GetTempPath()
            };

            start.ArgumentList.Add("-noconsole");
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add($"mount c \"{directory}\"");
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("c:");
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(Path.GetFileName(ExecutablePath));

            try
            {
                Process.Start(start);
            }
            catch (Exception e) when (
                e is Win32Exception ||
                e is InvalidOperationException)
            {
                Error = $"Не удалось запустить DOSBox ({DosBox}).";
                return false;
            }

            return true;
        }

        private bool Assemble(
            string directory,
            string objName,
            string asmName,
            bool omfOption)
        {
            var arguments = new List<string> { "/nologo", "/c" };

            if (omfOption)
                arguments.Add("/omf");

            arguments.Add("/Fo" + objName);
            arguments.Add(asmName);

            // Чтобы не принять за результат файл от прошлой сборки
            try
            {
                File.Delete(Path.Combine(directory, objName));
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException)
            {
            }

            return RunTool(
                Assembler,
                directory,
                "Ассемблер сообщил об ошибке",
                arguments.ToArray());
        }

        private bool CheckTool(string path, string title, string setting)
        {
            if (File.Exists(path))
                return true;

            Error =
                $"Не найден {title}: {path}. Укажите путь в строке " +
                $"\"{setting} = ...\" файла {SettingsFileName} рядом с программой.";

            return false;
        }

        // Запуск инструмента с ожиданием завершения.
        // При ненулевом коде возврата его вывод попадает в Error.
        private bool RunTool(
            string tool,
            string directory,
            string failure,
            params string[] arguments)
        {
            var start = new ProcessStartInfo(tool)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = directory
            };

            foreach (string argument in arguments)
                start.ArgumentList.Add(argument);

            try
            {
                using (Process process = Process.Start(start)!)
                {
                    var errors = process.StandardError.ReadToEndAsync();
                    string output = process.StandardOutput.ReadToEnd();

                    process.WaitForExit();

                    if (process.ExitCode == 0)
                        return true;

                    Error =
                        $"{failure} (код {process.ExitCode}):" +
                        Environment.NewLine +
                        (output + errors.Result).Trim();

                    return false;
                }
            }
            catch (Exception e) when (
                e is Win32Exception ||
                e is InvalidOperationException)
            {
                Error = $"Не удалось запустить {tool}.";
                return false;
            }
        }

        private static bool IsDosName(string name)
        {
            if (name.Length == 0 || name.Length > 8)
                return false;

            foreach (char c in name)
            {
                bool allowed =
                    (c >= 'A' && c <= 'Z') ||
                    (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_';

                if (!allowed)
                    return false;
            }

            return true;
        }

        // Объектный файл формата OMF начинается с записи THEADR (байт 80h)
        private static bool IsOmf(string objPath)
        {
            try
            {
                using (FileStream stream = File.OpenRead(objPath))
                {
                    return stream.ReadByte() == 0x80;
                }
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
