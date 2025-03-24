using PowwowLang.Ast;
using PowwowLang.Env;
using System;
using System.Collections.Generic;

namespace PowwowLang.Types
{
    public class LambdaValue : Box
    {
        private readonly List<string> _parameterNames;

        public LambdaValue(Func<ExecutionContext, AstNode, List<Value>, Value> value, List<string> parameterNames) :
            base(value, ValueType.Function)
        {
            _parameterNames = parameterNames;
        }

        public List<string> ParameterNames { get { return _parameterNames; } }

        public Func<ExecutionContext, AstNode, List<Value>, Value> Value()
        {
            return _value;
        }

        public override string Output()
        {
            return $"lambda({string.Join(", ", _parameterNames)})";
        }

        public override string JsonSerialize()
        {
            return $"\"func<lambda({string.Join(", ", _parameterNames)})>\"";
        }
    }
}
