using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class LetNode : AstNode
    {
        private readonly string _variableName;
        private readonly AstNode _expression;

        public LetNode(string variableName, AstNode expression, SourceLocation location) : base(location)
        {
            _variableName = variableName;
            _expression = expression;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            var value = _expression.Evaluate(context);
            try
            {
                context.DefineVariable(_variableName, value);
            }
            catch (InnerEvaluationException ex)
            {
                throw new TemplateEvaluationException(ex.Message, context, this);
            }
            return new Value(new StringValue(string.Empty)); // Let statements don't produce outpu)t
        }

        public override string Name()
        {
            return "<let>";
        }

        public override string ToStackString()
        {
            return $"<let {_variableName}>";
        }

        public override string ToString()
        {
            return $"LetNode(variableName=\"{_variableName}\", expression={_expression.ToString()})";
        }
    }
}
