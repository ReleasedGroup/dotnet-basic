namespace MicrosoftBasicApp.Runtime;

public class BasicException : Exception
{
    public BasicException(string message) : base(message)
    {
    }
}

public sealed class BasicSyntaxException : BasicException
{
    public BasicSyntaxException(string message) : base(message)
    {
    }
}

public sealed class BasicRuntimeException : BasicException
{
    public BasicRuntimeException(string message) : base(message)
    {
    }
}
