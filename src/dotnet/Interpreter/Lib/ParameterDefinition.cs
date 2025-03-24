using System;

namespace PowwowLang.Lib
{
    public class ParameterDefinition
    {
        public Type Type { get; }
        public bool IsOptional { get; }
        public dynamic DefaultValue { get; }

        public ParameterDefinition(Type type, bool isOptional = false, dynamic defaultValue = null)
        {
            Type = type;
            IsOptional = isOptional;
            DefaultValue = defaultValue;
        }
    }
}
