using Translator.Generation;

namespace Course_work_Savchuk_Savchuk.Tests;

// Сами инструменты (ml, link16, DOSBox) в тестах не запускаются
public class DosRunnerTests : IDisposable
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public DosRunnerTests()
    {
        Directory.CreateDirectory(directory);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
    }

    private string Missing(string name)
    {
        return Path.Combine(directory, name);
    }

    private DosRunner Load(params string[] lines)
    {
        string path = Path.Combine(directory, DosRunner.SettingsFileName);

        File.WriteAllLines(path, lines);

        var runner = new DosRunner();

        runner.LoadSettings(path);

        return runner;
    }

    [Fact]
    public void Settings_ReplaceDefaultPaths()
    {
        DosRunner runner = Load(
            @"ml = D:\tools\ml.exe",
            @"link16 = D:\tools\link16.exe",
            @"dosbox = D:\DOSBox\DOSBox.exe");

        Assert.Equal(@"D:\tools\ml.exe", runner.Assembler);
        Assert.Equal(@"D:\tools\link16.exe", runner.Linker);
        Assert.Equal(@"D:\DOSBox\DOSBox.exe", runner.DosBox);
    }

    [Fact]
    public void Settings_AllowAnyCaseSpacesAndQuotes()
    {
        DosRunner runner = Load(
            "  ML=\"D:\\my tools\\ml.exe\"  ",
            "Link16   =   D:\\link16.exe",
            "DOSBOX=D:\\a=b\\DOSBox.exe");

        Assert.Equal(@"D:\my tools\ml.exe", runner.Assembler);
        Assert.Equal(@"D:\link16.exe", runner.Linker);

        // Путь может содержать '='
        Assert.Equal(@"D:\a=b\DOSBox.exe", runner.DosBox);
    }

    [Fact]
    public void Settings_SkipCommentsEmptyValuesAndUnknownNames()
    {
        var defaults = new DosRunner();

        DosRunner runner = Load(
            "; ml = D:\\commented.exe",
            "",
            "просто текст",
            "link16 =",
            "dosbox = \"\"",
            "tasm = D:\\tasm.exe");

        Assert.Equal(defaults.Assembler, runner.Assembler);
        Assert.Equal(defaults.Linker, runner.Linker);
        Assert.Equal(defaults.DosBox, runner.DosBox);
    }

    [Fact]
    public void MissingSettingsFile_KeepsDefaultPaths()
    {
        var defaults = new DosRunner();
        var runner = new DosRunner();

        runner.LoadSettings(Missing("tools.txt"));

        Assert.Equal(defaults.Assembler, runner.Assembler);
        Assert.Equal(defaults.Linker, runner.Linker);
        Assert.Equal(defaults.DosBox, runner.DosBox);
    }

    [Fact]
    public void Build_WithoutAssembler_FailsWithHint()
    {
        DosRunner runner = Load("ml = " + Missing("ml.exe"));

        Assert.False(runner.Build(Missing("program.asm")));

        Assert.Contains("Не найден ассемблер", runner.Error);
        Assert.Contains(Missing("ml.exe"), runner.Error);
        Assert.Contains("\"ml = ...\"", runner.Error);
        Assert.Contains(DosRunner.SettingsFileName, runner.Error);
        Assert.Equal("", runner.ExecutablePath);
    }

    [Fact]
    public void Build_WithoutLinker_FailsWithHint()
    {
        string assembler = Missing("ml.exe");

        File.WriteAllText(assembler, "");

        DosRunner runner = Load(
            "ml = " + assembler,
            "link16 = " + Missing("link16.exe"));

        Assert.False(runner.Build(Missing("program.asm")));

        Assert.Contains("Не найден компоновщик", runner.Error);
        Assert.Contains("\"link16 = ...\"", runner.Error);
    }

    // Подставной ml: без /omf сообщает настоящую ошибку,
    // с /omf — «неизвестный ключ», как старая версия
    [Fact]
    public void Build_WhenBothAttemptsFail_ShowsBothMessages()
    {
        string assembler = Missing("ml.cmd");

        File.WriteAllText(
            assembler,
            "@echo off\r\n" +
            "echo %* | findstr /c:\"/omf\" >nul\r\n" +
            "if errorlevel 1 (echo REAL syntax error) else (echo unknown option omf)\r\n" +
            "exit /b 1\r\n");

        DosRunner runner = Load("ml = " + assembler, "link16 = " + assembler);

        Assert.False(runner.Build(Missing("program.asm")));

        Assert.Contains("REAL syntax error", runner.Error);
        Assert.Contains("unknown option omf", runner.Error);
    }

    [Fact]
    public void Run_WithoutDosBox_FailsWithHint()
    {
        DosRunner runner = Load("dosbox = " + Missing("DOSBox.exe"));

        Assert.False(runner.Run());

        Assert.Contains("Не найден DOSBox", runner.Error);
        Assert.Contains("\"dosbox = ...\"", runner.Error);
    }
}
