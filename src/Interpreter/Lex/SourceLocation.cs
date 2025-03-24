namespace PowwowLang.Lex
{
    public class SourceLocation
    {
        public int Line { get; }
        public int Column { get; }
        public int Position { get; }
        public string Source { get; }

        public SourceLocation(int line, int column, int position, string source = null)
        {
            Line = line;
            Column = column;
            Position = position;
            Source = source;
        }

        public override string ToString()
        {
            return Source != null
                ? $"line {Line}, column {Column} in {Source}"
                : $"line {Line}, column {Column}";
        }
    }
}
