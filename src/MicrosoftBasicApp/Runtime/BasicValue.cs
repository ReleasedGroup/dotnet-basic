using System.Globalization;

namespace MicrosoftBasicApp.Runtime;

public readonly struct BasicValue
{
    private readonly double? _number;
    private readonly string? _string;

    private BasicValue(double number)
    {
        _number = number;
        _string = null;
    }

    private BasicValue(string value)
    {
        _number = null;
        _string = value;
    }

    public bool IsString => _string is not null;

    public double AsNumber()
    {
        if (_number.HasValue)
        {
            return _number.Value;
        }

        if (_string is null)
        {
            return 0d;
        }

        if (double.TryParse(_string, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return 0d;
    }

    public int AsInt32() => (int)Math.Round(AsNumber());

    public string AsString()
    {
        if (_string is not null)
        {
            return _string;
        }

        if (_number is null)
        {
            return string.Empty;
        }

        var value = _number.Value;
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        return value.ToString("0.###############", CultureInfo.InvariantCulture);
    }

    public bool AsBoolean() => Math.Abs(AsNumber()) > double.Epsilon;

    public BasicValue EnsureString()
    {
        if (IsString)
        {
            return this;
        }

        return FromString(AsString());
    }

    public BasicValue EnsureNumber()
    {
        if (!IsString)
        {
            return this;
        }

        return FromNumber(AsNumber());
    }

    public override string ToString() => AsString();

    public static BasicValue FromNumber(double value) => new(value);

    public static BasicValue FromString(string value) => new(value);

    public static BasicValue FromBoolean(bool value) => new(value ? -1d : 0d);

    public static BasicValue Zero => new(0d);

    public static BasicValue EmptyString => new(string.Empty);

    public static BasicValue Add(BasicValue left, BasicValue right)
    {
        if (left.IsString || right.IsString)
        {
            return FromString(left.AsString() + right.AsString());
        }

        return FromNumber(left.AsNumber() + right.AsNumber());
    }

    public static BasicValue Subtract(BasicValue left, BasicValue right) => FromNumber(left.AsNumber() - right.AsNumber());

    public static BasicValue Multiply(BasicValue left, BasicValue right) => FromNumber(left.AsNumber() * right.AsNumber());

    public static BasicValue Divide(BasicValue left, BasicValue right)
    {
        var divisor = right.AsNumber();
        if (Math.Abs(divisor) < double.Epsilon)
        {
            throw new BasicRuntimeException("Division by zero");
        }

        return FromNumber(left.AsNumber() / divisor);
    }

    public static BasicValue Power(BasicValue left, BasicValue right) => FromNumber(Math.Pow(left.AsNumber(), right.AsNumber()));

    public static BasicValue Negate(BasicValue value) => FromNumber(-value.AsNumber());

    public static BasicValue Compare(BasicValue left, BasicValue right, string op)
    {
        if (left.IsString || right.IsString)
        {
            var l = left.AsString();
            var r = right.AsString();
            return op switch
            {
                "=" => FromBoolean(string.Equals(l, r, StringComparison.Ordinal)),
                "<>" => FromBoolean(!string.Equals(l, r, StringComparison.Ordinal)),
                "<" => FromBoolean(string.CompareOrdinal(l, r) < 0),
                "<=" => FromBoolean(string.CompareOrdinal(l, r) <= 0),
                ">" => FromBoolean(string.CompareOrdinal(l, r) > 0),
                ">=" => FromBoolean(string.CompareOrdinal(l, r) >= 0),
                _ => throw new BasicRuntimeException($"Unsupported comparison operator '{op}'")
            };
        }
        else
        {
            var l = left.AsNumber();
            var r = right.AsNumber();
            return op switch
            {
                "=" => FromBoolean(Math.Abs(l - r) < double.Epsilon),
                "<>" => FromBoolean(Math.Abs(l - r) >= double.Epsilon),
                "<" => FromBoolean(l < r),
                "<=" => FromBoolean(l <= r),
                ">" => FromBoolean(l > r),
                ">=" => FromBoolean(l >= r),
                _ => throw new BasicRuntimeException($"Unsupported comparison operator '{op}'")
            };
        }
    }

    public static BasicValue And(BasicValue left, BasicValue right)
    {
        var l = (int)Math.Round(left.AsNumber());
        var r = (int)Math.Round(right.AsNumber());
        return FromNumber(l & r);
    }

    public static BasicValue Or(BasicValue left, BasicValue right)
    {
        var l = (int)Math.Round(left.AsNumber());
        var r = (int)Math.Round(right.AsNumber());
        return FromNumber(l | r);
    }

    public static BasicValue Not(BasicValue value)
    {
        var v = (int)Math.Round(value.AsNumber());
        return FromNumber(~v);
    }

    public string ToPrintString()
    {
        if (IsString)
        {
            return AsString();
        }

        var value = _number ?? 0d;
        if (Math.Abs(value) >= 1e10 || (Math.Abs(value) > 0d && Math.Abs(value) < 1e-3))
        {
            return value.ToString("0.#########E+0", CultureInfo.InvariantCulture);
        }

        return value.ToString("0.############", CultureInfo.InvariantCulture);
    }
}
