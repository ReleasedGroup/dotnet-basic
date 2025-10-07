namespace MicrosoftBasicApp.Runtime;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public sealed class BasicRuntime
{
    private readonly CompiledProgram _program;
    private readonly IBasicIO _io;
    private readonly Dictionary<string, BasicValue> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BasicArray> _arrays = new(StringComparer.Ordinal);
    private readonly Stack<ProgramCounter> _returnStack = new();
    private readonly Stack<ForFrame> _forStack = new();
    private readonly Queue<char> _pendingInputChars = new();
    private readonly List<DataItem> _dataItems;
    private readonly Dictionary<string, UserFunctionDefinition> _functions = new(StringComparer.Ordinal);
    private readonly Dictionary<int, FileChannel> _openFiles = new();
    private ProgramCounter? _pendingJump;
    private bool _stopRequested;
    private int _dataIndex;
    private Random _random = new();

    public BasicRuntime(CompiledProgram program, IBasicIO io)
    {
        _program = program;
        _io = io;
        _dataItems = BuildDataTable(program);
    }

    public CompiledProgram Program => _program;
    public IBasicIO Output => _io;
    public IBasicIO Input => _io;

    public void Execute()
    {
        var counter = new ProgramCounter(0, 0);
        try
        {
            while (!_stopRequested && counter.LineIndex < _program.Lines.Count)
            {
                var line = _program.Lines[counter.LineIndex];
                if (counter.StatementIndex >= line.Statements.Count)
                {
                    counter = new ProgramCounter(counter.LineIndex + 1, 0);
                    continue;
                }

                _pendingJump = null;
                var statement = line.Statements[counter.StatementIndex];
                statement.Execute(this, counter);

                if (_stopRequested)
                {
                    break;
                }

                if (_pendingJump is { } jump)
                {
                    counter = jump;
                    continue;
                }

                counter = counter.Next(_program);
            }
        }
        finally
        {
            CloseAllFiles();
        }
    }

    public void Stop()
    {
        _stopRequested = true;
    }

    public void ClearVariables()
    {
        _variables.Clear();
        _arrays.Clear();
        _returnStack.Clear();
        _forStack.Clear();
        _pendingInputChars.Clear();
        _dataIndex = 0;
        _random = new Random();
    }

    internal void SetValue(VariableTarget target, BasicValue value)
    {
        if (target.IsArray)
        {
            SetArrayValue(target, value);
            return;
        }

        if (target.IsString)
        {
            _variables[target.Name] = BasicValue.FromString(value.AsString());
        }
        else
        {
            _variables[target.Name] = BasicValue.FromNumber(value.AsNumber());
        }
    }

    public BasicValue GetVariable(string name)
    {
        if (_variables.TryGetValue(name, out var value))
        {
            return value;
        }

        return name.EndsWith("$", StringComparison.Ordinal) ? BasicValue.EmptyString : BasicValue.Zero;
    }

    internal BasicValue GetArrayValue(string name, IReadOnlyList<Expression> indices)
    {
        var array = GetOrCreateArray(name, indices.Count, name.EndsWith("$", StringComparison.Ordinal));
        var resolvedIndices = indices.Select(i => i.Evaluate(this).AsInt32()).ToArray();
        return array.GetValue(resolvedIndices);
    }

    private void SetArrayValue(VariableTarget target, BasicValue value)
    {
        var indices = target.Indices!;
        var array = GetOrCreateArray(target.Name, indices.Count, target.IsString);
        var resolved = indices.Select(i => i.Evaluate(this).AsInt32()).ToArray();
        array.SetValue(resolved, target.IsString ? BasicValue.FromString(value.AsString()) : BasicValue.FromNumber(value.AsNumber()));
    }

    private BasicArray GetOrCreateArray(string name, int rank, bool isString)
    {
        if (_arrays.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var dimensions = Enumerable.Repeat(11, rank).ToArray();
        var array = new BasicArray(name, dimensions, isString);
        _arrays[name] = array;
        return array;
    }

    internal void DefineArray(DimEntry entry)
    {
        var isString = entry.Name.EndsWith("$", StringComparison.Ordinal);
        var dimensions = entry.Dimensions.Select(d => Math.Max(0, d.Evaluate(this).AsInt32()) + 1).ToArray();
        if (_arrays.TryGetValue(entry.Name, out var existing))
        {
            throw new BasicRuntimeException($"Array {entry.Name} already dimensioned");
        }

        _arrays[entry.Name] = new BasicArray(entry.Name, dimensions, isString);
    }

    internal BasicValue ReadDataValue()
    {
        if (_dataIndex >= _dataItems.Count)
        {
            throw new BasicRuntimeException("Out of data");
        }

        return _dataItems[_dataIndex++].Value;
    }

    internal void RestoreData(int? lineNumber)
    {
        if (lineNumber is null)
        {
            _dataIndex = 0;
            return;
        }

        var target = lineNumber.Value;
        var index = _dataItems.FindIndex(item => item.LineNumber >= target);
        _dataIndex = index >= 0 ? index : _dataItems.Count;
    }

    public void GotoLine(int lineNumber)
    {
        var index = _program.GetLineIndex(lineNumber);
        _pendingJump = new ProgramCounter(index, 0);
    }

    internal void PushReturn(ProgramCounter address)
    {
        _returnStack.Push(address);
    }

    public void ReturnFromGosub()
    {
        if (_returnStack.Count == 0)
        {
            throw new BasicRuntimeException("RETURN without GOSUB");
        }

        _pendingJump = _returnStack.Pop();
    }

    internal void PushFor(ForFrame frame)
    {
        _forStack.Push(frame);
    }

    public void ProcessNext(string? name)
    {
        if (_forStack.Count == 0)
        {
            throw new BasicRuntimeException("NEXT without FOR");
        }

        ForFrame frame;
        if (name is null)
        {
            frame = _forStack.Pop();
        }
        else
        {
            var buffer = new Stack<ForFrame>();
            var found = false;
            frame = default;
            while (_forStack.Count > 0)
            {
                var candidate = _forStack.Pop();
                if (string.Equals(candidate.VariableName, name, StringComparison.Ordinal))
                {
                    frame = candidate;
                    found = true;
                    break;
                }

                buffer.Push(candidate);
            }

            while (buffer.Count > 0)
            {
                _forStack.Push(buffer.Pop());
            }

            if (!found)
            {
                throw new BasicRuntimeException("NEXT without matching FOR");
            }
        }

        var current = GetVariable(frame.VariableName).AsNumber();
        var next = frame.Step == 0 ? current : current + frame.Step;
        SetValue(new VariableTarget(frame.VariableName), BasicValue.FromNumber(next));

        bool shouldContinue;
        if (frame.Step > 0)
        {
            shouldContinue = next <= frame.Limit + 1e-9;
        }
        else if (frame.Step < 0)
        {
            shouldContinue = next >= frame.Limit - 1e-9;
        }
        else
        {
            shouldContinue = false;
        }

        if (shouldContinue)
        {
            _forStack.Push(frame);
            _pendingJump = frame.BodyStart;
        }
    }

    internal void Randomize(double? seed)
    {
        if (seed is null)
        {
            _random = new Random(unchecked(Environment.TickCount));
            return;
        }

        _random = new Random((int)Math.Round(seed.Value));
    }

    internal void DefineFunction(string name, IReadOnlyList<string> parameters, Expression body)
    {
        _functions[name] = new UserFunctionDefinition(name, parameters, body);
    }

    internal bool TryInvokeFunction(string name, IReadOnlyList<BasicValue> arguments, out BasicValue result)
    {
        if (!_functions.TryGetValue(name, out var function))
        {
            result = default;
            return false;
        }

        if (function.Parameters.Count != arguments.Count)
        {
            throw new BasicRuntimeException($"Function {name} expects {function.Parameters.Count} argument(s)");
        }

        var previousValues = new Dictionary<string, BasicValue>(_variables.Comparer);
        var removed = new HashSet<string>(_variables.Comparer);

        for (var i = 0; i < function.Parameters.Count; i++)
        {
            var parameter = function.Parameters[i];
            if (_variables.TryGetValue(parameter, out var existing))
            {
                previousValues[parameter] = existing;
            }
            else
            {
                removed.Add(parameter);
            }

            _variables[parameter] = arguments[i];
        }

        try
        {
            result = function.Body.Evaluate(this);
        }
        finally
        {
            foreach (var parameter in function.Parameters)
            {
                if (removed.Contains(parameter))
                {
                    _variables.Remove(parameter);
                }
                else
                {
                    _variables[parameter] = previousValues[parameter];
                }
            }
        }

        return true;
    }

    internal int ReadKeyCode()
    {
        if (_pendingInputChars.Count == 0)
        {
            var line = _io.ReadLine();
            if (line is null)
            {
                throw new BasicRuntimeException("GET received end of stream");
            }

            foreach (var ch in line)
            {
                _pendingInputChars.Enqueue(ch);
            }

            _pendingInputChars.Enqueue('\n');
        }

        return _pendingInputChars.Dequeue();
    }

    internal void OpenFile(string path, FileOpenMode mode, int channel)
    {
        CloseFile(channel);

        var fullPath = Path.GetFullPath(path);
        FileChannel handle = mode switch
        {
            FileOpenMode.Input => FileChannel.ForReader(fullPath),
            FileOpenMode.Output => FileChannel.ForWriter(fullPath, append: false),
            FileOpenMode.Append => FileChannel.ForWriter(fullPath, append: true),
            _ => throw new BasicRuntimeException($"Unsupported file mode {mode}")
        };

        _openFiles[channel] = handle;
    }

    internal void WriteToFile(int channel, string text, bool appendNewLine)
    {
        if (!_openFiles.TryGetValue(channel, out var handle) || handle.Writer is null)
        {
            throw new BasicRuntimeException($"File #{channel} is not open for output");
        }

        if (appendNewLine)
        {
            handle.Writer.WriteLine(text);
        }
        else
        {
            handle.Writer.Write(text);
        }

        handle.Writer.Flush();
    }

    internal string ReadFileField(int channel)
    {
        if (!_openFiles.TryGetValue(channel, out var handle) || handle.Reader is null)
        {
            throw new BasicRuntimeException($"File #{channel} is not open for input");
        }

        if (!handle.TryReadField(out var value))
        {
            throw new BasicRuntimeException($"End of file on channel {channel}");
        }

        return value;
    }

    internal void CloseFile(int channel)
    {
        if (_openFiles.Remove(channel, out var handle))
        {
            handle.Dispose();
        }
    }

    internal void CloseAllFiles()
    {
        if (_openFiles.Count == 0)
        {
            return;
        }

        var channels = _openFiles.Keys.ToList();
        foreach (var channel in channels)
        {
            CloseFile(channel);
        }
    }

    private List<DataItem> BuildDataTable(CompiledProgram program)
    {
        var items = new List<DataItem>();
        foreach (var line in program.Lines)
        {
            foreach (var statement in line.Statements)
            {
                if (statement is DataStatement data)
                {
                    foreach (var value in data.Values)
                    {
                        items.Add(new DataItem(line.Number, value));
                    }
                }
            }
        }

        return items;
    }

    private static IEnumerable<string> SplitInputFields(string line)
    {
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                yield return builder.ToString().Trim();
                builder.Clear();
                continue;
            }

            builder.Append(ch);
        }

        if (builder.Length > 0 || line.Length == 0 || line.EndsWith(",", StringComparison.Ordinal))
        {
            yield return builder.ToString().Trim();
        }
    }

    private sealed class UserFunctionDefinition
    {
        public UserFunctionDefinition(string name, IReadOnlyList<string> parameters, Expression body)
        {
            Name = name;
            Parameters = parameters.ToArray();
            Body = body;
        }

        public string Name { get; }
        public IReadOnlyList<string> Parameters { get; }
        public Expression Body { get; }
    }

    private readonly struct DataItem
    {
        public DataItem(int lineNumber, BasicValue value)
        {
            LineNumber = lineNumber;
            Value = value;
        }

        public int LineNumber { get; }
        public BasicValue Value { get; }
    }

    private sealed class FileChannel : IDisposable
    {
        private readonly Queue<string> _pendingFields = new();

        private FileChannel(StreamReader? reader, StreamWriter? writer)
        {
            Reader = reader;
            Writer = writer;
        }

        public StreamReader? Reader { get; }
        public StreamWriter? Writer { get; }

        public static FileChannel ForReader(string path)
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return new FileChannel(new StreamReader(stream), null);
        }

        public static FileChannel ForWriter(string path, bool append)
        {
            var stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
            var writer = new StreamWriter(stream) { AutoFlush = true };
            return new FileChannel(null, writer);
        }

        public bool TryReadField(out string value)
        {
            while (_pendingFields.Count == 0)
            {
                if (Reader is null)
                {
                    value = string.Empty;
                    return false;
                }

                var line = Reader.ReadLine();
                if (line is null)
                {
                    value = string.Empty;
                    return false;
                }

                foreach (var field in SplitInputFields(line))
                {
                    _pendingFields.Enqueue(Unwrap(field));
                }
            }

            value = _pendingFields.Dequeue();
            return true;
        }

        public void Dispose()
        {
            Reader?.Dispose();
            Writer?.Dispose();
        }

        private static string Unwrap(string field)
        {
            if (field.Length >= 2 && field[0] == '"' && field[^1] == '"')
            {
                var inner = field[1..^1];
                return inner.Replace("\"\"", "\"");
            }

            return field;
        }
    }

    public double NextRandom() => _random.NextDouble();

    public void SeedRandom(int seed)
    {
        _random = new Random(seed);
    }
}

internal enum FileOpenMode
{
    Input,
    Output,
    Append
}

internal sealed class BasicArray
{
    private readonly Dictionary<int, BasicValue> _storage = new();

    public BasicArray(string name, IReadOnlyList<int> dimensions, bool isString)
    {
        Name = name;
        Dimensions = dimensions.ToArray();
        IsString = isString;
    }

    public string Name { get; }
    public IReadOnlyList<int> Dimensions { get; }
    public bool IsString { get; }

    public BasicValue GetValue(IReadOnlyList<int> indices)
    {
        var offset = GetOffset(indices);
        if (_storage.TryGetValue(offset, out var value))
        {
            return value;
        }

        return IsString ? BasicValue.EmptyString : BasicValue.Zero;
    }

    public void SetValue(IReadOnlyList<int> indices, BasicValue value)
    {
        var offset = GetOffset(indices);
        _storage[offset] = IsString ? BasicValue.FromString(value.AsString()) : BasicValue.FromNumber(value.AsNumber());
    }

    private int GetOffset(IReadOnlyList<int> indices)
    {
        if (indices.Count != Dimensions.Count)
        {
            throw new BasicRuntimeException($"Array {Name} expects {Dimensions.Count} dimensions");
        }

        var offset = 0;
        var stride = 1;
        for (var i = Dimensions.Count - 1; i >= 0; i--)
        {
            var index = indices[i];
            if (index < 0 || index >= Dimensions[i])
            {
                throw new BasicRuntimeException($"Index out of range for {Name}");
            }

            offset += index * stride;
            stride *= Dimensions[i];
        }

        return offset;
    }
}
