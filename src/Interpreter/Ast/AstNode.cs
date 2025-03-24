using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public abstract class AstNode
    {
        public SourceLocation Location { get; }

        protected AstNode(SourceLocation location)
        {
            Location = location;
        }

        public abstract Value Evaluate(ExecutionContext context);

        public override string ToString()
        {
            return "AstNode";
        }

        public abstract string ToStackString();

        public abstract string Name();
    }
}
