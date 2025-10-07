using System.Globalization;

namespace MicrosoftBasicApp.Runtime;

internal static class BuiltInFunctions
{
    private static readonly HashSet<string> Functions = new(StringComparer.Ordinal)
    {
        "ABS","ATN","COS","EXP","INT","LOG","RND","SGN","SIN","SQR","TAN","GET",
        "LEN","LEFT$","RIGHT$","MID$","CHR$","ASC","STR$","VAL","TAB","SPC"
    };

    public static bool IsBuiltIn(string name) => Functions.Contains(name);

    public static BasicValue Invoke(string name, IReadOnlyList<BasicValue> args, BasicRuntime runtime)
    {
        return name switch
        {
            "ABS" => RequireArgs(name, args, 1, () => BasicValue.FromNumber(Math.Abs(args[0].AsNumber()))),
            "ATN" => RequireArgs(name, args, 1, () => BasicValue.FromNumber(Math.Atan(args[0].AsNumber()))),
            "COS" => RequireArgs(name, args, 1, () => BasicValue.FromNumber(Math.Cos(args[0].AsNumber()))),
            "EXP" => RequireArgs(name, args, 1, () => BasicValue.FromNumber(Math.Exp(args[0].AsNumber()))),
            "INT" => RequireArgs(name, args, 1, () => BasicValue.FromNumber(Math.Floor(args[0].AsNumber()))),
            "LOG" => RequireArgs(name, args, 1, () => BasicValue.FromNumber(Math.Log(args[0].AsNumber()))),
            "RND" => InvokeRnd(args, runtime),
            "SGN" => RequireArgs(name, args, 1, () => BasicValue.FromNumber(Math.Sign(args[0].AsNumber()))),
            "SIN" => RequireArgs(name, args, 1, () => BasicValue.FromNumber(Math.Sin(args[0].AsNumber()))),
            "SQR" => RequireArgs(name, args, 1, () => BasicValue.FromNumber(Math.Sqrt(args[0].AsNumber()))),
            "TAN" => RequireArgs(name, args, 1, () => BasicValue.FromNumber(Math.Tan(args[0].AsNumber()))),
            "GET" => RequireArgs(name, args, 0, () => BasicValue.FromNumber(runtime.ReadKeyCode())),
            "LEN" => RequireArgs(name, args, 1, () => BasicValue.FromNumber(args[0].AsString().Length)),
            "LEFT$" => InvokeLeft(args),
            "RIGHT$" => InvokeRight(args),
            "MID$" => InvokeMid(args),
            "CHR$" => RequireArgs(name, args, 1, () => BasicValue.FromString(((char)args[0].AsInt32()).ToString())),
            "ASC" => RequireArgs(name, args, 1, () => InvokeAsc(args[0])),
            "STR$" => RequireArgs(name, args, 1, () => BasicValue.FromString(FormatStr(args[0]))),
            "VAL" => RequireArgs(name, args, 1, () => BasicValue.FromNumber(ParseVal(args[0].AsString()))),
            "TAB" => RequireArgs(name, args, 1, () => BasicValue.FromString(CreateSpaces(args[0]))),
            "SPC" => RequireArgs(name, args, 1, () => BasicValue.FromString(CreateSpaces(args[0]))),
            _ => throw new BasicRuntimeException($"Unknown function {name}")
        };
    }

    private static BasicValue RequireArgs(string name, IReadOnlyList<BasicValue> args, int count, Func<BasicValue> evaluator)
    {
        if (args.Count != count)
        {
            throw new BasicRuntimeException($"{name} expects {count} argument(s)");
        }

        return evaluator();
    }

    private static BasicValue InvokeRnd(IReadOnlyList<BasicValue> args, BasicRuntime runtime)
    {
        var seedValue = args.Count == 0 ? 1.0 : args[0].AsNumber();
        if (seedValue < 0)
        {
            runtime.SeedRandom((int)Math.Abs(seedValue));
        }

        return BasicValue.FromNumber(runtime.NextRandom());
    }

    private static BasicValue InvokeAsc(BasicValue value)
    {
        if (value.IsString)
        {
            var textValue = value.AsString();
            return BasicValue.FromNumber(textValue.Length > 0 ? textValue[0] : 0);
        }

        return BasicValue.FromNumber(value.AsInt32());
    }

    private static BasicValue InvokeLeft(IReadOnlyList<BasicValue> args)
    {
        if (args.Count != 2)
        {
            throw new BasicRuntimeException("LEFT$ expects 2 arguments");
        }

        var text = args[0].AsString();
        var length = Math.Max(0, args[1].AsInt32());
        length = Math.Min(length, text.Length);
        return BasicValue.FromString(text[..length]);
    }

    private static BasicValue InvokeRight(IReadOnlyList<BasicValue> args)
    {
        if (args.Count != 2)
        {
            throw new BasicRuntimeException("RIGHT$ expects 2 arguments");
        }

        var text = args[0].AsString();
        var length = Math.Max(0, args[1].AsInt32());
        length = Math.Min(length, text.Length);
        if (length == 0)
        {
            return BasicValue.EmptyString;
        }

        return BasicValue.FromString(text.Substring(text.Length - length));
    }

    private static BasicValue InvokeMid(IReadOnlyList<BasicValue> args)
    {
        if (args.Count is < 2 or > 3)
        {
            throw new BasicRuntimeException("MID$ expects 2 or 3 arguments");
        }

        var text = args[0].AsString();
        var start = Math.Max(1, args[1].AsInt32()) - 1;
        var length = args.Count == 3 ? args[2].AsInt32() : text.Length - start;
        if (start >= text.Length)
        {
            return BasicValue.FromString(string.Empty);
        }

        length = Math.Clamp(length, 0, text.Length - start);
        return BasicValue.FromString(text.Substring(start, length));
    }

    private static string FormatStr(BasicValue value)
    {
        if (value.IsString)
        {
            return value.AsString();
        }

        var number = value.AsNumber();
        var formatted = number.ToString("0.############", CultureInfo.InvariantCulture);
        return number >= 0 ? " " + formatted : formatted;
    }

    private static double ParseVal(string text)
    {
        var trimmed = text.TrimStart();
        var builder = new List<char>();
        foreach (var ch in trimmed)
        {
            if (char.IsDigit(ch) || ch is '+' or '-' || ch == '.' || ch is 'E' or 'D')
            {
                builder.Add(ch == 'D' ? 'E' : ch);
            }
            else
            {
                break;
            }
        }

        if (builder.Count == 0)
        {
            return 0d;
        }

        var numeric = new string(builder.ToArray());
        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;
    }

    private static string CreateSpaces(BasicValue value)
    {
        var count = Math.Max(0, value.AsInt32());
        return new string(' ', count);
    }
}
