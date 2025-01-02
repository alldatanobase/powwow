using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;

namespace Interpreter
{
    internal class Program
    {
        public static void Main()
        {
            var interpreter = new Interpreter();
            var data = new ExpandoObject();
            ((IDictionary<string, object>)data).Add("var1", "world");

            var result = interpreter.Evaluate("Hello {{var1}}", data);
            Console.WriteLine(result);
            // result will be "Hello world"

            // Example with nested objects
            var nestedData = new ExpandoObject();
            var nested = new ExpandoObject();
            ((IDictionary<string, object>)nested).Add("nested", "claude");
            ((IDictionary<string, object>)nestedData).Add("var1", nested);

            result = interpreter.Evaluate("Hello {{var1.nested}}", nestedData);
            Console.WriteLine(result);
            // result will be "Hello claude"

            // Example with each helper
            var arrayData = new ExpandoObject();
            var arr = new[]
            {
                new { name = "orange" },
                new { name = "apple" },
                new { name = "banana" }
            };
            ((IDictionary<string, object>)arrayData).Add("arr", arr);

            result = interpreter.Evaluate("{{#each arr}}name: {{name}}, {{/each}}", arrayData);
            Console.WriteLine(result);
            // result will be "name: orange, name: apple, name: banana, "

            var arrayData1 = new ExpandoObject();
            var arr1 = new[]
            {
                new { vals = new[] { new { name = "orange" }, new { name = "apple" }, new { name = "banana" } } },
                new { vals = new[] { new { name = "chicken" }, new { name = "beef" }, new { name = "pork" } } },
                new { vals = new[] { new { name = "broccolli" }, new { name = "carrot" }, new { name = "cauliflower" } } }
            };
            ((IDictionary<string, object>)arrayData1).Add("arr", arr1);

            result = interpreter.Evaluate("{{#each arr}}{{#each vals}}name: {{name}}, {{/each}}\n{{/each}}", arrayData1);
            Console.WriteLine(result);
        }

        public class Lexer
        {
            private readonly string template;
            private int position;

            public Lexer(string template)
            {
                this.template = template;
                this.position = 0;
            }

            public List<Token> Tokenize()
            {
                var tokens = new List<Token>();
                var currentText = new StringBuilder();

                while (position < template.Length)
                {
                    if (template[position] == '{' && LookAhead(1) == '{')
                    {
                        if (currentText.Length > 0)
                        {
                            tokens.Add(new Token { Type = Token.TokenType.Text, Value = currentText.ToString() });
                            currentText.Clear();
                        }

                        position += 2;
                        var tag = ReadUntil("}}");
                        if (tag.StartsWith("#"))
                        {
                            // Helper start
                            var parts = tag.Substring(1).Split(new[] { ' ' }, 2);
                            if (parts.Length != 2)
                                throw new Exception("Invalid helper syntax");

                            tokens.Add(new Token
                            {
                                Type = Token.TokenType.HelperStart,
                                HelperName = parts[0],
                                Variable = parts[1]
                            });
                        }
                        else if (tag.StartsWith("/"))
                        {
                            // Helper end
                            tokens.Add(new Token
                            {
                                Type = Token.TokenType.HelperEnd,
                                Value = tag.Substring(1)
                            });
                        }
                        else
                        {
                            // Variable
                            tokens.Add(new Token
                            {
                                Type = Token.TokenType.Variable,
                                Value = tag.Trim()
                            });
                        }
                    }
                    else
                    {
                        currentText.Append(template[position]);
                        position++;
                    }
                }

                if (currentText.Length > 0)
                {
                    tokens.Add(new Token { Type = Token.TokenType.Text, Value = currentText.ToString() });
                }

                return tokens;
            }

            private char LookAhead(int offset)
            {
                var index = position + offset;
                return index < template.Length ? template[index] : '\0';
            }

            private string ReadUntil(string end)
            {
                var start = position;
                var endIndex = template.IndexOf(end, start);
                if (endIndex == -1)
                    throw new Exception("Expected '" + end + "'");

                position = endIndex + end.Length;
                return template.Substring(start, endIndex - start);
            }
        }

        public class Token
        {
            public enum TokenType
            {
                Text,
                Variable,
                HelperStart,
                HelperEnd
            }

            public TokenType Type { get; set; }
            public string Value { get; set; }
            public string HelperName { get; set; }
            public string Variable { get; set; }
        }

        public class HelperContext
        {
            public List<Node> Content { get; set; }
            public dynamic Data { get; set; }
        }

        public abstract class Node
        {
            public abstract string Evaluate(dynamic scopedData, dynamic rootData, Dictionary<string, Func<object, HelperContext, string>> helpers);
        }

        public class TextNode : Node
        {
            public string Text { get; set; }

            public override string Evaluate(dynamic scopedData, dynamic rootData, Dictionary<string, Func<object, HelperContext, string>> helpers)
            {
                return Text;
            }
        }

        // This node type is used for direct variable substitution and enforces non-array values
        public class VariableNode : Node
        {
            public string Name { get; set; }

            public override string Evaluate(dynamic scopedData, dynamic rootData, Dictionary<string, Func<object, HelperContext, string>> helpers)
            {
                var value = GetValue(scopedData, Name.Split('.'));
                if (value == null)
                {
                    value = GetValue(rootData, Name.Split('.'));
                }

                if (value is System.Collections.IEnumerable && !(value is string))
                    throw new Exception($"Variable '{Name}' is an array and cannot be used as a single value");

                return value?.ToString() ?? "";
            }

            protected object GetValue(dynamic obj, string[] parts)
            {
                if (obj == null) return null;

                object current = obj;
                foreach (var part in parts)
                {
                    if (current == null) return null;

                    if (current is ExpandoObject)
                    {
                        var dict = (IDictionary<string, object>)current;
                        if (!dict.ContainsKey(part))
                            return null;
                        current = dict[part];
                    }
                    else
                    {
                        var prop = current.GetType().GetProperty(part);
                        if (prop == null)
                            return null;
                        current = prop.GetValue(current);
                    }
                }
                return current;
            }
        }

        // New node type specifically for helper arguments that allows any type
        public class HelperArgumentNode : VariableNode
        {
            public override string Evaluate(dynamic scopedData, dynamic rootData, Dictionary<string, Func<object, HelperContext, string>> helpers)
            {
                throw new InvalidOperationException("HelperArgumentNode.Evaluate should not be called directly. Use GetValue instead.");
            }

            public object GetArgumentValue(dynamic scopedData, dynamic rootData)
            {
                var value = GetValue(scopedData, Name.Split('.'));
                if (value == null)
                {
                    value = GetValue(rootData, Name.Split('.'));
                }
                return value;
            }
        }

        public class HelperNode : Node
        {
            public string Name { get; set; }
            public HelperArgumentNode Argument { get; set; }
            public List<Node> Content { get; set; }

            public override string Evaluate(dynamic scopedData, dynamic rootData, Dictionary<string, Func<object, HelperContext, string>> helpers)
            {
                if (!helpers.ContainsKey(Name))
                    throw new Exception($"Unknown helper: {Name}");

                // Get the argument value without type restrictions
                var argumentValue = Argument.GetArgumentValue(scopedData, rootData);

                var context = new HelperContext
                {
                    Content = Content,
                    Data = rootData
                };

                return helpers[Name](argumentValue, context);
            }
        }

        public class Parser
        {
            private readonly List<Token> tokens;
            private int position;

            public Parser(List<Token> tokens)
            {
                this.tokens = tokens;
                this.position = 0;
            }

            public List<Node> Parse()
            {
                var nodes = new List<Node>();
                while (position < tokens.Count)
                {
                    var token = tokens[position];
                    switch (token.Type)
                    {
                        case Token.TokenType.Text:
                            nodes.Add(new TextNode { Text = token.Value });
                            position++;
                            break;
                        case Token.TokenType.Variable:
                            nodes.Add(new VariableNode { Name = token.Value });
                            position++;
                            break;
                        case Token.TokenType.HelperStart:
                            nodes.Add(ParseHelper());
                            break;
                        default:
                            throw new Exception($"Unexpected token type: {token.Type}");
                    }
                }
                return nodes;
            }

            private HelperNode ParseHelper()
            {
                var startToken = tokens[position];
                position++;

                var innerNodes = new List<Node>();
                var nesting = 1;

                while (position < tokens.Count && nesting > 0)
                {
                    var token = tokens[position];
                    if (token.Type == Token.TokenType.HelperStart)
                    {
                        nesting++;
                    }
                    else if (token.Type == Token.TokenType.HelperEnd)
                    {
                        nesting--;
                        if (nesting == 0)
                        {
                            if (token.Value != startToken.HelperName)
                                throw new Exception($"Mismatched helper tags: {startToken.HelperName} and {token.Value}");
                            position++;
                            break;
                        }
                    }

                    if (nesting > 0)
                    {
                        switch (token.Type)
                        {
                            case Token.TokenType.Text:
                                innerNodes.Add(new TextNode { Text = token.Value });
                                break;
                            case Token.TokenType.Variable:
                                innerNodes.Add(new VariableNode { Name = token.Value });
                                break;
                            case Token.TokenType.HelperStart:
                                innerNodes.Add(ParseHelper());
                                continue;
                        }
                        position++;
                    }
                }

                return new HelperNode
                {
                    Name = startToken.HelperName,
                    Argument = new HelperArgumentNode { Name = startToken.Variable },
                    Content = innerNodes
                };
            }
        }

        public class Interpreter
        {
            private readonly Dictionary<string, Func<object, HelperContext, string>> helpers;

            public Interpreter()
            {
                helpers = new Dictionary<string, Func<object, HelperContext, string>>
                {
                    ["each"] = EachHelper
                };
            }

            public void RegisterHelper(string name, Func<object, HelperContext, string> helper)
            {
                helpers[name] = helper;
            }

            public string Evaluate(string template, dynamic data)
            {
                var lexer = new Lexer(template);
                var tokens = lexer.Tokenize();
                var parser = new Parser(tokens);
                var nodes = parser.Parse();

                return string.Join("", nodes.Select(n => n.Evaluate(data, data, helpers)));
            }

            private string EachHelper(object data, HelperContext context)
            {
                if (!(data is System.Collections.IEnumerable) || data is string)
                    throw new Exception("Each helper requires an array of objects");

                var result = new StringBuilder();
                foreach (var item in (System.Collections.IEnumerable)data)
                {
                    var evaluatedContent = string.Join("",
                        context.Content.Select(n => n.Evaluate(item, context.Data, helpers)));
                    result.Append(evaluatedContent);
                }

                return result.ToString();
            }
        }
    }
}
