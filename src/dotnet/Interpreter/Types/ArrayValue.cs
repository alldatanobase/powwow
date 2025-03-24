using System.Collections.Generic;
using System.Linq;

namespace PowwowLang.Types
{
    public class ArrayValue : Box
    {
        public ArrayValue(IEnumerable<Value> value) : base(value, ValueType.Array) { }

        public IEnumerable<Value> Value()
        {
            return _value;
        }

        public override string Output()
        {
            IEnumerable<Value> value = (IEnumerable<Value>)_value;
            return string.Concat("[", string.Join(", ", value.Select(item => item.Output())), "]");
        }

        public override string JsonSerialize()
        {
            IEnumerable<Value> value = (IEnumerable<Value>)_value;
            return string.Concat("[", string.Join(",", value.Select(item => item.JsonSerialize())), "]");
        }
    }
}
