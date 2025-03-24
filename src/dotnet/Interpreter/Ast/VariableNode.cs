using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class VariableNode : AstNode
    {
        private readonly string _path;

        public VariableNode(string path, SourceLocation location) : base(location)
        {
            _path = path;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            try
            {
                return context.ResolveValue(_path);
            }
            catch (InnerEvaluationException ex)
            {
                throw new TemplateEvaluationException(ex.Message, context, this);
            }
        }

        public override string Name()
        {
            return "<variable>";
        }

        public override string ToStackString()
        {
            return $"{_path}";
        }

        public override string ToString()
        {
            return $"VariableNode(path=\"{_path}\")";
        }
    }
}
