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
        }
    }

    public class Interpreter
    {
        private readonly Lexer _lexer;
        private readonly Parser _parser;
        private readonly FunctionRegistry _functionRegistry;
        private readonly ITemplateResolver _templateResolver;

        public Interpreter(ITemplateResolver templateResolver = null)
        {
            _functionRegistry = new FunctionRegistry();
            _lexer = new Lexer();
            _parser = new Parser(_functionRegistry);
            _templateResolver = templateResolver;
        }

        public void RegisterFunction(string name, List<ParameterDefinition> parameterTypes, Func<List<dynamic>, dynamic> implementation)
        {
            _functionRegistry.Register(name, parameterTypes, implementation);
        }

        public string Interpret(string template, dynamic data)
        {
            var tokens = _lexer.Tokenize(template);
            var ast = _parser.Parse(tokens);

            // If we have a template resolver, process includes
            if (_templateResolver != null)
            {
                ast = ProcessIncludes(ast);
            }

            return ast.Evaluate(new ExecutionContext(data, _functionRegistry));
        }

        private AstNode ProcessIncludes(AstNode node)
        {
            // Handle IncludeNode
            if (node is IncludeNode includeNode)
            {
                var templateContent = _templateResolver.ResolveTemplate(includeNode.TemplateName);
                var tokens = _lexer.Tokenize(templateContent);
                var includedAst = _parser.Parse(tokens);

                // Process includes in the included template
                includedAst = ProcessIncludes(includedAst);

                // Set the processed template
                includeNode.SetIncludedTemplate(includedAst);
                return includeNode;
            }

            // Handle TemplateNode
            if (node is TemplateNode templateNode)
            {
                var processedChildren = templateNode.Children.Select(ProcessIncludes).ToList();
                return new TemplateNode(processedChildren);
            }

            // Handle IfNode
            if (node is IfNode ifNode)
            {
                var processedBranches = ifNode.ConditionalBranches.Select(branch =>
                    new IfNode.IfBranch(branch.Condition, ProcessIncludes(branch.Body))).ToList();
                var processedElse = ifNode.ElseBranch != null ? ProcessIncludes(ifNode.ElseBranch) : null;
                return new IfNode(processedBranches, processedElse);
            }

            // Handle ForNode
            if (node is ForNode forNode)
            {
                var processedBody = ProcessIncludes(forNode.Body);
                return new ForNode(forNode.IteratorName, forNode.Collection, processedBody);
            }

            // For all other node types, return as is
            return node;
        }
    }

    public class ExecutionContext
    {
        private readonly dynamic _data;
        private readonly Dictionary<string, dynamic> _iteratorValues;
        private readonly Dictionary<string, dynamic> _variables;
        private readonly FunctionRegistry _functionRegistry;

        public ExecutionContext(dynamic data, FunctionRegistry functionRegistry)
        {
            _data = data;
            _iteratorValues = new Dictionary<string, dynamic>();
            _variables = new Dictionary<string, dynamic>();
            _functionRegistry = functionRegistry;
        }

        public void DefineVariable(string name, dynamic value)
        {
            // Check if already defined as a variable
            if (_variables.ContainsKey(name))
            {
                throw new Exception($"Variable '{name}' is already defined");
            }

            // Check if defined as an iterator value
            if (_iteratorValues.ContainsKey(name))
            {
                throw new Exception($"Cannot define variable '{name}' as it conflicts with an existing iterator");
            }

            // Check if defined in the data context
            if (TryResolveValue(name, out _))
            {
                throw new Exception($"Cannot define variable '{name}' as it conflicts with an existing data field");
            }

            // If we get here, the name is safe to use
            _variables[name] = value;
        }

        public ExecutionContext CreateIteratorContext(string iteratorName, dynamic value)
        {
            var newContext = new ExecutionContext(_data, _functionRegistry);

            // Copy variables to new context
            foreach (var variable in _variables)
            {
                newContext._variables.Add(variable.Key, variable.Value);
            }

            // Copy iterators to new context
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

        public bool TryResolveValue(string path, out dynamic value)
        {
            value = null;
            var parts = path.Split('.');
            dynamic current = null;

            // Check if the first part is an iterator
            if (_iteratorValues.ContainsKey(parts[0]))
            {
                current = _iteratorValues[parts[0]];
                parts = parts.Skip(1).ToArray();
            }
            else if (_variables.ContainsKey(parts[0]))
            {
                current = _variables[parts[0]];
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
                    return false;
                }
            }

            value = current;
            return true;
        }

        public virtual dynamic ResolveValue(string path)
        {
            if (TryResolveValue(path, out dynamic value))
            {
                return value;
            }
            throw new Exception($"Unable to resolve path: {path}");
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

        public bool HasParameter(string name)
        {
            return _parameters.ContainsKey(name);
        }

        public dynamic GetParameter(string name)
        {
            return _parameters[name];
        }

        public bool TryGetParameterFromAnyContext(string name, out dynamic value)
        {
            value = null;
            ExecutionContext currentContext = this;

            while (currentContext != null)
            {
                if (currentContext is LambdaExecutionContext lambdaContext)
                {
                    if (lambdaContext._parameters.TryGetValue(name, out value))
                    {
                        return true;
                    }
                    currentContext = lambdaContext._parentContext;
                }
                else
                {
                    // We've reached a non-lambda context (base ExecutionContext)
                    break;
                }
            }

            return false;
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
        Equal,             // ==
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
        For,               // #for
        In,                // in
        If,                // #if
        ElseIf,            // #elseif
        Else,              // #else
        EndFor,            // /for
        EndIf,             // /if
        Let,               // #let
        Assignment,        // =
        Function,          // function name
        Comma,             // ,
        Arrow,             // =>
        Parameter,         // lambda parameter name
        ObjectStart,       // obj(
        Colon,             // :
        Dot,               // .
        Field,             // object field name
        LeftBracket,       // [
        RightBracket,      // ]
        Include,           // #include
        Literal,           // #literal
        EndLiteral,        // /literal
        Capture,           // #capture
        EndCapture         // /capture
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
                if (TryMatch("{{*"))
                {
                    SkipComment();
                    continue;
                }
                else if (TryMatch("{{"))
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

        private void SkipComment()
        {
            // Skip the initial "{{*"
            _position += 3;

            while (_position < _input.Length)
            {
                if (TryMatch("*}}"))
                {
                    _position += 3; // Skip past "*}}"
                    return;
                }
                _position++;
            }

            throw new Exception("Unterminated comment");
        }

        private void TokenizeDirective()
        {
            if (TryMatch("#literal"))
            {
                SkipWhitespace();

                _tokens.Add(new Token(TokenType.Literal, "#literal", _position));
                _position += 8;

                SkipWhitespace();

                if (!TryMatch("}}"))
                {
                    throw new Exception("Unterminated literal directive");
                }
                _position += 2; // Skip }}

                // Capture everything until we find the closing literal directive
                var contentStart = _position;
                var literalStackCount = 0;
                while (_position < _input.Length)
                {
                    int originalPosition = _position;
                    int currentLookahead = _position;
                    if (TryMatch("{{"))
                    {
                        currentLookahead += 2 + WhitespaceCount();
                        if (TryMatchAt("#literal", currentLookahead))
                        {
                            currentLookahead += 8 + WhitespaceCount();
                            if (TryMatchAt("}}", currentLookahead))
                            {
                                currentLookahead += 2;
                                _position += currentLookahead - originalPosition;
                                literalStackCount++;
                                continue;
                            }
                        }

                        if (TryMatchAt("/literal", currentLookahead))
                        {
                            currentLookahead += 8 + WhitespaceCount();
                            if (TryMatchAt("}}", currentLookahead))
                            {
                                currentLookahead += 2;
                                if (literalStackCount > 0)
                                {
                                    literalStackCount--;
                                }
                                else
                                {
                                    // We found the end, create a token with the raw content
                                    var content = _input.Substring(contentStart, _position - contentStart);
                                    _tokens.Add(new Token(TokenType.Text, content, contentStart));
                                    _position += currentLookahead - originalPosition; // Skip {{/literal}} plus whitespace
                                    return;
                                }
                            }
                        }
                    }

                    _position++;
                }
                throw new Exception("Unterminated literal directive");
            }

            while (_position < _input.Length)
            {
                SkipWhitespace();

                if (TryMatch("}}"))
                {
                    _tokens.Add(new Token(TokenType.DirectiveEnd, "}}", _position));
                    _position += 2;
                    return;
                }

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
                if (TryMatch("#let"))
                {
                    _tokens.Add(new Token(TokenType.Let, "#let", _position));
                    _position += 4;
                    continue;
                }
                else if (TryMatch("#capture"))
                {
                    _tokens.Add(new Token(TokenType.Capture, "#capture", _position));
                    _position += 8;
                    continue;
                }
                else if (TryMatch("/capture"))
                {
                    _tokens.Add(new Token(TokenType.EndCapture, "/capture", _position));
                    _position += 8;
                    continue;
                }
                else if (TryMatch("#for"))
                {
                    _tokens.Add(new Token(TokenType.For, "#for", _position));
                    _position += 4;
                }
                else if (TryMatch("#include"))
                {
                    _tokens.Add(new Token(TokenType.Include, "#include", _position));
                    _position += 8;
                    continue;
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
                else if (TryMatch("/for"))
                {
                    _tokens.Add(new Token(TokenType.EndFor, "/for", _position));
                    _position += 4;
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
                else if (TryMatch("=="))
                {
                    _tokens.Add(new Token(TokenType.Equal, "==", _position));
                    _position += 2;
                }
                else if (TryMatch("="))
                {
                    _tokens.Add(new Token(TokenType.Assignment, "=", _position));
                    _position++;
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
            var result = new StringBuilder();

            while (_position < _input.Length && _input[_position] != '"')
            {
                if (_input[_position] == '\\' && _position + 1 < _input.Length)
                {
                    // Handle escape sequences
                    char nextChar = _input[_position + 1];
                    switch (nextChar)
                    {
                        case '"':
                            result.Append('"');
                            break;
                        case '\\':
                            result.Append('\\');
                            break;
                        case 'n':
                            result.Append('\n');
                            break;
                        case 'r':
                            result.Append('\r');
                            break;
                        case 't':
                            result.Append('\t');
                            break;
                        default:
                            throw new Exception($"Invalid escape sequence '\\{nextChar}' at position {_position}");
                    }
                    _position += 2; // Skip both the backslash and the escaped character
                }
                else
                {
                    result.Append(_input[_position]);
                    _position++;
                }
            }

            if (_position >= _input.Length)
            {
                throw new Exception("Unterminated string literal");
            }

            _tokens.Add(new Token(TokenType.String, result.ToString(), _position - result.Length - 1));
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
                case "in":
                    _tokens.Add(new Token(TokenType.In, value, start));
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

        private int WhitespaceCount()
        {
            int originalPosition = _position;
            int currentPosition = _position;
            while (currentPosition < _input.Length && char.IsWhiteSpace(_input[currentPosition]))
            {
                currentPosition++;
            }
            return currentPosition - originalPosition;
        }

        private bool TryMatch(string pattern)
        {
            if (_position + pattern.Length > _input.Length)
            {
                return false;
            }

            return _input.Substring(_position, pattern.Length) == pattern;
        }

        private bool TryMatchAt(string pattern, int position)
        {
            if (position + pattern.Length > _input.Length)
            {
                return false;
            }

            return _input.Substring(position, pattern.Length) == pattern;
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

    public class LiteralNode : AstNode
    {
        private readonly string _content;

        public LiteralNode(string content)
        {
            _content = content;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _content;
        }
    }

    public class IncludeNode : AstNode
    {
        private readonly string _templateName;
        private AstNode _includedTemplate;

        public IncludeNode(string templateName)
        {
            _templateName = templateName;
            _includedTemplate = null;
        }

        public string TemplateName { get { return _templateName; } }

        public void SetIncludedTemplate(AstNode template)
        {
            _includedTemplate = template;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            if (_includedTemplate == null)
            {
                throw new Exception($"Template '{_templateName}' has not been resolved");
            }
            return _includedTemplate.Evaluate(context);
        }
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

    public class InvocationNode : AstNode
    {
        private readonly AstNode _callable;
        private readonly List<AstNode> _arguments;

        public InvocationNode(AstNode callable, List<AstNode> arguments)
        {
            _callable = callable;
            _arguments = arguments;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            // First evaluate all arguments
            var evaluatedArgs = _arguments.Select(arg => arg.Evaluate(context)).ToList();

            // Then evaluate the callable, but handle special cases
            var callable = _callable.Evaluate(context);

            // Now handle the callable based on its type
            if (callable is Func<List<dynamic>, dynamic> lambdaFunc)
            {
                // Direct lambda invocation
                return lambdaFunc(evaluatedArgs);
            }
            else if (callable is FunctionInfo functionInfo)
            {
                FunctionRegistry registry;

                // First check if this is a parameter that contains a function in any parent context
                if (context is LambdaExecutionContext lambdaContext &&
                    lambdaContext.TryGetParameterFromAnyContext(functionInfo.Name, out var paramValue))
                {
                    if (paramValue is Func<List<dynamic>, dynamic> paramFunc)
                    {
                        return paramFunc(evaluatedArgs);
                    }
                    else if (paramValue is FunctionInfo paramFuncInfo)
                    {
                        registry = context.GetFunctionRegistry();
                        if (!registry.TryGetFunction(paramFuncInfo.Name, evaluatedArgs, out var function, out var effectiveArgs))
                        {
                            throw new Exception($"No matching overload found for function '{paramFuncInfo.Name}' with the provided arguments");
                        }
                        registry.ValidateArguments(function, effectiveArgs);
                        return function.Implementation(effectiveArgs);
                    }
                }

                // Check if this is a variable that contains a function
                if (context.TryResolveValue(functionInfo.Name, out var variableValue))
                {
                    if (variableValue is Func<List<dynamic>, dynamic> variableFunc)
                    {
                        return variableFunc(evaluatedArgs);
                    }
                }

                // If not a parameter in any context or parameter isn't a function, try the registry
                registry = context.GetFunctionRegistry();
                if (!registry.TryGetFunction(functionInfo.Name, evaluatedArgs, out var func, out var effArgs))
                {
                    throw new Exception($"No matching overload found for function '{functionInfo.Name}' with the provided arguments");
                }

                registry.ValidateArguments(func, effArgs);

                return func.Implementation(effArgs);
            }


            throw new Exception($"Expression is not callable: {callable?.GetType().Name ?? "null"}");
        }
    }

    public class FunctionInfo
    {
        public string Name { get; }

        public FunctionInfo(string name)
        {
            Name = name;
        }
    }

    public class FunctionReferenceNode : AstNode
    {
        private readonly string _functionName;

        public FunctionReferenceNode(string functionName)
        {
            _functionName = functionName;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return new FunctionInfo(_functionName);
        }
    }

    public class LambdaNode : AstNode
    {
        private readonly List<string> _parameters;
        private readonly AstNode _expression;

        public LambdaNode(List<string> parameters, AstNode expression, FunctionRegistry functionRegistry)
        {
            // Validate parameter names against function registry
            foreach (var param in parameters)
            {
                if (functionRegistry.HasFunction(param))
                {
                    throw new Exception($"Parameter name '{param}' conflicts with an existing function name");
                }
            }

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

    public class LetNode : AstNode
    {
        private readonly string _variableName;
        private readonly AstNode _expression;

        public LetNode(string variableName, AstNode expression)
        {
            _variableName = variableName;
            _expression = expression;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            var value = _expression.Evaluate(context);
            context.DefineVariable(_variableName, value);
            return string.Empty; // Let statements don't produce output
        }
    }

    public class CaptureNode : AstNode
    {
        private readonly string _variableName;
        private readonly AstNode _body;

        public CaptureNode(string variableName, AstNode body)
        {
            _variableName = variableName;
            _body = body;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            var result = _body.Evaluate(context);
            context.DefineVariable(_variableName, result.ToString());
            return string.Empty; // Capture doesn't output anything directly
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

    public class ForNode : AstNode
    {
        private readonly string _iteratorName;
        private readonly AstNode _collection;
        private readonly AstNode _body;

        public string IteratorName { get { return _iteratorName; } }

        public AstNode Collection { get { return _collection; } }

        public AstNode Body { get { return _body; } }

        public ForNode(string iteratorName, AstNode collection, AstNode body)
        {
            _iteratorName = iteratorName;
            _collection = collection;
            _body = body;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            // Check if iterator name conflicts with existing variable
            if (context.TryResolveValue(_iteratorName, out _))
            {
                throw new Exception($"Iterator name '{_iteratorName}' conflicts with an existing variable or field");
            }

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

        public List<IfBranch> ConditionalBranches { get { return _conditionalBranches; } }

        public AstNode ElseBranch { get { return _elseBranch; } }

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

        public List<AstNode> Children { get { return _children; } }

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
        private readonly FunctionRegistry _functionRegistry;

        public Parser(FunctionRegistry functionRegistry)
        {
            _functionRegistry = functionRegistry;
        }

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

                    if (nextToken.Type == TokenType.Let)
                    {
                        nodes.Add(ParseLetStatement());
                    }
                    else if (nextToken.Type == TokenType.Capture)
                    {
                        nodes.Add(ParseCaptureStatement());
                    }
                    else if (nextToken.Type == TokenType.Literal)
                    {
                        nodes.Add(ParseLiteralStatement());
                    }
                    else if (nextToken.Type == TokenType.Include)
                    {
                        nodes.Add(ParseIncludeStatement());
                    }
                    else if (nextToken.Type == TokenType.If)
                    {
                        nodes.Add(ParseIfStatement());
                    }
                    else if (nextToken.Type == TokenType.For)
                    {
                        nodes.Add(ParseForStatement());
                    }
                    else if (nextToken.Type == TokenType.ElseIf ||
                             nextToken.Type == TokenType.Else ||
                             nextToken.Type == TokenType.EndIf ||
                             nextToken.Type == TokenType.EndFor ||
                             nextToken.Type == TokenType.EndCapture)
                    {
                        if (_position == 0)
                        {
                            throw new Exception(string.Format("Unexpected token: {0} at position {1}", token.Type, token.Position));
                        }
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

        private AstNode ParseLetStatement()
        {
            Advance(); // Skip {{
            Advance(); // Skip #let

            var variableName = Expect(TokenType.Variable).Value;
            Advance();

            Expect(TokenType.Assignment);
            Advance();

            var expression = ParseExpression();

            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new LetNode(variableName, expression);
        }

        private AstNode ParseCaptureStatement()
        {
            Advance(); // Skip {{
            Advance(); // Skip #capture

            var variableName = Expect(TokenType.Variable).Value;
            Advance();

            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            var body = ParseTemplate();

            // Parse the closing capture tag
            Expect(TokenType.DirectiveStart);
            Advance(); // Skip {{
            Expect(TokenType.EndCapture);
            Advance(); // Skip /capture
            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new CaptureNode(variableName, body);
        }

        private AstNode ParseLiteralStatement()
        {
            Advance(); // Skip {{
            Advance(); // Skip #literal

            // The next token should be the raw content
            Expect(TokenType.Text);
            var content = Current().Value;
            Advance();

            return new LiteralNode(content);
        }

        private AstNode ParseIncludeStatement()
        {
            Advance(); // Skip {{
            Advance(); // Skip #include

            var templateName = Expect(TokenType.Variable).Value;
            Advance();

            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new IncludeNode(templateName);
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

        private AstNode ParseInvocation(AstNode callable)
        {
            Advance(); // Skip (
            var arguments = new List<AstNode>();

            if (Current().Type != TokenType.RightParen)
            {
                while (true)
                {
                    arguments.Add(ParseExpression());

                    if (Current().Type == TokenType.RightParen)
                        break;

                    Expect(TokenType.Comma);
                    Advance();
                }
            }

            Expect(TokenType.RightParen);
            Advance();

            return new InvocationNode(callable, arguments);
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

            return new LambdaNode(parameters, expression, _functionRegistry);
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

        private AstNode ParseForStatement()
        {
            Advance(); // Skip {{
            Advance(); // Skip #for
            var iteratorName = Expect(TokenType.Variable).Value;
            Advance();

            Expect(TokenType.In);
            Advance();

            var collection = ParseExpression();
            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            var body = ParseTemplate();

            // Handle the closing for tag
            Expect(TokenType.DirectiveStart);
            Advance(); // Skip {{
            Expect(TokenType.EndFor);
            Advance(); // Skip /for
            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new ForNode(iteratorName, collection, body);
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
                    if (IsLambdaAhead())
                    {
                        expr = ParseLambda();
                    }
                    else
                    {
                        expr = ParseGroupExpression();
                    }
                    break;

                case TokenType.Function:
                    expr = new FunctionReferenceNode(token.Value);
                    Advance();
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

            // Handle any invocations that follow the primary expression
            while (_position < _tokens.Count && Current().Type == TokenType.LeftParen)
            {
                expr = ParseInvocation(expr);
            }

            // Handle any fields accessed after a dot
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

        private bool IsLambdaAhead()
        {
            var pos = _position;
            try
            {
                Advance();
                bool firstParam = true;
                while (_position < _tokens.Count && Current().Type != TokenType.RightParen)
                {
                    if (firstParam)
                    {
                        if (Current().Type != TokenType.Variable)
                        {
                            return false;
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
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    Advance();
                }

                if (Current().Type != TokenType.RightParen)
                {
                    return false;
                }

                Advance();

                return Current().Type == TokenType.Arrow;
            }
            finally
            {
                _position = pos;
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

        public bool HasFunction(string name)
        {
            return _functions.ContainsKey(name);
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

            Register("datetime",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var dateStr = args[0] as string;
                    if (string.IsNullOrEmpty(dateStr))
                        throw new Exception("datetime function requires a non-empty string argument");

                    try
                    {
                        return DateTime.Parse(dateStr);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to parse date string '{dateStr}': {ex.Message}");
                    }
                });

            Register("format",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var date = args[0] as DateTime?;
                    var format = args[1] as string;

                    if (!date.HasValue)
                        throw new Exception("format function requires a valid DateTime as first argument");
                    if (string.IsNullOrEmpty(format))
                        throw new Exception("format function requires a non-empty format string as second argument");

                    try
                    {
                        return date.Value.ToString(format);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to format date with format string '{format}': {ex.Message}");
                    }
                });

            Register("addYears",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var date = args[0] as DateTime?;
                    var years = Convert.ToInt32(args[1]);

                    if (!date.HasValue)
                        throw new Exception("addYears function requires a valid DateTime as first argument");

                    try
                    {
                        return date.Value.AddYears(years);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to add {years} years to date: {ex.Message}");
                    }
                });

            Register("addMonths",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var date = args[0] as DateTime?;
                    var months = Convert.ToInt32(args[1]);

                    if (!date.HasValue)
                        throw new Exception("addMonths function requires a valid DateTime as first argument");

                    try
                    {
                        return date.Value.AddMonths(months);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to add {months} months to date: {ex.Message}");
                    }
                });

            Register("addDays",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var date = args[0] as DateTime?;
                    var days = Convert.ToInt32(args[1]);

                    if (!date.HasValue)
                        throw new Exception("addDays function requires a valid DateTime as first argument");

                    try
                    {
                        return date.Value.AddDays(days);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to add {days} days to date: {ex.Message}");
                    }
                });

            Register("addHours",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var date = args[0] as DateTime?;
                    var hours = Convert.ToInt32(args[1]);

                    if (!date.HasValue)
                        throw new Exception("addHours function requires a valid DateTime as first argument");

                    try
                    {
                        return date.Value.AddHours(hours);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to add {hours} hours to date: {ex.Message}");
                    }
                });

            Register("addMinutes",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var date = args[0] as DateTime?;
                    var minutes = Convert.ToInt32(args[1]);

                    if (!date.HasValue)
                        throw new Exception("addMinutes function requires a valid DateTime as first argument");

                    try
                    {
                        return date.Value.AddMinutes(minutes);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to add {minutes} minutes to date: {ex.Message}");
                    }
                });

            Register("addSeconds",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal))
                },
                args =>
                {
                    var date = args[0] as DateTime?;
                    var seconds = Convert.ToInt32(args[1]);

                    if (!date.HasValue)
                        throw new Exception("addSeconds function requires a valid DateTime as first argument");

                    try
                    {
                        return date.Value.AddSeconds(seconds);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to add {seconds} seconds to date: {ex.Message}");
                    }
                });

            Register("now",
                new List<ParameterDefinition>(),
                args =>
                {
                    return DateTime.Now;
                });

            Register("utcNow",
                new List<ParameterDefinition>(),
                args =>
                {
                    return DateTime.UtcNow;
                });

            Register("uri",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                args =>
                {
                    var uriString = args[0] as string;
                    if (string.IsNullOrEmpty(uriString))
                        throw new Exception("uri function requires a non-empty string argument");

                    try
                    {
                        return new Uri(uriString);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Failed to parse uri string '{uriString}': {ex.Message}");
                    }
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

    public interface ITemplateResolver
    {
        string ResolveTemplate(string templateName);
    }

    public class TemplateRegistry : ITemplateResolver
    {
        private readonly Dictionary<string, string> _templates;

        public TemplateRegistry()
        {
            _templates = new Dictionary<string, string>();
        }

        public void RegisterTemplate(string name, string template)
        {
            _templates[name] = template;
        }

        public string ResolveTemplate(string templateName)
        {
            if (!_templates.TryGetValue(templateName, out var template))
            {
                throw new Exception($"Template '{templateName}' not found");
            }
            return template;
        }
    }
}
