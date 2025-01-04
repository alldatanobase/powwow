using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;

namespace TemplateInterpreter
{
    public class Tester
    {
        static void Main(string[] args)
        {
            // Create the interpreter
            var interpreter = new TemplateInterpreter.Interpreter();

            //            // Example 1: Complex arithmetic with parentheses
            //            var template1 = "Result: {{(5 * (var2 + 1.5)) / 2}}";
            //            var data1 = new ExpandoObject();
            //            ((IDictionary<string, object>)data1).Add("var2", 2.5);
            //            // Result: 10 (calculates (5 * (2.5 + 1.5)) / 2 = (5 * 4) / 2 = 20 / 2 = 10)
            //            Console.WriteLine(interpreter.Interpret(template1, data1));

            //            // Example 2: Complex boolean logic with parentheses
            //            var template2 = "Is valid: {{!(var1 = \"test\" && (var2 > 2 || var3.x < 0))}}";
            //            var data2 = new ExpandoObject();
            //            var data2Dict = (IDictionary<string, object>)data2;
            //            data2Dict.Add("var1", "test");
            //            data2Dict.Add("var2", 2.5);
            //            var nested = new ExpandoObject();
            //            ((IDictionary<string, object>)nested).Add("x", 1.5);
            //            data2Dict.Add("var3", nested);
            //            // Is valid: false (negation of (true && (true || false)) = negation of true = false)
            //            Console.WriteLine(interpreter.Interpret(template2, data2));

            //            // Example 3: Nested if statements with complex conditions
            //            var template3 = @"
            //{{#if var1 = ""test""}}
            //    Outer if true
            //    {{#if var2 * 2 >= 5}}
            //        Inner if also true
            //    {{#else}}
            //        Inner if false
            //    {{/if}}
            //{{#else}}
            //    Outer if false
            //{{/if}}";
            //            // Output: Outer if true\n    Inner if true
            //            Console.WriteLine(interpreter.Interpret(template3, data2));

            //            // Example 4: Each statement with complex condition inside
            //            var template4 = @"
            //Users with high scores:
            //{{#each user in users}}
            //    {{#if user.score > 80 && (user.age < 18 || user.region = ""EU"")}}
            //        {{user.name}} ({{user.score}})
            //    {{/if}}
            //{{/each}}";

            //            var data4 = new ExpandoObject();
            //            var usersList = new List<ExpandoObject>();
            //            // Create user 1
            //            var user1 = new ExpandoObject();
            //            var user1Dict = (IDictionary<string, object>)user1;
            //            user1Dict.Add("name", "Alice");
            //            user1Dict.Add("score", 90);
            //            user1Dict.Add("age", 18);
            //            user1Dict.Add("region", "US");
            //            usersList.Add(user1);
            //            // Create user 2
            //            var user2 = new ExpandoObject();
            //            var user2Dict = (IDictionary<string, object>)user2;
            //            user2Dict.Add("name", "Bob");
            //            user2Dict.Add("score", 85);
            //            user2Dict.Add("age", 25);
            //            user2Dict.Add("region", "EU");
            //            usersList.Add(user2);
            //            // Add users to data
            //            ((IDictionary<string, object>)data4).Add("users", usersList);
            //            // Output: Users with high scores:\n    Alice (90)\n    Bob (85)
            //            Console.WriteLine(interpreter.Interpret(template4, data4));

            ////Example 5: Complex mathematical and logical expressions combined
            //var template5 = @"
            //Math result: {{ (var1 * 2 + var2) / var3.x }}
            //Logic result: {{ ((var1 > var2) = (var3.x < 5)) && !(var2 = 2.5) }}
            //";
            //var data5 = new ExpandoObject();
            //var data5Dict = (IDictionary<string, object>)data5;
            //data5Dict.Add("var1", 3);
            //data5Dict.Add("var2", 2.5);
            //var nested5 = new ExpandoObject();
            //((IDictionary<string, object>)nested5).Add("x", 2);
            //data5Dict.Add("var3", nested5);
            ///* Output:
            //Math result: 4.25 ((3 * 2 + 2.5) / 2 = 8.5 / 2 = 4.25)
            //Logic result: false (((3 > 2.5) = (2 < 5)) && !(2.5 = 2.5) = (true = true) && !true = true && false = false)
            //*/
            //Console.WriteLine(interpreter.Interpret(template5, data5));

            //// Example 6: Nested each statements with conditionals
            //var template6 = @"
            //{{#each department in departments}}
            //Department: {{department.name}}
            //    {{#each employee in department.employees}}
            //        {{#if employee.salary > department.avgSalary && !employee.isTemp}}
            //            {{employee.name}} (Senior)
            //        {{#elseif employee.salary > department.avgSalary * 0.8}}
            //            {{employee.name}} (Mid-level)
            //        {{#else}}
            //            {{employee.name}} (Junior)
            //        {{/if}}
            //    {{/each}}
            //{{/each}}";

            //var data6 = new ExpandoObject();
            //var deptList = new List<ExpandoObject>();
            //// Create IT department
            //var itDept = new ExpandoObject();
            //var itDeptDict = (IDictionary<string, object>)itDept;
            //itDeptDict.Add("name", "IT");
            //itDeptDict.Add("avgSalary", 75000);
            //var itEmployees = new List<ExpandoObject>();
            //// Add IT employees
            //var emp1 = new ExpandoObject();
            //((IDictionary<string, object>)emp1).Add("name", "John");
            //((IDictionary<string, object>)emp1).Add("salary", 80000);
            //((IDictionary<string, object>)emp1).Add("isTemp", false);
            //itEmployees.Add(emp1);
            //var emp2 = new ExpandoObject();
            //((IDictionary<string, object>)emp2).Add("name", "Jane");
            //((IDictionary<string, object>)emp2).Add("salary", 65000);
            //((IDictionary<string, object>)emp2).Add("isTemp", false);
            //itEmployees.Add(emp2);
            //itDeptDict.Add("employees", itEmployees);
            //deptList.Add(itDept);
            //((IDictionary<string, object>)data6).Add("departments", deptList);
            ///* Output:
            //Department: IT
            //    John (Senior)
            //    Jane (Mid-level)
            //*/
            //Console.WriteLine(interpreter.Interpret(template6, data6));

            //// Example 7: Arbitrarily deep variable path reference
            //var template7 = @"{{var1.foo.bar.baz}}";
            //var data7 = new ExpandoObject();
            //var data7Nested = new ExpandoObject();
            //var data7DoubleNested = new ExpandoObject();
            //var data7TripleNested = new ExpandoObject();
            //((IDictionary<string, object>)data7TripleNested).Add("baz", "hello world");
            //((IDictionary<string, object>)data7DoubleNested).Add("bar", data7TripleNested);
            //((IDictionary<string, object>)data7Nested).Add("foo", data7DoubleNested);
            //((IDictionary<string, object>)data7).Add("var1", data7Nested);
            //Console.WriteLine(interpreter.Interpret(template7, data7));

            //// Example 8: Call non-existent function
            //var template8 = @"Here is a function: {{myfunction()}}";
            //var data8 = new ExpandoObject();
            //((IDictionary<string, object>)data8).Add("var1", "foo");
            //// Expect this to throw an error
            //Console.WriteLine(interpreter.Interpret(template8, data8));

            // Example 9: Call length function
            var template9 = @"{{#if length(myArray) > 0}}has elements{{#else}}no elements{{/if}}";
            var data9notarray = new ExpandoObject();
            ((IDictionary<string, object>)data9notarray).Add("myArray", 2.5);
            var data9 = new ExpandoObject();
            var itEmployees = new List<ExpandoObject>();
            var emp1 = new ExpandoObject();
            ((IDictionary<string, object>)emp1).Add("name", "John");
            ((IDictionary<string, object>)emp1).Add("salary", 80000);
            ((IDictionary<string, object>)emp1).Add("isTemp", false);
            itEmployees.Add(emp1);
            var emp2 = new ExpandoObject();
            ((IDictionary<string, object>)emp2).Add("name", "Jane");
            ((IDictionary<string, object>)emp2).Add("salary", 65000);
            ((IDictionary<string, object>)emp2).Add("isTemp", false);
            itEmployees.Add(emp2);
            ((IDictionary<string, object>)data9).Add("myArray", itEmployees);
            Console.WriteLine(interpreter.Interpret(template9, data9notarray));
        }
    }
    public class Interpreter
    {
        private readonly Lexer _lexer;
        private readonly Parser _parser;
        private readonly FunctionRegistry _functionRegistry;

        public Interpreter()
        {
            _lexer = new Lexer();
            _parser = new Parser();
            _functionRegistry = new FunctionRegistry();
        }

        public void RegisterFunction(string name, List<Type> parameterTypes, Func<List<dynamic>, dynamic> implementation)
        {
            _functionRegistry.Register(name, parameterTypes, implementation);
        }

        public string Interpret(string template, dynamic data)
        {
            var tokens = _lexer.Tokenize(template);
            var ast = _parser.Parse(tokens);
            return ast.Evaluate(new ExecutionContext(data, _functionRegistry));
        }
    }

    public class ExecutionContext
    {
        private readonly dynamic _data;
        private readonly Dictionary<string, dynamic> _iteratorValues;
        private readonly FunctionRegistry _functionRegistry;

        public ExecutionContext(dynamic data, FunctionRegistry functionRegistry)
        {
            _data = data;
            _iteratorValues = new Dictionary<string, dynamic>();
            _functionRegistry = functionRegistry;
        }

        public ExecutionContext CreateIteratorContext(string iteratorName, dynamic value)
        {
            var newContext = new ExecutionContext(_data, _functionRegistry);
            newContext._iteratorValues.Add(iteratorName, value);
            foreach (var key in _iteratorValues.Keys)
            {
                newContext._iteratorValues.Add(key, _iteratorValues[key]);
            }
            return newContext;
        }

        public FunctionRegistry GetFunctionRegistry()
        {
            return _functionRegistry;
        }

        public dynamic ResolveValue(string path)
        {
            var parts = path.Split('.');
            dynamic current = null;

            // Check if the first part is an iterator
            if (_iteratorValues.ContainsKey(parts[0]))
            {
                current = _iteratorValues[parts[0]];
                parts = parts.Skip(1).ToArray();
            }
            else
            {
                current = _data;
            }

            foreach (var part in parts)
            {
                try
                {
                    current = ((IDictionary<string, object>)current)[part];
                    if (current.GetType() == typeof(int) ||
                        current.GetType() == typeof(double) ||
                        current.GetType() == typeof(float))
                    {
                        current = (decimal)current;
                    }
                }
                catch
                {
                    throw new Exception(string.Format("Unable to resolve path: {0}", path));
                }
            }

            return current;
        }
    }

    public class Token
    {
        public TokenType Type { get; private set; }
        public string Value { get; private set; }
        public int Position { get; private set; }

        public Token(TokenType type, string value, int position)
        {
            Type = type;
            Value = value;
            Position = position;
        }
    }

    public enum TokenType
    {
        Text,
        DirectiveStart,      // {{
        DirectiveEnd,        // }}
        Variable,           // alphanumeric+dots
        String,            // "..."
        Number,            // decimal
        True,              // true
        False,             // false
        Not,               // !
        Equal,             // =
        NotEqual,          // !=
        LessThan,          // <
        LessThanEqual,     // <=
        GreaterThan,       // >
        GreaterThanEqual,  // >=
        And,               // &&
        Or,                // ||
        Plus,              // +
        Minus,             // -
        Multiply,          // *
        Divide,            // /
        LeftParen,         // (
        RightParen,        // )
        Each,              // #each
        In,                // in
        If,                // #if
        ElseIf,            // #elseif
        Else,              // #else
        EndEach,           // /each
        EndIf,             // /if
        Function,          // function name
        Comma              // ,
    }

    public class Lexer
    {
        private string _input;
        private int _position;
        private readonly List<Token> _tokens;

        public Lexer()
        {
            _tokens = new List<Token>();
        }

        public IReadOnlyList<Token> Tokenize(string input)
        {
            _input = input;
            _position = 0;
            _tokens.Clear();

            while (_position < _input.Length)
            {
                if (TryMatch("{{"))
                {
                    _tokens.Add(new Token(TokenType.DirectiveStart, "{{", _position));
                    _position += 2;
                    TokenizeDirective();
                }
                else
                {
                    TokenizeText();
                }
            }

            return _tokens;
        }

        private void TokenizeDirective()
        {
            while (_position < _input.Length)
            {
                SkipWhitespace();

                if (TryMatch("}}"))
                {
                    _tokens.Add(new Token(TokenType.DirectiveEnd, "}}", _position));
                    _position += 2;
                    return;
                }

                // Add check for comma
                if (TryMatch(","))
                {
                    _tokens.Add(new Token(TokenType.Comma, ",", _position));
                    _position++;
                    continue;
                }

                // Check for function names before other identifiers
                if (char.IsLetter(_input[_position]))
                {
                    var start = _position;
                    while (_position < _input.Length && char.IsLetter(_input[_position]))
                    {
                        _position++;
                    }

                    var value = _input.Substring(start, _position - start);

                    // Look ahead for opening parenthesis to distinguish functions from variables
                    SkipWhitespace();
                    if (_position < _input.Length && _input[_position] == '(')
                    {
                        _tokens.Add(new Token(TokenType.Function, value, start));
                        continue;
                    }
                    else
                    {
                        // Rewind position as this is not a function
                        _position = start;
                    }
                }

                // Match keywords and operators
                if (TryMatch("#each"))
                {
                    _tokens.Add(new Token(TokenType.Each, "#each", _position));
                    _position += 5;
                }
                else if (TryMatch("in"))
                {
                    _tokens.Add(new Token(TokenType.In, "in", _position));
                    _position += 2;
                }
                else if (TryMatch("#if"))
                {
                    _tokens.Add(new Token(TokenType.If, "#if", _position));
                    _position += 3;
                }
                else if (TryMatch("#elseif"))
                {
                    _tokens.Add(new Token(TokenType.ElseIf, "#elseif", _position));
                    _position += 7;
                }
                else if (TryMatch("#else"))
                {
                    _tokens.Add(new Token(TokenType.Else, "#else", _position));
                    _position += 5;
                }
                else if (TryMatch("/each"))
                {
                    _tokens.Add(new Token(TokenType.EndEach, "/each", _position));
                    _position += 5;
                }
                else if (TryMatch("/if"))
                {
                    _tokens.Add(new Token(TokenType.EndIf, "/if", _position));
                    _position += 3;
                }
                else if (TryMatch(">="))
                {
                    _tokens.Add(new Token(TokenType.GreaterThanEqual, ">=", _position));
                    _position += 2;
                }
                else if (TryMatch("<="))
                {
                    _tokens.Add(new Token(TokenType.LessThanEqual, "<=", _position));
                    _position += 2;
                }
                else if (TryMatch("!="))
                {
                    _tokens.Add(new Token(TokenType.NotEqual, "!=", _position));
                    _position += 2;
                }
                else if (TryMatch("&&"))
                {
                    _tokens.Add(new Token(TokenType.And, "&&", _position));
                    _position += 2;
                }
                else if (TryMatch("||"))
                {
                    _tokens.Add(new Token(TokenType.Or, "||", _position));
                    _position += 2;
                }
                else if (TryMatch(">"))
                {
                    _tokens.Add(new Token(TokenType.GreaterThan, ">", _position));
                    _position++;
                }
                else if (TryMatch("<"))
                {
                    _tokens.Add(new Token(TokenType.LessThan, "<", _position));
                    _position++;
                }
                else if (TryMatch("="))
                {
                    _tokens.Add(new Token(TokenType.Equal, "=", _position));
                    _position++;
                }
                else if (TryMatch("!"))
                {
                    _tokens.Add(new Token(TokenType.Not, "!", _position));
                    _position++;
                }
                else if (TryMatch("+"))
                {
                    _tokens.Add(new Token(TokenType.Plus, "+", _position));
                    _position++;
                }
                else if (TryMatch("-"))
                {
                    _tokens.Add(new Token(TokenType.Minus, "-", _position));
                    _position++;
                }
                else if (TryMatch("*"))
                {
                    _tokens.Add(new Token(TokenType.Multiply, "*", _position));
                    _position++;
                }
                else if (TryMatch("/"))
                {
                    _tokens.Add(new Token(TokenType.Divide, "/", _position));
                    _position++;
                }
                else if (TryMatch("("))
                {
                    _tokens.Add(new Token(TokenType.LeftParen, "(", _position));
                    _position++;
                }
                else if (TryMatch(")"))
                {
                    _tokens.Add(new Token(TokenType.RightParen, ")", _position));
                    _position++;
                }
                else if (TryMatch("\""))
                {
                    TokenizeString();
                }
                else if (char.IsDigit(_input[_position]) || (_input[_position] == '-' && char.IsDigit(PeekNext())))
                {
                    TokenizeNumber();
                }
                else if (char.IsLetter(_input[_position]) || _input[_position] == '_')
                {
                    TokenizeIdentifier();
                }
                else
                {
                    throw new Exception(string.Format("Unexpected character at position {0}: {1}", _position, _input[_position]));
                }
            }
        }

        private void TokenizeText()
        {
            var start = _position;
            while (_position < _input.Length && !TryMatch("{{"))
            {
                _position++;
            }
            if (_position > start)
            {
                _tokens.Add(new Token(TokenType.Text, _input.Substring(start, _position - start), start));
            }
        }

        private void TokenizeString()
        {
            _position++; // Skip opening quote
            var start = _position;
            while (_position < _input.Length && _input[_position] != '"')
            {
                if (_input[_position] == '\\' && PeekNext() == '"')
                {
                    _position += 2;
                }
                else
                {
                    _position++;
                }
            }
            if (_position >= _input.Length)
            {
                throw new Exception("Unterminated string literal");
            }
            var value = _input.Substring(start, _position - start);
            _tokens.Add(new Token(TokenType.String, value, start - 1));
            _position++; // Skip closing quote
        }

        private void TokenizeNumber()
        {
            var start = _position;
            bool hasDecimal = false;

            if (_input[_position] == '-')
            {
                _position++;
            }

            while (_position < _input.Length &&
                   (char.IsDigit(_input[_position]) ||
                    (!hasDecimal && _input[_position] == '.')))
            {
                if (_input[_position] == '.')
                {
                    hasDecimal = true;
                }
                _position++;
            }

            var value = _input.Substring(start, _position - start);
            _tokens.Add(new Token(TokenType.Number, value, start));
        }

        private void TokenizeIdentifier()
        {
            var start = _position;
            while (_position < _input.Length &&
                   (char.IsLetterOrDigit(_input[_position]) ||
                    _input[_position] == '_' ||
                    _input[_position] == '.'))
            {
                _position++;
            }

            var value = _input.Substring(start, _position - start);
            switch (value)
            {
                case "true":
                    _tokens.Add(new Token(TokenType.True, value, start));
                    break;
                case "false":
                    _tokens.Add(new Token(TokenType.False, value, start));
                    break;
                default:
                    _tokens.Add(new Token(TokenType.Variable, value, start));
                    break;
            }
        }

        private void SkipWhitespace()
        {
            while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
            {
                _position++;
            }
        }

        private bool TryMatch(string pattern)
        {
            if (_position + pattern.Length > _input.Length)
            {
                return false;
            }

            return _input.Substring(_position, pattern.Length) == pattern;
        }

        private char PeekNext()
        {
            return _position + 1 < _input.Length ? _input[_position + 1] : '\0';
        }
    }

    public abstract class AstNode
    {
        public abstract dynamic Evaluate(ExecutionContext context);
    }

    public class TextNode : AstNode
    {
        private readonly string _text;

        public TextNode(string text)
        {
            _text = text;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _text;
        }
    }

    public class FunctionNode : AstNode
    {
        private readonly string _name;
        private readonly List<AstNode> _arguments;

        public FunctionNode(string name, List<AstNode> arguments)
        {
            _name = name;
            _arguments = arguments;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            // Evaluate all arguments first
            var evaluatedArgs = _arguments.Select(arg => arg.Evaluate(context)).ToList();

            // Get function registry
            var registry = context.GetFunctionRegistry();

            // Try to get function definition
            if (!registry.TryGetFunction(_name, out var function))
            {
                throw new Exception($"Function '{_name}' is not registered");
            }

            // Validate arguments
            registry.ValidateArguments(_name, evaluatedArgs);

            // Execute function
            return function.Implementation(evaluatedArgs);
        }
    }

    public class VariableNode : AstNode
    {
        private readonly string _path;

        public VariableNode(string path)
        {
            _path = path;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return context.ResolveValue(_path);
        }
    }

    public class StringNode : AstNode
    {
        private readonly string _value;

        public StringNode(string value)
        {
            _value = value;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _value;
        }
    }

    public class NumberNode : AstNode
    {
        private readonly decimal _value;

        public NumberNode(string value)
        {
            _value = decimal.Parse(value);
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _value;
        }
    }

    public class BooleanNode : AstNode
    {
        private readonly bool _value;

        public BooleanNode(bool value)
        {
            _value = value;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _value;
        }
    }

    public class UnaryNode : AstNode
    {
        private readonly TokenType _operator;
        private readonly AstNode _expression;

        public UnaryNode(TokenType op, AstNode expression)
        {
            _operator = op;
            _expression = expression;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            var value = _expression.Evaluate(context);

            switch (_operator)
            {
                case TokenType.Not:
                    return !Convert.ToBoolean(value);
                default:
                    throw new Exception(string.Format("Unknown unary operator: {0}", _operator));
            }
        }
    }

    public class BinaryNode : AstNode
    {
        private readonly TokenType _operator;
        private readonly AstNode _left;
        private readonly AstNode _right;

        public BinaryNode(TokenType op, AstNode left, AstNode right)
        {
            _operator = op;
            _left = left;
            _right = right;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            var left = _left.Evaluate(context);
            var right = _right.Evaluate(context);

            switch (_operator)
            {
                case TokenType.Plus:
                    return Convert.ToDecimal(left) + Convert.ToDecimal(right);
                case TokenType.Minus:
                    return Convert.ToDecimal(left) - Convert.ToDecimal(right);
                case TokenType.Multiply:
                    return Convert.ToDecimal(left) * Convert.ToDecimal(right);
                case TokenType.Divide:
                    return Convert.ToDecimal(left) / Convert.ToDecimal(right);
                case TokenType.Equal:
                    return Equals(left, right);
                case TokenType.NotEqual:
                    return !Equals(left, right);
                case TokenType.LessThan:
                    return Convert.ToDecimal(left) < Convert.ToDecimal(right);
                case TokenType.LessThanEqual:
                    return Convert.ToDecimal(left) <= Convert.ToDecimal(right);
                case TokenType.GreaterThan:
                    return Convert.ToDecimal(left) > Convert.ToDecimal(right);
                case TokenType.GreaterThanEqual:
                    return Convert.ToDecimal(left) >= Convert.ToDecimal(right);
                case TokenType.And:
                    return Convert.ToBoolean(left) && Convert.ToBoolean(right);
                case TokenType.Or:
                    return Convert.ToBoolean(left) || Convert.ToBoolean(right);
                default:
                    throw new Exception(string.Format("Unknown binary operator: {0}", _operator));
            }
        }
    }

    public class EachNode : AstNode
    {
        private readonly string _iteratorName;
        private readonly AstNode _collection;
        private readonly AstNode _body;

        public EachNode(string iteratorName, AstNode collection, AstNode body)
        {
            _iteratorName = iteratorName;
            _collection = collection;
            _body = body;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            var collection = _collection.Evaluate(context);
            if (!(collection is System.Collections.IEnumerable))
            {
                throw new Exception("Each statement requires an enumerable collection");
            }

            var result = new StringBuilder();
            foreach (var item in collection)
            {
                var iterationContext = context.CreateIteratorContext(_iteratorName, item);
                result.Append(_body.Evaluate(iterationContext));
            }

            return result.ToString();
        }
    }

    public class IfNode : AstNode
    {
        private readonly List<IfBranch> _conditionalBranches;
        private readonly AstNode _elseBranch;

        public class IfBranch
        {
            public AstNode Condition { get; private set; }
            public AstNode Body { get; private set; }

            public IfBranch(AstNode condition, AstNode body)
            {
                Condition = condition;
                Body = body;
            }
        }

        public IfNode(List<IfBranch> conditionalBranches, AstNode elseBranch)
        {
            _conditionalBranches = conditionalBranches;
            _elseBranch = elseBranch;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            foreach (var branch in _conditionalBranches)
            {
                if (Convert.ToBoolean(branch.Condition.Evaluate(context)))
                {
                    return branch.Body.Evaluate(context);
                }
            }

            if (_elseBranch != null)
            {
                return _elseBranch.Evaluate(context);
            }

            return string.Empty;
        }
    }

    public class TemplateNode : AstNode
    {
        private readonly List<AstNode> _children;

        public TemplateNode(List<AstNode> children)
        {
            _children = children;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            var result = new StringBuilder();
            foreach (var child in _children)
            {
                result.Append(child.Evaluate(context));
            }
            return result.ToString();
        }
    }

    public class Parser
    {
        private IReadOnlyList<Token> _tokens;
        private int _position;

        public AstNode Parse(IReadOnlyList<Token> tokens)
        {
            _tokens = tokens;
            _position = 0;
            return ParseTemplate();
        }

        private AstNode ParseTemplate()
        {
            var nodes = new List<AstNode>();

            while (_position < _tokens.Count)
            {
                var token = Current();

                if (token.Type == TokenType.Text)
                {
                    nodes.Add(new TextNode(token.Value));
                    Advance();
                }
                else if (token.Type == TokenType.DirectiveStart)
                {
                    // Look at the next token to determine what kind of directive we're dealing with
                    var nextToken = _tokens[_position + 1];
                    if (nextToken.Type == TokenType.If)
                    {
                        nodes.Add(ParseIfStatement());
                    }
                    else if (nextToken.Type == TokenType.Each)
                    {
                        nodes.Add(ParseEachStatement());
                    }
                    else if (nextToken.Type == TokenType.ElseIf ||
                             nextToken.Type == TokenType.Else ||
                             nextToken.Type == TokenType.EndIf ||
                             nextToken.Type == TokenType.EndEach)
                    {
                        // We've hit a closing directive - return control to the parent parser
                        break;
                    }
                    else
                    {
                        nodes.Add(ParseExpressionStatement());
                    }
                }
                else
                {
                    throw new Exception(string.Format("Unexpected token: {0} at position {1}", token.Type, token.Position));
                }
            }

            return new TemplateNode(nodes);
        }

        private AstNode ParseExpressionStatement()
        {
            Advance(); // Skip {{
            var expression = ParseExpression();
            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}
            return expression;
        }

        private AstNode ParseFunctionCall()
        {
            var functionName = Current().Value;
            Advance(); // Move past function name

            Expect(TokenType.LeftParen);
            Advance(); // Move past left paren

            var arguments = new List<AstNode>();

            // Parse arguments
            if (Current().Type != TokenType.RightParen)
            {
                while (true)
                {
                    arguments.Add(ParseExpression());

                    if (Current().Type == TokenType.RightParen)
                        break;

                    Expect(TokenType.Comma);
                    Advance(); // Move past comma
                }
            }

            Expect(TokenType.RightParen);
            Advance(); // Move past right paren

            return new FunctionNode(functionName, arguments);
        }

        private AstNode ParseIfStatement()
        {
            var conditionalBranches = new List<IfNode.IfBranch>();
            AstNode elseBranch = null;

            // Parse initial if
            Advance(); // Skip {{
            Advance(); // Skip #if
            var condition = ParseExpression();
            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            var body = ParseTemplate();
            conditionalBranches.Add(new IfNode.IfBranch(condition, body));

            // Parse any elseif/else clauses
            while (_position < _tokens.Count && Current().Type == TokenType.DirectiveStart)
            {
                var token = _tokens[_position + 1]; // Look at the directive type

                if (token.Type == TokenType.ElseIf)
                {
                    Advance(); // Skip {{
                    Advance(); // Skip #elseif
                    condition = ParseExpression();
                    Expect(TokenType.DirectiveEnd);
                    Advance(); // Skip }}
                    body = ParseTemplate();
                    conditionalBranches.Add(new IfNode.IfBranch(condition, body));
                }
                else if (token.Type == TokenType.Else)
                {
                    Advance(); // Skip {{
                    Advance(); // Skip #else
                    Expect(TokenType.DirectiveEnd);
                    Advance(); // Skip }}
                    elseBranch = ParseTemplate();
                }
                else if (token.Type == TokenType.EndIf)
                {
                    Advance(); // Skip {{
                    Advance(); // Skip /if
                    Expect(TokenType.DirectiveEnd);
                    Advance(); // Skip }}
                    break;
                }
                else
                {
                    // This is not an if-related token, so it must be the start of
                    // nested content - let ParseTemplate handle it
                    break;
                }
            }

            return new IfNode(conditionalBranches, elseBranch);
        }

        private AstNode ParseEachStatement()
        {
            Advance(); // Skip {{
            Advance(); // Skip #each
            var iteratorName = Expect(TokenType.Variable).Value;
            Advance();

            Expect(TokenType.In);
            Advance();

            var collection = ParseExpression();
            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            var body = ParseTemplate();

            // Handle the closing each tag
            Expect(TokenType.DirectiveStart);
            Advance(); // Skip {{
            Expect(TokenType.EndEach);
            Advance(); // Skip /each
            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new EachNode(iteratorName, collection, body);
        }

        private AstNode ParseExpression()
        {
            return ParseOr();
        }

        private AstNode ParseOr()
        {
            var left = ParseAnd();

            while (_position < _tokens.Count && Current().Type == TokenType.Or)
            {
                var op = Current().Type;
                Advance();
                var right = ParseAnd();
                left = new BinaryNode(op, left, right);
            }

            return left;
        }

        private AstNode ParseAnd()
        {
            var left = ParseComparison();

            while (_position < _tokens.Count && Current().Type == TokenType.And)
            {
                var op = Current().Type;
                Advance();
                var right = ParseComparison();
                left = new BinaryNode(op, left, right);
            }

            return left;
        }

        private AstNode ParseComparison()
        {
            var left = ParseAdditive();

            while (_position < _tokens.Count && IsComparisonOperator(Current().Type))
            {
                var op = Current().Type;
                Advance();
                var right = ParseAdditive();
                left = new BinaryNode(op, left, right);
            }

            return left;
        }

        private AstNode ParseAdditive()
        {
            var left = ParseMultiplicative();

            while (_position < _tokens.Count &&
                   (Current().Type == TokenType.Plus || Current().Type == TokenType.Minus))
            {
                var op = Current().Type;
                Advance();
                var right = ParseMultiplicative();
                left = new BinaryNode(op, left, right);
            }

            return left;
        }

        private AstNode ParseMultiplicative()
        {
            var left = ParseUnary();

            while (_position < _tokens.Count &&
                   (Current().Type == TokenType.Multiply || Current().Type == TokenType.Divide))
            {
                var op = Current().Type;
                Advance();
                var right = ParseUnary();
                left = new BinaryNode(op, left, right);
            }

            return left;
        }

        private AstNode ParseUnary()
        {
            if (Current().Type == TokenType.Not)
            {
                var op = Current().Type;
                Advance();
                var expression = ParseUnary();
                return new UnaryNode(op, expression);
            }

            return ParsePrimary();
        }

        private AstNode ParsePrimary()
        {
            var token = Current();

            switch (token.Type)
            {
                case TokenType.Function:
                    return ParseFunctionCall();

                case TokenType.Variable:
                    Advance();
                    return new VariableNode(token.Value);

                case TokenType.String:
                    Advance();
                    return new StringNode(token.Value);

                case TokenType.Number:
                    Advance();
                    return new NumberNode(token.Value);

                case TokenType.True:
                    Advance();
                    return new BooleanNode(true);

                case TokenType.False:
                    Advance();
                    return new BooleanNode(false);

                case TokenType.LeftParen:
                    Advance();
                    var expression = ParseExpression();
                    Expect(TokenType.RightParen);
                    Advance();
                    return expression;

                default:
                    throw new Exception(string.Format("Unexpected token: {0} at position {1}", token.Type, token.Position));
            }
        }

        private bool IsComparisonOperator(TokenType type)
        {
            return type == TokenType.Equal ||
                   type == TokenType.NotEqual ||
                   type == TokenType.LessThan ||
                   type == TokenType.LessThanEqual ||
                   type == TokenType.GreaterThan ||
                   type == TokenType.GreaterThanEqual;
        }

        private Token Current()
        {
            if (_position >= _tokens.Count)
            {
                throw new Exception("Unexpected end of input");
            }
            return _tokens[_position];
        }

        private void Advance()
        {
            _position++;
        }

        private Token Expect(TokenType type)
        {
            var token = Current();
            if (token.Type != type)
            {
                throw new Exception(string.Format("Expected {0} but got {1} at position {2}", type, token.Type, token.Position));
            }
            return token;
        }
    }

    public class FunctionDefinition
    {
        public string Name { get; }
        public List<Type> ParameterTypes { get; }
        public Func<List<dynamic>, dynamic> Implementation { get; }

        public FunctionDefinition(string name, List<Type> parameterTypes, Func<List<dynamic>, dynamic> implementation)
        {
            Name = name;
            ParameterTypes = parameterTypes;
            Implementation = implementation;
        }
    }

    public class FunctionRegistry
    {
        private readonly Dictionary<string, FunctionDefinition> _functions;

        public FunctionRegistry()
        {
            _functions = new Dictionary<string, FunctionDefinition>();
            RegisterBuiltInFunctions();
        }

        private void RegisterBuiltInFunctions()
        {
            // Register the length function
            Register("length",
                new List<Type> { typeof(System.Collections.IEnumerable) },
                args =>
                {
                    var enumerable = args[0] as System.Collections.IEnumerable;
                    if (enumerable == null)
                    {
                        throw new Exception("length function requires an enumerable argument");
                    }
                    return enumerable.Cast<object>().Count();
                });
        }

        public void Register(string name, List<Type> parameterTypes, Func<List<dynamic>, dynamic> implementation)
        {
            _functions[name] = new FunctionDefinition(name, parameterTypes, implementation);
        }

        public bool TryGetFunction(string name, out FunctionDefinition function)
        {
            return _functions.TryGetValue(name, out function);
        }

        public bool ValidateArguments(string functionName, List<dynamic> arguments)
        {
            if (!_functions.TryGetValue(functionName, out var function))
            {
                throw new Exception($"Function '{functionName}' is not registered");
            }

            if (arguments.Count != function.ParameterTypes.Count)
            {
                throw new Exception($"Function '{functionName}' expects {function.ParameterTypes.Count} arguments, but got {arguments.Count}");
            }

            for (int i = 0; i < arguments.Count; i++)
            {
                var argumentType = arguments[i]?.GetType();
                var expectedType = function.ParameterTypes[i];

                // Handle null arguments
                if (arguments[i] == null)
                {
                    if (!expectedType.IsClass) // Value types can't be null
                    {
                        throw new Exception($"Argument {i + 1} of function '{functionName}' cannot be null");
                    }
                    continue;
                }

                // Special handling for IEnumerable parameter type
                if (expectedType == typeof(System.Collections.IEnumerable))
                {
                    if (!(arguments[i] is System.Collections.IEnumerable))
                    {
                        throw new Exception($"Argument {i + 1} of function '{functionName}' must be an array or collection");
                    }
                    continue;
                }

                // Check if the argument can be converted to the expected type
                if (!expectedType.IsAssignableFrom(argumentType))
                {
                    throw new Exception($"Argument {i + 1} of function '{functionName}' must be of type {expectedType.Name}");
                }
            }

            return true;
        }
    }
}
