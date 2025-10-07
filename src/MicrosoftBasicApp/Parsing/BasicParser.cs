using MicrosoftBasicApp.Runtime;

namespace MicrosoftBasicApp.Parsing;

internal sealed class BasicParser
{
    private readonly List<Token> _tokens;
    private readonly HashSet<string> _userFunctions;
    private int _position;

    private BasicParser(List<Token> tokens, HashSet<string> userFunctions)
    {
        _tokens = tokens;
        _userFunctions = userFunctions;
    }

    public BasicParser()
    {
        _tokens = new List<Token>();
        _userFunctions = new HashSet<string>(StringComparer.Ordinal);
    }

    public List<BasicStatement> ParseLine(string source)
    {
        var tokenizer = new Tokenizer(source);
        var tokens = tokenizer.Tokenize();
        var parser = new BasicParser(new List<Token>(tokens), _userFunctions);
        return parser.ParseStatements();
    }

    public List<BasicStatement> ParseTokens(List<Token> tokens)
    {
        var parser = new BasicParser(tokens, _userFunctions);
        return parser.ParseStatements();
    }

    private List<BasicStatement> ParseStatements()
    {
        var statements = new List<BasicStatement>();

        while (!Match(TokenKind.End))
        {
            if (Check(TokenKind.Separator, ":"))
            {
                Advance();
                continue;
            }

            statements.Add(ParseStatement());

            if (Match(TokenKind.Separator, ":"))
            {
                continue;
            }

            if (Match(TokenKind.End))
            {
                break;
            }
        }

        return statements;
    }

    private BasicStatement ParseStatement()
    {
        if (MatchKeyword("REM"))
        {
            // Ignore rest of line
            while (!Check(TokenKind.End))
            {
                Advance();
            }

            return new RemStatement();
        }

        if (MatchKeyword("PRINT"))
        {
            return ParsePrint();
        }

        if (MatchKeyword("INPUT"))
        {
            return ParseInput();
        }

        if (MatchKeyword("READ"))
        {
            return ParseRead();
        }

        if (MatchKeyword("DATA"))
        {
            return ParseData();
        }

        if (MatchKeyword("IF"))
        {
            return ParseIf();
        }

        if (MatchKeyword("ON"))
        {
            return ParseOn();
        }

        if (MatchKeyword("FOR"))
        {
            return ParseFor();
        }

        if (MatchKeyword("NEXT"))
        {
            return ParseNext();
        }

        if (MatchKeyword("GOTO"))
        {
            return new GotoStatement(ParseExpression());
        }

        if (MatchKeyword("GOSUB"))
        {
            return new GosubStatement(ParseExpression());
        }

        if (MatchKeyword("RETURN"))
        {
            return new ReturnStatement();
        }

        if (MatchKeyword("END"))
        {
            return new EndStatement();
        }

        if (MatchKeyword("STOP"))
        {
            return new StopStatement();
        }

        if (MatchKeyword("RESTORE"))
        {
            return ParseRestore();
        }

        if (MatchKeyword("RANDOMIZE"))
        {
            return ParseRandomize();
        }

        if (MatchKeyword("DIM"))
        {
            return ParseDim();
        }

        if (MatchKeyword("OPEN"))
        {
            return ParseOpen();
        }

        if (MatchKeyword("CLOSE"))
        {
            return ParseClose();
        }

        if (MatchKeyword("CLEAR"))
        {
            return new ClearStatement();
        }

        if (MatchKeyword("DEF"))
        {
            return ParseDef();
        }

        if (MatchKeyword("LET"))
        {
            return ParseAssignment();
        }

        if (IsAssignmentStart())
        {
            return ParseAssignment();
        }

        throw new BasicSyntaxException($"Unexpected token '{Peek()?.Text}'");
    }

    private BasicStatement ParseAssignment()
    {
        var name = ConsumeIdentifier("Expected variable name");
        var indices = ParseOptionalIndices();
        Consume(TokenKind.Operator, "=", "Expected '=' in assignment");
        var expression = ParseExpression();
        return new AssignmentStatement(new VariableTarget(name, indices), expression);
    }

    private BasicStatement ParsePrint()
    {
        Expression? fileNumber = null;
        if (MatchSeparator("#"))
        {
            fileNumber = ParseExpression();
            if (!MatchSeparator(","))
            {
                throw new BasicSyntaxException("Expected ',' after file number");
            }
        }

        var items = new List<PrintItem>();

        if (IsEndOfStatement())
        {
            return new PrintStatement(items, fileNumber);
        }

        while (true)
        {
            if (IsStartOfStatement(Peek()))
            {
                break;
            }

            if (MatchSeparator(","))
            {
                items.Add(PrintItem.Comma);
                continue;
            }

            if (MatchSeparator(";"))
            {
                items.Add(PrintItem.Semicolon);
                continue;
            }

            if (IsEndOfStatement())
            {
                break;
            }

            items.Add(PrintItem.FromExpression(ParseExpression()));

            if (MatchSeparator(","))
            {
                items.Add(PrintItem.Comma);
                continue;
            }

            if (MatchSeparator(";"))
            {
                items.Add(PrintItem.Semicolon);
                continue;
            }

            if (IsEndOfStatement())
            {
                break;
            }
        }

        return new PrintStatement(items, fileNumber);
    }

    private BasicStatement ParseInput()
    {
        Expression? prompt = null;
        if (Check(TokenKind.String))
        {
            prompt = ParseExpression();
            if (!MatchSeparator(";"))
            {
                throw new BasicSyntaxException("Expected ';' after INPUT prompt");
            }
        }

        Expression? fileNumber = null;
        if (MatchSeparator("#"))
        {
            fileNumber = ParseExpression();
            if (!MatchSeparator(","))
            {
                throw new BasicSyntaxException("Expected ',' after file number");
            }
        }

        var variables = new List<VariableTarget>();
        variables.Add(ParseVariableTarget());

        while (MatchSeparator(","))
        {
            variables.Add(ParseVariableTarget());
        }

        return new InputStatement(prompt, variables, fileNumber);
    }

    private BasicStatement ParseRead()
    {
        var variables = new List<VariableTarget> { ParseVariableTarget() };
        while (MatchSeparator(","))
        {
            variables.Add(ParseVariableTarget());
        }

        return new ReadStatement(variables);
    }

    private BasicStatement ParseData()
    {
        var values = new List<BasicValue>();
        if (!IsEndOfStatement())
        {
            values.Add(ParseDataValue());
            while (MatchSeparator(","))
            {
                values.Add(ParseDataValue());
            }
        }

        return new DataStatement(values);
    }

    private BasicValue ParseDataValue()
    {
        if (Check(TokenKind.String))
        {
            var token = Advance();
            return BasicValue.FromString(token.Text);
        }

        var sign = 1d;
        if (Match(TokenKind.Operator, "-"))
        {
            sign = -1d;
        }
        else if (Match(TokenKind.Operator, "+"))
        {
            sign = 1d;
        }

        if (!Check(TokenKind.Number))
        {
            throw new BasicSyntaxException("Expected numeric or string literal in DATA");
        }

        var number = Advance().NumberValue ?? 0d;
        return BasicValue.FromNumber(sign * number);
    }

    private VariableTarget ParseVariableTarget()
    {
        var name = ConsumeIdentifier("Expected variable name in INPUT");
        var indices = ParseOptionalIndices();
        return new VariableTarget(name, indices);
    }

    private IReadOnlyList<Expression> ParseExpressionList()
    {
        var list = new List<Expression> { ParseExpression() };
        while (MatchSeparator(","))
        {
            list.Add(ParseExpression());
        }

        return list;
    }

    private BasicStatement ParseIf()
    {
        var condition = ParseExpression();
        ConsumeKeyword("THEN", "Expected THEN after IF condition");

        var thenTokens = new List<Token>();
        var elseTokens = new List<Token>();
        var collectingElse = false;

        while (!Check(TokenKind.End))
        {
            var token = Advance();
            if (token.Kind == TokenKind.Keyword && token.Text == "ELSE")
            {
                collectingElse = true;
                continue;
            }

            if (token.Kind == TokenKind.Separator && token.Text == ":")
            {
                if (collectingElse)
                {
                    elseTokens.Add(token);
                }
                else
                {
                    thenTokens.Add(token);
                }

                continue;
            }

            if (collectingElse)
            {
                elseTokens.Add(token);
            }
            else
            {
                thenTokens.Add(token);
            }
        }

        var thenStatement = ParseConditionalBranch(thenTokens, "THEN");
        var elseStatement = elseTokens.Count > 0 ? ParseConditionalBranch(elseTokens, "ELSE") : null;

        return new IfStatement(condition, thenStatement, elseStatement);
    }

    private BasicStatement ParseConditionalBranch(List<Token> tokens, string clauseName)
    {
        if (tokens.Count == 0)
        {
            throw new BasicSyntaxException($"Expected statement after {clauseName}");
        }

        if (tokens.Count == 1 && tokens[0].Kind == TokenKind.Number)
        {
            var number = tokens[0].NumberValue ?? 0;
            return new GotoStatement(new LiteralExpression(BasicValue.FromNumber(number)));
        }

        var buffer = new List<Token>(tokens) { Token.EndToken };
        var parser = new BasicParser(buffer, _userFunctions);
        var statements = parser.ParseStatements();
        if (statements.Count == 0)
        {
            throw new BasicSyntaxException($"{clauseName} clause must contain a statement");
        }

        return statements.Count == 1 ? statements[0] : new BlockStatement(statements);
    }

    private BasicStatement ParseFor()
    {
        var name = ConsumeIdentifier("Expected variable name after FOR");
        if (name.EndsWith("$", StringComparison.Ordinal))
        {
            throw new BasicSyntaxException("FOR variable must be numeric");
        }

        Consume(TokenKind.Operator, "=", "Expected '=' after FOR variable");
        var start = ParseExpression();
        ConsumeKeyword("TO", "Expected TO in FOR statement");
        var limit = ParseExpression();
        Expression step;
        if (MatchKeyword("STEP"))
        {
            step = ParseExpression();
        }
        else
        {
            step = new LiteralExpression(BasicValue.FromNumber(1));
        }

        return new ForStatement(name, start, limit, step);
    }

    private BasicStatement ParseNext()
    {
        string? name = null;
        if (Check(TokenKind.Identifier))
        {
            name = Advance().Text;
        }

        return new NextStatement(name);
    }

    private BasicStatement ParseOn()
    {
        var selector = ParseExpression();
        if (MatchKeyword("GOTO"))
        {
            var targets = ParseExpressionList();
            return new OnGotoStatement(selector, targets);
        }

        if (MatchKeyword("GOSUB"))
        {
            var targets = ParseExpressionList();
            return new OnGosubStatement(selector, targets);
        }

        throw new BasicSyntaxException("Expected GOTO or GOSUB after ON");
    }

    private BasicStatement ParseDim()
    {
        var dimensions = new List<DimEntry>();
        while (true)
        {
            var name = ConsumeIdentifier("Expected array name in DIM");
            var indices = ParseRequiredIndices();
            dimensions.Add(new DimEntry(name, indices));
            if (!MatchSeparator(","))
            {
                break;
            }
        }

        return new DimStatement(dimensions);
    }

    private BasicStatement ParseRestore()
    {
        Expression? line = null;
        if (!IsEndOfStatement())
        {
            line = ParseExpression();
        }

        return new RestoreStatement(line);
    }

    private BasicStatement ParseRandomize()
    {
        Expression? seed = null;
        if (!IsEndOfStatement())
        {
            seed = ParseExpression();
        }

        return new RandomizeStatement(seed);
    }

    private BasicStatement ParseOpen()
    {
        var path = ParseExpression();
        ConsumeKeyword("FOR", "Expected FOR in OPEN statement");

        if (!Check(TokenKind.Keyword))
        {
            throw new BasicSyntaxException("Expected mode after FOR");
        }

        var modeToken = Advance();
        var mode = modeToken.Text switch
        {
            "INPUT" => FileOpenMode.Input,
            "OUTPUT" => FileOpenMode.Output,
            "APPEND" => FileOpenMode.Append,
            _ => throw new BasicSyntaxException($"Unsupported file mode '{modeToken.Text}'")
        };

        ConsumeKeyword("AS", "Expected AS in OPEN statement");
        if (!MatchSeparator("#"))
        {
            throw new BasicSyntaxException("Expected '#' after AS");
        }

        var channel = ParseExpression();
        return new OpenStatement(path, mode, channel);
    }

    private BasicStatement ParseClose()
    {
        var channels = new List<Expression>();
        if (IsEndOfStatement())
        {
            return new CloseStatement(channels);
        }

        while (true)
        {
            if (MatchSeparator("#"))
            {
                channels.Add(ParseExpression());
            }
            else
            {
                channels.Add(ParseExpression());
            }

            if (!MatchSeparator(","))
            {
                break;
            }
        }

        return new CloseStatement(channels);
    }

    private BasicStatement ParseDef()
    {
        var name = ConsumeIdentifier("Expected function name after DEF");
        ConsumeSeparator("(", "Expected '(' in function definition");
        var parameters = new List<string>();
        if (!Check(TokenKind.Separator, ")"))
        {
            parameters.Add(ConsumeIdentifier("Expected parameter name"));
            while (MatchSeparator(","))
            {
                parameters.Add(ConsumeIdentifier("Expected parameter name"));
            }
        }

        ConsumeSeparator(")", "Expected ')' in function definition");
        Consume(TokenKind.Operator, "=", "Expected '=' in function definition");
        var body = ParseExpression();
        _userFunctions.Add(name);
        return new DefineFunctionStatement(name, parameters, body);
    }

    private IReadOnlyList<Expression> ParseRequiredIndices()
    {
        ConsumeSeparator("(", "Expected '(' for array dimensions");
        var list = new List<Expression> { ParseExpression() };
        while (MatchSeparator(","))
        {
            list.Add(ParseExpression());
        }

        ConsumeSeparator(")", "Expected ')' to close array dimensions");
        return list;
    }

    private IReadOnlyList<Expression>? ParseOptionalIndices()
    {
        if (!MatchSeparator("("))
        {
            return null;
        }

        var list = new List<Expression> { ParseExpression() };
        while (MatchSeparator(","))
        {
            list.Add(ParseExpression());
        }

        ConsumeSeparator(")", "Expected ')' to close indices");
        return list;
    }

    private Expression ParseExpression(int precedence = 0)
    {
        var left = ParsePrefix();

        while (true)
        {
            var token = Peek();
            var nextPrecedence = GetPrecedence(token);
            if (nextPrecedence < precedence)
            {
                break;
            }

            if (token is null)
            {
                break;
            }

            Advance();
            left = ParseInfix(left, token, nextPrecedence);
        }

        return left;
    }

    private Expression ParsePrefix()
    {
        var token = Advance();
        switch (token.Kind)
        {
            case TokenKind.Number:
                return new LiteralExpression(BasicValue.FromNumber(token.NumberValue ?? 0));
            case TokenKind.String:
                return new LiteralExpression(BasicValue.FromString(token.Text));
            case TokenKind.Identifier:
                return ParseIdentifierExpression(token.Text);
            case TokenKind.Operator when token.Text == "+":
                return ParseExpression(7);
            case TokenKind.Operator when token.Text == "-":
                return new UnaryExpression("-", ParseExpression(7));
            case TokenKind.Keyword when token.Text == "NOT":
                return new UnaryExpression("NOT", ParseExpression(6));
            case TokenKind.Separator when token.Text == "(":
                var expression = ParseExpression();
                ConsumeSeparator(")", "Expected ')' after expression");
                return expression;
            default:
                throw new BasicSyntaxException($"Unexpected token '{token.Text}' in expression");
        }
    }

    private Expression ParseIdentifierExpression(string name)
    {
        if (MatchSeparator("("))
        {
            var args = new List<Expression>();
            if (!Check(TokenKind.Separator, ")"))
            {
                args.Add(ParseExpression());
                while (MatchSeparator(","))
                {
                    args.Add(ParseExpression());
                }
            }

            ConsumeSeparator(")", "Expected ')' after arguments");
            if (BuiltInFunctions.IsBuiltIn(name) || _userFunctions.Contains(name))
            {
                return new FunctionCallExpression(name, args);
            }

            return new ArrayReferenceExpression(name, args);
        }

        if (BuiltInFunctions.IsBuiltIn(name) && AllowsImplicitCall(name))
        {
            return new FunctionCallExpression(name, Array.Empty<Expression>());
        }

        return new VariableExpression(name);
    }

    private Expression ParseInfix(Expression left, Token token, int precedence)
    {
        return token.Kind switch
        {
            TokenKind.Operator => token.Text switch
            {
                "+" or "-" or "*" or "/" or "^" or "=" or "<>" or "<" or "<=" or ">" or ">=" => new BinaryExpression(left, ParseExpression(precedence + (token.Text == "^" ? 0 : 1)), token.Text),
                _ => throw new BasicSyntaxException($"Unsupported operator '{token.Text}'")
            },
            TokenKind.Keyword when token.Text is "AND" or "OR" => new BinaryExpression(left, ParseExpression(precedence + 1), token.Text),
            _ => throw new BasicSyntaxException($"Unexpected token '{token.Text}' after expression")
        };
    }

    private int GetPrecedence(Token? token)
    {
        if (token is null)
        {
            return -1;
        }

        return token.Kind switch
        {
            TokenKind.Operator => token.Text switch
            {
                "^" => 7,
                "*" or "/" => 6,
                "+" or "-" => 5,
                "=" or "<>" or "<" or "<=" or ">" or ">=" => 4,
                _ => -1
            },
            TokenKind.Keyword when token.Text == "AND" => 3,
            TokenKind.Keyword when token.Text == "OR" => 2,
            _ => -1
        };
    }

    private static bool AllowsImplicitCall(string name) => name is "RND" or "GET";

    private bool IsEndOfStatement() => Check(TokenKind.End) || Check(TokenKind.Separator, ":");

    private static bool IsStartOfStatement(Token? token)
    {
        if (token is null || token.Kind != TokenKind.Keyword)
        {
            return false;
        }

        return token.Text is "PRINT" or "INPUT" or "IF" or "FOR" or "NEXT" or "GOTO" or "GOSUB"
            or "RETURN" or "END" or "STOP" or "DIM" or "CLEAR" or "LET" or "DATA" or "READ" or "RESTORE"
            or "RANDOMIZE" or "ON" or "OPEN" or "CLOSE" or "DEF";
    }

    private Token Advance() => _tokens[_position++];

    private bool IsAssignmentStart()
    {
        if (!Check(TokenKind.Identifier))
        {
            return false;
        }

        var offset = 1;
        if (Peek(offset) is { Kind: TokenKind.Separator, Text: "(" })
        {
            var depth = 1;
            offset++;
            while (depth > 0)
            {
                var token = Peek(offset);
                if (token is null)
                {
                    return false;
                }

                if (token.Kind == TokenKind.Separator)
                {
                    if (token.Text == "(")
                    {
                        depth++;
                    }
                    else if (token.Text == ")")
                    {
                        depth--;
                    }
                }

                offset++;
            }
        }

        return Peek(offset) is { Kind: TokenKind.Operator, Text: "=" };
    }

    private bool Match(TokenKind kind)
    {
        if (Check(kind))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool Match(TokenKind kind, string text)
    {
        if (Check(kind, text))
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool MatchKeyword(string keyword) => Match(TokenKind.Keyword, keyword);

    private bool MatchSeparator(string separator) => Match(TokenKind.Separator, separator);

    private Token? Peek(int offset = 0)
    {
        var index = _position + offset;
        if (index < 0 || index >= _tokens.Count)
        {
            return null;
        }

        return _tokens[index];
    }

    private bool Check(TokenKind kind) => Peek()?.Kind == kind;

    private bool Check(TokenKind kind, string text) => Peek() is { Kind: var k, Text: var t } && k == kind && string.Equals(t, text, StringComparison.Ordinal);

    private bool CheckNext(TokenKind kind, string text) => Peek(1) is { Kind: var k, Text: var t } && k == kind && string.Equals(t, text, StringComparison.Ordinal);

    private string ConsumeIdentifier(string message)
    {
        if (Peek() is { Kind: TokenKind.Identifier, Text: var name })
        {
            Advance();
            return name;
        }

        throw new BasicSyntaxException(message);
    }

    private void Consume(TokenKind kind, string text, string message)
    {
        if (!Match(kind, text))
        {
            throw new BasicSyntaxException(message);
        }
    }

    private void ConsumeSeparator(string separator, string message)
    {
        if (!MatchSeparator(separator))
        {
            throw new BasicSyntaxException(message);
        }
    }

    private void ConsumeKeyword(string keyword, string message)
    {
        if (!MatchKeyword(keyword))
        {
            throw new BasicSyntaxException(message);
        }
    }
}
