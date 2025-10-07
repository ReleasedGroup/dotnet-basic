using System.Linq;
using MicrosoftBasicApp.Parsing;

namespace MicrosoftBasicApp.Runtime;

public sealed class BasicProgram
{
    private readonly SortedDictionary<int, string> _lines = new();

    public void SetLine(int number, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            _lines.Remove(number);
            return;
        }

        _lines[number] = source.TrimEnd();
    }

    public void RemoveLine(int number) => _lines.Remove(number);

    public void Clear() => _lines.Clear();

    public IReadOnlyList<(int LineNumber, string Source)> GetLines() => _lines.Select(pair => (pair.Key, pair.Value)).ToList();

    public bool IsEmpty => _lines.Count == 0;

    public CompiledProgram Compile()
    {
        var parser = new BasicParser();
        var compiledLines = new List<CompiledLine>();
        foreach (var (number, source) in _lines)
        {
            List<BasicStatement> statements;
            try
            {
                statements = parser.ParseLine(source);
            }
            catch (BasicSyntaxException ex)
            {
                throw new BasicSyntaxException($"Line {number}: {ex.Message}");
            }

            compiledLines.Add(new CompiledLine(number, statements));
        }

        return new CompiledProgram(compiledLines);
    }
}

public sealed class CompiledProgram
{
    private readonly Dictionary<int, int> _lineLookup;

    public CompiledProgram(List<CompiledLine> lines)
    {
        Lines = lines;
        _lineLookup = lines.Select((line, index) => (line.Number, index))
            .ToDictionary(tuple => tuple.Number, tuple => tuple.index);
    }

    public IReadOnlyList<CompiledLine> Lines { get; }

    public bool TryGetLineIndex(int lineNumber, out int index) => _lineLookup.TryGetValue(lineNumber, out index);

    public int GetLineIndex(int lineNumber)
    {
        if (!_lineLookup.TryGetValue(lineNumber, out var index))
        {
            throw new BasicRuntimeException($"Undefined line {lineNumber}");
        }

        return index;
    }
}

public sealed class CompiledLine
{
    internal CompiledLine(int number, IReadOnlyList<BasicStatement> statements)
    {
        Number = number;
        Statements = statements;
    }

    public int Number { get; }
    internal IReadOnlyList<BasicStatement> Statements { get; }
}

internal readonly struct ProgramCounter
{
    public ProgramCounter(int lineIndex, int statementIndex)
    {
        LineIndex = lineIndex;
        StatementIndex = statementIndex;
    }

    public int LineIndex { get; }
    public int StatementIndex { get; }

    public ProgramCounter Next(CompiledProgram program)
    {
        if (LineIndex >= program.Lines.Count)
        {
            return this;
        }

        var line = program.Lines[LineIndex];
        if (StatementIndex + 1 < line.Statements.Count)
        {
            return new ProgramCounter(LineIndex, StatementIndex + 1);
        }

        return new ProgramCounter(LineIndex + 1, 0);
    }
}
