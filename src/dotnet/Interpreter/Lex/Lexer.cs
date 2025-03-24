using PowwowLang.Exceptions;
using System.Collections.Generic;
using System.Text;

namespace PowwowLang.Lex
{
    public class Lexer
    {
        private string _input;
        private int _position;
        private int _line;
        private int _column;
        private readonly List<Token> _tokens;
        private string _sourceName;

        private struct PositionState
        {
            public int Position { get; set; }
            public int Line { get; set; }
            public int Column { get; set; }

            public PositionState(int position, int line, int column)
            {
                Position = position;
                Line = line;
                Column = column;
            }
        }

        private void ThrowLexerError(string message)
        {
            var location = new SourceLocation(_line, _column, _position, _sourceName);
            throw new TemplateParsingException(message, location);
        }

        private PositionState SavePosition()
        {
            return new PositionState(_position, _line, _column);
        }

        private void RestorePosition(PositionState state)
        {
            _position = state.Position;
            _line = state.Line;
            _column = state.Column;
        }

        public Lexer()
        {
            _tokens = new List<Token>();
        }

        public IReadOnlyList<Token> Tokenize(string input, string sourceName = null)
        {
            _input = input;
            _position = 0;
            _line = 1;
            _column = 1;
            _tokens.Clear();

            if (sourceName != null)
            {
                _sourceName = sourceName;
            }

            while (_position < _input.Length)
            {
                if (TryMatch("{{-"))
                {
                    AddToken(TokenType.DirectiveStart, "{{-");
                    UpdatePositionAndTracking(3);
                    TokenizeDirective();
                }
                else if (TryMatch("{{"))
                {
                    AddToken(TokenType.DirectiveStart, "{{");
                    UpdatePositionAndTracking(2);
                    TokenizeDirective();
                }
                else if (IsNewline(_position))
                {
                    TokenizeNewline();
                }
                else if (IsWhitespace(_position))
                {
                    TokenizeWhitespace();
                }
                else
                {
                    TokenizeText();
                }
            }

            return _tokens;
        }

        private void TokenizeComment()
        {
            while (_position < _input.Length)
            {
                if (TryMatch("*"))
                {
                    var savedPosition = SavePosition();
                    UpdatePositionAndTracking(1); // Skip past "*"

                    while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
                    {
                        UpdatePositionAndTracking(1);
                    }

                    if (TryMatch("}}"))
                    {
                        AddToken(TokenType.CommentEnd, "*", savedPosition);
                        AddToken(TokenType.DirectiveEnd, "}}");
                        UpdatePositionAndTracking(2); // Skip past "}}"
                        return;
                    }
                    else if (TryMatch("-}}"))
                    {
                        AddToken(TokenType.CommentEnd, "*", savedPosition);
                        AddToken(TokenType.DirectiveEnd, "-}}");
                        UpdatePositionAndTracking(3); // Skip past "-}}"
                        return;
                    }
                }

                UpdatePositionAndTracking(1);
            }

            ThrowLexerError("Unterminated comment");
        }

        private void TokenizeDirective()
        {
            SkipWhitespace();

            if (TryMatch("*"))
            {
                AddToken(TokenType.CommentStart, "*");
                UpdatePositionAndTracking(1);
                TokenizeComment();
                return;
            }

            if (TryMatch("literal"))
            {
                AddToken(TokenType.Literal, "literal");
                UpdatePositionAndTracking(7);

                SkipWhitespace();

                if (!TryMatch("}}") && !TryMatch("-}}"))
                {
                    ThrowLexerError("Unterminated literal directive");
                }

                if (TryMatch("}}"))
                {
                    AddToken(TokenType.DirectiveEnd, "}}");
                    UpdatePositionAndTracking(2); // Skip }}
                }
                else
                {
                    AddToken(TokenType.DirectiveEnd, "-}}");
                    UpdatePositionAndTracking(3); // Skip -}}
                }

                // Capture everything until we find the closing literal directive
                var startPosition = SavePosition();
                var literalStackCount = 0;

                while (_position < _input.Length)
                {
                    int originalPosition = _position;
                    var savedPosition = SavePosition();

                    if (TryMatch("{{") || TryMatch("{{-"))
                    {
                        Token directiveStartToken = null;

                        if (TryMatch("{{"))
                        {
                            directiveStartToken = new Token(TokenType.DirectiveStart, "{{", CreateLocation(savedPosition));
                            savedPosition = UpdatePositionAndTrackingOnState(2 + WhitespaceCount(savedPosition.Position + 2), savedPosition);
                        }
                        else
                        {
                            directiveStartToken = new Token(TokenType.DirectiveStart, "{{-", CreateLocation(savedPosition));
                            savedPosition = UpdatePositionAndTrackingOnState(3 + WhitespaceCount(savedPosition.Position + 3), savedPosition);
                        }

                        if (TryMatchAt("literal", savedPosition.Position))
                        {
                            savedPosition = UpdatePositionAndTrackingOnState(7 + WhitespaceCount(savedPosition.Position + 7), savedPosition);

                            if (TryMatchAt("}}", savedPosition.Position))
                            {
                                savedPosition = UpdatePositionAndTrackingOnState(2, savedPosition);
                                UpdatePositionAndTracking(savedPosition.Position - originalPosition);
                                literalStackCount++;
                                continue;
                            }

                            if (TryMatchAt("-}}", savedPosition.Position))
                            {
                                savedPosition = UpdatePositionAndTrackingOnState(3, savedPosition);
                                UpdatePositionAndTracking(savedPosition.Position - originalPosition);
                                literalStackCount++;
                                continue;
                            }
                        }

                        if (TryMatchAt("/literal", savedPosition.Position))
                        {
                            var endLiteralToken = new Token(TokenType.EndLiteral, "/literal", CreateLocation(savedPosition));
                            savedPosition = UpdatePositionAndTrackingOnState(8 + WhitespaceCount(savedPosition.Position + 8), savedPosition);

                            if (TryMatchAt("}}", savedPosition.Position) || TryMatchAt("-}}", savedPosition.Position))
                            {
                                Token directiveEndToken = null;

                                if (TryMatchAt("}}", savedPosition.Position))
                                {
                                    directiveEndToken = new Token(TokenType.DirectiveEnd, "}}", CreateLocation(savedPosition));
                                    savedPosition = UpdatePositionAndTrackingOnState(2, savedPosition);
                                }
                                else
                                {
                                    directiveEndToken = new Token(TokenType.DirectiveEnd, "-}}", CreateLocation(savedPosition));
                                    savedPosition = UpdatePositionAndTrackingOnState(3, savedPosition);
                                }

                                if (literalStackCount > 0)
                                {
                                    literalStackCount--;
                                }
                                else
                                {
                                    // We found the end, create a token with the raw content
                                    var content = _input.Substring(startPosition.Position, _position - startPosition.Position);
                                    AddToken(TokenType.Text, content, startPosition);
                                    _tokens.Add(directiveStartToken);
                                    _tokens.Add(endLiteralToken);
                                    _tokens.Add(directiveEndToken);
                                    UpdatePositionAndTracking(savedPosition.Position - originalPosition); // Skip {{/literal}} plus whitespace
                                    return;
                                }
                            }
                        }
                    }

                    UpdatePositionAndTracking(1);
                }

                ThrowLexerError("Unterminated literal directive");
            }

            while (_position < _input.Length)
            {
                SkipWhitespace();

                if (_position >= _input.Length)
                {
                    // if the whitespace skipped was the last thing in the input buffer
                    continue;
                }

                if (TryMatch("}}"))
                {
                    AddToken(TokenType.DirectiveEnd, "}}");
                    UpdatePositionAndTracking(2);
                    return;
                }

                if (TryMatch("-}}"))
                {
                    AddToken(TokenType.DirectiveEnd, "-}}");
                    UpdatePositionAndTracking(3);
                    return;
                }

                if (TryMatch(","))
                {
                    AddToken(TokenType.Comma, ",");
                    UpdatePositionAndTracking(1);
                    continue;
                }

                if (TryMatch("=>"))
                {
                    AddToken(TokenType.Arrow, "=>");
                    UpdatePositionAndTracking(2);
                    continue;
                }

                if (TryMatch("obj("))
                {
                    AddToken(TokenType.ObjectStart, "obj(");
                    UpdatePositionAndTracking(4);
                    continue;
                }

                if (TryMatch(":"))
                {
                    AddToken(TokenType.Colon, ":");
                    UpdatePositionAndTracking(1);
                    continue;
                }

                if (TryMatch("."))
                {
                    AddToken(TokenType.Dot, ".");
                    UpdatePositionAndTracking(1);
                    continue;
                }

                if (TryMatch("["))
                {
                    AddToken(TokenType.LeftBracket, "[");
                    UpdatePositionAndTracking(1);
                    continue;
                }

                if (TryMatch("]"))
                {
                    AddToken(TokenType.RightBracket, "]");
                    UpdatePositionAndTracking(1);
                    continue;
                }

                // After a dot, treat identifiers as field names
                if (_position > 0 && _tokens.Count > 0 &&
                    _tokens[_tokens.Count - 1].Type == TokenType.Dot &&
                    _position < _input.Length &&
                    (char.IsLetter(_input[_position]) || _input[_position] == '_'))
                {
                    var savedState = SavePosition();

                    while (_position < _input.Length &&
                           (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
                    {
                        UpdatePositionAndTracking(1);
                    }

                    var fieldName = _input.Substring(savedState.Position, _position - savedState.Position);
                    AddToken(TokenType.Field, fieldName, savedState);
                    continue;
                }

                // Match function calls before other operations
                if (char.IsLetter(_input[_position]))
                {
                    var savedPosition = SavePosition();

                    while (_position < _input.Length && char.IsLetter(_input[_position]))
                    {
                        UpdatePositionAndTracking(1);
                    }

                    var value = _input.Substring(savedPosition.Position, _position - savedPosition.Position);

                    // Look ahead for opening parenthesis to distinguish functions from variables
                    SkipWhitespace();

                    if (_position < _input.Length && _input[_position] == '(')
                    {
                        AddToken(TokenType.Function, value, savedPosition);
                        continue;
                    }
                    else
                    {
                        // Rewind position as this is not a function
                        RestorePosition(savedPosition);
                    }
                }

                // Match keywords and operators
                if (TryMatch("let"))
                {
                    AddToken(TokenType.Let, "let");
                    UpdatePositionAndTracking(3);
                    continue;
                }
                else if (TryMatch("mut"))
                {
                    AddToken(TokenType.Mutation, "mut");
                    UpdatePositionAndTracking(3);
                    continue;
                }
                else if (TryMatch("capture"))
                {
                    AddToken(TokenType.Capture, "capture");
                    UpdatePositionAndTracking(7);
                    continue;
                }
                else if (TryMatch("/capture"))
                {
                    AddToken(TokenType.EndCapture, "/capture");
                    UpdatePositionAndTracking(8);
                    continue;
                }
                else if (TryMatch("for"))
                {
                    AddToken(TokenType.For, "for");
                    UpdatePositionAndTracking(3);
                    continue;
                }
                else if (TryMatch("include"))
                {
                    AddToken(TokenType.Include, "include");
                    UpdatePositionAndTracking(7);
                    continue;
                }
                else if (TryMatch("if"))
                {
                    AddToken(TokenType.If, "if");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch("elseif"))
                {
                    AddToken(TokenType.ElseIf, "elseif");
                    UpdatePositionAndTracking(6);
                    continue;
                }
                else if (TryMatch("else"))
                {
                    AddToken(TokenType.Else, "else");
                    UpdatePositionAndTracking(4);
                    continue;
                }
                else if (TryMatch("/for"))
                {
                    AddToken(TokenType.EndFor, "/for");
                    UpdatePositionAndTracking(4);
                    continue;
                }
                else if (TryMatch("/if"))
                {
                    AddToken(TokenType.EndIf, "/if");
                    UpdatePositionAndTracking(3);
                    continue;
                }
                else if (TryMatch("String"))
                {
                    AddToken(TokenType.Type, "String");
                    UpdatePositionAndTracking(6);
                    continue;
                }
                else if (TryMatch("Number"))
                {
                    AddToken(TokenType.Type, "Number");
                    UpdatePositionAndTracking(6);
                    continue;
                }
                else if (TryMatch("Boolean"))
                {
                    AddToken(TokenType.Type, "Boolean");
                    UpdatePositionAndTracking(7);
                    continue;
                }
                else if (TryMatch("Array"))
                {
                    AddToken(TokenType.Type, "Array");
                    UpdatePositionAndTracking(5);
                    continue;
                }
                else if (TryMatch("Object"))
                {
                    AddToken(TokenType.Type, "Object");
                    UpdatePositionAndTracking(6);
                    continue;
                }
                else if (TryMatch("Function"))
                {
                    AddToken(TokenType.Type, "Function");
                    UpdatePositionAndTracking(8);
                    continue;
                }

                else if (TryMatch("DateTime"))
                {
                    AddToken(TokenType.Type, "DateTime");
                    UpdatePositionAndTracking(8);
                    continue;
                }
                else if (TryMatch(">="))
                {
                    AddToken(TokenType.GreaterThanEqual, ">=");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch("<="))
                {
                    AddToken(TokenType.LessThanEqual, "<=");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch("=="))
                {
                    AddToken(TokenType.Equal, "==");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch("="))
                {
                    AddToken(TokenType.Assignment, "=");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("!="))
                {
                    AddToken(TokenType.NotEqual, "!=");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch("&&"))
                {
                    AddToken(TokenType.And, "&&");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch("||"))
                {
                    AddToken(TokenType.Or, "||");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch(">"))
                {
                    AddToken(TokenType.GreaterThan, ">");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("<"))
                {
                    AddToken(TokenType.LessThan, "<");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("!"))
                {
                    AddToken(TokenType.Not, "!");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("+"))
                {
                    AddToken(TokenType.Plus, "+");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("*"))
                {
                    AddToken(TokenType.Multiply, "*");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("/"))
                {
                    AddToken(TokenType.Divide, "/");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("("))
                {
                    AddToken(TokenType.LeftParen, "(");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch(")"))
                {
                    AddToken(TokenType.RightParen, ")");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("\""))
                {
                    TokenizeString();
                    continue;
                }
                else if (char.IsDigit(_input[_position]) || (_input[_position] == '-' && char.IsDigit(PeekNext())))
                {
                    TokenizeNumber();
                    continue;
                }
                else if (TryMatch("-"))
                {
                    AddToken(TokenType.Minus, "-");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (char.IsLetter(_input[_position]) || _input[_position] == '_')
                {
                    TokenizeIdentifier();
                    continue;
                }
                else
                {
                    ThrowLexerError($"Unexpected character '{_input[_position]}'");
                }
            }
        }

        private void TokenizeText()
        {
            var savedPosition = SavePosition();

            while (_position < _input.Length &&
                   !TryMatch("{{") &&
                   !IsNewline(_position) &&
                   !IsWhitespace(_position))
            {
                UpdatePositionAndTracking(1);
            }

            if (_position > savedPosition.Position)
            {
                AddToken(TokenType.Text, _input.Substring(savedPosition.Position, _position - savedPosition.Position), savedPosition);
            }
        }

        private void TokenizeWhitespace()
        {
            var savedPosition = SavePosition();

            while (_position < _input.Length &&
                   IsWhitespace(_position))
            {
                UpdatePositionAndTracking(1);
            }

            if (_position > savedPosition.Position)
            {
                AddToken(TokenType.Whitespace, _input.Substring(savedPosition.Position, _position - savedPosition.Position), savedPosition);
            }
        }

        private void TokenizeNewline()
        {
            var savedPosition = SavePosition();
            string newlineValue;

            if (_input[_position] == '\r' &&
                _position + 1 < _input.Length &&
                _input[_position + 1] == '\n')
            {
                newlineValue = "\r\n";
                UpdatePositionAndTracking(2);
            }
            else
            {
                newlineValue = _input[_position] == '\r' ? "\r" : "\n";
                UpdatePositionAndTracking(1);
            }

            AddToken(TokenType.Newline, newlineValue, savedPosition);
        }

        private bool IsNewline(int pos)
        {
            if (pos >= _input.Length)
                return false;

            return _input[pos] == '\r' || _input[pos] == '\n';
        }

        private bool IsWhitespace(int pos)
        {
            if (pos >= _input.Length)
                return false;

            return char.IsWhiteSpace(_input[pos]) &&
                   !IsNewline(pos);
        }

        private void TokenizeString()
        {
            UpdatePositionAndTracking(1); // Skip opening quote
            var result = new StringBuilder();
            var savedPosition = SavePosition();

            while (_position < _input.Length && _input[_position] != '"')
            {
                if (_input[_position] == '\\' && _position + 1 < _input.Length)
                {
                    // Handle escape sequences
                    char nextChar = _input[_position + 1];
                    switch (nextChar)
                    {
                        case '"':
                            result.Append('"');
                            break;
                        case '\\':
                            result.Append('\\');
                            break;
                        case 'n':
                            result.Append('\n');
                            break;
                        case 'r':
                            result.Append('\r');
                            break;
                        case 't':
                            result.Append('\t');
                            break;
                        default:
                            ThrowLexerError($"Invalid escape sequence '\\{nextChar}'");
                            break;
                    }
                    UpdatePositionAndTracking(2); // Skip both the backslash and the escaped character
                }
                else
                {
                    result.Append(_input[_position]);
                    UpdatePositionAndTracking(1);
                }
            }

            if (_position >= _input.Length)
            {
                ThrowLexerError("Unterminated string literal");
            }

            AddToken(TokenType.String, result.ToString(), savedPosition);
            UpdatePositionAndTracking(1); // Skip closing quote
        }

        private void TokenizeNumber()
        {
            var savedPosition = SavePosition();
            bool hasDecimal = false;

            if (_input[_position] == '-')
            {
                UpdatePositionAndTracking(1);
            }

            while (_position < _input.Length &&
                   (char.IsDigit(_input[_position]) ||
                    (!hasDecimal && _input[_position] == '.')))
            {
                if (_input[_position] == '.')
                {
                    hasDecimal = true;
                }
                UpdatePositionAndTracking(1);
            }

            var value = _input.Substring(savedPosition.Position, _position - savedPosition.Position);
            AddToken(TokenType.Number, value, savedPosition);
        }

        private void TokenizeIdentifier()
        {
            var savedPosition = SavePosition();

            while (_position < _input.Length &&
                   (char.IsLetterOrDigit(_input[_position]) ||
                    _input[_position] == '_'))
            {
                UpdatePositionAndTracking(1);
            }

            var value = _input.Substring(savedPosition.Position, _position - savedPosition.Position);

            switch (value)
            {
                case "true":
                    AddToken(TokenType.True, value, savedPosition);
                    break;
                case "false":
                    AddToken(TokenType.False, value, savedPosition);
                    break;
                case "in":
                    AddToken(TokenType.In, value, savedPosition);
                    break;
                default:
                    AddToken(TokenType.Variable, value, savedPosition);
                    break;
            }
        }

        private void SkipWhitespace()
        {
            while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
            {
                UpdatePositionAndTracking(1);
            }
        }

        private int WhitespaceCount(int position)
        {
            int originalPosition = position;
            int currentPosition = position;
            while (currentPosition < _input.Length && char.IsWhiteSpace(_input[currentPosition]))
            {
                currentPosition++;
            }
            return currentPosition - originalPosition;
        }

        private PositionState UpdatePositionAndTrackingOnState(int distance, PositionState state)
        {
            // For each character we're skipping, update line and column
            for (int i = 0; i < distance && state.Position < _input.Length; i++)
            {
                if (_input[state.Position] == '\n')
                {
                    state.Line++;
                    state.Column = 1;
                }
                else if (_input[state.Position] == '\r')
                {
                    // Handle Windows-style \r\n newlines
                    if (state.Position + 1 < _input.Length && _input[state.Position + 1] == '\n')
                    {
                        i++; // Skip the next character too
                        state.Position++; // Move past \r
                    }
                    state.Line++;
                    state.Column = 1;
                }
                else
                {
                    state.Column++;
                }
                state.Position++;
            }

            return state;
        }

        private void UpdatePositionAndTracking(int distance)
        {
            // For each character we're skipping, update line and column
            for (int i = 0; i < distance && _position < _input.Length; i++)
            {
                if (_input[_position] == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else if (_input[_position] == '\r')
                {
                    // Handle Windows-style \r\n newlines
                    if (_position + 1 < _input.Length && _input[_position + 1] == '\n')
                    {
                        i++; // Skip the next character too
                        _position++; // Move past \r
                    }
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
                _position++;
            }
        }

        private bool TryMatch(string pattern)
        {
            if (_position + pattern.Length > _input.Length)
            {
                return false;
            }

            return _input.Substring(_position, pattern.Length) == pattern;
        }

        private bool TryMatchAt(string pattern, int position)
        {
            if (position + pattern.Length > _input.Length)
            {
                return false;
            }

            return _input.Substring(position, pattern.Length) == pattern;
        }

        private char PeekNext()
        {
            return _position + 1 < _input.Length ? _input[_position + 1] : '\0';
        }

        private SourceLocation CreateLocation()
        {
            return new SourceLocation(_line, _column, _position, _sourceName);
        }

        private SourceLocation CreateLocation(PositionState savedState)
        {
            return new SourceLocation(savedState.Line, savedState.Column, savedState.Position, _sourceName);
        }

        private void AddToken(TokenType type, string value)
        {
            _tokens.Add(new Token(type, value, CreateLocation()));
        }

        private void AddToken(TokenType type, string value, PositionState savedState)
        {
            _tokens.Add(new Token(type, value, CreateLocation(savedState)));
        }
    }
}
