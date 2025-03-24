using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class BooleanNode : AstNode
    {
        private readonly bool _value;

        public BooleanNode(bool value, SourceLocation location) : base(location)
        {
            _value = value;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new Value(new BooleanValue(_value));
        }

        public override string Name()
        {
            return "<boolean>";
        }

        public override string ToStackString()
        {
            return $"{_value.ToString().ToLower()}";
        }

        public override string ToString()
        {
            return $"BooleanNode(value={_value.ToString().ToLower()})";
        }
    }
}
