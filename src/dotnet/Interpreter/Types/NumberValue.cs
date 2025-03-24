namespace PowwowLang.Types
{
    public class NumberValue : Box
    {
        public NumberValue(decimal value) : base(value, ValueType.Number) { }

        public decimal Value()
        {
            return _value;
        }

        public override string Output()
        {
            return ((decimal)_value).ToString();
        }

        public override string JsonSerialize()
        {
            return ((decimal)_value).ToString();
        }
    }
}
