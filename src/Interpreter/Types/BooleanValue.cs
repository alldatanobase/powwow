namespace PowwowLang.Types
{
    public class BooleanValue : Box
    {
        public BooleanValue(bool value) : base(value, ValueType.Boolean) { }

        public override string Output()
        {
            return (bool)_value ? "true" : "false";
        }

        public bool Value()
        {
            return _value;
        }

        public override string JsonSerialize()
        {
            return (bool)_value ? "true" : "false";
        }
    }
}
