using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class WhitespaceNode : AstNode
    {
        private readonly string _text;

        public WhitespaceNode(string text, SourceLocation location) : base(location)
        {
            _text = text;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new Value(new StringValue(_text));
        }

        public override string Name()
        {
            return "<whitespace>";
        }

        public override string ToStackString()
        {
            return $"\"{_text.Replace("\"", "\\\"")}\"";
        }

        public override string ToString()
        {
            return $"WhitespaceNode(text=\"{_text.Replace("\"", "\\\"")}\")";
        }
    }
}
