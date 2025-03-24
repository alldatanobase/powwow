using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class NewlineNode : AstNode
    {
        private readonly string _text;

        public NewlineNode(string text, SourceLocation location) : base(location)
        {
            _text = text;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new Value(new StringValue(_text));
        }

        public override string Name()
        {
            return "<newline>";
        }

        public override string ToStackString()
        {
            return $"<newline>";
        }

        public override string ToString()
        {
            return $"NewlineNode()";
        }
    }
}
