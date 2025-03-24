using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class TextNode : AstNode
    {
        private readonly string _text;

        public TextNode(string text, SourceLocation location) : base(location)
        {
            _text = text;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new Value(new StringValue(_text));
        }

        public override string Name()
        {
            return $"<text>";
        }

        public override string ToStackString()
        {
            return $"\"{_text.Replace("\"", "\\\"")}\"";
        }

        public override string ToString()
        {
            return $"TextNode(text=\"{_text.Replace("\"", "\\\"")}\")";
        }
    }
}
