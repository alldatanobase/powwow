using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;
using System.Collections.Generic;
using System.Linq;

namespace PowwowLang.Ast
{
    public class ObjectCreationNode : AstNode
    {
        private readonly List<KeyValuePair<string, AstNode>> _fields;

        public ObjectCreationNode(List<KeyValuePair<string, AstNode>> fields, SourceLocation location) : base(location)
        {
            _fields = fields;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            var dict = new Dictionary<string, Value>();

            foreach (var field in _fields)
            {
                dict[field.Key] = field.Value.Evaluate(context);
            }

            return new Value(new ObjectValue(dict));
        }

        public override string Name()
        {
            return "<obj>";
        }

        public override string ToStackString()
        {
            return "<obj>";
        }

        public override string ToString()
        {
            var fieldsStr = string.Join(", ", _fields.Select(f => $"{{key=\"{f.Key}\", value={f.Value.ToString()}}}"));
            return $"ObjectCreationNode(fields=[{fieldsStr}])";
        }
    }
}
