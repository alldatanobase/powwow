using PowwowLang.Ast;
using PowwowLang.Env;
using PowwowLang.Exceptions;
using System;
using System.Collections.Generic;

namespace PowwowLang.Types
{
    public class ValueComparer : IComparer<Value>
    {
        private readonly Func<ExecutionContext, AstNode, List<Value>, Value> _comparer;
        private readonly ExecutionContext _context;
        private readonly AstNode _callSite;

        public ValueComparer(ExecutionContext context, AstNode callSite, Func<ExecutionContext, AstNode, List<Value>, Value> comparer)
        {
            _comparer = comparer;
            _context = context;
            _callSite = callSite;
        }

        public int Compare(Value x, Value y)
        {
            var comparison = _comparer(_context, _callSite, new List<Value> { x, y });
            if (comparison.ValueOf() is NumberValue diff)
            {
                return Math.Sign(diff.Value());
            }
            else
            {
                throw new InnerEvaluationException(
                    $"Expected value of type number but found {comparison.GetType()}");
            }
        }
    }
}
