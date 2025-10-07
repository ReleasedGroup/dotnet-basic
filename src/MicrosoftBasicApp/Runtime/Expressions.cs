using System.Linq;

namespace MicrosoftBasicApp.Runtime;

internal abstract class Expression
{
    public abstract BasicValue Evaluate(BasicRuntime runtime);
}

internal sealed class LiteralExpression : Expression
{
    private readonly BasicValue _value;

    public LiteralExpression(BasicValue value)
    {
        _value = value;
    }

    public override BasicValue Evaluate(BasicRuntime runtime) => _value;
}

internal sealed class VariableExpression : Expression
{
    private readonly string _name;

    public VariableExpression(string name)
    {
        _name = name;
    }

    public override BasicValue Evaluate(BasicRuntime runtime) => runtime.GetVariable(_name);
}

internal sealed class ArrayReferenceExpression : Expression
{
    private readonly string _name;
    private readonly IReadOnlyList<Expression> _indices;

    public ArrayReferenceExpression(string name, IReadOnlyList<Expression> indices)
    {
        _name = name;
        _indices = indices;
    }

    public override BasicValue Evaluate(BasicRuntime runtime) => runtime.GetArrayValue(_name, _indices);
}

internal sealed class FunctionCallExpression : Expression
{
    private readonly string _name;
    private readonly IReadOnlyList<Expression> _arguments;

    public FunctionCallExpression(string name, IReadOnlyList<Expression> arguments)
    {
        _name = name;
        _arguments = arguments;
    }

    public override BasicValue Evaluate(BasicRuntime runtime)
    {
        var values = _arguments.Select(arg => arg.Evaluate(runtime)).ToArray();
        if (BuiltInFunctions.IsBuiltIn(_name))
        {
            return BuiltInFunctions.Invoke(_name, values, runtime);
        }

        if (runtime.TryInvokeFunction(_name, values, out var result))
        {
            return result;
        }

        throw new BasicRuntimeException($"Unknown function {_name}");
    }
}

internal sealed class UnaryExpression : Expression
{
    private readonly string _operator;
    private readonly Expression _operand;

    public UnaryExpression(string op, Expression operand)
    {
        _operator = op;
        _operand = operand;
    }

    public override BasicValue Evaluate(BasicRuntime runtime)
    {
        var value = _operand.Evaluate(runtime);
        return _operator switch
        {
            "-" => BasicValue.Negate(value),
            "NOT" => BasicValue.Not(value),
            "+" => value,
            _ => throw new BasicRuntimeException($"Unsupported unary operator '{_operator}'")
        };
    }
}

internal sealed class BinaryExpression : Expression
{
    private readonly Expression _left;
    private readonly Expression _right;
    private readonly string _operator;

    public BinaryExpression(Expression left, Expression right, string op)
    {
        _left = left;
        _right = right;
        _operator = op;
    }

    public override BasicValue Evaluate(BasicRuntime runtime)
    {
        var left = _left.Evaluate(runtime);
        var right = _right.Evaluate(runtime);
        return _operator switch
        {
            "+" => BasicValue.Add(left, right),
            "-" => BasicValue.Subtract(left, right),
            "*" => BasicValue.Multiply(left, right),
            "/" => BasicValue.Divide(left, right),
            "^" => BasicValue.Power(left, right),
            "=" or "<>" or "<" or "<=" or ">" or ">=" => BasicValue.Compare(left, right, _operator),
            "AND" => BasicValue.And(left, right),
            "OR" => BasicValue.Or(left, right),
            _ => throw new BasicRuntimeException($"Unsupported operator '{_operator}'")
        };
    }
}
