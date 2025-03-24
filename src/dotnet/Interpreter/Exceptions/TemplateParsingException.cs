using PowwowLang.Lex;
using System;
using System.Collections.Generic;

namespace PowwowLang.Exceptions
{
    public class TemplateParsingException : Exception
    {
        public SourceLocation Location { get; }

        public string Descriptor { get; }

        public IList<TemplateParsingException> InnerExceptions { get; }

        public TemplateParsingException()
            : base("Errors were encountered while parsing. See inner exceptions for details") 
        {
            InnerExceptions = new List<TemplateParsingException>();
        }

        public TemplateParsingException(string message, SourceLocation location)
            : base($"Error at {location}: {message}")
        {
            Location = location;
            Descriptor = message;
        }
    }
}
