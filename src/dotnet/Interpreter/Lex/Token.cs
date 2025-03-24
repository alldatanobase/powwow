namespace PowwowLang.Lex
{
    public class Token
    {
        public TokenType Type { get; private set; }
        public string Value { get; private set; }
        public SourceLocation Location { get; private set; }

        public Token(TokenType type, string value, SourceLocation location)
        {
            Type = type;
            Value = value;
            Location = location;
        }
    }
}
