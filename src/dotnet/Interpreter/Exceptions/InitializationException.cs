using System;

namespace PowwowLang.Exceptions
{
    public class InitializationException : Exception
    {
        public string Descriptor { get; }

        public InitializationException(string message)
            : base($"Error during initialization: {message}")
        {
            Descriptor = message;
        }
    }
}
