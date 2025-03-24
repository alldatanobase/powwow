using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;
using System.Collections.Generic;
using System.Linq;

namespace PowwowLang.Ast
{
    public class ArrayNode : AstNode
    {
        private readonly List<AstNode> _elements;

        public ArrayNode(List<AstNode> elements, SourceLocation location) : base(location)
        {
            _elements = elements;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new Value(new ArrayValue(_elements.Select(element => element.Evaluate(context)).ToList()));
        }

        public override string Name()
        {
            return "<array>";
        }

        public override string ToStackString()
        {
            var elementsStr = string.Join(", ", _elements.Select(e => e.ToStackString()));
            return $"[{elementsStr}]";
        }

        public override string ToString()
        {
            var elementsStr = string.Join(", ", _elements.Select(e => e.ToString()));
            return $"ArrayNode(elements=[{elementsStr}])";
        }
    }
}
