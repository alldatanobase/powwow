using System.Collections.Generic;
using System.Linq;

namespace PowwowLang.Types
{
    public class ObjectValue : Box
    {
        public ObjectValue(IDictionary<string, Value> value) : base(value, ValueType.Object) { }

        public IDictionary<string, Value> Value()
        {
            return _value;
        }

        public override string Output()
        {
            IDictionary<string, Value> dict = (IDictionary<string, Value>)_value;
            return string.Concat("{",
                string.Join(", ", dict.Keys.Select(key => string.Concat(key, ": ", (dict[key].Output())))), "}");
        }

        public override string JsonSerialize()
        {
            var dict = (IDictionary<string, Value>)_value;
            return string.Concat("{",
                string.Join(",", dict.Keys.Select(key => string.Concat("\"", key, "\"", ":", dict[key].JsonSerialize()))), "}");
        }
    }
}
