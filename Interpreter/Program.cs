using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

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
        private readonly IDataverseService _dataverseService;
        private readonly int _maxRecursionDepth;

        public Lexer Lexer { get { return _lexer; } }
        public Parser Parser { get { return _parser; } }

        public Interpreter(ITemplateResolver templateResolver = null, IDataverseService dataverseService = null, int maxRecursionDepth = 1000)
        {
            _functionRegistry = new FunctionRegistry();
            _lexer = new Lexer();
            _parser = new Parser(_functionRegistry);
            _templateResolver = templateResolver;
            _dataverseService = dataverseService;
            _maxRecursionDepth = maxRecursionDepth;

            RegisterDataverseFunctions();
        }

        public void RegisterFunction(string name, List<ParameterDefinition> parameterTypes, Func<ExecutionContext, AstNode, List<dynamic>, dynamic> implementation)
        {
            _functionRegistry.Register(name, parameterTypes, implementation);
        }

        private void RegisterDataverseFunctions()
        {
            _functionRegistry.Register("fetch",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    if (_dataverseService == null)
                    {
                        throw new TemplateEvaluationException("Dataverse service not configured. The fetch function requires a DataverseService to be provided to the Interpreter.", context);
                    }

                    var fetchXml = args[0] as string;

                    if (string.IsNullOrEmpty(fetchXml))
                    {
                        throw new TemplateEvaluationException("fetch function requires a non-empty FetchXML string", context);
                    }

                    return _dataverseService.RetrieveMultiple(fetchXml);
                });
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

            return ast.Evaluate(new ExecutionContext(data, _functionRegistry, null, _maxRecursionDepth, ast));
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
                return new TemplateNode(processedChildren, node.Location);
            }

            // Handle IfNode
            if (node is IfNode ifNode)
            {
                var processedBranches = ifNode.ConditionalBranches.Select(branch =>
                    new IfNode.IfBranch(branch.Condition, ProcessIncludes(branch.Body))).ToList();
                var processedElse = ifNode.ElseBranch != null ? ProcessIncludes(ifNode.ElseBranch) : null;
                return new IfNode(processedBranches, processedElse, node.Location);
            }

            // Handle ForNode
            if (node is ForNode forNode)
            {
                var processedBody = ProcessIncludes(forNode.Body);
                return new ForNode(forNode.IteratorName, forNode.Collection, processedBody, node.Location);
            }

            // For all other node types, return as is
            return node;
        }
    }

    public class ExecutionContext
    {
        protected readonly dynamic _data;
        private readonly Dictionary<string, dynamic> _iteratorValues;
        private readonly Dictionary<string, dynamic> _variables;
        private readonly FunctionRegistry _functionRegistry;
        protected readonly ExecutionContext _parentContext;
        protected readonly int _maxDepth;
        private readonly int _currentDepth;
        private readonly AstNode _callSite;

        public int MaxDepth { get { return _maxDepth; } }
        public int CurrentDepth { get { return _currentDepth; } }
        public ExecutionContext Parent { get { return _parentContext; } }
        public AstNode CallSite { get { return _callSite; } }

        public ExecutionContext(
            dynamic data,
            FunctionRegistry functionRegistry,
            ExecutionContext parentContext,
            int maxDepth,
            AstNode callSite)
        {
            _data = data;
            _iteratorValues = new Dictionary<string, dynamic>();
            _variables = new Dictionary<string, dynamic>();
            _functionRegistry = functionRegistry;
            _parentContext = parentContext;
            _maxDepth = maxDepth;
            _callSite = callSite;
            _currentDepth = _parentContext != null ? _parentContext.CurrentDepth + 1 : 0;
            CheckStackDepth();
        }

        public void CheckStackDepth()
        {
            if (_currentDepth >= _maxDepth)
            {
                throw new TemplateEvaluationException($"Maximum call stack depth {_maxDepth} has been exceeded.", this);
            }
        }

        public virtual void DefineVariable(string name, dynamic value)
        {
            // Check if already defined as a variable, an iterator variable, or defined in the data context
            if (_variables.ContainsKey(name) || _iteratorValues.ContainsKey(name) || TryResolveValue(name, out _))
            {
                throw new TemplateEvaluationException(
                    $"Cannot define variable '{name}' because it conflicts with an existing variable or field",
                    this);
            }

            // If we get here, the name is safe to use
            _variables[name] = value;
        }

        public ExecutionContext CreateIteratorContext(string iteratorName, dynamic value, AstNode callSite)
        {
            var newContext = new ExecutionContext(_data, _functionRegistry, this, MaxDepth, callSite);

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

        protected bool TryGetDataProperty(string propertyName, out dynamic value)
        {
            value = null;

            if (_data == null || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            if (_data is ExpandoObject)
            {
                return ((IDictionary<string, object>)_data).TryGetValue(propertyName, out value);
            }

            if (_data is IDictionary<string, object> dict)
            {
                return dict.TryGetValue(propertyName, out value);
            }

            PropertyInfo propertyInfo = _data.GetType().GetProperty(propertyName);
            if (propertyInfo != null)
            {
                value = _data[propertyName];
                return true;
            }

            return false;
        }

        public virtual bool TryResolveNonShadowableValue(string path, out dynamic value)
        {
            value = null;
            var parts = path.Split('.');
            dynamic current = null;

            if (_iteratorValues.ContainsKey(parts[0]))
            {
                current = _iteratorValues[parts[0]];
                parts = parts.Skip(1).ToArray();
            }
            else if (_variables.TryGetValue(parts[0], out current))
            {
                parts = parts.Skip(1).ToArray();
            }
            else if (TryGetDataProperty(parts[0], out current))
            {
                current = _data;
            }
            else
            {
                if (_parentContext != null)
                {
                    return _parentContext.TryResolveNonShadowableValue(path, out value);
                }
                return false;
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

        public virtual bool TryResolveValue(string path, out dynamic value)
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
            else if (TryGetDataProperty(parts[0], out current))
            {
                current = _data;
            }
            else if (_parentContext != null)
            {
                return _parentContext.TryResolveValue(path, out value);
            }
            else
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (!TryGetDataProperty(part, out current))
                {
                    return false;
                }

                if (TypeHelper.IsConvertibleToDecimal(current))
                {
                    current = (decimal)current;
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
            throw new TemplateEvaluationException($"Unknown identifier: {path}", this);
        }
    }

    public class LambdaExecutionContext : ExecutionContext
    {
        private readonly Dictionary<string, dynamic> _parameters;
        private readonly Dictionary<string, dynamic> _variables;
        private readonly ExecutionContext _definitionContext;

        public LambdaExecutionContext(
            ExecutionContext parentContext,
            ExecutionContext definitionContext,
            List<string> parameterNames,
            List<dynamic> parameterValues,
            AstNode node)
            : base((object)parentContext.GetData(), parentContext.GetFunctionRegistry(), parentContext, parentContext.MaxDepth, node)
        {
            _parameters = new Dictionary<string, dynamic>();
            _variables = new Dictionary<string, dynamic>();
            _definitionContext = definitionContext;

            if (parameterNames.Count > parameterValues.Count)
            {
                var missingCount = parameterNames.Count - parameterValues.Count;
                var missingParams = string.Join(", ", parameterNames.Skip(parameterValues.Count).Take(missingCount));
                throw new TemplateEvaluationException($"Not enough parameter values provided. Missing values for: {missingParams}", this);
            }

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

        public override void DefineVariable(string name, dynamic value)
        {
            // Check if already defined as a variable
            if (_variables.ContainsKey(name))
            {
                throw new TemplateEvaluationException(
                    $"Cannot define variable '{name}' because it conflicts with an existing variable or field",
                    this);
            }

            // Check if defined as a parameter
            if (_parameters.ContainsKey(name))
            {
                throw new TemplateEvaluationException(
                    $"Cannot define variable '{name}' because it conflicts with a parameter name",
                    this);
            }

            if (_parentContext.TryResolveNonShadowableValue(name, out _))
            {
                throw new TemplateEvaluationException(
                    $"Cannot define variable '{name}' because it conflicts with an existing variable or field",
                    this);
            }

            _variables[name] = value;
        }

        public bool TryGetParameterFromAnyContext(string name, out dynamic value)
        {
            value = null;

            // Check parameters in current context
            if (_parameters.TryGetValue(name, out value))
            {
                return true;
            }

            // Check variables in current context
            if (_variables.TryGetValue(name, out value))
            {
                return true;
            }

            // Check definition context (for closure variables)
            if (_definitionContext is LambdaExecutionContext defLambdaContext &&
                defLambdaContext.TryGetParameterFromAnyContext(name, out value))
            {
                return true;
            }

            // Check caller context (for recursive call stack)
            if (_parentContext is LambdaExecutionContext callerLambdaContext &&
                callerLambdaContext.TryGetParameterFromAnyContext(name, out value))
            {
                return true;
            }

            return false;
        }

        public override bool TryResolveNonShadowableValue(string path, out dynamic value)
        {
            value = null;
            var parts = path.Split('.');
            dynamic current = null;

            if (_variables.TryGetValue(parts[0], out current))
            {
                parts = parts.Skip(1).ToArray();
            }
            else if (_parameters.TryGetValue(parts[0], out current))
            {
                parts = parts.Skip(1).ToArray();
            }
            else
            {
                if (_parentContext != null)
                {
                    return _parentContext.TryResolveNonShadowableValue(path, out value);
                }
                return false;
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

        public override bool TryResolveValue(string path, out dynamic value)
        {
            value = null;
            var parts = path.Split('.');
            dynamic current = null;

            if (_variables.TryGetValue(parts[0], out current))
            {
                parts = parts.Skip(1).ToArray();
            }
            else if (_parameters.TryGetValue(parts[0], out current))
            {
                parts = parts.Skip(1).ToArray();
            }
            else
            {
                dynamic descendentValue;

                if (_definitionContext.TryResolveValue(path, out descendentValue))
                {
                    value = descendentValue;
                    return true;
                }
                else if (_parentContext.TryResolveValue(path, out descendentValue))
                {
                    value = descendentValue;
                    return true;
                }

                return false;
            }

            foreach (var part in parts)
            {
                if (!TryGetDataProperty(part, out current))
                {
                    return false;
                }

                if (TypeHelper.IsConvertibleToDecimal(current))
                {
                    current = (decimal)current;
                }
            }

            value = current;
            return true;
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
                        throw new TemplateEvaluationException($"Unknown identifier: {path}", this);
                    }
                }

                return current;
            }
            else if (_variables.ContainsKey(parts[0]))
            {
                dynamic current = _variables[parts[0]];

                // Handle nested property access for parameters
                for (int i = 1; i < parts.Length; i++)
                {
                    try
                    {
                        current = ((IDictionary<string, object>)current)[parts[i]];
                    }
                    catch
                    {
                        throw new TemplateEvaluationException($"Unknown identifier: {path}", this);
                    }
                }

                return current;
            }

            try
            {
                // try closure context first
                return _definitionContext.ResolveValue(path);
            }
            catch
            {
                // If not found in parameters, delegate to parent context
                return _parentContext.ResolveValue(path);
            }
        }
    }

    public class InitializationException : Exception
    {
        public string Descriptor { get; }

        public InitializationException(string message)
            : base($"Error during initialization: {message}")
        {
            Descriptor = message;
        }
    }

    public class TemplateParsingException : Exception
    {
        public SourceLocation Location { get; }

        public string Descriptor { get; }

        public TemplateParsingException(string message, SourceLocation location)
            : base($"Error at {location}: {message}")
        {
            Location = location;
            Descriptor = message;
        }
    }

    public class TemplateEvaluationException : Exception
    {
        public SourceLocation Location { get; }
        public string Descriptor { get; }
        public override string StackTrace { get; }
        public AstNode CallSite { get; }

        public TemplateEvaluationException(
            string message,
            ExecutionContext frame)
            : base(FormatMessage(message, frame.CallSite.Location))
        {
            Location = frame.CallSite.Location;
            Descriptor = message;
            CallSite = frame.CallSite;
            StackTrace = FormatStackTrace(message, frame.CallSite.Location, frame);
        }

        private static string FormatMessage(string message, SourceLocation location)
        {
            return $"Error at {location}: {message}";
        }

        private static string FormatStackTrace(string message, SourceLocation location, ExecutionContext frame)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Error at {location}: {message}");

            if (frame != null)
            {
                var current = frame;
                while (current != null)
                {
                    sb.AppendLine($"  at {current.CallSite.ToStackString()} (line {current.CallSite.Location.Line}, column {current.CallSite.Location.Column}{(current.CallSite.Location.Source != null ? ", " + current.CallSite.Location.Source : "")})");
                    current = current.Parent;
                }
            }

            return sb.ToString();
        }
    }

    public class SourceLocation
    {
        public int Line { get; }
        public int Column { get; }
        public int Position { get; }
        public string Source { get; }

        public SourceLocation(int line, int column, int position, string source = null)
        {
            Line = line;
            Column = column;
            Position = position;
            Source = source;
        }

        public override string ToString()
        {
            return Source != null
                ? $"line {Line}, column {Column} in {Source}"
                : $"line {Line}, column {Column}";
        }
    }

    public class Token
    {
        public TokenType Type { get; private set; }
        public string Value { get; private set; }
        public SourceLocation Location { get; private set; }

        public Token(TokenType type, string value, SourceLocation location)
        {
            Type = type;
            Value = value;
            Location = location;
        }
    }

    public enum TokenType
    {
        Text,
        Whitespace,
        Newline,
        DirectiveStart,    // {{ or {{-
        DirectiveEnd,      // }} or -}}
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
        For,               // for
        In,                // in
        If,                // if
        ElseIf,            // elseif
        Else,              // else
        EndFor,            // /for
        EndIf,             // /if
        Let,               // let
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
        Include,           // include
        Literal,           // literal
        EndLiteral,        // /literal
        Capture,           // capture
        EndCapture,        // /capture
        CommentStart,      // *
        CommentEnd         // *
    }

    public class Lexer
    {
        private string _input;
        private int _position;
        private int _line;
        private int _column;
        private readonly List<Token> _tokens;
        private string _sourceName;

        private struct PositionState
        {
            public int Position { get; set; }
            public int Line { get; set; }
            public int Column { get; set; }

            public PositionState(int position, int line, int column)
            {
                Position = position;
                Line = line;
                Column = column;
            }
        }

        private void ThrowLexerError(string message)
        {
            var location = new SourceLocation(_line, _column, _position, _sourceName);
            throw new TemplateParsingException(message, location);
        }

        private PositionState SavePosition()
        {
            return new PositionState(_position, _line, _column);
        }

        private void RestorePosition(PositionState state)
        {
            _position = state.Position;
            _line = state.Line;
            _column = state.Column;
        }

        public Lexer()
        {
            _tokens = new List<Token>();
        }

        public IReadOnlyList<Token> Tokenize(string input, string sourceName = null)
        {
            _input = input;
            _position = 0;
            _line = 1;
            _column = 1;
            _tokens.Clear();

            if (sourceName != null)
            {
                _sourceName = sourceName;
            }

            while (_position < _input.Length)
            {
                if (TryMatch("{{-"))
                {
                    AddToken(TokenType.DirectiveStart, "{{-");
                    UpdatePositionAndTracking(3);
                    TokenizeDirective();
                }
                else if (TryMatch("{{"))
                {
                    AddToken(TokenType.DirectiveStart, "{{");
                    UpdatePositionAndTracking(2);
                    TokenizeDirective();
                }
                else if (IsNewline(_position))
                {
                    TokenizeNewline();
                }
                else if (IsWhitespace(_position))
                {
                    TokenizeWhitespace();
                }
                else
                {
                    TokenizeText();
                }
            }

            return _tokens;
        }

        private void TokenizeComment()
        {
            while (_position < _input.Length)
            {
                if (TryMatch("*"))
                {
                    var savedPosition = SavePosition();
                    UpdatePositionAndTracking(1); // Skip past "*"

                    while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
                    {
                        UpdatePositionAndTracking(1);
                    }

                    if (TryMatch("}}"))
                    {
                        AddToken(TokenType.CommentEnd, "*", savedPosition);
                        AddToken(TokenType.DirectiveEnd, "}}");
                        UpdatePositionAndTracking(2); // Skip past "}}"
                        return;
                    }
                    else if (TryMatch("-}}"))
                    {
                        AddToken(TokenType.CommentEnd, "*", savedPosition);
                        AddToken(TokenType.DirectiveEnd, "-}}");
                        UpdatePositionAndTracking(3); // Skip past "-}}"
                        return;
                    }
                }

                UpdatePositionAndTracking(1);
            }

            ThrowLexerError("Unterminated comment");
        }

        private void TokenizeDirective()
        {
            SkipWhitespace();

            if (TryMatch("*"))
            {
                AddToken(TokenType.CommentStart, "*");
                UpdatePositionAndTracking(1);
                TokenizeComment();
                return;
            }

            if (TryMatch("literal"))
            {
                AddToken(TokenType.Literal, "literal");
                UpdatePositionAndTracking(7);

                SkipWhitespace();

                if (!TryMatch("}}") && !TryMatch("-}}"))
                {
                    ThrowLexerError("Unterminated literal directive");
                }

                if (TryMatch("}}"))
                {
                    AddToken(TokenType.DirectiveEnd, "}}");
                    UpdatePositionAndTracking(2); // Skip }}
                }
                else
                {
                    AddToken(TokenType.DirectiveEnd, "-}}");
                    UpdatePositionAndTracking(3); // Skip -}}
                }

                // Capture everything until we find the closing literal directive
                var startPosition = SavePosition();
                var literalStackCount = 0;

                while (_position < _input.Length)
                {
                    int originalPosition = _position;
                    var savedPosition = SavePosition();

                    if (TryMatch("{{") || TryMatch("{{-"))
                    {
                        Token directiveStartToken = null;

                        if (TryMatch("{{"))
                        {
                            directiveStartToken = new Token(TokenType.DirectiveStart, "{{", CreateLocation(savedPosition));
                            savedPosition = UpdatePositionAndTrackingOnState(2 + WhitespaceCount(savedPosition.Position + 2), savedPosition);
                        }
                        else
                        {
                            directiveStartToken = new Token(TokenType.DirectiveStart, "{{-", CreateLocation(savedPosition));
                            savedPosition = UpdatePositionAndTrackingOnState(3 + WhitespaceCount(savedPosition.Position + 3), savedPosition);
                        }

                        if (TryMatchAt("literal", savedPosition.Position))
                        {
                            savedPosition = UpdatePositionAndTrackingOnState(7 + WhitespaceCount(savedPosition.Position + 7), savedPosition);

                            if (TryMatchAt("}}", savedPosition.Position))
                            {
                                savedPosition = UpdatePositionAndTrackingOnState(2, savedPosition);
                                UpdatePositionAndTracking(savedPosition.Position - originalPosition);
                                literalStackCount++;
                                continue;
                            }

                            if (TryMatchAt("-}}", savedPosition.Position))
                            {
                                savedPosition = UpdatePositionAndTrackingOnState(3, savedPosition);
                                UpdatePositionAndTracking(savedPosition.Position - originalPosition);
                                literalStackCount++;
                                continue;
                            }
                        }

                        if (TryMatchAt("/literal", savedPosition.Position))
                        {
                            var endLiteralToken = new Token(TokenType.EndLiteral, "/literal", CreateLocation(savedPosition));
                            savedPosition = UpdatePositionAndTrackingOnState(8 + WhitespaceCount(savedPosition.Position + 8), savedPosition);

                            if (TryMatchAt("}}", savedPosition.Position) || TryMatchAt("-}}", savedPosition.Position))
                            {
                                Token directiveEndToken = null;

                                if (TryMatchAt("}}", savedPosition.Position))
                                {
                                    directiveEndToken = new Token(TokenType.DirectiveEnd, "}}", CreateLocation(savedPosition));
                                    savedPosition = UpdatePositionAndTrackingOnState(2, savedPosition);
                                }
                                else
                                {
                                    directiveEndToken = new Token(TokenType.DirectiveEnd, "-}}", CreateLocation(savedPosition));
                                    savedPosition = UpdatePositionAndTrackingOnState(3, savedPosition);
                                }

                                if (literalStackCount > 0)
                                {
                                    literalStackCount--;
                                }
                                else
                                {
                                    // We found the end, create a token with the raw content
                                    var content = _input.Substring(startPosition.Position, _position - startPosition.Position);
                                    AddToken(TokenType.Text, content, startPosition);
                                    _tokens.Add(directiveStartToken);
                                    _tokens.Add(endLiteralToken);
                                    _tokens.Add(directiveEndToken);
                                    UpdatePositionAndTracking(savedPosition.Position - originalPosition); // Skip {{/literal}} plus whitespace
                                    return;
                                }
                            }
                        }
                    }

                    UpdatePositionAndTracking(1);
                }

                ThrowLexerError("Unterminated literal directive");
            }

            while (_position < _input.Length)
            {
                SkipWhitespace();

                if (_position >= _input.Length)
                {
                    // if the whitespace skipped was the last thing in the input buffer
                    continue;
                }

                if (TryMatch("}}"))
                {
                    AddToken(TokenType.DirectiveEnd, "}}");
                    UpdatePositionAndTracking(2);
                    return;
                }

                if (TryMatch("-}}"))
                {
                    AddToken(TokenType.DirectiveEnd, "-}}");
                    UpdatePositionAndTracking(3);
                    return;
                }

                if (TryMatch(","))
                {
                    AddToken(TokenType.Comma, ",");
                    UpdatePositionAndTracking(1);
                    continue;
                }

                if (TryMatch("=>"))
                {
                    AddToken(TokenType.Arrow, "=>");
                    UpdatePositionAndTracking(2);
                    continue;
                }

                if (TryMatch("obj("))
                {
                    AddToken(TokenType.ObjectStart, "obj(");
                    UpdatePositionAndTracking(4);
                    continue;
                }

                if (TryMatch(":"))
                {
                    AddToken(TokenType.Colon, ":");
                    UpdatePositionAndTracking(1);
                    continue;
                }

                if (TryMatch("."))
                {
                    AddToken(TokenType.Dot, ".");
                    UpdatePositionAndTracking(1);
                    continue;
                }

                if (TryMatch("["))
                {
                    AddToken(TokenType.LeftBracket, "[");
                    UpdatePositionAndTracking(1);
                    continue;
                }

                if (TryMatch("]"))
                {
                    AddToken(TokenType.RightBracket, "]");
                    UpdatePositionAndTracking(1);
                    continue;
                }

                // After a dot, treat identifiers as field names
                if (_position > 0 && _tokens.Count > 0 &&
                    _tokens[_tokens.Count - 1].Type == TokenType.Dot &&
                    _position < _input.Length &&
                    (char.IsLetter(_input[_position]) || _input[_position] == '_'))
                {
                    var savedState = SavePosition();

                    while (_position < _input.Length &&
                           (char.IsLetterOrDigit(_input[_position]) || _input[_position] == '_'))
                    {
                        UpdatePositionAndTracking(1);
                    }

                    var fieldName = _input.Substring(savedState.Position, _position - savedState.Position);
                    AddToken(TokenType.Field, fieldName, savedState);
                    continue;
                }

                // Check for function names before other identifiers
                if (char.IsLetter(_input[_position]))
                {
                    var savedPosition = SavePosition();

                    while (_position < _input.Length && char.IsLetter(_input[_position]))
                    {
                        UpdatePositionAndTracking(1);
                    }

                    var value = _input.Substring(savedPosition.Position, _position - savedPosition.Position);

                    // Look ahead for opening parenthesis to distinguish functions from variables
                    SkipWhitespace();

                    if (_position < _input.Length && _input[_position] == '(')
                    {
                        AddToken(TokenType.Function, value, savedPosition);
                        continue;
                    }
                    else
                    {
                        // Rewind position as this is not a function
                        RestorePosition(savedPosition);
                    }
                }

                // Match keywords and operators
                if (TryMatch("let"))
                {
                    AddToken(TokenType.Let, "let");
                    UpdatePositionAndTracking(3);
                    continue;
                }
                else if (TryMatch("capture"))
                {
                    AddToken(TokenType.Capture, "capture");
                    UpdatePositionAndTracking(7);
                    continue;
                }
                else if (TryMatch("/capture"))
                {
                    AddToken(TokenType.EndCapture, "/capture");
                    UpdatePositionAndTracking(8);
                    continue;
                }
                else if (TryMatch("for"))
                {
                    AddToken(TokenType.For, "for");
                    UpdatePositionAndTracking(3);
                }
                else if (TryMatch("include"))
                {
                    AddToken(TokenType.Include, "include");
                    UpdatePositionAndTracking(7);
                    continue;
                }
                else if (TryMatch("if"))
                {
                    AddToken(TokenType.If, "if");
                    UpdatePositionAndTracking(2);
                }
                else if (TryMatch("elseif"))
                {
                    AddToken(TokenType.ElseIf, "elseif");
                    UpdatePositionAndTracking(6);
                }
                else if (TryMatch("else"))
                {
                    AddToken(TokenType.Else, "else");
                    UpdatePositionAndTracking(4);
                }
                else if (TryMatch("/for"))
                {
                    AddToken(TokenType.EndFor, "/for");
                    UpdatePositionAndTracking(4);
                }
                else if (TryMatch("/if"))
                {
                    AddToken(TokenType.EndIf, "/if");
                    UpdatePositionAndTracking(3);
                }
                else if (TryMatch(">="))
                {
                    AddToken(TokenType.GreaterThanEqual, ">=");
                    UpdatePositionAndTracking(2);
                }
                else if (TryMatch("<="))
                {
                    AddToken(TokenType.LessThanEqual, "<=");
                    UpdatePositionAndTracking(2);
                }
                else if (TryMatch("=="))
                {
                    AddToken(TokenType.Equal, "==");
                    UpdatePositionAndTracking(2);
                }
                else if (TryMatch("="))
                {
                    AddToken(TokenType.Assignment, "=");
                    UpdatePositionAndTracking(1);
                }
                else if (TryMatch("!="))
                {
                    AddToken(TokenType.NotEqual, "!=");
                    UpdatePositionAndTracking(2);
                }
                else if (TryMatch("&&"))
                {
                    AddToken(TokenType.And, "&&");
                    UpdatePositionAndTracking(2);
                }
                else if (TryMatch("||"))
                {
                    AddToken(TokenType.Or, "||");
                    UpdatePositionAndTracking(2);
                }
                else if (TryMatch(">"))
                {
                    AddToken(TokenType.GreaterThan, ">");
                    UpdatePositionAndTracking(1);
                }
                else if (TryMatch("<"))
                {
                    AddToken(TokenType.LessThan, "<");
                    UpdatePositionAndTracking(1);
                }
                else if (TryMatch("!"))
                {
                    AddToken(TokenType.Not, "!");
                    UpdatePositionAndTracking(1);
                }
                else if (TryMatch("+"))
                {
                    AddToken(TokenType.Plus, "+");
                    UpdatePositionAndTracking(1);
                }
                else if (TryMatch("*"))
                {
                    AddToken(TokenType.Multiply, "*");
                    UpdatePositionAndTracking(1);
                }
                else if (TryMatch("/"))
                {
                    AddToken(TokenType.Divide, "/");
                    UpdatePositionAndTracking(1);
                }
                else if (TryMatch("("))
                {
                    AddToken(TokenType.LeftParen, "(");
                    UpdatePositionAndTracking(1);
                }
                else if (TryMatch(")"))
                {
                    AddToken(TokenType.RightParen, ")");
                    UpdatePositionAndTracking(1);
                }
                else if (TryMatch("\""))
                {
                    TokenizeString();
                }
                else if (char.IsDigit(_input[_position]) || (_input[_position] == '-' && char.IsDigit(PeekNext())))
                {
                    TokenizeNumber();
                }
                else if (TryMatch("-"))
                {
                    AddToken(TokenType.Minus, "-");
                    UpdatePositionAndTracking(1);
                }
                else if (char.IsLetter(_input[_position]) || _input[_position] == '_')
                {
                    TokenizeIdentifier();
                }
                else
                {
                    ThrowLexerError($"Unexpected character '{_input[_position]}'");
                }
            }
        }

        private void TokenizeText()
        {
            var savedPosition = SavePosition();

            while (_position < _input.Length &&
                   !TryMatch("{{") &&
                   !IsNewline(_position) &&
                   !IsWhitespace(_position))
            {
                UpdatePositionAndTracking(1);
            }

            if (_position > savedPosition.Position)
            {
                AddToken(TokenType.Text, _input.Substring(savedPosition.Position, _position - savedPosition.Position), savedPosition);
            }
        }

        private void TokenizeWhitespace()
        {
            var savedPosition = SavePosition();

            while (_position < _input.Length &&
                   IsWhitespace(_position))
            {
                UpdatePositionAndTracking(1);
            }

            if (_position > savedPosition.Position)
            {
                AddToken(TokenType.Whitespace, _input.Substring(savedPosition.Position, _position - savedPosition.Position), savedPosition);
            }
        }

        private void TokenizeNewline()
        {
            var savedPosition = SavePosition();
            string newlineValue;

            if (_input[_position] == '\r' &&
                _position + 1 < _input.Length &&
                _input[_position + 1] == '\n')
            {
                newlineValue = "\r\n";
                UpdatePositionAndTracking(2);
            }
            else
            {
                newlineValue = _input[_position] == '\r' ? "\r" : "\n";
                UpdatePositionAndTracking(1);
            }

            AddToken(TokenType.Newline, newlineValue, savedPosition);
        }

        private bool IsNewline(int pos)
        {
            if (pos >= _input.Length)
                return false;

            return _input[pos] == '\r' || _input[pos] == '\n';
        }

        private bool IsWhitespace(int pos)
        {
            if (pos >= _input.Length)
                return false;

            return char.IsWhiteSpace(_input[pos]) &&
                   !IsNewline(pos);
        }

        private void TokenizeString()
        {
            UpdatePositionAndTracking(1); // Skip opening quote
            var result = new StringBuilder();
            var savedPosition = SavePosition();

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
                            ThrowLexerError($"Invalid escape sequence '\\{nextChar}'");
                            break;
                    }
                    UpdatePositionAndTracking(2); // Skip both the backslash and the escaped character
                }
                else
                {
                    result.Append(_input[_position]);
                    UpdatePositionAndTracking(1);
                }
            }

            if (_position >= _input.Length)
            {
                ThrowLexerError("Unterminated string literal");
            }

            AddToken(TokenType.String, result.ToString(), savedPosition);
            UpdatePositionAndTracking(1); // Skip closing quote
        }

        private void TokenizeNumber()
        {
            var savedPosition = SavePosition();
            bool hasDecimal = false;

            if (_input[_position] == '-')
            {
                UpdatePositionAndTracking(1);
            }

            while (_position < _input.Length &&
                   (char.IsDigit(_input[_position]) ||
                    (!hasDecimal && _input[_position] == '.')))
            {
                if (_input[_position] == '.')
                {
                    hasDecimal = true;
                }
                UpdatePositionAndTracking(1);
            }

            var value = _input.Substring(savedPosition.Position, _position - savedPosition.Position);
            AddToken(TokenType.Number, value, savedPosition);
        }

        private void TokenizeIdentifier()
        {
            var savedPosition = SavePosition();

            while (_position < _input.Length &&
                   (char.IsLetterOrDigit(_input[_position]) ||
                    _input[_position] == '_'))
            {
                UpdatePositionAndTracking(1);
            }

            var value = _input.Substring(savedPosition.Position, _position - savedPosition.Position);

            switch (value)
            {
                case "true":
                    AddToken(TokenType.True, value, savedPosition);
                    break;
                case "false":
                    AddToken(TokenType.False, value, savedPosition);
                    break;
                case "in":
                    AddToken(TokenType.In, value, savedPosition);
                    break;
                default:
                    AddToken(TokenType.Variable, value, savedPosition);
                    break;
            }
        }

        private void SkipWhitespace()
        {
            while (_position < _input.Length && char.IsWhiteSpace(_input[_position]))
            {
                UpdatePositionAndTracking(1);
            }
        }

        private int WhitespaceCount(int position)
        {
            int originalPosition = position;
            int currentPosition = position;
            while (currentPosition < _input.Length && char.IsWhiteSpace(_input[currentPosition]))
            {
                currentPosition++;
            }
            return currentPosition - originalPosition;
        }

        private PositionState UpdatePositionAndTrackingOnState(int distance, PositionState state)
        {
            // For each character we're skipping, update line and column
            for (int i = 0; i < distance && state.Position < _input.Length; i++)
            {
                if (_input[state.Position] == '\n')
                {
                    state.Line++;
                    state.Column = 1;
                }
                else if (_input[state.Position] == '\r')
                {
                    // Handle Windows-style \r\n newlines
                    if (state.Position + 1 < _input.Length && _input[state.Position + 1] == '\n')
                    {
                        i++; // Skip the next character too
                        state.Position++; // Move past \r
                    }
                    state.Line++;
                    state.Column = 1;
                }
                else
                {
                    state.Column++;
                }
                state.Position++;
            }

            return state;
        }

        private void UpdatePositionAndTracking(int distance)
        {
            // For each character we're skipping, update line and column
            for (int i = 0; i < distance && _position < _input.Length; i++)
            {
                if (_input[_position] == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else if (_input[_position] == '\r')
                {
                    // Handle Windows-style \r\n newlines
                    if (_position + 1 < _input.Length && _input[_position + 1] == '\n')
                    {
                        i++; // Skip the next character too
                        _position++; // Move past \r
                    }
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
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

        private SourceLocation CreateLocation()
        {
            return new SourceLocation(_line, _column, _position, _sourceName);
        }

        private SourceLocation CreateLocation(PositionState savedState)
        {
            return new SourceLocation(savedState.Line, savedState.Column, savedState.Position, _sourceName);
        }

        private void AddToken(TokenType type, string value)
        {
            _tokens.Add(new Token(type, value, CreateLocation()));
        }

        private void AddToken(TokenType type, string value, PositionState savedState)
        {
            _tokens.Add(new Token(type, value, CreateLocation(savedState)));
        }
    }

    public abstract class AstNode
    {
        public SourceLocation Location { get; }

        protected AstNode(SourceLocation location)
        {
            Location = location;
        }

        public abstract dynamic Evaluate(ExecutionContext context);

        public override string ToString()
        {
            return "AstNode";
        }

        public abstract string ToStackString();
    }

    public class LiteralNode : AstNode
    {
        private readonly string _content;

        public LiteralNode(string content, SourceLocation location) : base(location)
        {
            _content = content;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _content;
        }

        public override string ToStackString()
        {
            return "<literal>";
        }

        public override string ToString()
        {
            return $"LiteralNode(content=\"{_content.Replace("\"", "\\\"")}\")";
        }
    }

    public class IncludeNode : AstNode
    {
        private readonly string _templateName;
        private AstNode _includedTemplate;

        public IncludeNode(string templateName, SourceLocation location) : base(location)
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
                throw new TemplateEvaluationException($"Template '{_templateName}' could not been resolved", context);
            }
            return _includedTemplate.Evaluate(context);
        }

        public override string ToString()
        {
            string templateStr = _includedTemplate == null ? "null" : _includedTemplate.ToString();
            return $"IncludeNode(templateName=\"{_templateName}\", template={templateStr})";
        }

        public override string ToStackString()
        {
            return "<include>";
        }
    }

    public class TextNode : AstNode
    {
        private readonly string _text;

        public TextNode(string text, SourceLocation location) : base(location)
        {
            _text = text;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _text;
        }

        public override string ToStackString()
        {
            return $"\"{_text.Replace("\"", "\\\"")}\"";
        }

        public override string ToString()
        {
            return $"TextNode(text=\"{_text.Replace("\"", "\\\"")}\")";
        }
    }

    public class WhitespaceNode : AstNode
    {
        private readonly string _text;

        public WhitespaceNode(string text, SourceLocation location) : base(location)
        {
            _text = text;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _text;
        }

        public override string ToStackString()
        {
            return $"\"{_text.Replace("\"", "\\\"")}\"";
        }

        public override string ToString()
        {
            return $"WhitespaceNode(text=\"{_text.Replace("\"", "\\\"")}\")";
        }
    }

    public class NewlineNode : AstNode
    {
        private readonly string _text;

        public NewlineNode(string text, SourceLocation location) : base(location)
        {
            _text = text;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _text;
        }

        public override string ToStackString()
        {
            return $"<newline>";
        }

        public override string ToString()
        {
            return $"NewlineNode()";
        }
    }

    public class InvocationNode : AstNode
    {
        private readonly AstNode _callable;
        private readonly List<AstNode> _arguments;

        public InvocationNode(AstNode callable, List<AstNode> arguments, SourceLocation location) : base(location)
        {
            _callable = callable;
            _arguments = arguments;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            var parentContext = context;
            var currentContext = new ExecutionContext(
                parentContext.GetData(),
                parentContext.GetFunctionRegistry(),
                parentContext,
                parentContext.MaxDepth,
                this);
            currentContext.CheckStackDepth();

            // Evaluate the callable, but handle special cases
            var callable = _callable.Evaluate(currentContext);

            var args = IsLazilyEvaluatedFunction(callable, _arguments.Count(), currentContext) ?
                _arguments.Select(arg => new LazyValue(arg, currentContext)).ToList<dynamic>() :
                _arguments.Select(arg => arg.Evaluate(currentContext)).ToList();

            // Now handle the callable based on its type
            if (callable is Func<ExecutionContext, AstNode, List<dynamic>, dynamic> lambdaFunc)
            {
                // Direct lambda invocation
                // Lambda establishes its own new child context
                return lambdaFunc(parentContext, this, args);
            }
            else if (callable is FunctionInfo functionInfo)
            {
                var registry = context.GetFunctionRegistry();

                // First check if this is a parameter that contains a function in any parent context
                if (context is LambdaExecutionContext lambdaContext &&
                    lambdaContext.TryGetParameterFromAnyContext(functionInfo.Name, out var paramValue))
                {
                    if (paramValue is Func<ExecutionContext, AstNode, List<dynamic>, dynamic> paramFunc)
                    {
                        // Lambda establishes its own new child context
                        return paramFunc(parentContext, this, args);
                    }
                    else if (paramValue is FunctionInfo paramFuncInfo)
                    {
                        if (!registry.TryGetFunction(paramFuncInfo.Name, args, out var function, out var effectiveArgs, currentContext))
                        {
                            throw new TemplateEvaluationException(
                                $"No matching overload found for function '{paramFuncInfo.Name}' with the provided arguments",
                                currentContext);
                        }
                        registry.ValidateArguments(function, effectiveArgs, currentContext);
                        return function.Implementation(currentContext, this, effectiveArgs);
                    }
                }

                // Check if this is a variable that contains a function
                if (context.TryResolveValue(functionInfo.Name, out var variableValue))
                {
                    if (variableValue is Func<ExecutionContext, AstNode, List<dynamic>, dynamic> variableFunc)
                    {
                        // Lambda establishes its own new child context
                        return variableFunc(parentContext, this, args);
                    }
                }

                // If not a parameter in any context or parameter isn't a function, try the registry
                if (!registry.TryGetFunction(functionInfo.Name, args, out var func, out var effArgs, currentContext))
                {
                    throw new TemplateEvaluationException(
                        $"No matching overload found for function '{functionInfo.Name}' with the provided arguments",
                        currentContext);
                }
                registry.ValidateArguments(func, effArgs, currentContext);
                return func.Implementation(currentContext, this, effArgs);
            }

            throw new TemplateEvaluationException(
                $"Expression is not callable: {callable?.GetType().Name ?? "<unknown>"}",
                currentContext);
        }

        private bool IsLazilyEvaluatedFunction(dynamic callable, int argumentCount, ExecutionContext context)
        {
            // Check if this is a function that requires lazy evaluation
            if (callable is FunctionInfo functionInfo)
            {
                var registry = context.GetFunctionRegistry();
                return registry.LazyFunctionExists(functionInfo.Name, argumentCount);
            }
            return false;
        }

        public override string ToStackString()
        {
            var argsStr = string.Join(", ", _arguments.Select(arg => arg.ToStackString()));
            return $"{_callable.ToStackString()}({argsStr})";
        }

        public override string ToString()
        {
            var argsStr = string.Join(", ", _arguments.Select(arg => arg.ToString()));
            return $"InvocationNode(callable={_callable.ToString()}, arguments=[{argsStr}])";
        }
    }

    public class LazyValue
    {
        private readonly AstNode _expression;
        private readonly ExecutionContext _capturedContext;
        private bool _isEvaluated;
        private dynamic _value;

        public LazyValue(AstNode expression, ExecutionContext context)
        {
            _expression = expression;
            _capturedContext = context;
            _isEvaluated = false;
        }

        public dynamic Evaluate()
        {
            if (!_isEvaluated)
            {
                _value = _expression.Evaluate(_capturedContext);
                _isEvaluated = true;
            }
            return _value;
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

        public FunctionReferenceNode(string functionName, SourceLocation location) : base(location)
        {
            _functionName = functionName;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return new FunctionInfo(_functionName);
        }

        public override string ToStackString()
        {
            return $"{_functionName}";
        }

        public override string ToString()
        {
            return $"FunctionReferenceNode(name=\"{_functionName}\")";
        }
    }

    public class LambdaNode : AstNode
    {
        private readonly List<string> _parameters;
        private readonly List<KeyValuePair<string, AstNode>> _statements;
        private readonly AstNode _finalExpression;

        public LambdaNode(
            List<string> parameters,
            List<KeyValuePair<string, AstNode>> statements,
            AstNode finalExpression,
            FunctionRegistry functionRegistry,
            SourceLocation location) : base(location)
        {
            // Validate parameter names against function registry
            var seenParams = new HashSet<string>();
            foreach (var param in parameters)
            {
                if (functionRegistry.HasFunction(param))
                {
                    throw new TemplateParsingException(
                        $"Parameter name '{param}' conflicts with an existing function name",
                        location);
                }
                if (!seenParams.Add(param))
                {
                    throw new TemplateParsingException(
                        $"Parameter name '{param}' is already defined",
                        location);
                }
            }

            // Validate statement variable names don't conflict with parameters, each other, or functions in registry
            var seenVariables = new HashSet<string>();

            foreach (var statement in statements)
            {
                if (parameters.Contains(statement.Key))
                {
                    throw new TemplateParsingException(
                        $"Cannot define variable '{statement.Key}' because it conflicts with an existing variable or field",
                        location);
                }
                if (functionRegistry.HasFunction(statement.Key))
                {
                    throw new TemplateParsingException(
                        $"Cannot define variable '{statement.Key}' because it conflicts with an existing function",
                        location);
                }
                if (!seenVariables.Add(statement.Key))
                {
                    throw new TemplateParsingException(
                        $"Cannot define variable '{statement.Key}' because it conflicts with an existing variable or field",
                        location);
                }
            }

            _parameters = parameters;
            _statements = statements;
            _finalExpression = finalExpression;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            var definitionContext = context;

            // Return a delegate that can be called later with parameters
            // Context is captured here, during evaluation, not during parsing
            return new Func<ExecutionContext, AstNode, List<dynamic>, dynamic>((callerContext, callSite, args) =>
            {
                // Create a new context that includes both captured context and new parameters
                var lambdaContext = new LambdaExecutionContext(callerContext, definitionContext, _parameters, args, callSite);

                // Execute each statement in order
                foreach (var statement in _statements)
                {
                    var value = statement.Value.Evaluate(lambdaContext);
                    lambdaContext.DefineVariable(statement.Key, value);
                }

                return _finalExpression.Evaluate(lambdaContext);
            });
        }

        public override string ToStackString()
        {
            return $"<lambda>";
        }

        public override string ToString()
        {
            var paramsStr = string.Join(", ", _parameters.Select(p => $"\"{p}\""));
            var statementsStr = string.Join(", ", _statements.Select(st => $"{{key=\"{st.Key}\", value={st.Value.ToString()}}}"));

            return $"LambdaNode(parameters=[{paramsStr}], statements=[{statementsStr}], finalExpression={_finalExpression.ToString()})";
        }
    }

    public class LetNode : AstNode
    {
        private readonly string _variableName;
        private readonly AstNode _expression;

        public LetNode(string variableName, AstNode expression, SourceLocation location) : base(location)
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

        public override string ToStackString()
        {
            return $"<let>";
        }

        public override string ToString()
        {
            return $"LetNode(variableName=\"{_variableName}\", expression={_expression.ToString()})";
        }
    }

    public class CaptureNode : AstNode
    {
        private readonly string _variableName;
        private readonly AstNode _body;

        public CaptureNode(string variableName, AstNode body, SourceLocation location) : base(location)
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

        public override string ToStackString()
        {
            return $"<capture>";
        }

        public override string ToString()
        {
            return $"CaptureNode(variableName=\"{_variableName}\", body={_body.ToString()})";
        }
    }

    public class ObjectCreationNode : AstNode
    {
        private readonly List<KeyValuePair<string, AstNode>> _fields;

        public ObjectCreationNode(List<KeyValuePair<string, AstNode>> fields, SourceLocation location) : base(location)
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

        public override string ToStackString()
        {
            return $"<obj>";
        }

        public override string ToString()
        {
            var fieldsStr = string.Join(", ", _fields.Select(f => $"{{key=\"{f.Key}\", value={f.Value.ToString()}}}"));
            return $"ObjectCreationNode(fields=[{fieldsStr}])";
        }
    }

    public class FieldAccessNode : AstNode
    {
        private readonly AstNode _object;
        private readonly string _fieldName;

        public FieldAccessNode(AstNode obj, string fieldName, SourceLocation location) : base(location)
        {
            _object = obj;
            _fieldName = fieldName;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            var obj = _object.Evaluate(context);
            if (obj == null)
            {
                throw new TemplateEvaluationException($"Cannot access field '{_fieldName}' on null object", context);
            }

            // Handle dictionary-like objects (ExpandoObject, IDictionary)
            if (obj is IDictionary<string, object> dict)
            {
                if (!dict.ContainsKey(_fieldName))
                {
                    throw new TemplateEvaluationException($"Object does not contain field '{_fieldName}'", context);
                }
                return dict[_fieldName];
            }

            // Handle regular objects using reflection
            var property = obj.GetType().GetProperty(_fieldName);
            if (property == null)
            {
                throw new TemplateEvaluationException($"Object does not contain field '{_fieldName}'", context);
            }

            var value = property.GetValue(obj);

            if (TypeHelper.IsConvertibleToDecimal(value))
            {
                value = (decimal)value;
            }

            return value;
        }

        public override string ToStackString()
        {
            return $"{_object.ToStackString()}.{_fieldName}";
        }

        public override string ToString()
        {
            return $"FieldAccessNode(object={_object.ToString()}, fieldName=\"{_fieldName}\")";
        }
    }

    public class ArrayNode : AstNode
    {
        private readonly List<AstNode> _elements;

        public ArrayNode(List<AstNode> elements, SourceLocation location) : base(location)
        {
            _elements = elements;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _elements.Select(element => element.Evaluate(context)).ToList();
        }

        public override string ToStackString()
        {
            var elementsStr = string.Join(", ", _elements.Select(e => e.ToStackString()));
            return $"[{elementsStr}]";
        }

        public override string ToString()
        {
            var elementsStr = string.Join(", ", _elements.Select(e => e.ToString()));
            return $"ArrayNode(elements=[{elementsStr}])";
        }
    }

    public class VariableNode : AstNode
    {
        private readonly string _path;

        public VariableNode(string path, SourceLocation location) : base(location)
        {
            _path = path;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return context.ResolveValue(_path);
        }

        public override string ToStackString()
        {
            return $"{_path}";
        }

        public override string ToString()
        {
            return $"VariableNode(path=\"{_path}\")";
        }
    }

    public class StringNode : AstNode
    {
        private readonly string _value;

        public StringNode(string value, SourceLocation location) : base(location)
        {
            _value = value;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _value;
        }

        public override string ToStackString()
        {
            return $"\"{_value.Replace("\"", "\\\"")}\"";
        }

        public override string ToString()
        {
            return $"StringNode(value=\"{_value.Replace("\"", "\\\"")}\")";
        }
    }

    public class NumberNode : AstNode
    {
        private readonly decimal _value;

        public NumberNode(string value, SourceLocation location) : base(location)
        {
            _value = decimal.Parse(value);
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _value;
        }

        public override string ToStackString()
        {
            return $"{_value}";
        }

        public override string ToString()
        {
            return $"NumberNode(value={_value})";
        }
    }

    public class BooleanNode : AstNode
    {
        private readonly bool _value;

        public BooleanNode(bool value, SourceLocation location) : base(location)
        {
            _value = value;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            return _value;
        }

        public override string ToStackString()
        {
            return $"{_value.ToString().ToLower()}";
        }

        public override string ToString()
        {
            return $"BooleanNode(value={_value.ToString().ToLower()})";
        }
    }

    public class UnaryNode : AstNode
    {
        private readonly TokenType _operator;
        private readonly AstNode _expression;

        public UnaryNode(TokenType op, AstNode expression, SourceLocation location) : base(location)
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
                    throw new TemplateEvaluationException(string.Format("Unknown unary operator: {0}", _operator), context);
            }
        }

        public override string ToStackString()
        {
            return $"<!{_expression.ToStackString()}>";
        }

        public override string ToString()
        {
            return $"UnaryNode(operator={_operator}, expression={_expression.ToString()})";
        }
    }

    public class BinaryNode : AstNode
    {
        private readonly TokenType _operator;
        private readonly AstNode _left;
        private readonly AstNode _right;

        public BinaryNode(TokenType op, AstNode left, AstNode right, SourceLocation location) : base(location)
        {
            _operator = op;
            _left = left;
            _right = right;
        }

        public override dynamic Evaluate(ExecutionContext context)
        {
            // short circuit eval for &&
            if (_operator == TokenType.And)
            {
                return Convert.ToBoolean(_left.Evaluate(context)) && Convert.ToBoolean(_right.Evaluate(context));
            }

            // short circuit eval for ||
            if (_operator == TokenType.Or)
            {
                return Convert.ToBoolean(_left.Evaluate(context)) || Convert.ToBoolean(_right.Evaluate(context));
            }

            var left = _left.Evaluate(context);
            var right = _right.Evaluate(context);

            // type check?

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
                default:
                    throw new TemplateEvaluationException(string.Format("Unknown binary operator: {0}", _operator), context);
            }
        }

        public override string ToStackString()
        {
            return $"<{_left.ToStackString()} {_operator} {_right.ToStackString()}>";
        }

        public override string ToString()
        {
            return $"BinaryNode(operator={_operator}, left={_left.ToString()}, right={_right.ToString()})";
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

        public ForNode(string iteratorName, AstNode collection, AstNode body, SourceLocation location) : base(location)
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
                throw new TemplateEvaluationException(
                    $"Iterator name '{_iteratorName}' conflicts with an existing variable or field",
                    context);
            }

            var collection = _collection.Evaluate(context);
            if (!(collection is System.Collections.IEnumerable))
            {
                throw new TemplateEvaluationException(
                    "Each statement requires an enumerable collection",
                    context);
            }

            var result = new StringBuilder();
            foreach (var item in collection)
            {
                var iterationContext = context.CreateIteratorContext(_iteratorName, item, this);
                result.Append(_body.Evaluate(iterationContext));
            }

            return result.ToString();
        }

        public override string ToStackString()
        {
            return $"<iteration {_iteratorName} in {_collection.ToStackString()}>";
        }

        public override string ToString()
        {
            return $"ForNode(iteratorName=\"{_iteratorName}\", collection={_collection.ToString()}, body={_body.ToString()})";
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

        public IfNode(List<IfBranch> conditionalBranches, AstNode elseBranch, SourceLocation location) : base(location)
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

        public override string ToStackString()
        {
            return $"<if>";
        }

        public override string ToString()
        {
            var branchesStr = string.Join(", ", _conditionalBranches.Select(b =>
                $"{{condition={b.Condition.ToString()}, body={b.Body.ToString()}}}"
            ));

            string elseStr = _elseBranch != null ? _elseBranch.ToString() : "null";

            return $"IfNode(conditionalBranches=[{branchesStr}], elseBranch={elseStr})";
        }
    }

    public class TemplateNode : AstNode
    {
        private readonly List<AstNode> _children;

        public TemplateNode(List<AstNode> children, SourceLocation location) : base(location)
        {
            _children = children;
        }

        public List<AstNode> Children { get { return _children; } }

        public override dynamic Evaluate(ExecutionContext context)
        {
            var result = new StringBuilder();
            foreach (var child in _children)
            {
                result.Append(TypeHelper.FormatOutput(child.Evaluate(context)));
            }
            return result.ToString();
        }

        public override string ToStackString()
        {
            return $"<Template>";
        }

        public override string ToString()
        {
            var childrenStr = string.Join(", ", _children.Select(child => child.ToString()));
            return $"TemplateNode(children=[{childrenStr}])";
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
            var startLocation = _tokens[0]?.Location;

            while (_position < _tokens.Count)
            {
                var token = Current();

                if (token.Type == TokenType.Text)
                {
                    nodes.Add(new TextNode(token.Value, token.Location));
                    Advance();
                }
                else if (token.Type == TokenType.Whitespace)
                {
                    if (CheckSkipWhitespace())
                    {
                        Advance();
                    }
                    else
                    {
                        nodes.Add(new WhitespaceNode(token.Value, token.Location));
                        Advance();
                    }
                }
                else if (token.Type == TokenType.Newline)
                {
                    if (CheckSkipNewline())
                    {
                        Advance();
                    }
                    else
                    {
                        nodes.Add(new NewlineNode(token.Value, token.Location));
                        Advance();
                    }
                }
                else if (token.Type == TokenType.DirectiveStart)
                {
                    // Look at the next token to determine what kind of directive we're dealing with
                    var nextToken = _tokens[_position + 1];

                    if (nextToken.Type == TokenType.CommentStart)
                    {
                        ParseComment();
                    }
                    else if (nextToken.Type == TokenType.Let)
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
                            throw new TemplateParsingException($"Unexpected token: {token.Type}", token.Location);
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
                    throw new TemplateParsingException($"Unexpected token: {token.Type}", token.Location);
                }
            }

            return new TemplateNode(nodes, startLocation ?? null);
        }

        private void ParseComment()
        {
            Advance(); // Skip {{
            Advance(); // Skip *

            Expect(TokenType.CommentEnd);
            Advance();

            Expect(TokenType.DirectiveEnd);
            Advance();
        }

        private AstNode ParseLetStatement()
        {
            Advance(); // Skip {{
            var token = Current();
            Advance(); // Skip let

            var variableName = Expect(TokenType.Variable).Value;
            Advance();

            Expect(TokenType.Assignment);
            Advance();

            var expression = ParseExpression();

            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new LetNode(variableName, expression, token.Location);
        }

        private AstNode ParseCaptureStatement()
        {
            Advance(); // Skip {{
            var token = Current();
            Advance(); // Skip capture

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

            return new CaptureNode(variableName, body, token.Location);
        }

        private AstNode ParseLiteralStatement()
        {
            Advance(); // Skip {{
            var token = Current();
            Advance(); // Skip literal

            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            // The next token should be the raw content
            Expect(TokenType.Text);
            var content = Current().Value;
            Advance();

            // Parse the closing capture tag
            Expect(TokenType.DirectiveStart);
            Advance(); // Skip {{
            Expect(TokenType.EndLiteral);
            Advance(); // Skip /capture
            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new LiteralNode(content, token.Location);
        }

        private AstNode ParseIncludeStatement()
        {
            Advance(); // Skip {{
            var token = Current();
            Advance(); // Skip include

            var templateName = Expect(TokenType.Variable).Value;
            Advance();

            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new IncludeNode(templateName, token.Location);
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
            var token = Current();
            string callableName = callable is FunctionReferenceNode funcNode ? funcNode.ToString() : "lambda";

            Advance(); // Skip (
            var arguments = new List<AstNode>();

            if (Current().Type != TokenType.RightParen)
            {
                while (true)
                {
                    arguments.Add(ParseExpression());

                    if (Current().Type == TokenType.RightParen)
                        break;

                    try
                    {
                        Expect(TokenType.Comma);
                        Advance();
                    }
                    catch (TemplateParsingException ex)
                    {
                        throw new TemplateParsingException($"Expected comma between function arguments or a closing parenthesis: {ex.Descriptor}", Current().Location);
                    }
                }
            }

            try
            {
                Expect(TokenType.RightParen);
                Advance();
            }
            catch (TemplateParsingException ex)
            {
                new TemplateParsingException($"Unclosed functional call: {ex.Descriptor}", Current().Location);
            }

            return new InvocationNode(callable, arguments, token.Location);
        }

        private AstNode ParseLambda()
        {
            Expect(TokenType.LeftParen);
            var token = Current();
            Advance(); // Skip (

            var parameters = new List<string>();
            var statements = new List<KeyValuePair<string, AstNode>>();

            // Parse parameters
            if (Current().Type != TokenType.RightParen)
            {
                while (true)
                {
                    if (Current().Type != TokenType.Variable && Current().Type != TokenType.Parameter)
                    {
                        throw new TemplateParsingException($"Expected parameter name but got {Current().Type}", Current().Location);
                    }

                    if (parameters.Contains(Current().Value))
                    {
                        throw new TemplateParsingException(
                            $"Duplicate parameter name '{Current().Value}' in lambda definition",
                            Current().Location
                        );
                    }

                    parameters.Add(Current().Value);
                    Advance();

                    if (Current().Type == TokenType.RightParen)
                        break;

                    try
                    {
                        Expect(TokenType.Comma);
                        Advance(); // Skip comma
                    }
                    catch (TemplateParsingException ex)
                    {
                        throw new TemplateParsingException(
                            $"Expected comma between lambda parameters: {ex.Descriptor}",
                            Current().Location
                        );
                    }
                }
            }

            try
            {
                Expect(TokenType.RightParen);
                Advance(); // Skip )
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException(
                    $"Expected closing parenthesis after lambda parameters: {ex.Descriptor}",
                    Current().Location
                );
            }

            try
            {
                Expect(TokenType.Arrow);
                Advance(); // Skip =>
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException(
                    $"Expected '=>' after lambda parameters: {ex.Descriptor}",
                    Current().Location
                );
            }

            // Parse statement list
            while (true)
            {
                if (Current().Type != TokenType.Variable ||
                    _tokens.Count < _position + 1 ||
                    _tokens[_position + 1].Type != TokenType.Assignment)
                {
                    // If we don't see a variable or next token is not assignment, this must be the final expression
                    var finalExpression = ParseExpression();
                    return new LambdaNode(parameters, statements, finalExpression, _functionRegistry, token.Location);
                }

                var variableName = Current().Value;
                Advance();

                try
                {
                    Expect(TokenType.Assignment);
                    Advance();
                }
                catch (TemplateParsingException ex)
                {
                    throw new TemplateParsingException(
                        $"Expected assignment operator '=' after variable in lambda: {ex.Descriptor}",
                        Current().Location
                    );
                }

                var expression = ParseExpression();
                statements.Add(new KeyValuePair<string, AstNode>(variableName, expression));

                try
                {
                    Expect(TokenType.Comma);
                    Advance(); // Skip comma
                }
                catch (TemplateParsingException ex)
                {
                    throw new TemplateParsingException(
                        $"Expected comma after statement in lambda: {ex.Descriptor}",
                        Current().Location
                    );
                }
            }
        }

        private AstNode ParseObjectCreation()
        {
            var token = Current();
            Advance(); // Skip obj(

            var fields = new List<KeyValuePair<string, AstNode>>();

            while (_position < _tokens.Count && Current().Type != TokenType.RightParen)
            {
                // Parse field name
                if (Current().Type != TokenType.Variable)
                {
                    throw new TemplateParsingException($"Expected field name but got {Current().Type}", Current().Location);
                }

                var fieldName = Current().Value;
                if (fields.Any(f => f.Key == fieldName))
                {
                    throw new TemplateParsingException($"Duplicate field name '{fieldName}' defined in object", Current().Location);
                }
                Advance();

                // Parse colon
                if (Current().Type != TokenType.Colon)
                {
                    throw new TemplateParsingException($"Expected ':' but got {Current().Type}", Current().Location);
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
                    throw new TemplateParsingException($"Unclosed object literal: expected ',' or ')' but got {Current().Type}", Current().Location);
                }
            }

            Expect(TokenType.RightParen);
            Advance(); // Skip )

            return new ObjectCreationNode(fields, token.Location);
        }

        private AstNode ParseArrayCreation()
        {
            var token = Current();
            Advance(); // Skip [

            var elements = new List<AstNode>();

            // Handle empty array case
            if (Current().Type == TokenType.RightBracket)
            {
                Advance(); // Skip ]
                return new ArrayNode(elements, token.Location);
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
                    throw new TemplateParsingException($"Expected ',' or ']' but got {Current().Type}", Current().Location);
                }

                Advance(); // Skip comma
            }

            return new ArrayNode(elements, token.Location);
        }

        private AstNode ParseIfStatement()
        {
            var conditionalBranches = new List<IfNode.IfBranch>();
            AstNode elseBranch = null;
            bool foundClosingTag = false;

            // Parse initial if
            Advance(); // Skip {{
            var ifToken = Current();
            Advance(); // Skip if
            var condition = ParseExpression();

            try
            {
                Expect(TokenType.DirectiveEnd);
                Advance(); // Skip }}
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Unclosed if directive: {ex.Descriptor}", ifToken.Location);
            }

            var body = ParseTemplate();
            conditionalBranches.Add(new IfNode.IfBranch(condition, body));

            // Parse any elseif/else clauses
            while (_position < _tokens.Count && Current().Type == TokenType.DirectiveStart)
            {
                var token = _tokens[_position + 1]; // Look at the directive type

                if (token.Type == TokenType.ElseIf)
                {
                    Advance(); // Skip {{
                    Advance(); // Skip elseif
                    condition = ParseExpression();

                    try
                    {
                        Expect(TokenType.DirectiveEnd);
                        Advance(); // Skip }}
                    }
                    catch (TemplateParsingException ex)
                    {
                        throw new TemplateParsingException($"Unclosed elseif directive: {ex.Descriptor}", token.Location);
                    }

                    body = ParseTemplate();
                    conditionalBranches.Add(new IfNode.IfBranch(condition, body));
                }
                else if (token.Type == TokenType.Else)
                {
                    Advance(); // Skip {{
                    Advance(); // Skip else

                    try
                    {
                        Expect(TokenType.DirectiveEnd);
                        Advance(); // Skip }}
                    }
                    catch (TemplateParsingException ex)
                    {
                        throw new TemplateParsingException($"Unclosed else directive: {ex.Descriptor}", token.Location);
                    }
                    elseBranch = ParseTemplate();
                }

                else if (token.Type == TokenType.EndIf)
                {
                    Advance(); // Skip {{
                    Advance(); // Skip /if

                    try
                    {
                        Expect(TokenType.DirectiveEnd);
                        Advance(); // Skip }}
                    }
                    catch (TemplateParsingException ex)
                    {
                        throw new TemplateParsingException($"Unclosed /if directive: {ex.Descriptor}", token.Location);
                    }

                    foundClosingTag = true;
                    break;
                }
                else
                {
                    // This is not an if-related token, so it must be the start of
                    // nested content - let ParseTemplate handle it
                    break;
                }
            }

            // Check if we found a closing tag
            if (!foundClosingTag)
            {
                try
                {
                    var token = Current();
                    throw new TemplateParsingException("Unclosed if statement: Missing {{/if}} directive", Current().Location);
                }
                catch (TemplateParsingException)
                {
                    var lastToken = _tokens.Count > 0 ? _tokens[_tokens.Count - 1] : null;
                    var location = lastToken?.Location ?? new SourceLocation(0, 0, 0);
                    throw new TemplateParsingException("Unclosed if statement: Missing {{/if}} directive", location);
                }
            }

            return new IfNode(conditionalBranches, elseBranch, ifToken.Location);
        }

        private AstNode ParseForStatement()
        {
            Advance(); // Skip {{
            var token = Current();
            Advance(); // Skip for

            string iteratorName;

            try
            {
                iteratorName = Expect(TokenType.Variable).Value;
                Advance();
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Expected iterator variable name: {ex.Descriptor}", Current().Location);
            }

            try
            {
                Expect(TokenType.In);
                Advance();
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Expected 'in' keyword after iterator name: {ex.Descriptor}", Current().Location);
            }

            var collection = ParseExpression();

            try
            {
                Expect(TokenType.DirectiveEnd);
                Advance(); // Skip }}
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Unclosed for directive: {ex.Descriptor}", Current().Location);
            }

            var body = ParseTemplate();

            // Handle the closing for tag
            try
            {
                Expect(TokenType.DirectiveStart);
                Advance(); // Skip {{
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Expected closing /for directive: {ex.Descriptor}", Current().Location);
            }

            try
            {
                Expect(TokenType.EndFor);
                Advance(); // Skip /for
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Expected /for in closing directive: {ex.Descriptor}", Current().Location);
            }

            try
            {
                Expect(TokenType.DirectiveEnd);
                Advance(); // Skip }}
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Unclosed /for directive: {ex.Descriptor}", Current().Location);
            }

            return new ForNode(iteratorName, collection, body, token.Location);
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
                var token = Current();
                var op = token.Type;
                Advance();
                var right = ParseAnd();
                left = new BinaryNode(op, left, right, token.Location);
            }

            return left;
        }

        private AstNode ParseAnd()
        {
            var left = ParseComparison();

            while (_position < _tokens.Count && Current().Type == TokenType.And)
            {
                var token = Current();
                var op = token.Type;
                Advance();
                var right = ParseComparison();
                left = new BinaryNode(op, left, right, token.Location);
            }

            return left;
        }

        private AstNode ParseComparison()
        {
            var left = ParseAdditive();

            while (_position < _tokens.Count && IsComparisonOperator(Current().Type))
            {
                var token = Current();
                var op = token.Type;
                Advance();
                var right = ParseAdditive();
                left = new BinaryNode(op, left, right, token.Location);
            }

            return left;
        }

        private AstNode ParseAdditive()
        {
            var left = ParseMultiplicative();

            while (_position < _tokens.Count &&
                   (Current().Type == TokenType.Plus || Current().Type == TokenType.Minus))
            {
                var token = Current();
                var op = token.Type;
                Advance();
                var right = ParseMultiplicative();
                left = new BinaryNode(op, left, right, token.Location);
            }

            return left;
        }

        private AstNode ParseMultiplicative()
        {
            var left = ParseUnary();

            while (_position < _tokens.Count &&
                   (Current().Type == TokenType.Multiply || Current().Type == TokenType.Divide))
            {
                var token = Current();
                var op = token.Type;
                Advance();
                var right = ParseUnary();
                left = new BinaryNode(op, left, right, token.Location);
            }

            return left;
        }

        private AstNode ParseUnary()
        {
            if (Current().Type == TokenType.Not)
            {
                var token = Current();
                var op = token.Type;
                Advance();
                var expression = ParseUnary();
                return new UnaryNode(op, expression, token.Location);
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
                    expr = new FunctionReferenceNode(token.Value, token.Location);
                    Advance();
                    break;

                case TokenType.Variable:
                    Advance();
                    expr = new VariableNode(token.Value, token.Location);
                    break;

                case TokenType.String:
                    Advance();
                    expr = new StringNode(token.Value, token.Location);
                    break;

                case TokenType.Number:
                    Advance();
                    expr = new NumberNode(token.Value, token.Location);
                    break;

                case TokenType.True:
                    Advance();
                    expr = new BooleanNode(true, token.Location);
                    break;

                case TokenType.False:
                    Advance();
                    expr = new BooleanNode(false, token.Location);
                    break;

                default:
                    string expectedTokens = "LeftBracket, ObjectStart, LeftParen, Function, Variable, String, Number, True, or False";
                    throw new TemplateParsingException($"Unexpected token: {token.Type}. Expected one of: {expectedTokens}", token.Location);
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
                    throw new TemplateParsingException($"Expected field name but got {fieldToken.Type}", fieldToken.Location);
                }
                expr = new FieldAccessNode(expr, fieldToken.Value, fieldToken.Location);
                Advance();

                // Handle any invocations that follow nested object invocation
                while (_position < _tokens.Count && Current().Type == TokenType.LeftParen)
                {
                    expr = ParseInvocation(expr);
                }
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

        private bool CheckSkipNewline()
        {
            var token = Current();

            if (token.Type == TokenType.Newline &&
                (_tokens.Count > _position + 1 &&
                 _tokens[_position + 1].Type == TokenType.DirectiveStart &&
                 _tokens[_position + 1].Value == "{{-") ||
                (_tokens.Count > _position + 2 &&
                 _tokens[_position + 1].Type == TokenType.Whitespace &&
                 _tokens[_position + 2].Type == TokenType.DirectiveStart &&
                 _tokens[_position + 2].Value == "{{-") ||
                (_position > 0 &&
                 _tokens[_position - 1].Type == TokenType.DirectiveEnd &&
                 _tokens[_position - 1].Value == "-}}") ||
                (_position > 1 &&
                 _tokens[_position - 1].Type == TokenType.Whitespace &&
                 _tokens[_position - 2].Type == TokenType.DirectiveEnd &&
                 _tokens[_position - 2].Value == "-}}"))
            {
                return true;
            }

            return false;
        }

        private bool CheckSkipWhitespace()
        {
            var token = Current();

            if (token.Type == TokenType.Whitespace &&
                (_tokens.Count > _position + 1 &&
                 _tokens[_position + 1].Type == TokenType.DirectiveStart &&
                 _tokens[_position + 1].Value == "{{-") ||
                (_position > 0 &&
                 _tokens[_position - 1].Type == TokenType.DirectiveEnd &&
                 _tokens[_position - 1].Value == "-}}"))
            {
                return true;
            }

            return false;
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
                var lastToken = _tokens.Count > 0 ? _tokens[_tokens.Count - 1] : null;
                var location = lastToken?.Location ?? new SourceLocation(0, 0, 0);

                throw new TemplateParsingException("Unexpected end of template: the template is incomplete or contains a syntax error", location);
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
                throw new TemplateParsingException(
                    $"Expected <{type.ToString().ToLower()}> but got <{token.Type.ToString().ToLower()}>",
                    token.Location);
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
        public Func<ExecutionContext, AstNode, List<dynamic>, dynamic> Implementation { get; }
        public bool IsLazilyEvaluated { get; }

        public FunctionDefinition(
            string name,
            List<ParameterDefinition> parameters,
            Func<ExecutionContext, AstNode, List<dynamic>, dynamic> implementation,
            bool isLazilyEvaluated)
        {
            Name = name;
            Parameters = parameters;
            Implementation = implementation;
            IsLazilyEvaluated = isLazilyEvaluated;
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
                (context, callSite, args) =>
                {
                    var enumerable = args[0] as System.Collections.IEnumerable;
                    if (enumerable == null)
                    {
                        throw new TemplateEvaluationException("length function requires an enumerable argument", context);
                    }
                    return Convert.ToDecimal(enumerable.Cast<object>().Count());
                });

            Register("length",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var str = args[0] as string;
                    if (str == null)
                    {
                        throw new TemplateEvaluationException("length function requires a string argument", context);
                    }
                    return Convert.ToDecimal(str.Length);
                });

            Register("empty",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var str = args[0] as string;
                    if (str == null)
                    {
                        throw new TemplateEvaluationException("length function requires a string argument", context);
                    }
                    return string.IsNullOrEmpty(str);
                });

            Register("concat",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string)),
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
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
                (context, callSite, args) =>
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
                (context, callSite, args) =>
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
                (context, callSite, args) =>
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
                (context, callSite, args) =>
                {
                    var str = args[0]?.ToString() ?? "";
                    var searchStr = args[1]?.ToString() ?? "";
                    return str.EndsWith(searchStr);
                });

            Register("toUpper",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var str = args[0]?.ToString() ?? "";
                    return str.ToUpper();
                });

            Register("toLower",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var str = args[0]?.ToString() ?? "";
                    return str.ToLower();
                });

            Register("trim",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var str = args[0]?.ToString() ?? "";
                    return str.Trim();
                });

            Register("indexOf",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string)),
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
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
                (context, callSite, args) =>
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
                (context, callSite, args) =>
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

            Register("range",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(decimal)),
                    new ParameterDefinition(typeof(decimal)),
                    new ParameterDefinition(typeof(decimal), true, new decimal(1))
                },
                (context, callSite, args) =>
                {
                    var start = Convert.ToDecimal(args[0]);
                    var end = Convert.ToDecimal(args[1]);
                    var step = Convert.ToDecimal(args[2]);

                    if (step == 0)
                    {
                        throw new TemplateEvaluationException("range function requires a non-zero step value", context);
                    }

                    var result = new List<decimal>();

                    // Handle both positive and negative step values
                    if (step > 0)
                    {
                        for (var value = start + step - step; value < end; value += step)
                        {
                            result.Add(value);
                        }
                    }
                    else
                    {
                        for (var value = start + step - step; value > end; value += step)
                        {
                            result.Add(value);
                        }
                    }

                    return result;
                });

            Register("rangeYear",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal), true, new decimal(1))
                },
                (context, callSite, args) =>
                {
                    var start = args[0] as DateTime?;
                    var end = args[1] as DateTime?;
                    var stepDecimal = Convert.ToDecimal(args[2]);
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                        throw new TemplateEvaluationException("rangeYear function requires valid DateTime parameters", context);

                    if (step <= 0)
                        throw new TemplateEvaluationException("rangeYear function requires a positive step value", context);

                    if (start >= end)
                        return new List<DateTime>();

                    var result = new List<DateTime>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(current);
                        current = current.AddYears(step);
                    }

                    return result;
                });

            Register("rangeMonth",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal), true, new decimal(1))
                },
                (context, callSite, args) =>
                {
                    var start = args[0] as DateTime?;
                    var end = args[1] as DateTime?;
                    var stepDecimal = Convert.ToDecimal(args[2]);
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                        throw new TemplateEvaluationException("rangeMonth function requires valid DateTime parameters", context);

                    if (step <= 0)
                        throw new TemplateEvaluationException("rangeMonth function requires a positive step value", context);

                    if (start >= end)
                        return new List<DateTime>();

                    var result = new List<DateTime>();
                    var current = start.Value;
                    var originalDay = current.Day;

                    while (current < end)
                    {
                        result.Add(current);

                        // Use AddMonths to get the next month
                        var nextMonth = current.AddMonths(step);

                        // Check if original day exists in the new month
                        var daysInNextMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                        var targetDay = Math.Min(originalDay, daysInNextMonth);

                        // Create a new DateTime with the correct day
                        current = new DateTime(nextMonth.Year, nextMonth.Month, targetDay,
                                              current.Hour, current.Minute, current.Second,
                                              current.Millisecond);
                    }

                    return result;
                });

            Register("rangeDay",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal), true, new decimal(1))
                },
                (context, callSite, args) =>
                {
                    var start = args[0] as DateTime?;
                    var end = args[1] as DateTime?;
                    var stepDecimal = Convert.ToDecimal(args[2]);
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                        throw new TemplateEvaluationException("rangeDay function requires valid DateTime parameters", context);

                    if (step <= 0)
                        throw new TemplateEvaluationException("rangeDay function requires a positive step value", context);

                    if (start >= end)
                        return new List<DateTime>();

                    var result = new List<DateTime>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(current);
                        current = current.AddDays(step);
                    }

                    return result;
                });

            Register("rangeHour",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal), true, new decimal(1))
                },
                (context, callSite, args) =>
                {
                    var start = args[0] as DateTime?;
                    var end = args[1] as DateTime?;
                    var stepDecimal = Convert.ToDecimal(args[2]);
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                        throw new TemplateEvaluationException("rangeHour function requires valid DateTime parameters", context);

                    if (step <= 0)
                        throw new TemplateEvaluationException("rangeHour function requires a positive step value", context);

                    if (start >= end)
                        return new List<DateTime>();

                    var result = new List<DateTime>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(current);
                        current = current.AddHours(step);
                    }

                    return result;
                });

            Register("rangeMinute",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal), true, new decimal(1))
                },
                (context, callSite, args) =>
                {
                    var start = args[0] as DateTime?;
                    var end = args[1] as DateTime?;
                    var stepDecimal = Convert.ToDecimal(args[2]);
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                        throw new TemplateEvaluationException("rangeMinute function requires valid DateTime parameters", context);

                    if (step <= 0)
                        throw new TemplateEvaluationException("rangeMinute function requires a positive step value", context);

                    if (start >= end)
                        return new List<DateTime>();

                    var result = new List<DateTime>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(current);
                        current = current.AddMinutes(step);
                    }

                    return result;
                });

            Register("rangeSecond",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal), true, new decimal(1))
                },
                (context, callSite, args) =>
                {
                    var start = args[0] as DateTime?;
                    var end = args[1] as DateTime?;
                    var stepDecimal = Convert.ToDecimal(args[2]);
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                        throw new TemplateEvaluationException("rangeSecond function requires valid DateTime parameters", context);

                    if (step <= 0)
                        throw new TemplateEvaluationException("rangeSecond function requires a positive step value", context);

                    if (start >= end)
                        return new List<DateTime>();

                    var result = new List<DateTime>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(current);
                        current = current.AddSeconds(step);
                    }

                    return result;
                });

            Register("filter",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(Func<ExecutionContext, AstNode, List<dynamic>, dynamic>))
                },
                (context, callSite, args) =>
                {
                    var collection = args[0] as System.Collections.IEnumerable;
                    var predicate = args[1] as Func<ExecutionContext, AstNode, List<dynamic>, dynamic>;

                    if (collection == null || predicate == null)
                    {
                        throw new TemplateEvaluationException("filter function requires an array and a lambda function", context);
                    }

                    var result = new List<dynamic>();
                    foreach (var item in collection)
                    {
                        var predicateResult = predicate(context, callSite, new List<dynamic> { item });
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
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var index = Convert.ToInt32(args[1]);

                    if (array == null)
                        throw new TemplateEvaluationException("at function requires an array as first argument", context);

                    var list = array.Cast<object>().ToList();
                    if (index < 0 || index >= list.Count)
                        throw new TemplateEvaluationException($"Index {index} is out of bounds for array of length {list.Count}", context);

                    return list[index];
                });

            Register("first",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    if (array == null)
                        throw new TemplateEvaluationException("first function requires an array argument", context);

                    var list = array.Cast<object>().ToList();
                    if (list.Count == 0)
                        throw new TemplateEvaluationException("Cannot get first element of empty array", context);

                    return list[0];
                });

            Register("rest",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    if (array == null)
                        throw new TemplateEvaluationException("rest function requires an array argument", context);

                    var list = array.Cast<object>().ToList();
                    if (list.Count == 0)
                        return new List<dynamic>();

                    return list.Skip(1);
                });

            Register("last",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    if (array == null)
                        throw new TemplateEvaluationException("last function requires an array argument", context);

                    var list = array.Cast<object>().ToList();
                    if (list.Count == 0)
                        throw new TemplateEvaluationException("Cannot get last element of empty array", context);

                    return list[list.Count - 1];
                });

            Register("any",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    if (array == null)
                        throw new TemplateEvaluationException("any function requires an array argument", context);

                    return array.Cast<object>().Any();
                });

            Register("if",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(LazyValue)),
                    new ParameterDefinition(typeof(LazyValue)),
                    new ParameterDefinition(typeof(LazyValue))
                },
                (context, callSite, args) =>
                {
                    var condition = args[0] as LazyValue;
                    var trueBranch = args[1] as LazyValue;
                    var falseBranch = args[2] as LazyValue;

                    bool conditionResult = Convert.ToBoolean(condition.Evaluate());

                    return conditionResult ? trueBranch.Evaluate() : falseBranch.Evaluate();
                },
                isLazilyEvaluated: true);

            Register("join",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var delimiter = args[1] as string;

                    if (array == null)
                        throw new TemplateEvaluationException("join function requires an array as first argument", context);
                    if (delimiter == null)
                        throw new TemplateEvaluationException("join function requires a string as second argument", context);

                    return string.Join(delimiter, array.Cast<object>().Select(x => TypeHelper.FormatOutput(x ?? "")));
                });

            Register("explode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string)),
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var str = args[0] as string;
                    var delimiter = args[1] as string;

                    if (str == null)
                        throw new TemplateEvaluationException("explode function requires a string as first argument", context);
                    if (delimiter == null)
                        throw new TemplateEvaluationException("explode function requires a string as second argument", context);

                    return str.Split(new[] { delimiter }, StringSplitOptions.None).ToList();
                });

            Register("map",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(Func<ExecutionContext, AstNode, List<dynamic>, dynamic>))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var mapper = args[1] as Func<ExecutionContext, AstNode, List<dynamic>, dynamic>;

                    if (array == null || mapper == null)
                        throw new TemplateEvaluationException("map function requires an array and a function", context);

                    return array.Cast<object>()
                        .Select(item => mapper(context, callSite, new List<dynamic> { item }))
                        .ToList();
                });

            Register("reduce",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(Func<ExecutionContext, AstNode, List<dynamic>, dynamic>)),
                    new ParameterDefinition(typeof(object))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var reducer = args[1] as Func<ExecutionContext, AstNode, List<dynamic>, dynamic>;
                    var initialValue = args[2];

                    if (array == null || reducer == null)
                        throw new TemplateEvaluationException("reduce function requires an array and a function", context);

                    return array.Cast<object>()
                        .Aggregate((object)initialValue, (acc, curr) =>
                            reducer(context, callSite, new List<dynamic> { acc, curr }));
                });

            Register("concat",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(System.Collections.IEnumerable))
                },
                (context, callSite, args) =>
                {
                    var first = args[0] as System.Collections.IEnumerable;
                    var second = args[1] as System.Collections.IEnumerable;

                    if (first == null || second == null)
                        throw new TemplateEvaluationException("concat function requires both arguments to be arrays", context);

                    // Combine both enumerables into a single list
                    var result = first.Cast<object>().Concat(second.Cast<object>()).ToList();

                    return result;
                });

            Register("take",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    int count = Convert.ToInt32(args[1]);

                    if (array == null)
                        throw new TemplateEvaluationException("take function requires an array as first argument", context);

                    if (count <= 0)
                        return new List<object>();

                    return array.Cast<object>().Take(count).ToList();
                });

            Register("skip",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    int count = Convert.ToInt32(args[1]);

                    if (array == null)
                        throw new TemplateEvaluationException("skip function requires an array as first argument", context);

                    return array.Cast<object>().Skip(count).ToList();
                });

            Register("order",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    if (array == null)
                        throw new TemplateEvaluationException("order function requires an array argument", context);

                    return array.Cast<object>().OrderBy(x => x).ToList();
                });

            Register("order",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(bool))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var ascending = Convert.ToBoolean(args[1]);

                    if (array == null)
                        throw new TemplateEvaluationException("order function requires an array as first argument", context);

                    var ordered = array.Cast<object>();
                    return (ascending ? ordered.OrderBy(x => x) : ordered.OrderByDescending(x => x)).ToList();
                });

            Register("order",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(Func<ExecutionContext, AstNode, List<dynamic>, dynamic>))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var comparer = args[1] as Func<ExecutionContext, AstNode, List<dynamic>, dynamic>;

                    if (array == null || comparer == null)
                        throw new TemplateEvaluationException("order function requires an array and a comparison function", context);

                    return array.Cast<object>()
                        .OrderBy(x => x, new DynamicComparer(context, callSite, comparer))
                        .ToList();
                });

            Register("group",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(System.Collections.IEnumerable)),
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var array = args[0] as System.Collections.IEnumerable;
                    var fieldName = args[1] as string;

                    if (array == null)
                        throw new TemplateEvaluationException("group function requires an array as first argument", context);
                    if (string.IsNullOrEmpty(fieldName))
                        throw new TemplateEvaluationException("group function requires a non-empty string as second argument", context);

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
                                throw new TemplateEvaluationException($"Object does not contain field '{fieldName}'", context);
                            key = dict[fieldName]?.ToString();
                        }
                        else
                        {
                            var property = item.GetType().GetProperty(fieldName);
                            if (property == null)
                                throw new TemplateEvaluationException($"Object does not contain field '{fieldName}'", context);
                            key = property.GetValue(item)?.ToString();
                        }

                        if (key == null)
                            throw new TemplateEvaluationException($"Field '{fieldName}' must be present", context);

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
                (context, callSite, args) =>
                {
                    var obj = args[0];
                    var fieldName = args[1] as string;

                    if (obj == null)
                        throw new TemplateEvaluationException("get function requires an object as first argument", context);
                    if (string.IsNullOrEmpty(fieldName))
                        throw new TemplateEvaluationException("get function requires a non-empty string as second argument", context);

                    // Handle ExpandoObject and other dictionary types
                    if (obj is IDictionary<string, object> dict)
                    {
                        if (!dict.ContainsKey(fieldName))
                            throw new TemplateEvaluationException($"Object does not contain field '{fieldName}'", context);

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
                        throw new TemplateEvaluationException($"Object does not contain field '{fieldName}'", context);

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
                (context, callSite, args) =>
                {
                    var obj = args[0];
                    if (obj == null)
                        throw new TemplateEvaluationException("keys function requires an object argument", context);

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
                (context, callSite, args) =>
                {
                    var number1 = Convert.ToInt32(args[0]);
                    var number2 = Convert.ToInt32(args[1]);

                    if (number2 == 0)
                        throw new TemplateEvaluationException("Cannot perform modulo with zero as divisor", context);

                    return new decimal(number1 % number2);
                });

            Register("floor",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var number = Convert.ToDecimal(args[0]);
                    return Math.Floor(number);
                });

            Register("ceil",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var number = Convert.ToDecimal(args[0]);
                    return Math.Ceiling(number);
                });

            Register("round",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var number = Convert.ToDecimal(args[0]);
                    return Math.Round(number, 0);
                });

            Register("round",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(decimal)),
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var number = Convert.ToDecimal(args[0]);
                    var decimals = Convert.ToInt32(args[1]);

                    if (decimals < 0)
                        throw new TemplateEvaluationException("Number of decimal places cannot be negative", context);

                    return Math.Round(number, decimals);
                });

            Register("string",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var number = Convert.ToDecimal(args[0]);
                    return number.ToString();
                });

            Register("string",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(bool))
                },
                (context, callSite, args) =>
                {
                    var boolean = Convert.ToBoolean(args[0]);
                    return boolean.ToString().ToLower(); // returning "true" or "false" in lowercase
                });

            Register("number",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var str = args[0] as string;

                    if (string.IsNullOrEmpty(str))
                        throw new TemplateEvaluationException("Cannot convert empty or null string to number", context);

                    if (!decimal.TryParse(str, out decimal result))
                        throw new TemplateEvaluationException($"Cannot convert string '{str}' to number", context);

                    return result;
                });

            Register("numeric",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
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
                (context, callSite, args) =>
                {
                    var dateStr = args[0] as string;
                    if (string.IsNullOrEmpty(dateStr))
                        throw new TemplateEvaluationException("datetime function requires a non-empty string argument", context);

                    try
                    {
                        return DateTime.Parse(dateStr);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to parse date string '{dateStr}': {ex.Message}", context);
                    }
                });

            Register("format",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var date = args[0] as DateTime?;
                    var format = args[1] as string;

                    if (!date.HasValue)
                        throw new TemplateEvaluationException("format function requires a valid DateTime as first argument", context);
                    if (string.IsNullOrEmpty(format))
                        throw new TemplateEvaluationException("format function requires a non-empty format string as second argument", context);

                    try
                    {
                        return date.Value.ToString(format);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to format date with format string '{format}': {ex.Message}", context);
                    }
                });

            Register("addYears",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var date = args[0] as DateTime?;
                    var years = Convert.ToInt32(args[1]);

                    if (!date.HasValue)
                        throw new TemplateEvaluationException("addYears function requires a valid DateTime as first argument", context);

                    try
                    {
                        return date.Value.AddYears(years);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to add {years} years to date: {ex.Message}", context);
                    }
                });

            Register("addMonths",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var date = args[0] as DateTime?;
                    var months = Convert.ToInt32(args[1]);

                    if (!date.HasValue)
                        throw new TemplateEvaluationException("addMonths function requires a valid DateTime as first argument", context);

                    try
                    {
                        return date.Value.AddMonths(months);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to add {months} months to date: {ex.Message}", context);
                    }
                });

            Register("addDays",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var date = args[0] as DateTime?;
                    var days = Convert.ToInt32(args[1]);

                    if (!date.HasValue)
                        throw new TemplateEvaluationException("addDays function requires a valid DateTime as first argument", context);

                    try
                    {
                        return date.Value.AddDays(days);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to add {days} days to date: {ex.Message}", context);
                    }
                });

            Register("addHours",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var date = args[0] as DateTime?;
                    var hours = Convert.ToInt32(args[1]);

                    if (!date.HasValue)
                        throw new TemplateEvaluationException("addHours function requires a valid DateTime as first argument", context);

                    try
                    {
                        return date.Value.AddHours(hours);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to add {hours} hours to date: {ex.Message}", context);
                    }
                });

            Register("addMinutes",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var date = args[0] as DateTime?;
                    var minutes = Convert.ToInt32(args[1]);

                    if (!date.HasValue)
                        throw new TemplateEvaluationException("addMinutes function requires a valid DateTime as first argument", context);

                    try
                    {
                        return date.Value.AddMinutes(minutes);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to add {minutes} minutes to date: {ex.Message}", context);
                    }
                });

            Register("addSeconds",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTime)),
                    new ParameterDefinition(typeof(decimal))
                },
                (context, callSite, args) =>
                {
                    var date = args[0] as DateTime?;
                    var seconds = Convert.ToInt32(args[1]);

                    if (!date.HasValue)
                        throw new TemplateEvaluationException("addSeconds function requires a valid DateTime as first argument", context);

                    try
                    {
                        return date.Value.AddSeconds(seconds);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to add {seconds} seconds to date: {ex.Message}", context);
                    }
                });

            Register("now",
                new List<ParameterDefinition>(),
                (context, callSite, args) =>
                {
                    return DateTime.Now;
                });

            Register("utcNow",
                new List<ParameterDefinition>(),
                (context, callSite, args) =>
                {
                    return DateTime.UtcNow;
                });

            Register("uri",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var uriString = args[0] as string;
                    if (string.IsNullOrEmpty(uriString))
                        throw new TemplateEvaluationException("uri function requires a non-empty string argument", context);

                    try
                    {
                        return new Uri(uriString);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to parse uri string '{uriString}': {ex.Message}", context);
                    }
                });

            Register("htmlEncode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var html = args[0] as string;

                    try
                    {
                        return WebUtility.HtmlEncode(html);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to encode html: {ex.Message}", context);
                    }
                });

            Register("htmlDecode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var html = args[0] as string;

                    try
                    {
                        return WebUtility.HtmlDecode(html);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to decode html: {ex.Message}", context);
                    }
                });

            Register("urlEncode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var url = args[0] as string;

                    try
                    {
                        return WebUtility.UrlEncode(url);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to encode url: {ex.Message}", context);
                    }
                });

            Register("urlDecode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var url = args[0] as string;

                    try
                    {
                        return WebUtility.UrlDecode(url);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to decode url: {ex.Message}", context);
                    }
                });

            Register("fromJson",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(string))
                },
                (context, callSite, args) =>
                {
                    var jsonString = args[0] as string;
                    if (string.IsNullOrEmpty(jsonString))
                        throw new TemplateEvaluationException("fromJson function requires a non-empty string argument", context);

                    try
                    {
                        return ParseJsonToExpando(jsonString);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to parse JSON string: {ex.Message}", context);
                    }
                });

            Register("toJson",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(object)),
                    new ParameterDefinition(typeof(bool), true, false)
                },
                (context, callSite, args) =>
                {
                    var obj = args[0];
                    var formatted = Convert.ToBoolean(args[1]);

                    if (obj == null)
                        return "null";

                    try
                    {
                        // Convert the object to a format suitable for serialization
                        var serializable = ConvertToSerializable(obj);
                        var json = TypeHelper.JsonSerialize(serializable);

                        if (formatted)
                        {
                            return FormatJson(json);
                        }

                        return json;
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException($"Failed to serialize object to JSON: {ex.Message}", context);
                    }
                });
        }

        private dynamic ParseJsonToExpando(string jsonString)
        {
            var serializer = new JavaScriptSerializer();
            var deserializedObject = serializer.Deserialize<object>(jsonString);
            return ConvertToExpando(deserializedObject);
        }

        private dynamic ConvertToExpando(object obj)
        {
            if (obj == null) return null;

            // Handle dictionary (objects in JSON)
            if (obj is Dictionary<string, object> dict)
            {
                var expando = new ExpandoObject() as IDictionary<string, object>;
                foreach (var kvp in dict)
                {
                    var convertedValue = ConvertToExpando(kvp.Value);
                    if (convertedValue != null)  // Skip null values
                    {
                        expando[kvp.Key] = convertedValue;
                    }
                }
                return expando;
            }

            // Handle array
            if (obj is System.Collections.ArrayList arrayList)
            {
                return arrayList.Cast<object>()
                               .Select(item => ConvertToExpando(item))
                               .Where(item => item != null)  // Filter out null values
                               .ToList();
            }

            if (obj is Object[] array)
            {
                return array.Cast<object>()
                               .Select(item => ConvertToExpando(item))
                               .Where(item => item != null)  // Filter out null values
                               .ToList();
            }

            // Handle numbers - convert to decimal where possible
            if (TypeHelper.IsConvertibleToDecimal(obj))
            {
                return Convert.ToDecimal(obj);
            }

            // Return other primitives as-is (string, bool)
            return obj;
        }

        private object ConvertToSerializable(object obj)
        {
            if (obj == null) return null;

            // Handle ExpandoObject (dynamic objects)
            if (obj is ExpandoObject expando)
            {
                var dict = new Dictionary<string, object>();
                foreach (var kvp in (IDictionary<string, object>)expando)
                {
                    dict[kvp.Key] = ConvertToSerializable(kvp.Value);
                }
                return dict;
            }

            // Handle arrays and collections
            if (obj is System.Collections.IEnumerable enumerable && !(obj is string))
            {
                return enumerable.Cast<object>()
                                .Select(item => ConvertToSerializable(item))
                                .ToList();
            }

            // Handle DateTime
            if (obj is DateTime dateTime)
            {
                return dateTime.ToString("o"); // ISO 8601 format
            }

            // Handle Uri
            if (obj is Uri uri)
            {
                return uri.ToString();
            }

            // Handle numeric types - ensure proper decimal serialization
            if (TypeHelper.IsConvertibleToDecimal(obj))
            {
                return Convert.ToDecimal(obj);
            }

            // Return primitives and other basic types as-is
            return obj;
        }

        private string FormatJson(string json)
        {
            var indent = 0;
            var quoted = false;
            var sb = new StringBuilder();

            for (var i = 0; i < json.Length; i++)
            {
                var ch = json[i];
                switch (ch)
                {
                    case '{':
                    case '[':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            indent++;
                            sb.Append(new string(' ', indent * 4));
                        }
                        break;
                    case '}':
                    case ']':
                        if (!quoted)
                        {
                            sb.AppendLine();
                            indent--;
                            sb.Append(new string(' ', indent * 4));
                        }
                        sb.Append(ch);
                        break;
                    case '"':
                        sb.Append(ch);
                        bool escaped = false;
                        var index = i;
                        while (index > 0 && json[--index] == '\\')
                            escaped = !escaped;
                        if (!escaped)
                            quoted = !quoted;
                        break;
                    case ',':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            sb.Append(new string(' ', indent * 4));
                        }
                        break;
                    case ':':
                        sb.Append(ch);
                        if (!quoted)
                            sb.Append(" ");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }

        public void Register(
            string name,
            List<ParameterDefinition> parameters,
            Func<ExecutionContext, AstNode, List<dynamic>, dynamic> implementation,
            bool isLazilyEvaluated = false)
        {
            var definition = new FunctionDefinition(name, parameters, implementation, isLazilyEvaluated);

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
                throw new InitializationException($"Function '{name}' is already registered with the same parameter types");
            }

            _functions[name].Add(definition);
        }

        public bool LazyFunctionExists(
            string name,
            int argumentCount)
        {
            if (!_functions.TryGetValue(name, out var overloads))
            {
                return false;
            }

            var candidates = overloads.Where(f =>
                argumentCount >= f.RequiredParameterCount &&
                argumentCount <= f.TotalParameterCount &&
                f.IsLazilyEvaluated
            ).ToList();

            if (!candidates.Any())
            {
                return false;
            }

            return true;
        }

        public bool TryGetFunction(
            string name,
            List<dynamic> arguments,
            out FunctionDefinition matchingFunction,
            out List<dynamic> effectiveArguments,
            ExecutionContext context)
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
                EffectiveArgs = CreateEffectiveArguments(overload.Parameters, arguments, context)
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
                throw new TemplateEvaluationException($"Ambiguous function call to '{name}'. Multiple overloads match the provided arguments.", context);
            }

            var bestMatch = scoredOverloads.First();
            matchingFunction = bestMatch.Function;
            effectiveArguments = bestMatch.EffectiveArgs;
            return true;
        }

        private List<dynamic> CreateEffectiveArguments(
            List<ParameterDefinition> parameters,
            List<dynamic> providedArgs,
            ExecutionContext context)
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
                    throw new TemplateEvaluationException("Function missing required argument", context);
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

        public void ValidateArguments(FunctionDefinition function, List<dynamic> arguments, ExecutionContext context)
        {
            if (arguments.Count != function.Parameters.Count)
            {
                throw new TemplateEvaluationException(
                    $"Function '{function.Name}' expects {function.Parameters.Count} arguments, but got {arguments.Count}",
                    context);
            }

            for (int i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                var parameter = function.Parameters[i];

                // Special handling for LazyValue
                if (parameter.Type == typeof(LazyValue) && argument is LazyValue)
                {
                    continue;
                }

                // Handle null arguments
                if (argument == null)
                {
                    if (!parameter.Type.IsClass)
                    {
                        throw new TemplateEvaluationException($"Argument {i + 1} of function '{function.Name}' cannot be null", context);
                    }
                    continue;
                }

                var argumentType = argument.GetType();

                // Special handling for IEnumerable parameter type
                if (parameter.Type == typeof(System.Collections.IEnumerable))
                {
                    if (!(argument is System.Collections.IEnumerable))
                    {
                        throw new TemplateEvaluationException(
                            $"Argument {i + 1} of function '{function.Name}' must be an array or collection",
                            context);
                    }
                    continue;
                }

                // Check if the argument can be converted to the expected type
                if (!parameter.Type.IsAssignableFrom(argumentType))
                {
                    throw new TemplateEvaluationException(
                        $"Argument {i + 1} of function '{function.Name}' must be of type {parameter.Type.Name}",
                        context);
                }
            }
        }
    }

    public class TypeHelper
    {
        public static string FormatOutput(dynamic evaluated, bool serializing = false)
        {
            if (TypeHelper.IsConvertibleToDecimal(evaluated))
            {
                return evaluated.ToString();
            }
            else if (evaluated is bool)
            {
                return evaluated ? "true" : "false";
            }
            if (evaluated is DateTime)
            {
                return evaluated.ToString("o"); // ISO 8601 format
            }
            else if (evaluated is Uri)
            {
                return evaluated.ToString();
            }
            else if ((evaluated is string || evaluated is char) && serializing)
            {
                return $"\"{evaluated.ToString()}\"";
            }
            else if ((evaluated is string || evaluated is char))
            {
                return evaluated.ToString();
            }
            else if (
                evaluated is List<dynamic> ||
                evaluated is List<decimal> ||
                evaluated is List<bool> ||
                evaluated is List<string> ||
                evaluated is List<char> ||
                evaluated is List<DateTime> ||
                evaluated is List<Uri>)
            {
                return FormatArrayOutput(evaluated);
            }
            else if (evaluated is IDictionary<string, object> dict)
            {
                return string.Concat("{",
                    string.Join(", ", dict.Keys.Select(key => string.Concat(key, ": ", FormatOutput(dict[key], true)))), "}");
            }
            else if (evaluated is ExpandoObject expando)
            {
                return string.Concat("{",
                    string.Join(", ", expando.Select(kvp => string.Concat(kvp.Key, ": ", FormatOutput(kvp.Value, true)))), "}");
            }
            else if (evaluated is Func<ExecutionContext, AstNode, List<object>, object> func)
            {
                return "lambda()";
            }
            else
            {
                return "object{}";
            }
        }

        public static string FormatArrayOutput<T>(List<T> array)
        {
            return string.Concat("[", string.Join(", ", array.Select(item => FormatOutput(item, true))), "]");
        }

        public static string JsonSerialize(dynamic evaluated)
        {
            if (TypeHelper.IsConvertibleToDecimal(evaluated))
            {
                return evaluated.ToString();
            }
            else if (evaluated is bool)
            {
                return evaluated ? "true" : "false";
            }
            if (evaluated is DateTime)
            {
                var serializer = new JavaScriptSerializer();
                return serializer.Serialize(evaluated.ToString("o")); // ISO 8601 format
            }
            else if (evaluated is string || evaluated is char || evaluated is Uri)
            {
                var serializer = new JavaScriptSerializer();
                return serializer.Serialize(evaluated.ToString());
            }
            else if (
                evaluated is List<dynamic> ||
                evaluated is List<decimal> ||
                evaluated is List<bool> ||
                evaluated is List<string> ||
                evaluated is List<char> ||
                evaluated is List<DateTime> ||
                evaluated is List<Uri>)
            {
                return JsonSerializeArray(evaluated);
            }
            else if (evaluated is IDictionary<string, object> dict)
            {
                return string.Concat("{",
                    string.Join(",", dict.Keys.Select(key => string.Concat("\"", key, "\"", ":", JsonSerialize(dict[key])))), "}");
            }
            else if (evaluated is ExpandoObject expando)
            {
                return string.Concat("{",
                    string.Join(",", expando.Select(kvp => string.Concat("\"", kvp.Key, "\"", ":", JsonSerialize(kvp.Value)))), "}");
            }
            else if (evaluated is Func<ExecutionContext, AstNode, List<object>, object> func)
            {
                return "\"lambda()\"";
            }
            else
            {
                return $"\"{evaluated.ToString()}\"";
            }
        }

        public static string JsonSerializeArray<T>(List<T> array)
        {
            return string.Concat("[", string.Join(",", array.Select(item => JsonSerialize(item))), "]");
        }

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
        private readonly Func<ExecutionContext, AstNode, List<dynamic>, dynamic> _comparer;
        private readonly ExecutionContext _context;
        private readonly AstNode _callSite;

        public DynamicComparer(ExecutionContext context, AstNode callSite, Func<ExecutionContext, AstNode, List<dynamic>, dynamic> comparer)
        {
            _comparer = comparer;
            _context = context;
            _callSite = callSite;
        }

        public int Compare(object x, object y)
        {
            var result = Convert.ToDecimal(_comparer(_context, _callSite, new List<dynamic> { x, y }));
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
                throw new InitializationException($"Template '{templateName}' not found");
            }
            return template;
        }
    }

    public interface IDataverseService
    {
        List<ExpandoObject> RetrieveMultiple(string fetchXml);
    }

    public class DataverseService : IDataverseService
    {
        private readonly IOrganizationService _organizationService;

        public DataverseService(IOrganizationService organizationService)
        {
            _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
        }

        public List<ExpandoObject> RetrieveMultiple(string fetchXml)
        {
            var fetch = new FetchExpression(fetchXml);
            var results = _organizationService.RetrieveMultiple(fetch);
            return ConvertToExpandoObjects(results);
        }

        private List<ExpandoObject> ConvertToExpandoObjects(EntityCollection entityCollection)
        {
            var expandoObjects = new List<ExpandoObject>();

            foreach (var entity in entityCollection.Entities)
            {
                var expando = new ExpandoObject() as IDictionary<string, object>;

                foreach (var attribute in entity.Attributes)
                {
                    // Skip null values
                    if (attribute.Value != null)
                    {
                        // Handle special types like EntityReference, OptionSetValue, etc.
                        var value = ConvertAttributeValue(attribute.Value);
                        if (value != null)
                        {
                            expando[attribute.Key] = value;
                        }
                    }
                }

                expandoObjects.Add(expando as ExpandoObject);
            }

            return expandoObjects;
        }

        private object ConvertAttributeValue(object attributeValue)
        {
            if (attributeValue == null) return null;

            switch (attributeValue)
            {
                case EntityReference entityRef:
                    return entityRef.Id.ToString();

                case OptionSetValue optionSet:
                    return optionSet.Value;

                case Money money:
                    return money.Value;

                case AliasedValue aliased:
                    return ConvertAttributeValue(aliased.Value);

                default:
                    return attributeValue;
            }
        }
    }
}
