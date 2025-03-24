using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class IncludeNode : AstNode
    {
        private readonly string _templateName;
        private AstNode _includedTemplate;

        public IncludeNode(string templateName, SourceLocation location) : base(location)
        {
            _templateName = templateName;
            _includedTemplate = null;
        }

        public string TemplateName { get { return _templateName; } }

        public void SetIncludedTemplate(AstNode template)
        {
            _includedTemplate = template;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            if (_includedTemplate == null)
            {
                throw new TemplateEvaluationException(
                    $"Template '{_templateName}' could not been resolved",
                    context,
                    _includedTemplate);
            }
            var currentContext = new ExecutionContext(
                context.GetData(),
                context.GetFunctionRegistry(),
                context,
                context.MaxDepth,
                this);
            return _includedTemplate.Evaluate(currentContext);
        }

        public override string ToString()
        {
            string templateStr = _includedTemplate == null ? "null" : _includedTemplate.ToString();
            return $"IncludeNode(templateName=\"{_templateName}\", template={templateStr})";
        }

        public override string ToStackString()
        {
            return $"<include {_templateName}>";
        }

        public override string Name()
        {
            return "<include>";
        }
    }
}
