﻿using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class CaptureNode : AstNode
    {
        private readonly string _variableName;
        private readonly AstNode _body;

        public CaptureNode(string variableName, AstNode body, SourceLocation location) : base(location)
        {
            _variableName = variableName;
            _body = body;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            var result = _body.Evaluate(context);
            try
            {
                context.DefineVariable(_variableName, result);
            }
            catch (InnerEvaluationException ex)
            {
                throw new TemplateEvaluationException(ex.Message, context, this);
            }
            return new Value(new StringValue(string.Empty)); // Capture doesn't output anything directly
        }

        public override string Name()
        {
            return "<capture>";
        }

        public override string ToStackString()
        {
            return $"<capture {_variableName}>";
        }

        public override string ToString()
        {
            return $"CaptureNode(variableName=\"{_variableName}\", body={_body.ToString()})";
        }
    }
}
