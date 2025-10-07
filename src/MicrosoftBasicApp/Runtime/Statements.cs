using System.Globalization;
using System.Text;

namespace MicrosoftBasicApp.Runtime;

internal abstract class BasicStatement
{
    public abstract void Execute(BasicRuntime runtime, ProgramCounter location);
}

internal sealed class RemStatement : BasicStatement
{
    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        // No operation
    }
}

internal sealed class AssignmentStatement : BasicStatement
{
    private readonly VariableTarget _target;
    private readonly Expression _expression;

    public AssignmentStatement(VariableTarget target, Expression expression)
    {
        _target = target;
        _expression = expression;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        var value = _expression.Evaluate(runtime);
        runtime.SetValue(_target, value);
    }
}

internal readonly struct PrintItem
{
    private PrintItem(Expression? expression, PrintSeparator? separator)
    {
        Expression = expression;
        Separator = separator;
    }

    public Expression? Expression { get; }
    public PrintSeparator? Separator { get; }
    public bool IsSeparator => Separator.HasValue;

    public static PrintItem FromExpression(Expression expression) => new(expression, null);
    public static PrintItem Comma => new(null, PrintSeparator.Comma);
    public static PrintItem Semicolon => new(null, PrintSeparator.Semicolon);
}

internal enum PrintSeparator
{
    Comma,
    Semicolon
}

internal sealed class PrintStatement : BasicStatement
{
    private readonly IReadOnlyList<PrintItem> _items;
    private readonly Expression? _fileNumber;

    public PrintStatement(IReadOnlyList<PrintItem> items, Expression? fileNumber = null)
    {
        _items = items;
        _fileNumber = fileNumber;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        if (_fileNumber is not null)
        {
            ExecuteFilePrint(runtime);
            return;
        }

        if (_items.Count == 0)
        {
            runtime.Output.WriteLine();
            return;
        }

        var appendNewline = true;
        foreach (var item in _items)
        {
            if (!item.IsSeparator)
            {
                var value = item.Expression!.Evaluate(runtime);
                runtime.Output.Write(value.ToPrintString());
                appendNewline = true;
            }
            else if (item.Separator == PrintSeparator.Comma)
            {
                runtime.Output.Write("\t");
                appendNewline = false;
            }
            else
            {
                appendNewline = false;
            }
        }

        if (appendNewline)
        {
            runtime.Output.WriteLine();
        }
    }

    private void ExecuteFilePrint(BasicRuntime runtime)
    {
        var channel = _fileNumber!.Evaluate(runtime).AsInt32();
        if (_items.Count == 0)
        {
            runtime.WriteToFile(channel, string.Empty, appendNewLine: true);
            return;
        }

        var builder = new StringBuilder();
        var appendNewline = true;
        foreach (var item in _items)
        {
            if (!item.IsSeparator)
            {
                var value = item.Expression!.Evaluate(runtime);
                builder.Append(value.ToPrintString());
                appendNewline = true;
            }
            else if (item.Separator == PrintSeparator.Comma)
            {
                builder.Append(',');
                appendNewline = false;
            }
            else
            {
                appendNewline = false;
            }
        }

        runtime.WriteToFile(channel, builder.ToString(), appendNewline);
    }
}

internal sealed class InputStatement : BasicStatement
{
    private readonly Expression? _prompt;
    private readonly IReadOnlyList<VariableTarget> _targets;

    private readonly Expression? _fileNumber;

    public InputStatement(Expression? prompt, IReadOnlyList<VariableTarget> targets, Expression? fileNumber = null)
    {
        _prompt = prompt;
        _targets = targets;
        _fileNumber = fileNumber;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        if (_fileNumber is not null)
        {
            ExecuteFileInput(runtime);
            return;
        }

        string? prompt = null;
        if (_prompt is not null)
        {
            prompt = _prompt.Evaluate(runtime).AsString();
        }

        foreach (var target in _targets)
        {
            while (true)
            {
                if (prompt is not null)
                {
                    runtime.Output.Write(prompt);
                    runtime.Output.Write("? ");
                }

                var input = runtime.Input.ReadLine();
                if (input is null)
                {
                    throw new BasicRuntimeException("INPUT received end of stream");
                }

                input = input.Trim();

                try
                {
                    if (target.IsString)
                    {
                        runtime.SetValue(target, BasicValue.FromString(input));
                    }
                    else
                    {
                        if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                        {
                            throw new FormatException();
                        }

                        runtime.SetValue(target, BasicValue.FromNumber(number));
                    }

                    break;
                }
                catch (Exception)
                {
                    runtime.Output.WriteLine("?Redo from start");
                    continue;
                }
            }
        }
    }

    private void ExecuteFileInput(BasicRuntime runtime)
    {
        var channel = _fileNumber!.Evaluate(runtime).AsInt32();
        foreach (var target in _targets)
        {
            var raw = runtime.ReadFileField(channel);
            if (target.IsString)
            {
                runtime.SetValue(target, BasicValue.FromString(raw));
            }
            else
            {
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    throw new BasicRuntimeException($"Invalid numeric input '{raw}'");
                }

                runtime.SetValue(target, BasicValue.FromNumber(number));
            }
        }
    }
}

internal sealed class IfStatement : BasicStatement
{
    private readonly Expression _condition;
    private readonly BasicStatement _thenStatement;
    private readonly BasicStatement? _elseStatement;

    public IfStatement(Expression condition, BasicStatement thenStatement, BasicStatement? elseStatement)
    {
        _condition = condition;
        _thenStatement = thenStatement;
        _elseStatement = elseStatement;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        if (_condition.Evaluate(runtime).AsBoolean())
        {
            _thenStatement.Execute(runtime, location);
        }
        else
        {
            _elseStatement?.Execute(runtime, location);
        }
    }
}

internal sealed class GotoStatement : BasicStatement
{
    private readonly Expression _target;

    public GotoStatement(Expression target)
    {
        _target = target;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        var value = _target.Evaluate(runtime).AsInt32();
        runtime.GotoLine(value);
    }
}

internal sealed class GosubStatement : BasicStatement
{
    private readonly Expression _target;

    public GosubStatement(Expression target)
    {
        _target = target;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        var returnAddress = location.Next(runtime.Program);
        runtime.PushReturn(returnAddress);
        runtime.GotoLine(_target.Evaluate(runtime).AsInt32());
    }
}

internal sealed class ReturnStatement : BasicStatement
{
    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        runtime.ReturnFromGosub();
    }
}

internal sealed class ForStatement : BasicStatement
{
    private readonly string _name;
    private readonly Expression _start;
    private readonly Expression _limit;
    private readonly Expression _step;

    public ForStatement(string name, Expression start, Expression limit, Expression step)
    {
        _name = name;
        _start = start;
        _limit = limit;
        _step = step;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        var startValue = _start.Evaluate(runtime).AsNumber();
        var limitValue = _limit.Evaluate(runtime).AsNumber();
        var stepValue = _step.Evaluate(runtime).AsNumber();

        runtime.SetValue(new VariableTarget(_name), BasicValue.FromNumber(startValue));
        runtime.PushFor(new ForFrame(_name, limitValue, stepValue, location.Next(runtime.Program)));
    }
}

internal sealed class NextStatement : BasicStatement
{
    private readonly string? _name;

    public NextStatement(string? name)
    {
        _name = name;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        runtime.ProcessNext(_name);
    }
}

internal sealed class DimStatement : BasicStatement
{
    private readonly IReadOnlyList<DimEntry> _entries;

    public DimStatement(IReadOnlyList<DimEntry> entries)
    {
        _entries = entries;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        foreach (var entry in _entries)
        {
            runtime.DefineArray(entry);
        }
    }
}

internal sealed class EndStatement : BasicStatement
{
    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        runtime.Stop();
    }
}

internal sealed class StopStatement : BasicStatement
{
    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        runtime.Stop();
    }
}

internal sealed class ClearStatement : BasicStatement
{
    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        runtime.ClearVariables();
    }
}

internal sealed class BlockStatement : BasicStatement
{
    private readonly IReadOnlyList<BasicStatement> _statements;

    public BlockStatement(IReadOnlyList<BasicStatement> statements)
    {
        _statements = statements;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        foreach (var statement in _statements)
        {
            statement.Execute(runtime, location);
        }
    }
}

internal sealed class OnGotoStatement : BasicStatement
{
    private readonly Expression _selector;
    private readonly IReadOnlyList<Expression> _targets;

    public OnGotoStatement(Expression selector, IReadOnlyList<Expression> targets)
    {
        _selector = selector;
        _targets = targets;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        var index = _selector.Evaluate(runtime).AsInt32();
        if (index <= 0 || index > _targets.Count)
        {
            return;
        }

        var lineNumber = _targets[index - 1].Evaluate(runtime).AsInt32();
        runtime.GotoLine(lineNumber);
    }
}

internal sealed class OnGosubStatement : BasicStatement
{
    private readonly Expression _selector;
    private readonly IReadOnlyList<Expression> _targets;

    public OnGosubStatement(Expression selector, IReadOnlyList<Expression> targets)
    {
        _selector = selector;
        _targets = targets;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        var index = _selector.Evaluate(runtime).AsInt32();
        if (index <= 0 || index > _targets.Count)
        {
            return;
        }

        var lineNumber = _targets[index - 1].Evaluate(runtime).AsInt32();
        var returnAddress = location.Next(runtime.Program);
        runtime.PushReturn(returnAddress);
        runtime.GotoLine(lineNumber);
    }
}

internal sealed class ReadStatement : BasicStatement
{
    private readonly IReadOnlyList<VariableTarget> _targets;

    public ReadStatement(IReadOnlyList<VariableTarget> targets)
    {
        _targets = targets;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        foreach (var target in _targets)
        {
            var value = runtime.ReadDataValue();
            var coerced = target.IsString
                ? BasicValue.FromString(value.AsString())
                : BasicValue.FromNumber(value.AsNumber());
            runtime.SetValue(target, coerced);
        }
    }
}

internal sealed class RestoreStatement : BasicStatement
{
    private readonly Expression? _lineNumber;

    public RestoreStatement(Expression? lineNumber)
    {
        _lineNumber = lineNumber;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        int? target = null;
        if (_lineNumber is not null)
        {
            target = _lineNumber.Evaluate(runtime).AsInt32();
        }

        runtime.RestoreData(target);
    }
}

internal sealed class RandomizeStatement : BasicStatement
{
    private readonly Expression? _seed;

    public RandomizeStatement(Expression? seed)
    {
        _seed = seed;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        double? value = null;
        if (_seed is not null)
        {
            value = _seed.Evaluate(runtime).AsNumber();
        }

        runtime.Randomize(value);
    }
}

internal sealed class DefineFunctionStatement : BasicStatement
{
    private readonly string _name;
    private readonly IReadOnlyList<string> _parameters;
    private readonly Expression _body;

    public DefineFunctionStatement(string name, IReadOnlyList<string> parameters, Expression body)
    {
        _name = name;
        _parameters = parameters;
        _body = body;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        runtime.DefineFunction(_name, _parameters, _body);
    }
}

internal sealed class DataStatement : BasicStatement
{
    public DataStatement(IReadOnlyList<BasicValue> values)
    {
        Values = values;
    }

    public IReadOnlyList<BasicValue> Values { get; }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        // DATA is non-executable at runtime.
    }
}

internal sealed class OpenStatement : BasicStatement
{
    private readonly Expression _path;
    private readonly FileOpenMode _mode;
    private readonly Expression _channel;

    public OpenStatement(Expression path, FileOpenMode mode, Expression channel)
    {
        _path = path;
        _mode = mode;
        _channel = channel;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        var filePath = _path.Evaluate(runtime).AsString();
        var channel = _channel.Evaluate(runtime).AsInt32();
        runtime.OpenFile(filePath, _mode, channel);
    }
}

internal sealed class CloseStatement : BasicStatement
{
    private readonly IReadOnlyList<Expression> _channels;

    public CloseStatement(IReadOnlyList<Expression> channels)
    {
        _channels = channels;
    }

    public override void Execute(BasicRuntime runtime, ProgramCounter location)
    {
        if (_channels.Count == 0)
        {
            runtime.CloseAllFiles();
            return;
        }

        foreach (var expression in _channels)
        {
            var channel = expression.Evaluate(runtime).AsInt32();
            runtime.CloseFile(channel);
        }
    }
}

internal readonly record struct VariableTarget(string Name, IReadOnlyList<Expression>? Indices = null)
{
    public bool IsArray => Indices is not null;
    public bool IsString => Name.EndsWith("$", StringComparison.Ordinal);
}

internal readonly record struct DimEntry(string Name, IReadOnlyList<Expression> Dimensions);

internal readonly record struct ForFrame(string VariableName, double Limit, double Step, ProgramCounter BodyStart);
