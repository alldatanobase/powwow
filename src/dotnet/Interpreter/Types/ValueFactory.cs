using PowwowLang.Exceptions;
using System;
using System.Collections.Generic;

namespace PowwowLang.Types
{
    public static class ValueFactory
    {
        public static Value Create(dynamic value)
        {
            if (value is IDictionary<string, dynamic> dict)
            {
                IDictionary<string, Value> valueObj = new Dictionary<string, Value>();
                foreach (var key in dict.Keys)
                {
                    valueObj[key] = ValueFactory.Create(dict[key]);
                }
                return new Value(new ObjectValue(valueObj));
            }
            else if (value is IEnumerable<dynamic> ||
                value is IEnumerable<decimal> ||
                value is IEnumerable<int> ||
                value is IEnumerable<long> ||
                value is IEnumerable<double> ||
                value is IEnumerable<float> ||
                value is IEnumerable<byte> ||
                value is IEnumerable<sbyte> ||
                value is IEnumerable<short> ||
                value is IEnumerable<ushort> ||
                value is IEnumerable<uint> ||
                value is IEnumerable<string> ||
                value is IEnumerable<bool> ||
                value is IEnumerable<DateTime> ||
                value is IEnumerable<IDictionary<string, dynamic>>)
            {
                var arr = new List<Value>();
                foreach (var item in value)
                {
                    arr.Add(ValueFactory.Create(item));
                }
                return new Value(new ArrayValue(arr));
            }
            else if (value is string || value is char)
            {
                return new Value(new StringValue(value.ToString()));
            }
            else if (TypeHelper.IsConvertibleToDecimal(value))
            {
                return new Value(new NumberValue(Convert.ToDecimal(value)));
            }
            else if (value is bool)
            {
                return new Value(new BooleanValue(value));
            }
            else if (value is DateTime)
            {
                return new Value(new DateTimeValue(value));
            }
            else if (value is ValueType)
            {
                return new Value(new TypeValue(value));
            }
            else if (value is object)
            {
                IDictionary<string, Value> valueObj = new Dictionary<string, Value>();
                var properties = value.GetType().GetProperties();
                foreach (var property in properties)
                {
                    valueObj[property.Name] = ValueFactory.Create(property.GetValue(value));
                }
                return new Value(new ObjectValue(valueObj));
            }
            else
            {
                throw new InitializationException("Unable to resolve initial data object as a dynamically typed language object. Encountered an unxpected type.");
            }
        }
    }
}
