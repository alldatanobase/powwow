using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class FunctionReferenceNode : AstNode
    {
        private readonly string _functionName;

        public FunctionReferenceNode(string functionName, SourceLocation location) : base(location)
        {
            _functionName = functionName;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new Value(new FunctionReferenceValue(_functionName));
        }

        public override string Name()
        {
            return "<function ref>";
        }

        public override string ToStackString()
        {
            return $"{_functionName}";
        }

        public override string ToString()
        {
            return $"FunctionReferenceNode(name=\"{_functionName}\")";
        }
    }
}
