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

            //// Example 1: Complex arithmetic with parentheses
            //var template1 = "Result: {{(5 * (var2 + 1.5)) / 2}}";
            //var data1 = new ExpandoObject();
            //((IDictionary<string, object>)data1).Add("var2", 2.5);
            //// Result: 10 (calculates (5 * (2.5 + 1.5)) / 2 = (5 * 4) / 2 = 20 / 2 = 10)
            //Console.WriteLine(interpreter.Interpret(template1, data1));

            //// Example 2: Complex boolean logic with parentheses
            //var template2 = "Is valid: {{!(var1 = \"test\" && (var2 > 2 || var3.x < 0))}}";
            //var data2 = new ExpandoObject();
            //var data2Dict = (IDictionary<string, object>)data2;
            //data2Dict.Add("var1", "test");
            //data2Dict.Add("var2", 2.5);
            //var nested = new ExpandoObject();
            //((IDictionary<string, object>)nested).Add("x", 1.5);
            //data2Dict.Add("var3", nested);
            //// Is valid: false (negation of (true && (true || false)) = negation of true = false)
            //Console.WriteLine(interpreter.Interpret(template2, data2));

            //// Example 3: Nested if statements with complex conditions
            //var template3 = @"
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
            //// Output: Outer if true\n    Inner if true
            //Console.WriteLine(interpreter.Interpret(template3, data2));

            //// Example 4: Each statement with complex condition inside
            //var template4 = @"
            //Users with high scores:
            //{{#each user in users}}
            //    {{#if user.score > 80 && (user.age < 18 || user.region = ""EU"")}}
            //        {{user.name}} ({{user.score}})
            //    {{/if}}
            //{{/each}}";

            //var data4 = new ExpandoObject();
            //var usersList = new List<ExpandoObject>();
            //// Create user 1
            //var user1 = new ExpandoObject();
            //var user1Dict = (IDictionary<string, object>)user1;
            //user1Dict.Add("name", "Alice");
            //user1Dict.Add("score", 90);
            //user1Dict.Add("age", 18);
            //user1Dict.Add("region", "US");
            //usersList.Add(user1);
            //// Create user 2
            //var user2 = new ExpandoObject();
            //var user2Dict = (IDictionary<string, object>)user2;
            //user2Dict.Add("name", "Bob");
            //user2Dict.Add("score", 85);
            //user2Dict.Add("age", 25);
            //user2Dict.Add("region", "EU");
            //usersList.Add(user2);
            //// Add users to data
            //((IDictionary<string, object>)data4).Add("users", usersList);
            //// Output: Users with high scores:\n    Alice (90)\n    Bob (85)
            //Console.WriteLine(interpreter.Interpret(template4, data4));

            //////Example 5: Complex mathematical and logical expressions combined
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
            /////* Output:
            ////Math result: 4.25 ((3 * 2 + 2.5) / 2 = 8.5 / 2 = 4.25)
            ////Logic result: false (((3 > 2.5) = (2 < 5)) && !(2.5 = 2.5) = (true = true) && !true = true && false = false)
            ////*/
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
            /////* Output:
            ////Department: IT
            ////    John (Senior)
            ////    Jane (Mid-level)
            ////*/
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
            ////var template8 = @"Here is a function: {{myfunction()}}";
            ////var data8 = new ExpandoObject();
            ////((IDictionary<string, object>)data8).Add("var1", "foo");
            ////// Expect this to throw an error
            ////Console.WriteLine(interpreter.Interpret(template8, data8));

            //// Example 9: Call length function
            //var template9 = @"{{length(myArray)}}";
            //var data9notarray = new ExpandoObject();
            //((IDictionary<string, object>)data9notarray).Add("myArray", "hello");
            //var data9 = new ExpandoObject();
            //var itEmployees1 = new List<ExpandoObject>();
            //var empa1 = new ExpandoObject();
            //((IDictionary<string, object>)empa1).Add("name", "John");
            //((IDictionary<string, object>)empa1).Add("salary", 80000);
            //((IDictionary<string, object>)empa1).Add("isTemp", false);
            //itEmployees1.Add(empa1);
            //var empa2 = new ExpandoObject();
            //((IDictionary<string, object>)empa2).Add("name", "Jane");
            //((IDictionary<string, object>)empa2).Add("salary", 65000);
            //((IDictionary<string, object>)empa2).Add("isTemp", false);
            //itEmployees1.Add(empa2);
            //((IDictionary<string, object>)data9).Add("myArray", itEmployees1);
            //Console.WriteLine(interpreter.Interpret(template9, data9));

            //// Example 10: nested function calls
            //var template10 = @"{{concat(""hello"", concat("" "", ""world""))}}";
            //var data10 = new ExpandoObject();
            //Console.WriteLine(interpreter.Interpret(template10, data10));

            ////Example 11: string functions
            //var template11 = @"
            //{{contains(""Hello World"", ""World"")}}        // Returns true
            //{{startsWith(""Hello World"", ""Hello"")}}      // Returns true
            //{{endsWith(""Hello World"", ""World"")}}        // Returns true
            //{{toUpper(""Hello World"")}}                  // Returns ""HELLO WORLD""
            //{{toLower(""Hello World"")}}                  // Returns ""hello world""
            //{{trim(""  Hello World  "")}}                 // Returns ""Hello World""
            //{{indexOf(""Hello World"", ""World"")}}         // Returns 6
            //{{lastIndexOf(""Hello World World"", ""World"")}} // Returns 12
            //{{substring(""Hello World"", 6)}}             // Returns ""World""
            //{{substring(""Hello World"", 0, 5)}}             // Returns ""Hello""
            //";
            //var data11 = new ExpandoObject();
            //((IDictionary<string, object>)data11).Add("var1", "Hello World");
            //Console.WriteLine(interpreter.Interpret(template11, data11));

            ////Example 12: multiple string function calls
            //var template12 = @"{{substring(""Hello World"", indexOf(var1, ""W""))}} // Returns ""World""";
            //var data12 = new ExpandoObject();
            //((IDictionary<string, object>)data12).Add("var1", "Hello World");
            //Console.WriteLine(interpreter.Interpret(template12, data12));

            ////Example 13: overloaded contains functions
            //var template13 = @"
            //{{contains(""Hello World"", ""World"")}}     // true
            //{{contains(""Hello World"", ""foo"")}}     // false
            //{{contains(user, ""firstName"")}} // true
            //{{contains(user, ""age"")}}       // false
            //{{contains(person, ""name"")}}    // true
            //{{contains(person, ""age"")}}     // false
            //{{contains(dict, ""key"")}}       // true
            //{{contains(dict, ""missing"")}}   // false";
            //// Regular objects
            //var user = new { firstName = "John", lastName = "Doe" };
            //// Dynamic objects
            //dynamic person = new ExpandoObject();
            //person.name = "John";
            //// Dictionary objects
            //var dict = new Dictionary<string, object> { ["key"] = "value" };
            //var data13 = new ExpandoObject();
            //((IDictionary<string, object>)data13).Add("user", user);
            //((IDictionary<string, object>)data13).Add("person", person);
            //((IDictionary<string, object>)data13).Add("dict", dict);
            //Console.WriteLine(interpreter.Interpret(template13, data13));

            //// Example 14: lambda function
            //var template14 = @"{{#each user in filter(users, (x) => x.age > 17 && length(filter(x.loc, (x) => x.name = ""Atlanta"")) > 0)}}{{user.age}}{{#each loc in user.loc}}{{loc.name}}{{/each}}{{/each}}";
            //var data14 = new ExpandoObject();
            //var users = new List<ExpandoObject>();
            //var empx1 = new ExpandoObject();
            //var locs1 = new List<ExpandoObject>();
            //var loc1a = new ExpandoObject();
            //var loc1b = new ExpandoObject();
            //((IDictionary<string, object>)loc1a).Add("name", "Atlanta");
            //((IDictionary<string, object>)loc1b).Add("name", "Denver");
            //locs1.Add(loc1a);
            //locs1.Add(loc1b);
            //((IDictionary<string, object>)empx1).Add("age", "17");
            //((IDictionary<string, object>)empx1).Add("loc", locs1);
            //users.Add(empx1);
            //var empx2 = new ExpandoObject();
            //var locs2 = new List<ExpandoObject>();
            //var loc2a = new ExpandoObject();
            //var loc2b = new ExpandoObject();
            //((IDictionary<string, object>)loc2a).Add("name", "Atlanta");
            //((IDictionary<string, object>)loc2b).Add("name", "Decatur");
            //locs2.Add(loc2a);
            //locs2.Add(loc2b);
            //((IDictionary<string, object>)empx2).Add("age", "21");
            //((IDictionary<string, object>)empx2).Add("loc", locs2);
            //users.Add(empx2);
            //((IDictionary<string, object>)data14).Add("users", users);
            //Console.WriteLine(interpreter.Interpret(template14, data14));

            ////Example 15: objects
            //var template15 = @"{{obj(name: ""John"", age: 30).name}}
            //{{obj(person: obj(name: ""John"", age: 30), active: true).person.age}}";
            //var data15 = new ExpandoObject();
            //Console.WriteLine(interpreter.Interpret(template15, data15));

            //            //Example 16: arrays
            //            var template16 = @"
            //{{#each x in []}}{{x}}, {{/each}}
            //{{#each x in [1.1, 2, 0.3]}}{{x}}, {{/each}}
            //{{#each x in [""hello world"", ""foo bar""]}}{{x}}, {{/each}}
            //{{#each x in [obj(name: ""Jeff""), obj(name: ""Jim"")]}}{{x.name}}, {{/each}}
            //{{#each x in [""foo"", 2, ""bar"", false, obj(x: 1, y: 2)]}}{{x}}, {{/each}}
            //{{#each x in [1, [2, 3], 4]}}{{x}}, {{/each}}
            //{{#each x in obj(arr: [1, 2, 3]).arr}}{{x}}, {{/each}}";
            //            var data16 = new ExpandoObject();
            //            Console.WriteLine(interpreter.Interpret(template16, data16));

            //Example 17: operations
            var template17 = @"
{{at([1, 2, 3], 1)}} // expect 2
{{first([1, 2, 3])}} // expect 1
{{last([1, 2, 3])}} // expect 3
{{any([1, 2, 3])}} // expect true
{{any([])}} // expect false
{{if(10 > 11, ""yes"", ""no"")}} // expect no
{{if(10 < 11, ""yes"", ""no"")}} // expect yes
{{join([3.4, false, ""foo""], "" | "")}} // expect 3.4 | false | foo
{{#each x in explode(""a,b,c"", "","")}}{{x}} {{/each}} // expect a b c 
{{#each x in map([1, 2, 3], (x) => x * 2)}}{{x}} {{/each}} // expect 2 4 6 
{{reduce([1, 2, 3, 4], (acc, curr) => acc + curr, 0)}} // expect 10
{{#each x in take([""foo"", ""bar"", ""baz""], 2)}}{{x}} {{/each}} // expect foo bar 
{{#each x in skip([""foo"", ""bar"", ""baz""], 2)}}{{x}} {{/each}} // expect baz 
{{#each x in order([4, 7, 2])}}{{x}} {{/each}} // expect 2 4 7 
{{#each x in order([4, 7, 2], false)}}{{x}} {{/each}} // expect 7 4 2 
{{#each x in order([""aaaa"", ""zz"", ""yyy""], (a, b) => length(a) - length(b))}}{{x}} {{/each}} // expect zz yyy aaaa 
{{get(obj(name: ""gordon"", age: 22), ""name"")}} // expect gordon
{{#each x in keys(obj(name: ""John"", age: 30, city: ""Atlanta""))}}{{x}} {{/each}} // expect name, age, city
{{#each key in keys(group([obj(city: ""Atlanta"", name: ""Jeff"", age: 10), 
         obj(city: ""Atlanta"", name: ""Jim"", age: 44), 
         obj(city: ""Denver"", name: ""Cindy"", age: 23)], 
       ""city""))}}{{key}} {{/each}} // expect Atlanta Denver
{{mod(7, 3)}}           // Returns 1
{{floor(3.7)}}         // Returns 3
{{ceil(3.2)}}          // Returns 4
{{round(3.45)}}        // Returns 3
{{round(3.45678, 2)}}  // Returns 3.46
{{string(123.45)}}     // Returns ""123.45""
{{string(true)}}       // Returns ""true""
{{number(""123.45"")}}   // Returns 123.45
{{numeric(""123.45"")}}  // Returns true
{{numeric(""abc"")}}     // Returns false";
            var data17 = new ExpandoObject();
            Console.WriteLine(interpreter.Interpret(template17, data17));
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

        public void RegisterFunction(string name, List<ParameterDefinition> parameterTypes, Func<List<dynamic>, dynamic> implementation)
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

        public dynamic GetData()
        {
            return _data;
        }

        public virtual dynamic ResolveValue(string path)
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
                    if (TypeHelper.IsConvertibleToDecimal(current))
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

    public class LambdaExecutionContext : ExecutionContext
    {
        private readonly ExecutionContext _parentContext;
        private readonly Dictionary<string, dynamic> _parameters;

        public LambdaExecutionContext(
            ExecutionContext parentContext,
            List<string> parameterNames,
            List<dynamic> parameterValues)
            : base((object)parentContext.GetData(), parentContext.GetFunctionRegistry())
        {
            _parentContext = parentContext;
            _parameters = new Dictionary<string, dynamic>();

            // Map parameter names to values
            for (int i = 0; i < parameterNames.Count; i++)
            {
                _parameters[parameterNames[i]] = parameterValues[i];
            }
        }

        public override dynamic ResolveValue(string path)
        {
            var parts = path.Split('.');

            // First check if it's a parameter
            if (_parameters.ContainsKey(parts[0]))
            {
                dynamic current = _parameters[parts[0]];

                // Handle nested property access for parameters
                for (int i = 1; i < parts.Length; i++)
                {
                    try
                    {
                        current = ((IDictionary<string, object>)current)[parts[i]];
                    }
                    catch
                    {
                        throw new Exception($"Unable to resolve path: {path}");
                    }
                }

                return current;
            }

            // If not found in parameters, delegate to parent context
            return _parentContext.ResolveValue(path);
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
        DirectiveStart,    // {{
        DirectiveEnd,      // }}
        Variable,          // alphanumeric+dots
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
        Comma,             // ,
        Arrow,             // =>
        Parameter,         // lambda parameter name
        ObjectStart,       // obj(
        Colon,             // :
        Dot,               // .
        Field,             // object field name
        LeftBracket,       // [
        RightBracket       // ]
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

                if (TryMatch("=>"))
                {
                    _tokens.Add(new Token(TokenType.Arrow, "=>", _position));
                    _position += 2;
                    continue;
                }

                if (TryMatch("obj("))
                {
                    _tokens.Add(new Token(TokenType.ObjectStart, "obj(", _position));
                    _position += 4;
                    continue;
                }

                if (TryMatch(":"))
                {
                    _tokens.Add(new Token(TokenType.Colon, ":", _position));
                    _position++;
                    continue;
                }

                if (TryMatch("."))
                {
                    _tokens.Add(new Token(TokenType.Dot, ".", _position));
                    _position++;
                    continue;
                }

                if (TryMatch("["))
                {
                    _tokens.Add(new Token(TokenType.LeftBracket, "[", _position));
                    _position++;
                    continue;
                }

                if (TryMatch("]"))
                {
                    _tokens.Add(new Token(TokenType.RightBracket, "]", _position));
                    _position++;
                    continue;
                }

                // After a dot, treat identifiers as field names
                if (_position > 0 && _tokens.Count > 0 &&
                    _tokens[_tokens.Count - 1].Type == TokenType.Dot &&
                    _position < _input.Length &&
                    (char.IsLetter(_input[_position]) || _input[_position] == '_'))
                {
                    var start = _position;
                    while (_position < _input.Length &&
                           (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
                    {
                        _position++;
                    }
                    var fieldName = _input.Substring(start, _position - start);
                    _tokens.Add(new Token(TokenType.Field, fieldName, start));
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
                    _input[_position] == '_'))
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
            // Evaluate all arguments
            var evaluatedArgs = _arguments.Select(arg => arg.Evaluate(context)).ToList();

            // Get function registry
            var registry = context.GetFunctionRegistry();

            // Try to get matching function definition
            if (!registry.TryGetFunction(_name, evaluatedArgs, out var function, out var effectiveArgs))
            {
                throw new Exception($"No matching overload found for function '{_name}' with the provided arguments");
            }

            // Validate arguments
            registry.ValidateArguments(function, effectiveArgs);

            // Execute function with effective arguments (including defaults for optional parameters)
            return function.Implementation(effectiveArgs);
        }
    }

    public class LambdaNode : AstNode
    {
        private readonly List<string> _parameters;
        private readonly AstNode _expression;

        public LambdaNode(List<string> parameters, AstNode expression)
        {
            _parameters = parameters;
            _expression = expression;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            // Return a delegate that can be called later with parameters
            // Context is captured here, during evaluation, not during parsing
            return new Func<List<dynamic>, dynamic>(args =>
            {
                // Create a new context that includes both captured context and new parameters
                var lambdaContext = new LambdaExecutionContext(context, _parameters, args);
                return _expression.Evaluate(lambdaContext);
            });
        }
    }

    public class ObjectCreationNode : AstNode
    {
        private readonly List<KeyValuePair<string, AstNode>> _fields;

        public ObjectCreationNode(List<KeyValuePair<string, AstNode>> fields)
        {
            _fields = fields;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            var obj = new ExpandoObject();
            var dict = obj as IDictionary<string, object>;

            foreach (var field in _fields)
            {
                dict[field.Key] = field.Value.Evaluate(context);
            }

            return obj;
        }
    }

    public class FieldAccessNode : AstNode
    {
        private readonly AstNode _object;
        private readonly string _fieldName;

        public FieldAccessNode(AstNode obj, string fieldName)
        {
            _object = obj;
            _fieldName = fieldName;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            var obj = _object.Evaluate(context);
            if (obj == null)
            {
                throw new Exception($"Cannot access field '{_fieldName}' on null object");
            }

            // Handle dictionary-like objects (ExpandoObject, IDictionary)
            if (obj is IDictionary<string, object> dict)
            {
                if (!dict.ContainsKey(_fieldName))
                {
                    throw new Exception($"Object does not contain field '{_fieldName}'");
                }
                return dict[_fieldName];
            }

            // Handle regular objects using reflection
            var property = obj.GetType().GetProperty(_fieldName);
            if (property == null)
            {
                throw new Exception($"Object does not contain field '{_fieldName}'");
            }

            var value = property.GetValue(obj);

            if (TypeHelper.IsConvertibleToDecimal(value))
            {
                value = (decimal)value;
            }

            return value;
        }
    }

    public class ArrayNode : AstNode
    {
        private readonly List<AstNode> _elements;

        public ArrayNode(List<AstNode> elements)
        {
            _elements = elements;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _elements.Select(element => element.Evaluate(context)).ToList();
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

        private AstNode ParseGroupExpression()
        {
            Advance(); // Skip (
            var expression = ParseExpression();
            Expect(TokenType.RightParen);
            Advance(); // Skip )
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

        private AstNode ParseLambda()
        {
            Expect(TokenType.LeftParen);
            Advance(); // Skip (

            var parameters = new List<string>();

            // Parse parameters
            if (Current().Type != TokenType.RightParen)
            {
                while (true)
                {
                    if (Current().Type != TokenType.Variable && Current().Type != TokenType.Parameter)
                    {
                        throw new Exception($"Expected parameter name but got {Current().Type} at position {Current().Position}");
                    }

                    parameters.Add(Current().Value);
                    Advance();

                    if (Current().Type == TokenType.RightParen)
                        break;

                    Expect(TokenType.Comma);
                    Advance(); // Skip comma
                }
            }

            Expect(TokenType.RightParen);
            Advance(); // Skip )

            Expect(TokenType.Arrow);
            Advance(); // Skip =>

            var expression = ParseExpression();

            return new LambdaNode(parameters, expression);
        }

        private AstNode ParseObjectCreation()
        {
            Advance(); // Skip obj(

            var fields = new List<KeyValuePair<string, AstNode>>();

            while (_position < _tokens.Count && Current().Type != TokenType.RightParen)
            {
                // Parse field name
                if (Current().Type != TokenType.Variable)
                {
                    throw new Exception($"Expected field name but got {Current().Type} at position {Current().Position}");
                }

                var fieldName = Current().Value;
                Advance();

                // Parse colon
                if (Current().Type != TokenType.Colon)
                {
                    throw new Exception($"Expected ':' but got {Current().Type} at position {Current().Position}");
                }
                Advance();

                // Parse field value expression
                var fieldValue = ParseExpression();

                fields.Add(new KeyValuePair<string, AstNode>(fieldName, fieldValue));

                // If there's a comma, skip it and continue
                if (Current().Type == TokenType.Comma)
                {
                    Advance();
                }
                // If there's a right paren, we're done
                else if (Current().Type == TokenType.RightParen)
                {
                    break;
                }
                else
                {
                    throw new Exception($"Expected ',' or ')' but got {Current().Type} at position {Current().Position}");
                }
            }

            Expect(TokenType.RightParen);
            Advance(); // Skip )

            return new ObjectCreationNode(fields);
        }

        private AstNode ParseArrayCreation()
        {
            Advance(); // Skip [

            var elements = new List<AstNode>();

            // Handle empty array case
            if (Current().Type == TokenType.RightBracket)
            {
                Advance(); // Skip ]
                return new ArrayNode(elements);
            }

            // Parse array elements
            while (true)
            {
                elements.Add(ParseExpression());

                if (Current().Type == TokenType.RightBracket)
                {
                    Advance(); // Skip ]
                    break;
                }

                if (Current().Type != TokenType.Comma)
                {
                    throw new Exception($"Expected ',' or ']' but got {Current().Type} at position {Current().Position}");
                }

                Advance(); // Skip comma
            }

            return new ArrayNode(elements);
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
            AstNode expr = null;

            switch (token.Type)
            {
                case TokenType.LeftBracket:
                    expr = ParseArrayCreation();
                    break;

                case TokenType.ObjectStart:
                    expr = ParseObjectCreation();
                    break;

                case TokenType.LeftParen:
                    // Store current position so we can backtrack if needed
                    var pos = _position;

                    // Look ahead to see if this could be a lambda expression
                    Advance(); // Skip (

                    // Check if we have valid parameter list
                    bool couldBeLambda = true;
                    bool firstParam = true;

                    while (_position < _tokens.Count && Current().Type != TokenType.RightParen)
                    {
                        if (firstParam)
                        {
                            if (Current().Type != TokenType.Variable)
                            {
                                couldBeLambda = false;
                                break;
                            }
                            firstParam = false;
                        }
                        else
                        {
                            if (Current().Type == TokenType.Comma)
                            {
                                Advance();
                                if (Current().Type != TokenType.Variable)
                                {
                                    couldBeLambda = false;
                                    break;
                                }
                            }
                            else
                            {
                                couldBeLambda = false;
                                break;
                            }
                        }
                        Advance();
                    }

                    if (couldBeLambda && Current().Type == TokenType.RightParen)
                    {
                        Advance(); // Skip )
                        couldBeLambda = Current().Type == TokenType.Arrow;
                    }
                    else
                    {
                        couldBeLambda = false;
                    }

                    // Reset position
                    _position = pos;

                    // Parse as either lambda or grouped expression
                    expr = couldBeLambda ? ParseLambda() : ParseGroupExpression();
                    break;

                case TokenType.Function:
                    expr = ParseFunctionCall();
                    break;

                case TokenType.Variable:
                    Advance();
                    expr = new VariableNode(token.Value);
                    break;

                case TokenType.String:
                    Advance();
                    expr = new StringNode(token.Value);
                    break;

                case TokenType.Number:
                    Advance();
                    expr = new NumberNode(token.Value);
                    break;

                case TokenType.True:
                    Advance();
                    expr = new BooleanNode(true);
                    break;

                case TokenType.False:
                    Advance();
                    expr = new BooleanNode(false);
                    break;

                default:
                    throw new Exception(string.Format("Unexpected token: {0} at position {1}", token.Type, token.Position));
            }

            while (_position < _tokens.Count && Current().Type == TokenType.Dot)
            {
                Advance(); // Skip the dot
                var fieldToken = Current();
                if (fieldToken.Type != TokenType.Field && fieldToken.Type != TokenType.Variable)
                {
                    throw new Exception($"Expected field name but got {fieldToken.Type} at position {fieldToken.Position}");
                }
                expr = new FieldAccessNode(expr, fieldToken.Value);
                Advance();
            }

            return expr;
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

    public class ParameterDefinition
    {
        public Type Type { get; }
        public bool IsOptional { get; }
        public dynamic DefaultValue { get; }

        public ParameterDefinition(Type type, bool isOptional = false, dynamic defaultValue = null)
        {
            Type = type;
            IsOptional = isOptional;
            DefaultValue = defaultValue;
        }
    }

    public class FunctionDefinition
    {
        public string Name { get; }
        public List<ParameterDefinition> Parameters { get; }
        public Func<List<dynamic>, dynamic> Implementation { get; }

        public FunctionDefinition(string name, List<ParameterDefinition> parameters, Func<List<dynamic>, dynamic> implementation)
        {
            Name = name;
            Parameters = parameters;
            Implementation = implementation;
        }

        public int RequiredParameterCount => Parameters.Count(p => !p.IsOptional);
        public int TotalParameterCount => Parameters.Count;
    }

    public class FunctionRegistry
    {
        private readonly Dictionary<string, List<FunctionDefinition>> _functions;

        public FunctionRegistry()
        {
            _functions = new Dictionary<string, List<FunctionDefinition>>();
            RegisterBuiltInFunctions();
        }

        private void RegisterBuiltInFunctions()
        {
            Register("length",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable))
                },
                args =>
                {
                    var enumerable = args[0] as System.Collections.IEnumerable;
                    if (enumerable == null)
                    {
                        throw new Exception("length function requires an enumerable argument");
                    }
                    return enumerable.Cast<object>().Count();
                });

            Register("length",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str = args[0] as string;
                    if (str == null)
                    {
                        throw new Exception("length function requires a string argument");
                    }
                    return str.Length;
                });

            Register("concat",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string)),
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str1 = args[0]?.ToString() ?? "";
                    var str2 = args[1]?.ToString() ?? "";
                    return str1 + str2;
                });

            Register("contains",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string)),
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str = args[0]?.ToString() ?? "";
                    var searchStr = args[1]?.ToString() ?? "";
                    return str.Contains(searchStr);
                });

            Register("contains",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(object)),
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    if (args[0] == null)
                        return false;

                    var obj = args[0];
                    var propertyName = args[1]?.ToString() ?? "";

                    // Handle ExpandoObject separately
                    if (obj is ExpandoObject)
                    {
                        return ((IDictionary<string, object>)obj).ContainsKey(propertyName);
                    }

                    // Handle dictionary types
                    if (obj is IDictionary<string, object> dict)
                    {
                        return dict.ContainsKey(propertyName);
                    }

                    // For regular objects, check if the property exists
                    var type = obj.GetType();
                    var propertyExists = type.GetProperty(propertyName) != null;

                    return propertyExists;
                });

            Register("startsWith",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string)),
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str = args[0]?.ToString() ?? "";
                    var searchStr = args[1]?.ToString() ?? "";
                    return str.StartsWith(searchStr);
                });

            Register("endsWith",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string)),
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str = args[0]?.ToString() ?? "";
                    var searchStr = args[1]?.ToString() ?? "";
                    return str.EndsWith(searchStr);
                });

            Register("toUpper",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str = args[0]?.ToString() ?? "";
                    return str.ToUpper();
                });

            Register("toLower",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str = args[0]?.ToString() ?? "";
                    return str.ToLower();
                });

            Register("trim",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str = args[0]?.ToString() ?? "";
                    return str.Trim();
                });

            Register("indexOf",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string)),
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str = args[0]?.ToString() ?? "";
                    var searchStr = args[1]?.ToString() ?? "";
                    return new decimal(str.IndexOf(searchStr));
                });

            Register("lastIndexOf",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string)),
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str = args[0]?.ToString() ?? "";
                    var searchStr = args[1]?.ToString() ?? "";
                    return new decimal(str.LastIndexOf(searchStr));
                });

            Register("substring",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string)),
                    new ParameterDefinition(typeof(decimal)),
                    new ParameterDefinition(typeof(decimal), true, new decimal(-1)) // Optional end index
                },
                args =>
                {
                    var str = args[0]?.ToString() ?? "";
                    var startIndex = Convert.ToInt32(args[1]);
                    var endIndex = Convert.ToInt32(args[2]);

                    // If end index is provided, use it; otherwise substring to the end
                    if (endIndex >= 0)
                    {
                        var length = endIndex - startIndex;
                        return str.Substring(startIndex, length);
                    }

                    return str.Substring(startIndex);
                });

            Register("filter",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(Func<List<dynamic>, dynamic>))
                },
                args =>
                {
                    var collection = args[0] as System.Collections.IEnumerable;
                    var predicate = args[1] as Func<List<dynamic>, dynamic>;

                    if (collection == null || predicate == null)
                    {
                        throw new Exception("filter function requires an array and a lambda function");
                    }

                    var result = new List<dynamic>();
                    foreach (var item in collection)
                    {
                        var predicateResult = predicate(new List<dynamic> { item });
                        if (Convert.ToBoolean(predicateResult))
                        {
                            result.Add(item);
                        }
                    }

                    return result;
                });

            Register("at",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var index = Convert.ToInt32(args[1]);

                    if (array == null)
                        throw new Exception("at function requires an array as first argument");

                    var list = array.Cast<object>().ToList();
                    if (index < 0 || index >= list.Count)
                        throw new Exception($"Index {index} is out of bounds for array of length {list.Count}");

                    return list[index];
                });

            Register("first",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    if (array == null)
                        throw new Exception("first function requires an array argument");

                    var list = array.Cast<object>().ToList();
                    if (list.Count == 0)
                        throw new Exception("Cannot get first element of empty array");

                    return list[0];
                });

            Register("last",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    if (array == null)
                        throw new Exception("last function requires an array argument");

                    var list = array.Cast<object>().ToList();
                    if (list.Count == 0)
                        throw new Exception("Cannot get last element of empty array");

                    return list[list.Count - 1];
                });

            Register("any",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    if (array == null)
                        throw new Exception("any function requires an array argument");

                    return array.Cast<object>().Any();
                });

            Register("if",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(bool)),
                    new ParameterDefinition(typeof(object)),
                    new ParameterDefinition(typeof(object))
                },
                args =>
                {
                    var condition = Convert.ToBoolean(args[0]);
                    return condition ? args[1] : args[2];
                });

            Register("join",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var delimiter = args[1] as string;

                    if (array == null)
                        throw new Exception("join function requires an array as first argument");
                    if (delimiter == null)
                        throw new Exception("join function requires a string as second argument");

                    return string.Join(delimiter, array.Cast<object>().Select(x => x?.ToString() ?? ""));
                });

            Register("explode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string)),
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str = args[0] as string;
                    var delimiter = args[1] as string;

                    if (str == null)
                        throw new Exception("explode function requires a string as first argument");
                    if (delimiter == null)
                        throw new Exception("explode function requires a string as second argument");

                    return str.Split(new[] { delimiter }, StringSplitOptions.None).ToList();
                });

            Register("map",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(Func<List<dynamic>, dynamic>))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var mapper = args[1] as Func<List<dynamic>, dynamic>;

                    if (array == null || mapper == null)
                        throw new Exception("map function requires an array and a function");

                    return array.Cast<object>()
                        .Select(item => mapper(new List<dynamic> { item }))
                        .ToList();
                });

            Register("reduce",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(Func<List<dynamic>, dynamic>)),
                    new ParameterDefinition(typeof(object))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var reducer = args[1] as Func<List<dynamic>, dynamic>;
                    var initialValue = args[2];

                    if (array == null || reducer == null)
                        throw new Exception("reduce function requires an array and a function");

                    return array.Cast<object>()
                        .Aggregate((object)initialValue, (acc, curr) =>
                            reducer(new List<dynamic> { acc, curr }));
                });

            Register("take",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    int count = Convert.ToInt32(args[1]);

                    if (array == null)
                        throw new Exception("take function requires an array as first argument");

                    if (count <= 0)
                        return new List<object>();

                    return array.Cast<object>().Take(count).ToList();
                });

            Register("skip",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    int count = Convert.ToInt32(args[1]);

                    if (array == null)
                        throw new Exception("skip function requires an array as first argument");

                    return array.Cast<object>().Skip(count).ToList();
                });

            Register("order",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    if (array == null)
                        throw new Exception("order function requires an array argument");

                    return array.Cast<object>().OrderBy(x => x).ToList();
                });

            Register("order",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(bool))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var ascending = Convert.ToBoolean(args[1]);

                    if (array == null)
                        throw new Exception("order function requires an array as first argument");

                    var ordered = array.Cast<object>();
                    return (ascending ? ordered.OrderBy(x => x) : ordered.OrderByDescending(x => x)).ToList();
                });

            Register("order",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(Func<List<dynamic>, dynamic>))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var comparer = args[1] as Func<List<dynamic>, dynamic>;

                    if (array == null || comparer == null)
                        throw new Exception("order function requires an array and a comparison function");

                    return array.Cast<object>()
                        .OrderBy(x => x, new DynamicComparer(comparer))
                        .ToList();
                });

            Register("group",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var fieldName = args[1] as string;

                    if (array == null)
                        throw new Exception("group function requires an array as first argument");
                    if (string.IsNullOrEmpty(fieldName))
                        throw new Exception("group function requires a non-empty string as second argument");

                    var result = new ExpandoObject() as IDictionary<string, object>;

                    foreach (var item in array)
                    {
                        // Skip null items
                        if (item == null) continue;

                        // Get the group key from the item
                        string key;
                        if (item is IDictionary<string, object> dict)
                        {
                            if (!dict.ContainsKey(fieldName))
                                throw new Exception($"Object does not contain field '{fieldName}'");
                            key = dict[fieldName]?.ToString();
                        }
                        else
                        {
                            var property = item.GetType().GetProperty(fieldName);
                            if (property == null)
                                throw new Exception($"Object does not contain field '{fieldName}'");
                            key = property.GetValue(item)?.ToString();
                        }

                        if (key == null)
                            throw new Exception($"Field '{fieldName}' value cannot be null");

                        // Add item to the appropriate group
                        if (!result.ContainsKey(key))
                        {
                            result[key] = new List<object>();
                        }
                        ((List<object>)result[key]).Add(item);
                    }

                    return result;
                });

            Register("get",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(object)),
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var obj = args[0];
                    var fieldName = args[1] as string;

                    if (obj == null)
                        throw new Exception("get function requires an object as first argument");
                    if (string.IsNullOrEmpty(fieldName))
                        throw new Exception("get function requires a non-empty string as second argument");

                    // Handle ExpandoObject and other dictionary types
                    if (obj is IDictionary<string, object> dict)
                    {
                        if (!dict.ContainsKey(fieldName))
                            throw new Exception($"Object does not contain field '{fieldName}'");

                        var value = dict[fieldName];
                        if (TypeHelper.IsConvertibleToDecimal(value))
                        {
                            value = (decimal)value;
                        }
                        return value;
                    }

                    // Handle regular objects using reflection
                    var property = obj.GetType().GetProperty(fieldName);
                    if (property == null)
                        throw new Exception($"Object does not contain field '{fieldName}'");

                    var propValue = property.GetValue(obj);
                    if (TypeHelper.IsConvertibleToDecimal(propValue))
                    {
                        propValue = (decimal)propValue;
                    }
                    return propValue;
                });

            Register("keys",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(object))
                },
                args =>
                {
                    var obj = args[0];
                    if (obj == null)
                        throw new Exception("keys function requires an object argument");

                    // Handle ExpandoObject and other dictionary types
                    if (obj is IDictionary<string, object> dict)
                    {
                        return dict.Keys.ToList();
                    }

                    // Handle regular objects using reflection
                    var properties = obj.GetType().GetProperties();
                    var propertyNames = new List<string>();
                    foreach (var prop in properties)
                    {
                        propertyNames.Add(prop.Name);
                    }
                    return propertyNames;
                });

            Register("mod",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(decimal)),
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var number1 = Convert.ToInt32(args[0]);
                    var number2 = Convert.ToInt32(args[1]);

                    if (number2 == 0)
                        throw new Exception("Cannot perform modulo with zero as divisor");

                    return new decimal(number1 % number2);
                });

            Register("floor",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var number = Convert.ToDecimal(args[0]);
                    return Math.Floor(number);
                });

            Register("ceil",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var number = Convert.ToDecimal(args[0]);
                    return Math.Ceiling(number);
                });

            Register("round",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var number = Convert.ToDecimal(args[0]);
                    return Math.Round(number, 0);
                });

            Register("round",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(decimal)),
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var number = Convert.ToDecimal(args[0]);
                    var decimals = Convert.ToInt32(args[1]);

                    if (decimals < 0)
                        throw new Exception("Number of decimal places cannot be negative");

                    return Math.Round(number, decimals);
                });

            Register("string",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var number = Convert.ToDecimal(args[0]);
                    return number.ToString();
                });

            Register("string",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(bool))
                },
                args =>
                {
                    var boolean = Convert.ToBoolean(args[0]);
                    return boolean.ToString().ToLower(); // returning "true" or "false" in lowercase
                });

            Register("number",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str = args[0] as string;

                    if (string.IsNullOrEmpty(str))
                        throw new Exception("Cannot convert empty or null string to number");

                    if (!decimal.TryParse(str, out decimal result))
                        throw new Exception($"Cannot convert string '{str}' to number");

                    return result;
                });

            Register("numeric",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var str = args[0] as string;

                    if (string.IsNullOrEmpty(str))
                        return false;

                    return decimal.TryParse(str, out _);
                });
        }

        public void Register(string name, List<ParameterDefinition> parameters, Func<List<dynamic>, dynamic> implementation)
        {
            var definition = new FunctionDefinition(name, parameters, implementation);

            if (!_functions.ContainsKey(name))
            {
                _functions[name] = new List<FunctionDefinition>();
            }

            // Check if an identical overload already exists
            var existingOverload = _functions[name].FirstOrDefault(f =>
                f.Parameters.Count == parameters.Count &&
                f.Parameters.Zip(parameters, (a, b) => a.Type == b.Type && a.IsOptional == b.IsOptional).All(x => x));

            if (existingOverload != null)
            {
                throw new Exception($"Function '{name}' is already registered with the same parameter types");
            }

            _functions[name].Add(definition);
        }

        public bool TryGetFunction(string name, List<dynamic> arguments, out FunctionDefinition matchingFunction, out List<dynamic> effectiveArguments)
        {
            matchingFunction = null;
            effectiveArguments = null;

            if (!_functions.TryGetValue(name, out var overloads))
            {
                return false;
            }

            // Find all overloads with the correct number of parameters
            var candidateOverloads = overloads.Where(f =>
                arguments.Count >= f.RequiredParameterCount &&
                arguments.Count <= f.TotalParameterCount
            ).ToList();

            if (!candidateOverloads.Any())
            {
                return false;
            }

            // Score each overload based on type compatibility
            var scoredOverloads = candidateOverloads.Select(overload => new
            {
                Function = overload,
                Score = ScoreTypeMatch(overload.Parameters, arguments),
                EffectiveArgs = CreateEffectiveArguments(overload.Parameters, arguments)
            })
            .Where(x => x.Score >= 0) // Filter out incompatible matches
            .OrderByDescending(x => x.Score)
            .ToList();

            if (!scoredOverloads.Any())
            {
                return false;
            }

            // If we have multiple matches with the same best score, it's ambiguous
            if (scoredOverloads.Count > 1 && scoredOverloads[0].Score == scoredOverloads[1].Score)
            {
                throw new Exception($"Ambiguous function call to '{name}'. Multiple overloads match the provided arguments.");
            }

            var bestMatch = scoredOverloads.First();
            matchingFunction = bestMatch.Function;
            effectiveArguments = bestMatch.EffectiveArgs;
            return true;
        }

        private List<dynamic> CreateEffectiveArguments(List<ParameterDefinition> parameters, List<dynamic> providedArgs)
        {
            var effectiveArgs = new List<dynamic>();

            for (int i = 0; i < parameters.Count; i++)
            {
                if (i < providedArgs.Count)
                {
                    effectiveArgs.Add(providedArgs[i]);
                }
                else if (parameters[i].IsOptional)
                {
                    effectiveArgs.Add(parameters[i].DefaultValue);
                }
                else
                {
                    // This shouldn't happen due to earlier checks, but just in case
                    throw new Exception("Missing required argument");
                }
            }

            return effectiveArgs;
        }

        private int ScoreTypeMatch(List<ParameterDefinition> parameters, List<dynamic> arguments)
        {
            if (arguments.Count < parameters.Count(p => !p.IsOptional) ||
                arguments.Count > parameters.Count)
            {
                return -1;
            }

            int totalScore = 0;

            for (int i = 0; i < arguments.Count; i++)
            {
                var arg = arguments[i];
                var paramType = parameters[i].Type;

                // Handle null arguments
                if (arg == null)
                {
                    if (!paramType.IsClass) // Value types can't be null
                    {
                        return -1;
                    }
                    totalScore += 1;
                    continue;
                }

                var argType = arg.GetType();

                // Exact type match
                if (paramType == argType)
                {
                    totalScore += 3;
                    continue;
                }

                // Special handling for IEnumerable
                if (paramType == typeof(System.Collections.IEnumerable))
                {
                    if (arg is System.Collections.IEnumerable)
                    {
                        totalScore += 2;
                        continue;
                    }
                    return -1;
                }

                // Assignable type match (inheritance)
                if (paramType.IsAssignableFrom(argType))
                {
                    totalScore += 2;
                    continue;
                }

                return -1; // No valid conversion possible
            }

            return totalScore;
        }

        public void ValidateArguments(FunctionDefinition function, List<dynamic> arguments)
        {
            if (arguments.Count != function.Parameters.Count)
            {
                throw new Exception($"Function '{function.Name}' expects {function.Parameters.Count} arguments, but got {arguments.Count}");
            }

            for (int i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                var parameter = function.Parameters[i];

                // Handle null arguments
                if (argument == null)
                {
                    if (!parameter.Type.IsClass)
                    {
                        throw new Exception($"Argument {i + 1} of function '{function.Name}' cannot be null");
                    }
                    continue;
                }

                var argumentType = argument.GetType();

                // Special handling for IEnumerable parameter type
                if (parameter.Type == typeof(System.Collections.IEnumerable))
                {
                    if (!(argument is System.Collections.IEnumerable))
                    {
                        throw new Exception($"Argument {i + 1} of function '{function.Name}' must be an array or collection");
                    }
                    continue;
                }

                // Check if the argument can be converted to the expected type
                if (!parameter.Type.IsAssignableFrom(argumentType))
                {
                    throw new Exception($"Argument {i + 1} of function '{function.Name}' must be of type {parameter.Type.Name}");
                }
            }
        }
    }

    public class TypeHelper
    {
        public static bool IsConvertibleToDecimal(dynamic value)
        {
            if (value == null)
                return false;

            Type valueType = value.GetType();

            // Check numeric types that can be safely converted to decimal
            if (valueType == typeof(decimal) ||
                valueType == typeof(int) ||
                valueType == typeof(long) ||
                valueType == typeof(double) ||
                valueType == typeof(float) ||
                valueType == typeof(byte) ||
                valueType == typeof(sbyte) ||
                valueType == typeof(short) ||
                valueType == typeof(ushort) ||
                valueType == typeof(uint) ||
                valueType == typeof(ulong))
            {
                return true;
            }

            return false;
        }
    }

    public class DynamicComparer : IComparer<object>
    {
        private readonly Func<List<dynamic>, dynamic> _comparer;

        public DynamicComparer(Func<List<dynamic>, dynamic> comparer)
        {
            _comparer = comparer;
        }

        public int Compare(object x, object y)
        {
            var result = Convert.ToDecimal(_comparer(new List<dynamic> { x, y }));
            return Math.Sign(result);
        }
    }
}
