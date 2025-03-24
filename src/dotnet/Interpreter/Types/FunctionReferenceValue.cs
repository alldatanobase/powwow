namespace PowwowLang.Types
{
    public class FunctionReferenceValue : Box
    {
        public string Name { get; }

        public FunctionReferenceValue(string name) : base(name, ValueType.Function)
        {
            Name = name;
        }

        public override string Output()
        {
            return $"func<{Name}>";
        }

        public override string JsonSerialize()
        {
            return $"\"func<{Name}>\"";
        }
    }
}
