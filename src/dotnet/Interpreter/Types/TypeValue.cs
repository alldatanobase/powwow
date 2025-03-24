namespace PowwowLang.Types
{
    public class TypeValue : Box
    {
        public TypeValue(ValueType value) : base(value, ValueType.Type) { }

        public ValueType Value()
        {
            return _value;
        }

        public override string Output()
        {
            return $"type<{(ValueType)_value}>";
        }

        public override string JsonSerialize()
        {
            return $"\"type<{(ValueType)_value}>\"";
        }
    }
}
