using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MicrosoftBasicApp.Parsing;
using MicrosoftBasicApp.Runtime;
using Xunit;

namespace MicrosoftBasicApp.Tests;

public class BasicInterpreterTests
{
    [Fact]
    public void RunProgram_PrintsExpectedOutput()
    {
        var program = BuildProgram(
            "10 PRINT \"HELLO\"",
            "20 PRINT 2+2",
            "30 END");

        var io = new BufferedBasicIO();
        Execute(program, io);
        var output = io.GetBuffer();

        Assert.Contains("HELLO", output);
        Assert.Contains("4", output);
    }

    [Fact]
    public void IfGoto_EvaluatesCondition()
    {
        var program = BuildProgram(
            "10 I=0",
            "20 IF I=5 THEN 60",
            "30 I=I+1",
            "40 GOTO 20",
            "50 END",
            "60 PRINT I");

        var io = new BufferedBasicIO();
        Execute(program, io);
        Assert.Contains("5", io.GetBuffer());
    }

    [Fact]
    public void ForNext_LoopsAccumulate()
    {
        var program = BuildProgram(
            "10 S=0",
            "20 FOR I=1 TO 5",
            "30 S=S+I",
            "40 NEXT I",
            "50 PRINT S",
            "60 END");

        var io = new BufferedBasicIO();
        Execute(program, io);
        Assert.Contains("15", io.GetBuffer());
    }

    [Fact]
    public void Gosub_ReturnsToCaller()
    {
        var program = BuildProgram(
            "10 GOSUB 100",
            "20 PRINT X",
            "30 END",
            "100 X=42",
            "110 RETURN");

        var io = new BufferedBasicIO();
        Execute(program, io);
        Assert.Contains("42", io.GetBuffer());
    }

    [Fact]
    public void Arrays_StoreValues()
    {
        var program = BuildProgram(
            "10 DIM A(5)",
            "20 FOR I=0 TO 5",
            "30 A(I)=I*I",
            "40 NEXT I",
            "50 PRINT A(3)",
            "60 END");

        var io = new BufferedBasicIO();
        Execute(program, io);
        Assert.Contains("9", io.GetBuffer());
    }

    [Fact]
    public void StringFunctions_OperateCorrectly()
    {
        var program = BuildProgram(
            "10 A$=\"HELLO\"",
            "20 PRINT LEFT$(A$,2);MID$(A$,3,2)",
            "30 END");

        var io = new BufferedBasicIO();
        Execute(program, io);
        Assert.Contains("HELL", io.GetBuffer());
    }

    [Fact]
    public void Input_ReadsValues()
    {
        var program = BuildProgram(
            "10 INPUT \"NUMBER\";N",
            "20 PRINT N*2",
            "30 END");

        var io = new BufferedBasicIO(new[] { "5" });
        Execute(program, io);
        Assert.Contains("10", io.GetBuffer());
    }

    [Fact]
    public void InputPrompt_PrintsQuestionMark()
    {
        var program = BuildProgram(
            "10 INPUT \"COMMAND\";A$",
            "20 PRINT A$",
            "30 END");

        var io = new BufferedBasicIO(new[] { "HELLO" });
        Execute(program, io);

        var output = io.GetBuffer();
        Assert.Contains("COMMAND? ", output, StringComparison.Ordinal);
        Assert.Contains("HELLO", output, StringComparison.Ordinal);
    }

    private static BasicProgram BuildProgram(params string[] lines)
    {
        var program = new BasicProgram();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var spaceIndex = trimmed.IndexOf(' ');
            if (spaceIndex < 0)
            {
                var number = int.Parse(trimmed, System.Globalization.CultureInfo.InvariantCulture);
                program.SetLine(number, string.Empty);
            }
            else
            {
                var number = int.Parse(trimmed[..spaceIndex], System.Globalization.CultureInfo.InvariantCulture);
                var source = trimmed[(spaceIndex + 1)..];
                program.SetLine(number, source);
            }
        }

        return program;
    }

    private static void Execute(BasicProgram program, BufferedBasicIO io)
    {
        var runtime = new BasicRuntime(program.Compile(), io);
        runtime.ClearVariables();
        runtime.Execute();
    }

    [Fact]
    public void TestBasScript_RunsSuccessfully()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var scriptPath = Path.Combine(repoRoot, "TEST.BAS");
        var program = BuildProgramFromFile(scriptPath);

        var tempDir = Path.Combine(Path.GetTempPath(), "BasicInterpreterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var originalDir = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(tempDir);

            var io = new BufferedBasicIO(new[] { "5", "A" });
            var runtime = new BasicRuntime(program.Compile(), io);
            runtime.ClearVariables();
            runtime.Execute();

            var output = io.GetBuffer();
            Assert.Contains("READ V(", output, StringComparison.Ordinal);
            Assert.Contains("Unreachable?", output, StringComparison.Ordinal);
            Assert.Contains("Some math functions", output, StringComparison.Ordinal);
            Assert.Contains("Testing IF…THEN…ELSE", output, StringComparison.Ordinal);
            Assert.Contains("Test F(3) =", output, StringComparison.Ordinal);

            var dataPath = Path.Combine(tempDir, "TEST.DAT");
            Assert.True(File.Exists(dataPath));
            var fileContent = File.ReadAllText(dataPath);
            Assert.Contains("Line one", fileContent, StringComparison.Ordinal);
            Assert.Contains("X=", fileContent, StringComparison.Ordinal);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void StartrekScript_AllowsImmediateResign()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var scriptPath = Path.Combine(repoRoot, "STARTREK.BAS");
        var program = BuildProgramFromFile(scriptPath);

        var io = new BufferedBasicIO(new[] { "XXX", string.Empty });
        var runtime = new BasicRuntime(program.Compile(), io);
        runtime.ClearVariables();
        runtime.Execute();

        var output = io.GetBuffer();
        Assert.Contains("THE USS ENTERPRISE", output, StringComparison.Ordinal);
        Assert.Contains("YOUR ORDERS", output, StringComparison.Ordinal);
        Assert.Contains("COMMAND? ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void StartrekScript_AllowsMultipleCommands()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var scriptPath = Path.Combine(repoRoot, "STARTREK.BAS");
        var program = BuildProgramFromFile(scriptPath);

        var inputs = new[] { "SRS", "XXX", string.Empty };
        var io = new InstrumentedBasicIO(inputs);
        var runtime = new BasicRuntime(program.Compile(), io);
        runtime.ClearVariables();
        runtime.Execute();

        var output = io.GetBuffer();
        var promptCount = CountOccurrences(output, "COMMAND? ");
        Assert.True(promptCount >= 2, $"Expected at least two command prompts but saw {promptCount}.");
        Assert.Equal(inputs.Length, io.ConsumedInputs);
        Assert.Contains("LET HIM STEP FORWARD", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadCommand_LoadsProgramFromFile()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"basic-load-{Guid.NewGuid():N}.bas");
        File.WriteAllLines(tempPath, new[]
        {
            "10 PRINT \"HELLO\"",
            "20 END"
        });

        try
        {
            var io = new BufferedBasicIO();
            var interpreter = new BasicInterpreter(io);

            interpreter.ProcessCommand($"LOAD \"{tempPath}\"");

            var programText = interpreter.DumpProgram();
            Assert.Contains("10 PRINT \"HELLO\"", programText, StringComparison.Ordinal);
            Assert.Contains("20 END", programText, StringComparison.Ordinal);
            Assert.Contains("READY.", io.GetBuffer(), StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void SaveCommand_WritesProgramToFile()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"basic-save-{Guid.NewGuid():N}.bas");

        try
        {
            var io = new BufferedBasicIO();
            var interpreter = new BasicInterpreter(io);

            interpreter.ProcessCommand("10 PRINT \"HI\"");
            interpreter.ProcessCommand("20 END");

            interpreter.ProcessCommand($"SAVE \"{tempPath}\"");
            Assert.True(File.Exists(tempPath));
            var saved = File.ReadAllText(tempPath);
            Assert.Contains("10 PRINT \"HI\"", saved, StringComparison.Ordinal);
            Assert.Contains("20 END", saved, StringComparison.Ordinal);

            var reloadIo = new BufferedBasicIO();
            var reloadInterpreter = new BasicInterpreter(reloadIo);
            reloadInterpreter.ProcessCommand($"LOAD \"{tempPath}\"");
            reloadInterpreter.ProcessCommand("SAVE");
            Assert.Contains("READY.", reloadIo.GetBuffer(), StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static BasicProgram BuildProgramFromFile(string path)
    {
        var program = new BasicProgram();
        foreach (var rawLine in File.ReadLines(path))
        {
            var trimmed = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var spaceIndex = trimmed.IndexOf(' ');
            if (spaceIndex < 0)
            {
                var number = int.Parse(trimmed, CultureInfo.InvariantCulture);
                program.SetLine(number, string.Empty);
            }
            else
            {
                var number = int.Parse(trimmed[..spaceIndex], CultureInfo.InvariantCulture);
                var source = trimmed[(spaceIndex + 1)..];
                program.SetLine(number, source);
            }
        }

        return program;
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while (true)
        {
            index = text.IndexOf(value, index, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class InstrumentedBasicIO : IBasicIO
    {
        private readonly Queue<string> _inputs;
        private readonly StringBuilder _buffer = new();
        private readonly List<string> _lines = new();

        public InstrumentedBasicIO(IEnumerable<string> inputs)
        {
            _inputs = new Queue<string>(inputs);
        }

        public int ConsumedInputs { get; private set; }

        public string? ReadLine()
        {
            ConsumedInputs++;
            return _inputs.Count > 0 ? _inputs.Dequeue() : null;
        }

        public void Write(string text)
        {
            _buffer.Append(text);
        }

        public void WriteLine(string text = "")
        {
            _buffer.AppendLine(text);
            _lines.Add(text);
        }

        public string GetBuffer() => _buffer.ToString();

        public IReadOnlyList<string> Lines => _lines;
    }

    [Fact]
    public void Parser_HandlesOpenStatementWithoutSpaces()
    {
        const string line = "OPEN \"TEST.DAT\" FOR OUTPUT AS #1";
        var parser = new BasicParser();
        var statements = parser.ParseLine(line);
        Assert.Single(statements);
    }

}
