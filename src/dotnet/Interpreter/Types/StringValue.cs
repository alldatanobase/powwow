using System.Web.Script.Serialization;

namespace PowwowLang.Types
{
    public class StringValue : Box
    {
        public StringValue(string value) : base(value, ValueType.String) { }

        public string Value()
        {
            return _value;
        }

        public override string Output()
        {
            return _value as string;
        }

        public override string JsonSerialize()
        {
            return new JavaScriptSerializer().Serialize((string)_value);
        }
    }
}
