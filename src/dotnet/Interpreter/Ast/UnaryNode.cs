using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class UnaryNode : AstNode
    {
        private readonly TokenType _operator;
        private readonly AstNode _expression;

        public UnaryNode(TokenType op, AstNode expression, SourceLocation location) : base(location)
        {
            _operator = op;
            _expression = expression;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            var value = _expression.Evaluate(context);

            switch (_operator)
            {
                case TokenType.Not:
                    if (!(value.ValueOf() is BooleanValue boolValue))
                    {
                        throw new TemplateEvaluationException(
                            $"Expected value of type boolean but found {value.GetType()}",
                            context,
                            _expression);
                    }
                    return new Value(new BooleanValue(!boolValue.Value()));
                default:
                    throw new TemplateEvaluationException(
                        $"Unknown unary operator: {_operator}",
                        context,
                        this);
            }
        }

        public override string Name()
        {
            return "<unary>";
        }

        public override string ToStackString()
        {
            return $"<!{_expression.ToStackString()}>";
        }

        public override string ToString()
        {
            return $"UnaryNode(operator={_operator}, expression={_expression.ToString()})";
        }
    }
}
