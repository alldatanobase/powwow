namespace PowwowLang.Types
{
    public abstract class Box
    {
        protected dynamic _value;
        private ValueType _type;

        public Box(dynamic value, ValueType type)
        {
            _value = value;
            _type = type;
        }

        public dynamic Unbox()
        {
            return _value;
        }

        public ValueType TypeOf()
        {
            return _type;
        }

        public override string ToString()
        {
            return _value.ToString();
        }

        public abstract string Output();

        public abstract string JsonSerialize();
    }
}
