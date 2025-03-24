using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class TypeNode : AstNode
    {
        private readonly ValueType _type;

        public TypeNode(ValueType type, SourceLocation location) : base(location)
        {
            _type = type;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new Value(new TypeValue(_type));
        }

        public override string Name()
        {
            return "<type>";
        }

        public override string ToStackString()
        {
            return $"<type<{_type.ToString()}>>";
        }

        public override string ToString()
        {
            return $"TypeNode(type={_type.ToString()})";
        }
    }
}
