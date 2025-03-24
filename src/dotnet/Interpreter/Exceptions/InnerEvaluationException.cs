using System;

namespace PowwowLang.Exceptions
{
    public class InnerEvaluationException : Exception
    {
        public InnerEvaluationException(string message) : base(message) { }
    }
}
