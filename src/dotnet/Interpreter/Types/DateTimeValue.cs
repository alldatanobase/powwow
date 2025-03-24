using System;

namespace PowwowLang.Types
{
    public class DateTimeValue : Box
    {
        public DateTimeValue(DateTime value) : base(value, ValueType.DateTime) { }

        public override string Output()
        {
            return ((DateTime)_value).ToString("o"); // ISO 8601 format
        }

        public DateTime Value()
        {
            return _value;
        }

        public override string JsonSerialize()
        {
            return $"\"{_value.ToString("o")}\""; // ISO 8601 format
        }
    }
}
