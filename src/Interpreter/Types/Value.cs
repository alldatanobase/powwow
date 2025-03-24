using PowwowLang.Exceptions;

namespace PowwowLang.Types
{
    public class Value
    {
        private Box _box;

        public Value(Box box)
        {
            _box = box;
        }

        public Box ValueOf()
        {
            return _box;
        }

        public void Mutate(Box value)
        {
            _box = value;
        }

        public ValueType TypeOf()
        {
            return _box.TypeOf();
        }

        public override string ToString()
        {
            return _box.ToString();
        }

        public string Output()
        {
            return _box.Output();
        }

        public string JsonSerialize()
        {
            return _box.JsonSerialize();
        }

        public bool ExpectType(ValueType type)
        {
            if (_box.TypeOf() == type)
            {
                return true;
            }
            else
            {
                throw new InnerEvaluationException($"Expected type {type} but found {_box.TypeOf()}");
            }
        }
    }
}
