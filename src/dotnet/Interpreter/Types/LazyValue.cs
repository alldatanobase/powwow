using PowwowLang.Ast;
using PowwowLang.Env;

namespace PowwowLang.Types
{
    public class LazyValue : Box
    {
        private readonly AstNode _expression;
        private readonly ExecutionContext _capturedContext;
        private bool _isEvaluated;

        public LazyValue(AstNode expression, ExecutionContext context) : base(expression, ValueType.Lazy)
        {
            _expression = expression;
            _capturedContext = context;
            _isEvaluated = false;
        }

        public Value Evaluate()
        {
            if (!_isEvaluated)
            {
                _value = _expression.Evaluate(_capturedContext);
                _isEvaluated = true;
            }
            return _value;
        }

        public override string Output()
        {
            if (_isEvaluated)
            {
                return ((Value)_value).Output();
            }
            else
            {
                return ($"lazy<{_expression.ToStackString()}>");
            }
        }

        public override string JsonSerialize()
        {
            if (_isEvaluated)
            {
                return ((Value)_value).Output();
            }
            else
            {
                return ($"\"lazy<{_expression.ToStackString()}>\"");
            }
        }
    }
}
