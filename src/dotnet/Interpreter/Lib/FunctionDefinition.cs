using PowwowLang.Ast;
using PowwowLang.Env;
using PowwowLang.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PowwowLang.Lib
{
    public class FunctionDefinition
    {
        public string Name { get; }
        public List<ParameterDefinition> Parameters { get; }
        public Func<ExecutionContext, AstNode, List<Value>, Value> Implementation { get; }
        public bool IsLazilyEvaluated { get; }

        public FunctionDefinition(
            string name,
            List<ParameterDefinition> parameters,
            Func<ExecutionContext, AstNode, List<Value>, Value> implementation,
            bool isLazilyEvaluated)
        {
            Name = name;
            Parameters = parameters;
            Implementation = implementation;
            IsLazilyEvaluated = isLazilyEvaluated;
        }

        public int RequiredParameterCount => Parameters.Count(p => !p.IsOptional);
        public int TotalParameterCount => Parameters.Count;
    }
}
