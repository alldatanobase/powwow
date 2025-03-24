using PowwowLang.Ast;
using PowwowLang.Env;
using PowwowLang.Lex;
using System;
using System.Text;

namespace PowwowLang.Exceptions
{
    public class TemplateEvaluationException : Exception
    {
        public SourceLocation Location { get; }
        public string Descriptor { get; }
        public override string StackTrace { get; }
        public AstNode CallSite { get; }

        public TemplateEvaluationException(
            string message,
            ExecutionContext frame,
            AstNode callSite)
            : base(FormatMessage(message, frame.CallSite.Location))
        {
            Location = frame.CallSite.Location;
            Descriptor = message;
            CallSite = callSite;
            StackTrace = FormatStackTrace(message, frame.CallSite.Location, frame);
        }

        private static string FormatMessage(string message, SourceLocation location)
        {
            return $"Error at {location}: {message}";
        }

        private static string FormatStackTrace(string message, SourceLocation location, ExecutionContext frame)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Error at {location}: {message}");

            if (frame != null)
            {
                var current = frame;
                while (current != null)
                {
                    sb.AppendLine($"  at {current.CallSite.ToStackString()} (line {current.CallSite.Location.Line}, column {current.CallSite.Location.Column}{(current.CallSite.Location.Source != null ? ", " + current.CallSite.Location.Source : "")})");
                    current = current.Parent;
                }
            }

            return sb.ToString();
        }
    }
}
