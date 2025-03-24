using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class LiteralNode : AstNode
    {
        private readonly string _content;

        public LiteralNode(string content, SourceLocation location) : base(location)
        {
            _content = content;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new Value(new StringValue(_content));
        }

        public override string Name()
        {
            return "<literal>";
        }

        public override string ToStackString()
        {
            return "<literal>";
        }

        public override string ToString()
        {
            return $"LiteralNode(content=\"{_content.Replace("\"", "\\\"")}\")";
        }
    }
}
