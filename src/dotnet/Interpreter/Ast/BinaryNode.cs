using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class BinaryNode : AstNode
    {
        private readonly TokenType _operator;
        private readonly AstNode _left;
        private readonly AstNode _right;

        public BinaryNode(TokenType op, AstNode left, AstNode right, SourceLocation location) : base(location)
        {
            _operator = op;
            _left = left;
            _right = right;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            // short circuit eval for &&
            if (_operator == TokenType.And)
            {
                return new Value(new BooleanValue(
                    TypeHelper.UnboxBoolean(_left.Evaluate(context), context, _left) &&
                    TypeHelper.UnboxBoolean(_right.Evaluate(context), context, _right)));
            }

            // short circuit eval for ||
            if (_operator == TokenType.Or)
            {
                return new Value(new BooleanValue(
                    TypeHelper.UnboxBoolean(_left.Evaluate(context), context, _left) ||
                    TypeHelper.UnboxBoolean(_right.Evaluate(context), context, _right)));
            }

            var left = _left.Evaluate(context);
            var right = _right.Evaluate(context);

            // type check?

            switch (_operator)
            {
                case TokenType.Plus:
                    return new Value(new NumberValue(TypeHelper.UnboxNumber(left, context, _left) + TypeHelper.UnboxNumber(right, context, _right)));
                case TokenType.Minus:
                    return new Value(new NumberValue(TypeHelper.UnboxNumber(left, context, _left) - TypeHelper.UnboxNumber(right, context, _right)));
                case TokenType.Multiply:
                    return new Value(new NumberValue(TypeHelper.UnboxNumber(left, context, _left) * TypeHelper.UnboxNumber(right, context, _right)));
                case TokenType.Divide:
                    return new Value(new NumberValue(TypeHelper.UnboxNumber(left, context, _left) / TypeHelper.UnboxNumber(right, context, _right)));
                case TokenType.LessThan:
                    return new Value(new BooleanValue(TypeHelper.UnboxNumber(left, context, _left) < TypeHelper.UnboxNumber(right, context, _right)));
                case TokenType.LessThanEqual:
                    return new Value(new BooleanValue(TypeHelper.UnboxNumber(left, context, _left) <= TypeHelper.UnboxNumber(right, context, _right)));
                case TokenType.GreaterThan:
                    return new Value(new BooleanValue(TypeHelper.UnboxNumber(left, context, _left) > TypeHelper.UnboxNumber(right, context, _right)));
                case TokenType.GreaterThanEqual:
                    return new Value(new BooleanValue(TypeHelper.UnboxNumber(left, context, _left) >= TypeHelper.UnboxNumber(right, context, _right)));
                case TokenType.Equal:
                    if (left.TypeOf() != right.TypeOf())
                    {
                        throw new TemplateEvaluationException(
                            $"Expected similar types but found {left.TypeOf()} and {right.TypeOf()}",
                            context,
                            this);
                    }
                    else
                    {
                        if (left.ValueOf() is DateTimeValue leftDateTime && right.ValueOf() is DateTimeValue rightDateTime)
                        {
                            return new Value(new BooleanValue(Equals(leftDateTime.Value().Ticks, rightDateTime.Value().Ticks)));
                        }
                        else
                        {
                            return new Value(new BooleanValue(Equals(left.ValueOf().Unbox(), right.ValueOf().Unbox())));
                        }
                    }
                case TokenType.NotEqual:
                    if (left.TypeOf() != right.TypeOf())
                    {
                        throw new TemplateEvaluationException(
                            $"Expected similar types but found {left.TypeOf()} and {right.TypeOf()}",
                            context,
                            this);
                    }
                    else
                    {
                        if (left.ValueOf() is DateTimeValue leftDateTime && right.ValueOf() is DateTimeValue rightDateTIme)
                        {
                            return new Value(new BooleanValue(!Equals(leftDateTime.Value().Ticks, rightDateTIme.Value().Ticks)));
                        }
                        else
                        {
                            return new Value(new BooleanValue(!Equals(left.ValueOf().Unbox(), right.ValueOf().Unbox())));
                        }
                    }
                default:
                    throw new TemplateEvaluationException(
                        $"Unknown binary operator: {_operator}",
                        context,
                        this);
            }
        }

        public override string Name()
        {
            return "<binary>";
        }

        public override string ToStackString()
        {
            return $"<{_left.ToStackString()} {_operator} {_right.ToStackString()}>";
        }

        public override string ToString()
        {
            return $"BinaryNode(operator={_operator}, left={_left.ToString()}, right={_right.ToString()})";
        }
    }
}
