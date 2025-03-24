using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class MutationNode : AstNode
    {
        private readonly string _variableName;
        private readonly AstNode _expression;

        public MutationNode(string variableName, AstNode expression, SourceLocation location) : base(location)
        {
            _variableName = variableName;
            _expression = expression;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            var value = _expression.Evaluate(context);
            try
            {
                context.RedefineVariable(_variableName, value);
            }
            catch (InnerEvaluationException ex)
            {
                throw new TemplateEvaluationException(ex.Message, context, this);
            }
            return new Value(new StringValue(string.Empty)); // Mutations don't produce output
        }

        public override string Name()
        {
            return "<mut>";
        }

        public override string ToStackString()
        {
            return $"<mut {_variableName}>";
        }

        public override string ToString()
        {
            return $"MutationNode(variableName=\"{_variableName}\", expression={_expression.ToString()})";
        }
    }
}
