using PowwowLang.Ast;
using PowwowLang.Env;
using PowwowLang.Exceptions;
using System;

namespace PowwowLang.Types
{
    public class TypeHelper
    {
        public static bool UnboxBoolean(Value value, ExecutionContext context, AstNode astNode)
        {
            if (value.ValueOf() is BooleanValue booleanValue)
            {
                return booleanValue.Value();
            }
            else
            {
                throw new TemplateEvaluationException(
                    $"Expected value of type boolean but found {value.GetType()}",
                    context,
                    astNode);
            }
        }

        public static decimal UnboxNumber(Value value, ExecutionContext context, AstNode astNode)
        {
            if (value.ValueOf() is NumberValue booleanValue)
            {
                return booleanValue.Value();
            }
            else
            {
                throw new TemplateEvaluationException(
                    $"Expected value of type Number but found {value.GetType()}",
                    context,
                    astNode);
            }
        }

        public static bool IsConvertibleToDecimal(dynamic value)
        {
            if (value == null)
                return false;

            Type valueType = value.GetType();

            // Check numeric types that can be safely converted to decimal
            if (valueType == typeof(decimal) ||
                valueType == typeof(int) ||
                valueType == typeof(long) ||
                valueType == typeof(double) ||
                valueType == typeof(float) ||
                valueType == typeof(byte) ||
                valueType == typeof(sbyte) ||
                valueType == typeof(short) ||
                valueType == typeof(ushort) ||
                valueType == typeof(uint) ||
                valueType == typeof(ulong))
            {
                return true;
            }

            return false;
        }
    }
}
