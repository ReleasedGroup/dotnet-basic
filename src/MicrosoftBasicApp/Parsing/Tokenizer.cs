using System.Globalization;
using System.Linq;
using System.Text;

using MicrosoftBasicApp.Runtime;

namespace MicrosoftBasicApp.Parsing;

internal sealed class Tokenizer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "PRINT","IF","THEN","ELSE","GOTO","GOSUB","RETURN","FOR","TO","STEP","NEXT","LET",
        "DIM","INPUT","CLEAR","END","STOP","REM","NEW","RUN","AND","OR","NOT","DATA","READ","RESTORE","RANDOMIZE",
        "DEF","ON","OPEN","CLOSE","AS","OUTPUT","APPEND"
    };

    private static readonly string[] OrderedKeywords = Keywords.OrderByDescending(k => k.Length).ToArray();
    private static readonly HashSet<string> KeywordsAllowingAdjacency = new(StringComparer.Ordinal)
    {
        "FOR","NEXT","TO","STEP","THEN","GOTO","GOSUB","IF","ON","DIM","DATA","READ","RESTORE",
        "RANDOMIZE","RETURN","END","STOP","LET","ELSE","PRINT","INPUT","RUN","NEW","CLEAR","REM","DEF","OR","AND","NOT"
    };
    private static readonly string[] EmbeddedKeywords = { "THEN", "GOTO", "GOSUB" };

    private readonly string _source;
    private readonly List<Token> _tokens = new();
    private int _position;

    public Tokenizer(string source)
    {
        _source = source;
    }

    public IReadOnlyList<Token> Tokenize()
    {
        while (!IsAtEnd())
        {
            var c = Peek();

            if (char.IsWhiteSpace(c))
            {
                Advance();
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && char.IsDigit(Peek(1))))
            {
                _tokens.Add(ReadNumber());
                continue;
            }

            if (c == '"')
            {
                _tokens.Add(ReadString());
                continue;
            }

            if (c == '?')
            {
                Advance();
                _tokens.Add(new Token(TokenKind.Keyword, "PRINT"));
                continue;
            }

            if (c == '\'')
            {
                Advance();
                _tokens.Add(new Token(TokenKind.Keyword, "REM"));
                SkipToLineEnd();
                break;
            }

            if (char.IsLetter(c) || c == '_')
            {
                if (TryReadKeyword(out var keyword))
                {
                    _tokens.Add(new Token(TokenKind.Keyword, keyword));
                    if (keyword == "REM")
                    {
                        SkipToLineEnd();
                        break;
                    }

                    continue;
                }

                var word = ReadWord();
                EmitWordTokens(word);
                continue;
            }

            if (c is ':' or ';' or ',' or '(' or ')' or '#')
            {
                Advance();
                _tokens.Add(new Token(TokenKind.Separator, c.ToString()));
                continue;
            }

            if (c is '+' or '-' or '*' or '/' or '^')
            {
                Advance();
                _tokens.Add(new Token(TokenKind.Operator, c.ToString()));
                continue;
            }

            if (c == '=')
            {
                Advance();
                _tokens.Add(new Token(TokenKind.Operator, "="));
                continue;
            }

            if (c is '<' or '>')
            {
                _tokens.Add(ReadComparisonOperator());
                continue;
            }

            throw new BasicSyntaxException($"Unexpected character '{c}'");
        }

        _tokens.Add(Token.EndToken);
        return _tokens;
    }

    private bool IsAtEnd() => _position >= _source.Length;

    private char Peek(int offset = 0)
    {
        var index = _position + offset;
        return index >= 0 && index < _source.Length ? _source[index] : '\0';
    }

    private char Advance() => _source[_position++];

    private Token ReadNumber()
    {
        var start = _position;

        while (char.IsDigit(Peek()))
        {
            Advance();
        }

        if (Peek() == '.')
        {
            Advance();
            while (char.IsDigit(Peek()))
            {
                Advance();
            }
        }

        if (Peek() is 'E' or 'D')
        {
            Advance();
            if (Peek() is '+' or '-')
            {
                Advance();
            }

            while (char.IsDigit(Peek()))
            {
                Advance();
            }
        }

        var text = _source[start.._position];
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new BasicSyntaxException($"Invalid numeric literal '{text}'");
        }

        return new Token(TokenKind.Number, text.ToUpperInvariant(), value);
    }

    private Token ReadString()
    {
        Advance();
        var builder = new StringBuilder();

        while (!IsAtEnd())
        {
            var c = Advance();
            if (c == '"')
            {
                if (Peek() == '"')
                {
                    Advance();
                    builder.Append('"');
                    continue;
                }

                break;
            }

            builder.Append(c);
        }

        return new Token(TokenKind.String, builder.ToString());
    }

    private bool TryReadKeyword(out string keyword)
    {
        foreach (var candidate in OrderedKeywords)
        {
            if (MatchesKeyword(candidate))
            {
                keyword = candidate;
                _position += candidate.Length;
                return true;
            }
        }

        keyword = string.Empty;
        return false;
    }

    private bool MatchesKeyword(string keyword)
    {
        if (_position + keyword.Length > _source.Length)
        {
            return false;
        }

        if (!MemoryExtensions.Equals(_source.AsSpan(_position, keyword.Length), keyword.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var next = Peek(keyword.Length);
        if (next == '\0')
        {
            return true;
        }

        if (char.IsLetterOrDigit(next) || next == '_' || next == '$')
        {
            return KeywordsAllowingAdjacency.Contains(keyword);
        }

        return true;
    }

    private void EmitWordTokens(string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return;
        }

        if (Keywords.Contains(word))
        {
            _tokens.Add(new Token(TokenKind.Keyword, word));
            return;
        }

        foreach (var keyword in EmbeddedKeywords)
        {
            var index = word.IndexOf(keyword, StringComparison.Ordinal);
            if (index > 0 && index + keyword.Length <= word.Length)
            {
                var prefix = word[..index];
                if (prefix.Length > 0)
                {
                    EmitWordTokens(prefix);
                }

                _tokens.Add(new Token(TokenKind.Keyword, keyword));

                var suffix = word[(index + keyword.Length)..];
                if (suffix.Length > 0)
                {
                    EmitWordTokens(suffix);
                }

                return;
            }
        }

        if (double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericValue))
        {
            _tokens.Add(new Token(TokenKind.Number, word, numericValue));
        }
        else
        {
            _tokens.Add(new Token(TokenKind.Identifier, word));
        }
    }

    private string ReadWord()
    {
        var start = _position;
        while (char.IsLetterOrDigit(Peek()) || Peek() is '_' or '$')
        {
            Advance();
        }

        return _source[start.._position].ToUpperInvariant();
    }

    private Token ReadComparisonOperator()
    {
        var first = Advance();
        var second = Peek();
        if (second == '=')
        {
            Advance();
            return new Token(TokenKind.Operator, string.Concat(first, '='));
        }

        if (first == '<' && second == '>')
        {
            Advance();
            return new Token(TokenKind.Operator, "<>");
        }

        return new Token(TokenKind.Operator, first.ToString());
    }

    private void SkipToLineEnd()
    {
        _position = _source.Length;
    }
}
