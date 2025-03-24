using PowwowLang.Exceptions;
using System.Collections.Generic;

namespace PowwowLang.Lib
{
    public class TemplateRegistry : ITemplateResolver
    {
        private readonly Dictionary<string, string> _templates;

        public TemplateRegistry()
        {
            _templates = new Dictionary<string, string>();
        }

        public void RegisterTemplate(string name, string template)
        {
            _templates[name] = template;
        }

        public string ResolveTemplate(string templateName)
        {
            if (!_templates.TryGetValue(templateName, out var template))
            {
                throw new InitializationException($"Template '{templateName}' not found");
            }
            return template;
        }
    }
}
