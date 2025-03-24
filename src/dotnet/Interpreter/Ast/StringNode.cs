using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class StringNode : AstNode
    {
        private readonly string _value;

        public StringNode(string value, SourceLocation location) : base(location)
        {
            _value = value;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new Value(new StringValue(_value));
        }

        public override string Name()
        {
            return "<string>";
        }

        public override string ToStackString()
        {
            return $"\"{_value.Replace("\"", "\\\"")}\"";
        }

        public override string ToString()
        {
            return $"StringNode(value=\"{_value.Replace("\"", "\\\"")}\")";
        }
    }
}
