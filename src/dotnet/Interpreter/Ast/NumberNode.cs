using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class NumberNode : AstNode
    {
        private readonly decimal _value;

        public NumberNode(string value, SourceLocation location) : base(location)
        {
            _value = decimal.Parse(value);
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new Value(new NumberValue(_value));
        }

        public override string Name()
        {
            return "<number>";
        }

        public override string ToStackString()
        {
            return $"{_value}";
        }

        public override string ToString()
        {
            return $"NumberNode(value={_value})";
        }
    }
}
