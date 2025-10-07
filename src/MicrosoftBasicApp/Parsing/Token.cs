namespace MicrosoftBasicApp.Parsing;

internal enum TokenKind
{
    Number,
    String,
    Identifier,
    Keyword,
    Operator,
    Separator,
    End
}

internal sealed class Token
{
    public Token(TokenKind kind, string text, double? numberValue = null)
    {
        Kind = kind;
        Text = text;
        NumberValue = numberValue;
    }

    public TokenKind Kind { get; }
    public string Text { get; }
    public double? NumberValue { get; }

    public override string ToString() => $"{Kind}:{Text}";

    public static Token EndToken { get; } = new(TokenKind.End, string.Empty);
}
