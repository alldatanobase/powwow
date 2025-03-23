using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace TemplateInterpreter
{
    public class Tester
    {
        static void Main(string[] args)
        {
            // Create the interpreter
            var interpreter = new Interpreter();
        }
    }

    public enum ValueType
    {
        Object,
        Array,
        String,
        Number,
        Boolean,
        DateTime,
        Lazy,
        Function,
        Type
    }

    public abstract class Value
    {
        protected dynamic _value;
        private ValueType _type;

        public Value(dynamic value, ValueType type)
        {
            this._value = value;
            this._type = type;
        }

        public dynamic Unbox()
        {
            return _value;
        }

        public void Mutate(Value value)
        {
            _value = value._value;
            _type = value._type;
        }

        public ValueType TypeOf()
        {
            return _type;
        }

        public override string ToString()
        {
            return _value.ToString();
        }

        public bool ExpectType(ValueType type, ExecutionContext context)
        {
            if (_type == type)
            {
                return true;
            }
            else
            {
                throw new InnerEvaluationException($"Expected type {type} but found {_type}");
            }
        }
    }

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
                return new ObjectValue(valueObj);
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
                return new ArrayValue(arr);
            }
            else if (value is string || value is char)
            {
                return new StringValue(value.ToString());
            }
            else if (TypeHelper.IsConvertibleToDecimal(value))
            {
                return new NumberValue(Convert.ToDecimal(value));
            }
            else if (value is bool)
            {
                return new BooleanValue(value);
            }
            else if (value is DateTime)
            {
                return new DateTimeValue(value);
            }
            else if (value is object)
            {
                IDictionary<string, Value> valueObj = new Dictionary<string, Value>();
                var properties = value.GetType().GetProperties();
                foreach (var property in properties)
                {
                    valueObj[property.Name] = ValueFactory.Create(property.GetValue(value));
                }
                return new ObjectValue(valueObj);
            }
            else
            {
                throw new InitializationException("Unable to resolve initial data object as a dynamically typed language object. Encountered an unxpected type.");
            }
        }
    }

    public class FunctionReferenceValue : Value
    {
        public string Name { get; }

        public FunctionReferenceValue(string name) : base(name, ValueType.Function)
        {
            Name = name;
        }
    }

    public class StringValue : Value
    {
        public StringValue(string value) : base(value, ValueType.String) { }

        public string Value()
        {
            return _value;
        }
    }

    public class NumberValue : Value
    {
        public NumberValue(decimal value) : base(value, ValueType.Number) { }

        public decimal Value()
        {
            return _value;
        }
    }

    public class BooleanValue : Value
    {
        public BooleanValue(bool value) : base(value, ValueType.Boolean) { }

        public bool Value()
        {
            return _value;
        }
    }

    public class DateTimeValue : Value
    {
        public DateTimeValue(DateTime value) : base(value, ValueType.DateTime) { }

        public DateTime Value()
        {
            return _value;
        }
    }

    public class ObjectValue : Value
    {
        public ObjectValue(IDictionary<string, Value> value) : base(value, ValueType.Object) { }

        public IDictionary<string, Value> Value()
        {
            return _value;
        }
    }

    public class ArrayValue : Value
    {
        public ArrayValue(IEnumerable<Value> value) : base(value, ValueType.Array) { }

        public IEnumerable<Value> Value()
        {
            return _value;
        }
    }

    public class LambdaValue : Value
    {
        private readonly List<string> _parameterNames;

        public LambdaValue(Func<ExecutionContext, AstNode, List<Value>, Value> value, List<string> parameterNames) :
            base(value, ValueType.Function)
        {
            _parameterNames = parameterNames;
        }

        public List<string> ParameterNames { get { return _parameterNames; } }

        public Func<ExecutionContext, AstNode, List<Value>, Value> Value()
        {
            return _value;
        }
    }

    public class LazyValue : Value
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
    }

    public class TypeValue : Value
    {
        public TypeValue(ValueType value) : base(value, ValueType.Type) { }

        public ValueType Value()
        {
            return _value;
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

        public void RegisterFunction(string name, List<ParameterDefinition> parameterTypes, Func<ExecutionContext, AstNode, List<Value>, Value> implementation)
        {
            _functionRegistry.Register(name, parameterTypes, implementation);
        }

        private void RegisterDataverseFunctions()
        {
            _functionRegistry.Register("fetch",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    if (_dataverseService == null)
                    {
                        throw new TemplateEvaluationException(
                            "Dataverse service not configured. The fetch function requires a DataverseService to be provided to the Interpreter.",
                            context,
                            callSite);
                    }

                    var fetchXml = (args[0] as StringValue).Value();

                    if (string.IsNullOrEmpty(fetchXml))
                    {
                        throw new TemplateEvaluationException(
                            "fetch function requires a non-empty FetchXML string",
                            context,
                            callSite);
                    }

                    return _dataverseService.RetrieveMultiple(fetchXml);
                });
        }

        public string Interpret(string template, IDictionary<string, dynamic> data)
        {
            var tokens = _lexer.Tokenize(template);
            var ast = _parser.Parse(tokens);

            // If we have a template resolver, process includes
            if (_templateResolver != null)
            {
                ast = ProcessIncludes(ast);
            }

            return ast.Evaluate(new ExecutionContext(
                data != null ? ValueFactory.Create(data) as ObjectValue : new ObjectValue(new Dictionary<string, Value>()),
                _functionRegistry,
                null,
                _maxRecursionDepth,
                ast)).ToString();
        }

        private AstNode ProcessIncludes(AstNode node, HashSet<string> descendantIncludes = null)
        {
            descendantIncludes = descendantIncludes ?? new HashSet<string>();

            // Handle IncludeNode
            if (node is IncludeNode includeNode)
            {
                if (!descendantIncludes.Add(includeNode.TemplateName))
                {
                    throw new TemplateParsingException(
                        $"Circular template reference detected: '{includeNode.TemplateName}'",
                        node.Location);
                }

                try
                {
                    var templateContent = _templateResolver.ResolveTemplate(includeNode.TemplateName);
                    var tokens = _lexer.Tokenize(templateContent);
                    var includedAst = _parser.Parse(tokens);

                    // Process includes in the included template
                    includedAst = ProcessIncludes(includedAst, descendantIncludes);

                    // Set the processed template
                    includeNode.SetIncludedTemplate(includedAst);
                    return includeNode;
                }
                finally
                {
                    descendantIncludes.Remove(includeNode.TemplateName);
                }
            }

            // Handle TemplateNode
            if (node is TemplateNode templateNode)
            {
                var processedChildren = templateNode.Children.Select(child => ProcessIncludes(child, descendantIncludes)).ToList();
                return new TemplateNode(processedChildren, node.Location);
            }

            // Handle IfNode
            if (node is IfNode ifNode)
            {
                var processedBranches = ifNode.ConditionalBranches.Select(branch =>
                    new IfNode.IfBranch(branch.Condition, ProcessIncludes(branch.Body, descendantIncludes))).ToList();
                var processedElse = ifNode.ElseBranch != null ? ProcessIncludes(ifNode.ElseBranch, descendantIncludes) : null;
                return new IfNode(processedBranches, processedElse, node.Location);
            }

            // Handle ForNode
            if (node is ForNode forNode)
            {
                var processedBody = ProcessIncludes(forNode.Body, descendantIncludes);
                return new ForNode(forNode.IteratorName, forNode.Collection, processedBody, node.Location);
            }

            // For all other node types, return as is
            return node;
        }
    }

    public class ExecutionContext
    {
        protected readonly ObjectValue _data;
        protected readonly Dictionary<string, Value> _iteratorValues;
        protected Dictionary<string, Value> _variables;
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
            ObjectValue data,
            FunctionRegistry functionRegistry,
            ExecutionContext parentContext,
            int maxDepth,
            AstNode callSite)
        {
            _data = data;
            _iteratorValues = new Dictionary<string, Value>();
            _variables = new Dictionary<string, Value>();
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
                throw new TemplateEvaluationException(
                    $"Maximum call stack depth {_maxDepth} has been exceeded.",
                    this,
                    _callSite);
            }
        }

        public virtual void DefineVariable(string name, Value value)
        {
            // Check if already defined as a variable, an iterator variable, or defined in the data context
            if (_variables.ContainsKey(name) || _iteratorValues.ContainsKey(name) || TryResolveValue(name, out _))
            {
                throw new InnerEvaluationException(
                    $"Cannot define variable '{name}' because it conflicts with an existing variable or field");
            }

            // If we get here, the name is safe to use
            _variables[name] = value;
        }

        public virtual void RedefineVariable(string name, Value value)
        {
            bool result = TryResolveMutableValue(name, out Value variable);
            if (!result)
            {
                throw new InnerEvaluationException(
                    $"Cannot mutate variable '{name}' because it has not been defined");
            }

            // If we get here, the name is safe to use
            variable.Mutate(value);
        }

        public ExecutionContext CreateIteratorContext(string iteratorName, Value value, AstNode callSite)
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

        public ObjectValue GetData()
        {
            return _data;
        }

        protected bool TryGetDataProperty(string propertyName, out Value value)
        {
            value = null;

            if (_data == null || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            return _data.Value().TryGetValue(propertyName, out value);
        }

        public virtual bool TryResolveNonShadowableValue(string path, out Value value)
        {
            value = null;
            var parts = path.Split('.');
            Value current = null;

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
                    IDictionary<string, Value> currentObject = null;
                    if (current is ObjectValue obj)
                    {
                        currentObject = obj.Value();
                    }
                    else if (current is IDictionary<string, Value> dict)
                    {
                        currentObject = dict;
                    }
                    current = currentObject[part];
                }
                catch
                {
                    return false;
                }
            }

            value = current;
            return true;
        }

        public virtual bool TryResolveMutableValue(string path, out Value value)
        {
            value = null;
            var parts = path.Split('.');
            Value current = null;

            // Check if the first part is an iterator
            if (_iteratorValues.ContainsKey(parts[0]))
            {
                throw new InnerEvaluationException($"Iterator variable {path} is not mutable and cannot be reassigned");
            }
            else if (_variables.ContainsKey(parts[0]))
            {
                current = _variables[parts[0]];
                parts = parts.Skip(1).ToArray();

                foreach (var part in parts)
                {
                    try
                    {
                        IDictionary<string, Value> currentObject = null;
                        if (current is ObjectValue obj)
                        {
                            currentObject = obj.Value();
                        }
                        else if (current is IDictionary<string, Value> dict)
                        {
                            currentObject = dict;
                        }
                        current = currentObject[part];
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            else if (TryGetDataProperty(parts[0], out current))
            {
                throw new InnerEvaluationException($"Global variable {path} is not mutable and cannot be reassigned");
            }
            else if (_parentContext != null)
            {
                return _parentContext.TryResolveMutableValue(path, out value);
            }
            else
            {
                return false;
            }

            value = current;
            return true;
        }

        public virtual bool TryResolveValue(string path, out Value value)
        {
            value = null;
            var parts = path.Split('.');
            Value current = null;

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
            }

            value = current;
            return true;
        }

        public virtual Value ResolveValue(string path)
        {
            if (TryResolveValue(path, out Value value))
            {
                return value;
            }

            if (GetFunctionRegistry().HasFunction(path))
            {
                return new FunctionReferenceValue(path);
            }

            throw new InnerEvaluationException($"Unknown identifier: {path}");
        }
    }

    public class LambdaExecutionContext : ExecutionContext
    {
        private readonly Dictionary<string, Value> _parameters;
        private readonly ExecutionContext _definitionContext;

        public LambdaExecutionContext(
            ExecutionContext parentContext,
            ExecutionContext definitionContext,
            List<string> parameterNames,
            List<Value> parameterValues,
            AstNode node)
            : base(parentContext.GetData(), parentContext.GetFunctionRegistry(), parentContext, parentContext.MaxDepth, node)
        {
            _parameters = new Dictionary<string, Value>();
            _variables = new Dictionary<string, Value>();
            _definitionContext = definitionContext;

            if (parameterNames.Count > parameterValues.Count)
            {
                var missingCount = parameterNames.Count - parameterValues.Count;
                var missingParams = string.Join(", ", parameterNames.Skip(parameterValues.Count).Take(missingCount));
                throw new InnerEvaluationException($"Not enough parameter values provided. Missing values for: {missingParams}");
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

        public override void DefineVariable(string name, Value value)
        {
            // Check if already defined as a variable
            if (_variables.ContainsKey(name))
            {
                throw new InnerEvaluationException(
                    $"Cannot define variable '{name}' because it conflicts with an existing variable or field");
            }

            // Check if defined as a parameter
            if (_parameters.ContainsKey(name))
            {
                throw new InnerEvaluationException(
                    $"Cannot define variable '{name}' because it conflicts with a parameter name");
            }

            if (_parentContext.TryResolveNonShadowableValue(name, out _))
            {
                throw new InnerEvaluationException(
                    $"Cannot define variable '{name}' because it conflicts with an existing variable or field");
            }

            _variables[name] = value;
        }

        public bool TryGetParameterFromAnyContext(string name, out Value value)
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

        public override bool TryResolveNonShadowableValue(string path, out Value value)
        {
            value = null;
            var parts = path.Split('.');
            Value current = null;

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
                    IDictionary<string, Value> currentObject = null;
                    if (current is ObjectValue obj)
                    {
                        currentObject = obj.Value();
                    }
                    else if (current is IDictionary<string, Value> dict)
                    {
                        currentObject = dict;
                    }
                    current = currentObject[part];
                }
                catch
                {
                    return false;
                }
            }

            value = current;
            return true;
        }

        public override bool TryResolveMutableValue(string path, out Value value)
        {
            value = null;
            var parts = path.Split('.');
            Value current = null;

            // Check if the first part is an iterator
            if (_iteratorValues.ContainsKey(parts[0]))
            {
                throw new InnerEvaluationException($"Iterator variable {path} is not mutable and cannot be reassigned");
            }
            else if (_parameters.TryGetValue(parts[0], out current))
            {
                throw new InnerEvaluationException($"Parameter {path} is not mutable and cannot be reassigned");
            }
            else if (_variables.ContainsKey(parts[0]))
            {
                current = _variables[parts[0]];
                parts = parts.Skip(1).ToArray();

                foreach (var part in parts)
                {
                    try
                    {
                        IDictionary<string, Value> currentObject = null;
                        if (current is ObjectValue obj)
                        {
                            currentObject = obj.Value();
                        }
                        else if (current is IDictionary<string, Value> dict)
                        {
                            currentObject = dict;
                        }
                        current = currentObject[part];
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            else if (TryGetDataProperty(parts[0], out current))
            {
                throw new InnerEvaluationException($"Global variable {path} is not mutable and cannot be reassigned");
            }
            else
            {
                Value descendentValue;

                if (_definitionContext.TryResolveMutableValue(path, out descendentValue))
                {
                    value = descendentValue;
                    return true;
                }
                else if (_parentContext.TryResolveMutableValue(path, out descendentValue))
                {
                    value = descendentValue;
                    return true;
                }

                return false;
            }

            value = current;
            return true;
        }

        public override bool TryResolveValue(string path, out Value value)
        {
            value = null;
            var parts = path.Split('.');
            Value current = null;

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
                Value descendentValue;

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
            }

            value = current;
            return true;
        }

        public override Value ResolveValue(string path)
        {
            var parts = path.Split('.');

            // First check if it's a parameter
            if (_parameters.ContainsKey(parts[0]))
            {
                Value current = _parameters[parts[0]];

                // Handle nested property access for parameters
                for (int i = 1; i < parts.Length; i++)
                {
                    try
                    {
                        IDictionary<string, Value> currentObject = null;
                        if (current is ObjectValue obj)
                        {
                            currentObject = obj.Value();
                        }
                        else if (current is IDictionary<string, Value> dict)
                        {
                            currentObject = dict;
                        }
                        current = currentObject[parts[i]];
                    }
                    catch
                    {
                        throw new InnerEvaluationException($"Unknown identifier: {path}");
                    }
                }

                return current;
            }
            else if (_variables.ContainsKey(parts[0]))
            {
                Value current = _variables[parts[0]];

                // Handle nested property access for parameters
                for (int i = 1; i < parts.Length; i++)
                {
                    try
                    {
                        IDictionary<string, Value> currentObject = null;
                        if (current is ObjectValue obj)
                        {
                            currentObject = obj.Value();
                        }
                        else if (current is IDictionary<string, Value> dict)
                        {
                            currentObject = dict;
                        }
                        current = currentObject[parts[i]];
                    }
                    catch
                    {
                        throw new InnerEvaluationException($"Unknown identifier: {path}");
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

    public class InnerEvaluationException : Exception
    {
        public InnerEvaluationException(string message) : base(message) { }
    }

    public class TemplateEvaluationException : Exception
    {
        public SourceLocation Location { get; }
        public string Descriptor { get; }
        public override string StackTrace { get; }
        public AstNode CallSite { get; }

        public TemplateEvaluationException(
            string message,
            ExecutionContext frame,
            AstNode callSite)
            : base(FormatMessage(message, frame.CallSite.Location))
        {
            Location = frame.CallSite.Location;
            Descriptor = message;
            CallSite = callSite;
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
        CommentEnd,        // *
        Type,              // String | Number | Boolean | Array | Object | Function | DateTime
        Mutation           // mut
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

                // Match function calls before other operations
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
                else if (TryMatch("mut"))
                {
                    AddToken(TokenType.Mutation, "mut");
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
                    continue;
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
                    continue;
                }
                else if (TryMatch("elseif"))
                {
                    AddToken(TokenType.ElseIf, "elseif");
                    UpdatePositionAndTracking(6);
                    continue;
                }
                else if (TryMatch("else"))
                {
                    AddToken(TokenType.Else, "else");
                    UpdatePositionAndTracking(4);
                    continue;
                }
                else if (TryMatch("/for"))
                {
                    AddToken(TokenType.EndFor, "/for");
                    UpdatePositionAndTracking(4);
                    continue;
                }
                else if (TryMatch("/if"))
                {
                    AddToken(TokenType.EndIf, "/if");
                    UpdatePositionAndTracking(3);
                    continue;
                }
                else if (TryMatch("String"))
                {
                    AddToken(TokenType.Type, "String");
                    UpdatePositionAndTracking(6);
                    continue;
                }
                else if (TryMatch("Number"))
                {
                    AddToken(TokenType.Type, "Number");
                    UpdatePositionAndTracking(6);
                    continue;
                }
                else if (TryMatch("Boolean"))
                {
                    AddToken(TokenType.Type, "Boolean");
                    UpdatePositionAndTracking(7);
                    continue;
                }
                else if (TryMatch("Array"))
                {
                    AddToken(TokenType.Type, "Array");
                    UpdatePositionAndTracking(5);
                    continue;
                }
                else if (TryMatch("Object"))
                {
                    AddToken(TokenType.Type, "Object");
                    UpdatePositionAndTracking(6);
                    continue;
                }
                else if (TryMatch("Function"))
                {
                    AddToken(TokenType.Type, "Function");
                    UpdatePositionAndTracking(8);
                    continue;
                }

                else if (TryMatch("DateTime"))
                {
                    AddToken(TokenType.Type, "DateTime");
                    UpdatePositionAndTracking(8);
                    continue;
                }
                else if (TryMatch(">="))
                {
                    AddToken(TokenType.GreaterThanEqual, ">=");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch("<="))
                {
                    AddToken(TokenType.LessThanEqual, "<=");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch("=="))
                {
                    AddToken(TokenType.Equal, "==");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch("="))
                {
                    AddToken(TokenType.Assignment, "=");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("!="))
                {
                    AddToken(TokenType.NotEqual, "!=");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch("&&"))
                {
                    AddToken(TokenType.And, "&&");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch("||"))
                {
                    AddToken(TokenType.Or, "||");
                    UpdatePositionAndTracking(2);
                    continue;
                }
                else if (TryMatch(">"))
                {
                    AddToken(TokenType.GreaterThan, ">");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("<"))
                {
                    AddToken(TokenType.LessThan, "<");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("!"))
                {
                    AddToken(TokenType.Not, "!");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("+"))
                {
                    AddToken(TokenType.Plus, "+");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("*"))
                {
                    AddToken(TokenType.Multiply, "*");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("/"))
                {
                    AddToken(TokenType.Divide, "/");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("("))
                {
                    AddToken(TokenType.LeftParen, "(");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch(")"))
                {
                    AddToken(TokenType.RightParen, ")");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (TryMatch("\""))
                {
                    TokenizeString();
                    continue;
                }
                else if (char.IsDigit(_input[_position]) || (_input[_position] == '-' && char.IsDigit(PeekNext())))
                {
                    TokenizeNumber();
                    continue;
                }
                else if (TryMatch("-"))
                {
                    AddToken(TokenType.Minus, "-");
                    UpdatePositionAndTracking(1);
                    continue;
                }
                else if (char.IsLetter(_input[_position]) || _input[_position] == '_')
                {
                    TokenizeIdentifier();
                    continue;
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

        public abstract Value Evaluate(ExecutionContext context);

        public override string ToString()
        {
            return "AstNode";
        }

        public abstract string ToStackString();
    }

    public class TypeNode : AstNode
    {
        private readonly ValueType _type;

        public TypeNode(ValueType type, SourceLocation location) : base(location)
        {
            _type = type;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new TypeValue(_type);
        }

        public override string ToStackString()
        {
            return $"<type<{_type.ToString()}>>";
        }

        public override string ToString()
        {
            return $"TypeNode(type={_type.ToString()})";
        }
    }

    public class LiteralNode : AstNode
    {
        private readonly string _content;

        public LiteralNode(string content, SourceLocation location) : base(location)
        {
            _content = content;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new StringValue(_content);
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

        public override Value Evaluate(ExecutionContext context)
        {
            if (_includedTemplate == null)
            {
                throw new TemplateEvaluationException(
                    $"Template '{_templateName}' could not been resolved",
                    context,
                    _includedTemplate);
            }
            var currentContext = new ExecutionContext(
                context.GetData(),
                context.GetFunctionRegistry(),
                context,
                context.MaxDepth,
                this);
            return _includedTemplate.Evaluate(currentContext);
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

        public override Value Evaluate(ExecutionContext context)
        {
            return new StringValue(_text);
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

        public override Value Evaluate(ExecutionContext context)
        {
            return new StringValue(_text);
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

        public override Value Evaluate(ExecutionContext context)
        {
            return new StringValue(_text);
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

        public override Value Evaluate(ExecutionContext context)
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
                _arguments.Select(arg => new LazyValue(arg, currentContext)).ToList<Value>() :
                _arguments.Select(arg => arg.Evaluate(currentContext)).ToList<Value>();

            // Now handle the callable based on its type
            if (callable is LambdaValue lambdaFunc)
            {
                // Direct lambda invocation
                // Lambda establishes its own new child context
                return lambdaFunc.Value()(parentContext, this, args);
            }
            else if (callable is FunctionReferenceValue functionInfo)
            {
                var registry = context.GetFunctionRegistry();

                // First check if this is a parameter that contains a function in any parent context
                if (context is LambdaExecutionContext lambdaContext &&
                    lambdaContext.TryGetParameterFromAnyContext(functionInfo.Name, out var paramValue))
                {
                    if (paramValue is LambdaValue paramFunc)
                    {
                        // Lambda establishes its own new child context
                        return paramFunc.Value()(parentContext, this, args);
                    }
                    else if (paramValue is FunctionReferenceValue paramFuncInfo)
                    {

                        try
                        {
                            if (!registry.TryGetFunction(paramFuncInfo.Name, args, out var function, out var effectiveArgs))
                            {
                                throw new TemplateEvaluationException(
                                    $"No matching overload found for function '{paramFuncInfo.Name}' with the provided arguments",
                                    currentContext,
                                    _callable);
                            }
                            registry.ValidateArguments(function, effectiveArgs);
                            return function.Implementation(currentContext, this, effectiveArgs);
                        }
                        catch (InnerEvaluationException ex)
                        {
                            throw new TemplateEvaluationException(ex.Message, currentContext, _callable);
                        }
                    }
                }

                // Check if this is a variable that contains a function
                if (context.TryResolveValue(functionInfo.Name, out var variableValue))
                {
                    if (variableValue is LambdaValue variableFunc)
                    {
                        // Lambda establishes its own new child context
                        return variableFunc.Value()(parentContext, this, args);
                    }

                    if (variableValue is FunctionReferenceValue variableFuncInfo)
                    {
                        try
                        {
                            if (!registry.TryGetFunction(variableFuncInfo.Name, args, out var varFunc, out var varEffArgs))
                            {
                                throw new TemplateEvaluationException(
                                    $"No matching overload found for function '{functionInfo.Name}' with the provided arguments",
                                    currentContext,
                                    _callable);
                            }
                            registry.ValidateArguments(varFunc, varEffArgs);
                            return varFunc.Implementation(currentContext, this, varEffArgs);
                        }
                        catch (InnerEvaluationException ex)
                        {
                            throw new TemplateEvaluationException(ex.Message, currentContext, _callable);
                        }
                    }
                }

                try
                {
                    // If not a parameter in any context or parameter isn't a function, try the registry
                    if (!registry.TryGetFunction(functionInfo.Name, args, out var func, out var effArgs))
                    {
                        throw new TemplateEvaluationException(
                            $"No matching overload found for function '{functionInfo.Name}' with the provided arguments",
                            currentContext,
                            _callable);
                    }
                    registry.ValidateArguments(func, effArgs);
                    return func.Implementation(currentContext, this, effArgs);
                }
                catch (InnerEvaluationException ex)
                {
                    throw new TemplateEvaluationException(ex.Message, currentContext, _callable);
                }
            }

            throw new TemplateEvaluationException(
                $"Expression is not callable: {callable?.GetType().Name ?? "<unknown>"}",
                currentContext,
                _callable);
        }

        private bool IsLazilyEvaluatedFunction(Value callable, int argumentCount, ExecutionContext context)
        {
            // Check if this is a function that requires lazy evaluation
            if (callable is FunctionReferenceValue functionInfo)
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

    public class FunctionReferenceNode : AstNode
    {
        private readonly string _functionName;

        public FunctionReferenceNode(string functionName, SourceLocation location) : base(location)
        {
            _functionName = functionName;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            return new FunctionReferenceValue(_functionName);
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
        private readonly List<KeyValuePair<string, Tuple<AstNode, StatementType>>> _statements;
        private readonly AstNode _finalExpression;

        public enum StatementType
        {
            Declaration,
            Mutation
        }

        public LambdaNode(
            List<string> parameters,
            List<KeyValuePair<string, Tuple<AstNode, StatementType>>> statements,
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
                if (statement.Value.Item2 == StatementType.Declaration)
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
            }

            _parameters = parameters;
            _statements = statements;
            _finalExpression = finalExpression;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            var definitionContext = context;

            // Return a delegate that can be called later with parameters
            // Context is captured here, during evaluation, not during parsing
            return new LambdaValue(new Func<ExecutionContext, AstNode, List<Value>, Value>((callerContext, callSite, args) =>
            {
                try
                {
                    // Create a new context that includes both captured context and new parameters
                    var lambdaContext = new LambdaExecutionContext(callerContext, definitionContext, _parameters, args, callSite);

                    // Execute each statement in order
                    foreach (var statement in _statements)
                    {
                        var value = statement.Value.Item1.Evaluate(lambdaContext);
                        try
                        {
                            if (statement.Value.Item2 == StatementType.Declaration)
                            {
                                lambdaContext.DefineVariable(statement.Key, value);
                            }
                            else
                            {
                                lambdaContext.RedefineVariable(statement.Key, value);
                            }
                        }
                        catch (InnerEvaluationException ex)
                        {
                            throw new TemplateEvaluationException(ex.Message, lambdaContext, callSite);
                        }
                    }

                    return _finalExpression.Evaluate(lambdaContext);
                }
                catch (InnerEvaluationException ex)
                {
                    throw new TemplateEvaluationException(ex.Message, context, this);
                }
            }), _parameters);
        }

        public override string ToStackString()
        {
            return $"<lambda>";
        }

        public override string ToString()
        {
            var paramsStr = string.Join(", ", _parameters.Select(p => $"\"{p}\""));
            var statementsStr = string.Join(", ",
                _statements.Select(st => $"{{key=\"{st.Key}\", value={st.Value.Item1.ToString()}, type={st.Value.Item2.ToString()}}}"));

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

        public override Value Evaluate(ExecutionContext context)
        {
            var value = _expression.Evaluate(context);
            try
            {
                context.DefineVariable(_variableName, value);
            }
            catch (InnerEvaluationException ex)
            {
                throw new TemplateEvaluationException(ex.Message, context, this);
            }
            return new StringValue(string.Empty); // Let statements don't produce output
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

    public class MutationNode : AstNode
    {
        private readonly string _variableName;
        private readonly AstNode _expression;

        public MutationNode(string variableName, AstNode expression, SourceLocation location) : base(location)
        {
            _variableName = variableName;
            _expression = expression;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            var value = _expression.Evaluate(context);
            try
            {
                context.RedefineVariable(_variableName, value);
            }
            catch (InnerEvaluationException ex)
            {
                throw new TemplateEvaluationException(ex.Message, context, this);
            }
            return new StringValue(string.Empty); // Mutations don't produce output
        }

        public override string ToStackString()
        {
            return $"<mut>";
        }

        public override string ToString()
        {
            return $"MutationNode(variableName=\"{_variableName}\", expression={_expression.ToString()})";
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

        public override Value Evaluate(ExecutionContext context)
        {
            var result = _body.Evaluate(context);
            try
            {
                context.DefineVariable(_variableName, result);
            }
            catch (InnerEvaluationException ex)
            {
                throw new TemplateEvaluationException(ex.Message, context, this);
            }
            return new StringValue(string.Empty); // Capture doesn't output anything directly
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

        public override Value Evaluate(ExecutionContext context)
        {
            var dict = new Dictionary<string, Value>();

            foreach (var field in _fields)
            {
                dict[field.Key] = field.Value.Evaluate(context);
            }

            return new ObjectValue(dict);
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

        public override Value Evaluate(ExecutionContext context)
        {
            var evaluated = _object.Evaluate(context);
            if (evaluated == null)
            {
                throw new TemplateEvaluationException(
                    $"Cannot access field '{_fieldName}' on null object",
                    context,
                    _object);
            }

            if (evaluated is ObjectValue obj)
            {
                var value = obj.Value();
                if (!value.ContainsKey(_fieldName))
                {
                    throw new TemplateEvaluationException(
                        $"Object does not contain field '{_fieldName}'",
                        context,
                        _object);
                }
                return value[_fieldName];
            }

            throw new TemplateEvaluationException(
                $"Object does not contain field '{_fieldName}'",
                context,
                _object);
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

        public override Value Evaluate(ExecutionContext context)
        {
            return new ArrayValue(_elements.Select(element => element.Evaluate(context)).ToList());
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

        public override Value Evaluate(ExecutionContext context)
        {
            try
            {
                return context.ResolveValue(_path);
            }
            catch (InnerEvaluationException ex)
            {
                throw new TemplateEvaluationException(ex.Message, context, this);
            }
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

        public override Value Evaluate(ExecutionContext context)
        {
            return new StringValue(_value);
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

        public override Value Evaluate(ExecutionContext context)
        {
            return new NumberValue(_value);
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

        public override Value Evaluate(ExecutionContext context)
        {
            return new BooleanValue(_value);
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

        public override Value Evaluate(ExecutionContext context)
        {
            var value = _expression.Evaluate(context);

            switch (_operator)
            {
                case TokenType.Not:
                    if (!(value is BooleanValue boolValue))
                    {
                        throw new TemplateEvaluationException(
                            $"Expected value of type boolean but found {value.GetType()}",
                            context,
                            _expression);
                    }
                    return new BooleanValue(!boolValue.Value());
                default:
                    throw new TemplateEvaluationException(
                        $"Unknown unary operator: {_operator}",
                        context,
                        this);
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

        public override Value Evaluate(ExecutionContext context)
        {
            // short circuit eval for &&
            if (_operator == TokenType.And)
            {
                return new BooleanValue(
                    TypeHelper.UnboxBoolean(_left.Evaluate(context), context, _left) &&
                    TypeHelper.UnboxBoolean(_right.Evaluate(context), context, _right));
            }

            // short circuit eval for ||
            if (_operator == TokenType.Or)
            {
                return new BooleanValue(
                    TypeHelper.UnboxBoolean(_left.Evaluate(context), context, _left) ||
                    TypeHelper.UnboxBoolean(_right.Evaluate(context), context, _right));
            }

            var left = _left.Evaluate(context);
            var right = _right.Evaluate(context);

            // type check?

            switch (_operator)
            {
                case TokenType.Plus:
                    return new NumberValue(TypeHelper.UnboxNumber(left, context, _left) + TypeHelper.UnboxNumber(right, context, _right));
                case TokenType.Minus:
                    return new NumberValue(TypeHelper.UnboxNumber(left, context, _left) - TypeHelper.UnboxNumber(right, context, _right));
                case TokenType.Multiply:
                    return new NumberValue(TypeHelper.UnboxNumber(left, context, _left) * TypeHelper.UnboxNumber(right, context, _right));
                case TokenType.Divide:
                    return new NumberValue(TypeHelper.UnboxNumber(left, context, _left) / TypeHelper.UnboxNumber(right, context, _right));
                case TokenType.LessThan:
                    return new BooleanValue(TypeHelper.UnboxNumber(left, context, _left) < TypeHelper.UnboxNumber(right, context, _right));
                case TokenType.LessThanEqual:
                    return new BooleanValue(TypeHelper.UnboxNumber(left, context, _left) <= TypeHelper.UnboxNumber(right, context, _right));
                case TokenType.GreaterThan:
                    return new BooleanValue(TypeHelper.UnboxNumber(left, context, _left) > TypeHelper.UnboxNumber(right, context, _right));
                case TokenType.GreaterThanEqual:
                    return new BooleanValue(TypeHelper.UnboxNumber(left, context, _left) >= TypeHelper.UnboxNumber(right, context, _right));
                case TokenType.Equal:
                    if (left.TypeOf() != right.TypeOf())
                    {
                        throw new TemplateEvaluationException(
                            $"Expected similar types but found {left.TypeOf()} and {right.TypeOf()}",
                            context,
                            this);
                    }
                    else
                    {
                        if (left is DateTimeValue leftDateTime && right is DateTimeValue rightDateTIme)
                        {
                            return new BooleanValue(Equals(leftDateTime.Value().Ticks, rightDateTIme.Value().Ticks));
                        }
                        else
                        {
                            return new BooleanValue(Equals(left.Unbox(), right.Unbox()));
                        }
                    }
                case TokenType.NotEqual:
                    if (left.TypeOf() != right.TypeOf())
                    {
                        throw new TemplateEvaluationException(
                            $"Expected similar types but found {left.TypeOf()} and {right.TypeOf()}",
                            context,
                            this);
                    }
                    else
                    {
                        if (left is DateTimeValue leftDateTime && right is DateTimeValue rightDateTIme)
                        {
                            return new BooleanValue(!Equals(leftDateTime.Value().Ticks, rightDateTIme.Value().Ticks));
                        }
                        else
                        {
                            return new BooleanValue(!Equals(left.Unbox(), right.Unbox()));
                        }
                    }
                default:
                    throw new TemplateEvaluationException(
                        $"Unknown binary operator: {_operator}",
                        context,
                        this);
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

        public override Value Evaluate(ExecutionContext context)
        {
            // Check if iterator name conflicts with existing variable
            if (context.TryResolveValue(_iteratorName, out _))
            {
                throw new TemplateEvaluationException(
                    $"Iterator name '{_iteratorName}' conflicts with an existing variable or field",
                    context,
                    this);
            }

            var collection = _collection.Evaluate(context);
            if (collection is ArrayValue array)
            {
                var result = new StringBuilder();
                foreach (var item in array.Value())
                {
                    var iterationContext = context.CreateIteratorContext(_iteratorName, item, this);
                    result.Append(_body.Evaluate(iterationContext));
                }
                return new StringValue(result.ToString());
            }
            else
            {
                throw new TemplateEvaluationException(
                    "Each statement requires an array",
                    context,
                    _collection);
            }
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

        public override Value Evaluate(ExecutionContext context)
        {
            foreach (var branch in _conditionalBranches)
            {
                var evaluated = branch.Condition.Evaluate(context);
                if (TypeHelper.UnboxBoolean(evaluated, context, this))
                {
                    return branch.Body.Evaluate(context);
                }
            }

            if (_elseBranch != null)
            {
                return _elseBranch.Evaluate(context);
            }

            return new StringValue(string.Empty);
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

        public override Value Evaluate(ExecutionContext context)
        {
            var result = new StringBuilder();
            foreach (var child in _children)
            {
                result.Append(TypeHelper.FormatOutput(child.Evaluate(context).Unbox()));
            }
            return new StringValue(result.ToString());
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
                    else if (nextToken.Type == TokenType.Mutation)
                    {
                        nodes.Add(ParseMutationStatement());
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

        private AstNode ParseMutationStatement()
        {
            Advance(); // Skip {{
            var token = Current();
            Advance(); // Skip mut

            var variableName = Expect(TokenType.Variable).Value;
            Advance();

            // Handle any fields accessed after a dot
            while (_position < _tokens.Count && Current().Type == TokenType.Dot)
            {
                Advance(); // Skip the dot
                var fieldToken = Current();
                if (fieldToken.Type != TokenType.Field && fieldToken.Type != TokenType.Variable)
                {
                    throw new TemplateParsingException($"Expected field name but got {fieldToken.Type}", fieldToken.Location);
                }
                variableName = $"{variableName}.{fieldToken.Value}";
                Advance();
            }

            Expect(TokenType.Assignment);
            Advance();

            var expression = ParseExpression();

            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new MutationNode(variableName, expression, token.Location);
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
            var statements = new List<KeyValuePair<string, Tuple<AstNode, LambdaNode.StatementType>>>();

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
                if (Current().Type != TokenType.Let && Current().Type != TokenType.Mutation)
                {
                    // If next expression is not a variable declaration or mutation then must be return statement
                    var finalExpression = ParseExpression();
                    return new LambdaNode(parameters, statements, finalExpression, _functionRegistry, token.Location);
                }

                var statementType = Current().Type == TokenType.Let ? LambdaNode.StatementType.Declaration : LambdaNode.StatementType.Mutation;
                Advance(); // skip let or mut

                string variableName = null;
                try
                {
                    Expect(TokenType.Variable);
                    variableName = Current().Value;
                    Advance();
                }
                catch (TemplateParsingException ex)
                {
                    throw new TemplateParsingException(
                        $"Expected variable name after 'let' or 'mut' in lambda: {ex.Descriptor}",
                        Current().Location
                    );
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
                    variableName = $"{variableName}.{fieldToken.Value}";
                    Advance();
                }

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
                statements.Add(new KeyValuePair<string, Tuple<AstNode, LambdaNode.StatementType>>(
                    variableName,
                    Tuple.Create(expression, statementType)));

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

                case TokenType.Type:
                    expr = ParseType();
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

        private AstNode ParseType()
        {
            var token = Current();
            Advance();
            ValueType type = ValueType.Type;
            switch (token.Value)
            {
                case "String":
                    type = ValueType.String;
                    break;
                case "Number":
                    type = ValueType.Number;
                    break;
                case "Boolean":
                    type = ValueType.Boolean;
                    break;
                case "Array":
                    type = ValueType.Array;
                    break;
                case "Object":
                    type = ValueType.Object;
                    break;
                case "Function":
                    type = ValueType.Function;
                    break;
                case "DateTime":
                    type = ValueType.DateTime;
                    break;
                default:
                    throw new TemplateParsingException($"Unable to parse unknown type {token.Value}", token.Location);
            }
            return new TypeNode(type, token.Location);
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
        public Func<ExecutionContext, AstNode, List<Value>, Value> Implementation { get; }
        public bool IsLazilyEvaluated { get; }

        public FunctionDefinition(
            string name,
            List<ParameterDefinition> parameters,
            Func<ExecutionContext, AstNode, List<Value>, Value> implementation,
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
            Register("typeof",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(Value))
                },
                (context, callSite, args) =>
                {
                    var value = args[0];
                    return new TypeValue(value.TypeOf());
                });

            Register("length",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var enumerable = (args[0] as ArrayValue).Value();
                    if (enumerable == null)
                    {
                        throw new TemplateEvaluationException(
                            "length function requires an array argument",
                            context,
                            callSite);
                    }
                    return new NumberValue(enumerable.Count());
                });

            Register("length",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value();
                    if (str == null)
                    {
                        throw new TemplateEvaluationException(
                            "length function requires a string argument",
                            context,
                            callSite);
                    }
                    return new NumberValue(str.Length);
                });

            Register("empty",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value();
                    if (str == null)
                    {
                        throw new TemplateEvaluationException(
                            "length function requires a string argument",
                            context,
                            callSite);
                    }
                    return new BooleanValue(string.IsNullOrEmpty(str));
                });

            Register("concat",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var first = (args[0] as ArrayValue).Value();
                    var second = (args[1] as ArrayValue).Value();

                    if (first == null || second == null)
                    {
                        throw new TemplateEvaluationException(
                            "concat function requires both arguments to be arrays",
                            context,
                            callSite);
                    }

                    // Combine both enumerables into a single list
                    return new ArrayValue(first.Concat(second));
                });

            Register("concat",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str1 = (args[0] as StringValue).Value()?.ToString() ?? "";
                    var str2 = (args[1] as StringValue).Value()?.ToString() ?? "";
                    return new StringValue(str1 + str2);
                });

            Register("contains",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value()?.ToString() ?? "";
                    var searchStr = (args[1] as StringValue).Value()?.ToString() ?? "";
                    return new BooleanValue(str.Contains(searchStr));
                });

            Register("contains",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ObjectValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    if (args[0] == null)
                    {
                        return new BooleanValue(false);
                    }

                    var obj = (args[0] as ObjectValue).Value();
                    var propertyName = (args[1] as StringValue).Value();

                    if (string.IsNullOrEmpty(propertyName))
                    {
                        return new BooleanValue(true);
                    }

                    return new BooleanValue(obj.ContainsKey(propertyName));
                });

            Register("startsWith",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value()?.ToString() ?? "";
                    var searchStr = (args[1] as StringValue).Value()?.ToString() ?? "";
                    return new BooleanValue(str.StartsWith(searchStr));
                });

            Register("endsWith",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value()?.ToString() ?? "";
                    var searchStr = (args[1] as StringValue).Value()?.ToString() ?? "";
                    return new BooleanValue(str.EndsWith(searchStr));
                });

            Register("toUpper",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value()?.ToString() ?? "";
                    return new StringValue(str.ToUpper());
                });

            Register("toLower",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value()?.ToString() ?? "";
                    return new StringValue(str.ToLower());
                });

            Register("trim",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value()?.ToString() ?? "";
                    return new StringValue(str.Trim());
                });

            Register("indexOf",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value()?.ToString() ?? "";
                    var searchStr = (args[1] as StringValue).Value()?.ToString() ?? "";
                    return new NumberValue(str.IndexOf(searchStr));
                });

            Register("lastIndexOf",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value()?.ToString() ?? "";
                    var searchStr = (args[1] as StringValue).Value()?.ToString() ?? "";
                    return new NumberValue(str.LastIndexOf(searchStr));
                });

            Register("substring",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(NumberValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new NumberValue(-1)) // Optional end index
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value()?.ToString() ?? "";
                    var startIndex = Convert.ToInt32((args[1] as NumberValue).Value());
                    var endIndex = Convert.ToInt32((args[2] as NumberValue).Value());

                    // If end index is provided, use it; otherwise substring to the end
                    if (endIndex >= 0)
                    {
                        var length = endIndex - startIndex;
                        return new StringValue(str.Substring(startIndex, length));
                    }

                    return new StringValue(str.Substring(startIndex));
                });

            Register("range",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue)),
                    new ParameterDefinition(typeof(NumberValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new NumberValue(1))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0] as NumberValue).Value();
                    var end = (args[1] as NumberValue).Value();
                    var step = (args[2] as NumberValue).Value();

                    if (step == 0)
                    {
                        throw new TemplateEvaluationException(
                            "range function requires a non-zero step value",
                            context,
                            callSite);
                    }

                    var result = new List<NumberValue>();

                    // Handle both positive and negative step values
                    if (step > 0)
                    {
                        for (var value = start + step - step; value < end; value += step)
                        {
                            result.Add(new NumberValue(value));
                        }
                    }
                    else
                    {
                        for (var value = start + step - step; value > end; value += step)
                        {
                            result.Add(new NumberValue(value));
                        }
                    }

                    return new ArrayValue(result);
                });

            Register("rangeYear",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new NumberValue(1))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0] as DateTimeValue).Value() as DateTime?;
                    var end = (args[1] as DateTimeValue).Value() as DateTime?;
                    var stepDecimal = (args[2] as NumberValue).Value();
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "rangeYear function requires valid DateTime parameters",
                            context,
                            callSite);
                    }

                    if (step <= 0)
                    {
                        throw new TemplateEvaluationException(
                            "rangeYear function requires a positive step value",
                            context,
                            callSite);
                    }

                    if (start >= end)
                    {
                        return new ArrayValue(new List<DateTimeValue>());
                    }

                    var result = new List<DateTimeValue>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(new DateTimeValue(new DateTime(current.Ticks)));
                        current = current.AddYears(step);
                    }

                    return new ArrayValue(result);
                });

            Register("rangeMonth",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new NumberValue(1))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0] as DateTimeValue).Value() as DateTime?;
                    var end = (args[1] as DateTimeValue).Value() as DateTime?;
                    var stepDecimal = (args[2] as NumberValue).Value();
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "rangeMonth function requires valid DateTime parameters",
                            context,
                            callSite);
                    }

                    if (step <= 0)
                    {
                        throw new TemplateEvaluationException(
                            "rangeMonth function requires a positive step value",
                            context,
                            callSite);
                    }

                    if (start >= end)
                    {
                        return new ArrayValue(new List<DateTimeValue>());
                    }

                    var result = new List<DateTimeValue>();
                    var current = start.Value;
                    var originalDay = current.Day;

                    while (current < end)
                    {
                        result.Add(new DateTimeValue(new DateTime(current.Ticks)));

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

                    return new ArrayValue(result);
                });

            Register("rangeDay",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new NumberValue(1))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0] as DateTimeValue).Value() as DateTime?;
                    var end = (args[1] as DateTimeValue).Value() as DateTime?;
                    var stepDecimal = (args[2] as NumberValue).Value();
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "rangeDay function requires valid DateTime parameters",
                            context,
                            callSite);
                    }

                    if (step <= 0)
                    {
                        throw new TemplateEvaluationException(
                            "rangeDay function requires a positive step value",
                            context,
                            callSite);
                    }

                    if (start >= end)
                    {
                        return new ArrayValue(new List<DateTimeValue>());
                    }

                    var result = new List<DateTimeValue>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(new DateTimeValue(new DateTime(current.Ticks)));
                        current = current.AddDays(step);
                    }

                    return new ArrayValue(result);
                });

            Register("rangeHour",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new NumberValue(1))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0] as DateTimeValue).Value() as DateTime?;
                    var end = (args[1] as DateTimeValue).Value() as DateTime?;
                    var stepDecimal = (args[2] as NumberValue).Value();
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "rangeHour function requires valid DateTime parameters",
                            context,
                            callSite);
                    }

                    if (step <= 0)
                    {
                        throw new TemplateEvaluationException(
                            "rangeHour function requires a positive step value",
                            context,
                            callSite);
                    }

                    if (start >= end)
                    {
                        return new ArrayValue(new List<DateTimeValue>());
                    }

                    var result = new List<DateTimeValue>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(new DateTimeValue(new DateTime(current.Ticks)));
                        current = current.AddHours(step);
                    }

                    return new ArrayValue(result);
                });

            Register("rangeMinute",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new NumberValue(1))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0] as DateTimeValue).Value() as DateTime?;
                    var end = (args[1] as DateTimeValue).Value() as DateTime?;
                    var stepDecimal = (args[2] as NumberValue).Value();
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "rangeMinute function requires valid DateTime parameters",
                            context,
                            callSite);
                    }

                    if (step <= 0)
                    {
                        throw new TemplateEvaluationException(
                            "rangeMinute function requires a positive step value",
                            context,
                            callSite);
                    }

                    if (start >= end)
                    {
                        return new ArrayValue(new List<DateTimeValue>());
                    }

                    var result = new List<DateTimeValue>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(new DateTimeValue(new DateTime(current.Ticks)));
                        current = current.AddMinutes(step);
                    }

                    return new ArrayValue(result);
                });

            Register("rangeSecond",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new NumberValue(1))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0] as DateTimeValue).Value() as DateTime?;
                    var end = (args[1] as DateTimeValue).Value() as DateTime?;
                    var stepDecimal = (args[2] as NumberValue).Value();
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "rangeSecond function requires valid DateTime parameters",
                            context,
                            callSite);
                    }

                    if (step <= 0)
                    {
                        throw new TemplateEvaluationException(
                            "rangeSecond function requires a positive step value",
                            context,
                            callSite);
                    }

                    if (start >= end)
                    {
                        return new ArrayValue(new List<DateTimeValue>());
                    }

                    var result = new List<DateTimeValue>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(new DateTimeValue(new DateTime(current.Ticks)));
                        current = current.AddSeconds(step);
                    }

                    return new ArrayValue(result);
                });

            Register("filter",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(LambdaValue))
                },
                (context, callSite, args) =>
                {
                    var collection = (args[0] as ArrayValue).Value();
                    var predicate = (args[1] as LambdaValue).Value();

                    if (collection == null || predicate == null)
                    {
                        throw new TemplateEvaluationException(
                            "filter function requires an array and a lambda function",
                            context,
                            callSite);
                    }

                    var result = new List<Value>();
                    foreach (var item in collection)
                    {
                        var predicateResult = predicate(context, callSite, new List<Value> { item });
                        if (predicateResult is BooleanValue boolResult)
                        {
                            if (boolResult.Value())
                            {
                                result.Add(item);
                            }
                        }
                        else
                        {
                            throw new TemplateEvaluationException(
                                $"Filter predicate should evaluate to a boolean value but found {predicateResult.GetType()}",
                                context,
                                callSite);
                        }
                    }

                    return new ArrayValue(result);
                });

            Register("at",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    var index = Convert.ToInt32((args[1] as NumberValue).Value());

                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "at function requires an array as first argument",
                            context,
                            callSite);
                    }

                    var list = array.Cast<Value>().ToList();
                    if (index < 0 || index >= list.Count)
                    {
                        throw new TemplateEvaluationException(
                            $"Index {index} is out of bounds for array of length {list.Count}",
                            context,
                            callSite);
                    }

                    return list[index];
                });

            Register("first",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "first function requires an array argument",
                            context,
                            callSite);
                    }

                    var list = array.Cast<Value>().ToList();
                    if (list.Count == 0)
                    {
                        throw new TemplateEvaluationException(
                            "Cannot get first element of empty array",
                            context,
                            callSite);
                    }

                    return list[0];
                });

            Register("rest",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "rest function requires an array argument",
                            context,
                            callSite);
                    }

                    var list = array.Cast<Value>().ToList();
                    if (list.Count == 0)
                    {
                        return new ArrayValue(new List<Value>());
                    }

                    return new ArrayValue(list.Skip(1));
                });

            Register("last",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "last function requires an array argument",
                            context,
                            callSite);
                    }

                    var list = array.Cast<Value>().ToList();
                    if (list.Count == 0)
                    {
                        throw new TemplateEvaluationException(
                            "Cannot get last element of empty array",
                            context,
                            callSite);
                    }

                    return list[list.Count - 1];
                });

            Register("any",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "any function requires an array argument",
                            context,
                            callSite);
                    }

                    return new BooleanValue(array.Any());
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

                    var conditionResult = condition.Evaluate();
                    try
                    {
                        conditionResult.ExpectType(ValueType.Boolean, context);
                    }
                    catch (InnerEvaluationException ex)
                    {
                        throw new TemplateEvaluationException(ex.Message, context, callSite);
                    }

                    return (conditionResult as BooleanValue).Value() ?
                        trueBranch.Evaluate() :
                        falseBranch.Evaluate();
                },
                isLazilyEvaluated: true);

            Register("join",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    var delimiter = (args[1] as StringValue).Value();

                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "join function requires an array as first argument",
                            context,
                            callSite);
                    }
                    if (delimiter == null)
                    {
                        throw new TemplateEvaluationException(
                            "join function requires a string as second argument",
                            context,
                            callSite);
                    }

                    // TODO: reimplement tostring for all value types and remove FormatOutput
                    return new StringValue(string.Join(delimiter, array.Select(x => TypeHelper.FormatOutput(x.Unbox() ?? ""))));
                });

            Register("explode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value();
                    var delimiter = (args[1] as StringValue).Value();

                    if (str == null)
                    {
                        throw new TemplateEvaluationException(
                            "explode function requires a string as first argument",
                            context,
                            callSite);
                    }
                    if (delimiter == null)
                    {
                        throw new TemplateEvaluationException(
                            "explode function requires a string as second argument",
                            context,
                            callSite);
                    }

                    return new ArrayValue(str.Split(new[] { delimiter }, StringSplitOptions.None).Select(s => new StringValue(s)).ToList());
                });

            Register("map",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(LambdaValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    var mapper = (args[1] as LambdaValue).Value();

                    if (array == null || mapper == null)
                    {
                        throw new TemplateEvaluationException(
                            "map function requires an array and a function",
                            context,
                            callSite);
                    }

                    return new ArrayValue(array.Select(item => mapper(context, callSite, new List<Value> { item })).ToList());
                });

            Register("reduce",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(LambdaValue)),
                    new ParameterDefinition(typeof(Value))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    var reducer = (args[1] as LambdaValue).Value();
                    var initialValue = args[2];

                    if (array == null || reducer == null)
                    {
                        throw new TemplateEvaluationException(
                            "reduce function requires an array and a function",
                            context,
                            callSite);
                    }

                    return array.Aggregate(initialValue, (acc, curr) =>
                            reducer(context, callSite, new List<Value> { acc, curr }));
                });

            Register("take",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    int count = Convert.ToInt32((args[1] as NumberValue).Value());

                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "take function requires an array as first argument",
                            context,
                            callSite);
                    }

                    if (count <= 0)
                    {
                        return new ArrayValue(new List<Value>());
                    }

                    return new ArrayValue(array.Take(count));
                });

            Register("skip",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    int count = Convert.ToInt32((args[1] as NumberValue).Value());

                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "skip function requires an array as first argument",
                            context,
                            callSite);
                    }

                    return new ArrayValue(array.Skip(count));
                });

            Register("order",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "order function requires an array argument",
                            context,
                            callSite);
                    }

                    return new ArrayValue(array.OrderBy(x => x.Unbox()).ToList());
                });

            Register("order",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(BooleanValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    var ascending = (args[1] as BooleanValue).Value();

                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "order function requires an array as first argument",
                            context,
                            callSite);
                    }

                    return new ArrayValue(ascending ?
                        array.OrderBy(x => x.Unbox()).ToList() :
                        array.OrderByDescending(x => x.Unbox()).ToList());
                });

            Register("order",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(LambdaValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    var comparer = (args[1] as LambdaValue).Value();

                    if (array == null || comparer == null)
                    {
                        throw new TemplateEvaluationException(
                            "order function requires an array and a comparison function",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new ArrayValue(array.OrderBy(x => x, new ValueComparer(context, callSite, comparer)).ToList());
                    }
                    catch (InnerEvaluationException ex)
                    {
                        throw new TemplateEvaluationException(ex.Message, context, callSite);
                    }
                });

            Register("group",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0] as ArrayValue).Value();
                    var fieldName = (args[1] as StringValue).Value();

                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "group function requires an array as first argument",
                            context,
                            callSite);
                    }
                    if (string.IsNullOrEmpty(fieldName))
                    {
                        throw new TemplateEvaluationException(
                            "group function requires a non-empty string as second argument",
                            context,
                            callSite);
                    }

                    var result = new Dictionary<string, Value>();

                    foreach (var item in array)
                    {
                        // Skip null items
                        if (item == null) continue;

                        // Get the group key from the item
                        string key;
                        if (item.Unbox() is IDictionary<string, Value> dict)
                        {
                            if (!dict.ContainsKey(fieldName))
                            {
                                throw new TemplateEvaluationException(
                                    $"Object does not contain field '{fieldName}'",
                                    context,
                                    callSite);
                            }
                            else
                            {
                                var value = dict[fieldName];
                                if (value is StringValue str)
                                {
                                    key = str.Value();
                                }
                                else
                                {
                                    throw new TemplateEvaluationException(
                                        $"Cannot group by value of type '{value.GetType()}'",
                                        context,
                                        callSite);
                                }
                            }
                        }
                        else
                        {
                            throw new TemplateEvaluationException(
                                $"group function requires an array of objects to group",
                                context,
                                callSite);
                        }

                        if (key == null)
                        {
                            throw new TemplateEvaluationException(
                                $"Field '{fieldName}' must be present",
                                context,
                                callSite);
                        }

                        // Add item to the appropriate group
                        if (!result.ContainsKey(key))
                        {
                            result[key] = new ArrayValue(new List<Value>());
                        }
                        result[key].Unbox().Add(item);
                    }

                    return new ObjectValue(result);
                });

            Register("get",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ObjectValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var obj = (args[0] as ObjectValue).Value();
                    var fieldName = (args[1] as StringValue).Value();

                    if (obj == null)
                    {
                        throw new TemplateEvaluationException(
                            "get function requires an object as first argument",
                            context,
                            callSite);
                    }
                    if (string.IsNullOrEmpty(fieldName))
                    {
                        throw new TemplateEvaluationException(
                            "get function requires a non-empty string as second argument",
                            context,
                            callSite);
                    }
                    if (!obj.ContainsKey(fieldName))
                    {
                        throw new TemplateEvaluationException(
                            $"Object does not contain field '{fieldName}'",
                            context,
                            callSite);
                    }

                    return obj[fieldName];
                });

            Register("keys",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ObjectValue))
                },
                (context, callSite, args) =>
                {
                    var obj = (args[0] as ObjectValue).Value();
                    if (obj == null)
                    {
                        throw new TemplateEvaluationException(
                            "keys function requires an object argument",
                            context,
                            callSite);
                    }

                    return new ArrayValue(obj.Keys.Select(key => new StringValue(key)).ToList());
                });

            Register("mod",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var number1 = Convert.ToInt32((args[0] as NumberValue).Value());
                    var number2 = Convert.ToInt32((args[1] as NumberValue).Value());

                    if (number2 == 0)
                    {
                        throw new TemplateEvaluationException(
                            "Cannot perform modulo with zero as divisor",
                            context,
                            callSite);
                    }

                    return new NumberValue(number1 % number2);
                });

            Register("floor",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var number = (args[0] as NumberValue).Value();
                    return new NumberValue(Math.Floor(number));
                });

            Register("ceil",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var number = (args[0] as NumberValue).Value();
                    return new NumberValue(Math.Ceiling(number));
                });

            Register("round",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var number = (args[0] as NumberValue).Value();
                    return new NumberValue(Math.Round(number, 0));
                });

            Register("round",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var number = (args[0] as NumberValue).Value();
                    var decimals = Convert.ToInt32((args[1] as NumberValue).Value());

                    if (decimals < 0)
                    {
                        throw new TemplateEvaluationException(
                            "Number of decimal places cannot be negative",
                            context,
                            callSite);
                    }

                    return new NumberValue(Math.Round(number, decimals));
                });

            Register("string",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var number = (args[0] as NumberValue).Value();
                    return new StringValue(number.ToString());
                });

            Register("string",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(BooleanValue))
                },
                (context, callSite, args) =>
                {
                    var boolean = (args[0] as BooleanValue).Value();
                    return new StringValue(boolean.ToString().ToLower()); // returning "true" or "false" in lowercase
                });

            Register("number",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value();

                    if (string.IsNullOrEmpty(str))
                    {
                        throw new TemplateEvaluationException(
                            "Cannot convert empty or null string to number",
                            context,
                            callSite);
                    }

                    if (!decimal.TryParse(str, out decimal result))
                    {
                        throw new TemplateEvaluationException(
                            $"Cannot convert string '{str}' to number",
                            context,
                            callSite);
                    }

                    return new NumberValue(result);
                });

            Register("numeric",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0] as StringValue).Value();

                    if (string.IsNullOrEmpty(str))
                    {
                        return new BooleanValue(false);
                    }

                    return new BooleanValue(decimal.TryParse(str, out _));
                });

            Register("datetime",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var dateStr = (args[0] as StringValue).Value();
                    if (string.IsNullOrEmpty(dateStr))
                    {
                        throw new TemplateEvaluationException(
                            "datetime function requires a non-empty string argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new DateTimeValue(DateTime.Parse(dateStr));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to parse date string '{dateStr}': {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("format",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0] as DateTimeValue).Value() as DateTime?;
                    var format = (args[1] as StringValue).Value();

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "format function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    if (string.IsNullOrEmpty(format))
                    {
                        throw new TemplateEvaluationException(
                            "format function requires a non-empty format string as second argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new StringValue(date.Value.ToString(format));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to format date with format string '{format}': {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("addYears",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0] as DateTimeValue).Value() as DateTime?;
                    var years = Convert.ToInt32((args[1] as NumberValue).Value());

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "addYears function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new DateTimeValue(date.Value.AddYears(years));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to add {years} years to date: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("addMonths",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0] as DateTimeValue).Value() as DateTime?;
                    var months = Convert.ToInt32((args[1] as NumberValue).Value());

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "addMonths function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new DateTimeValue(date.Value.AddMonths(months));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to add {months} months to date: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("addDays",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0] as DateTimeValue).Value() as DateTime?;
                    var days = Convert.ToInt32((args[1] as NumberValue).Value());

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "addDays function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new DateTimeValue(date.Value.AddDays(days));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to add {days} days to date: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("addHours",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0] as DateTimeValue).Value() as DateTime?;
                    var hours = Convert.ToInt32((args[1] as NumberValue).Value());

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "addHours function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new DateTimeValue(date.Value.AddHours(hours));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to add {hours} hours to date: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("addMinutes",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0] as DateTimeValue).Value() as DateTime?;
                    var minutes = Convert.ToInt32((args[1] as NumberValue).Value());

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "addMinutes function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new DateTimeValue(date.Value.AddMinutes(minutes));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to add {minutes} minutes to date: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("addSeconds",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0] as DateTimeValue).Value() as DateTime?;
                    var seconds = Convert.ToInt32((args[1] as NumberValue).Value());

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "addSeconds function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new DateTimeValue(date.Value.AddSeconds(seconds));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to add {seconds} seconds to date: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("now",
                new List<ParameterDefinition>(),
                (context, callSite, args) =>
                {
                    return new DateTimeValue(DateTime.Now);
                });

            Register("utcNow",
                new List<ParameterDefinition>(),
                (context, callSite, args) =>
                {
                    return new DateTimeValue(DateTime.UtcNow);
                });

            Register("uri",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var uriString = (args[0] as StringValue).Value();
                    if (string.IsNullOrEmpty(uriString))
                    {
                        throw new TemplateEvaluationException(
                            "uri function requires a non-empty string argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        var uri = new Uri(uriString);
                        var dict = new Dictionary<string, Value>();
                        dict["AbsolutePath"] = new StringValue(uri.AbsolutePath);
                        dict["AbsoluteUri"] = new StringValue(uri.AbsoluteUri);
                        dict["DnsSafeHost"] = new StringValue(uri.DnsSafeHost);
                        dict["Fragment"] = new StringValue(uri.Fragment);
                        dict["Host"] = new StringValue(uri.Host);
                        dict["HostNameType"] = new StringValue(uri.HostNameType.ToString());
                        dict["IdnHost"] = new StringValue(uri.IdnHost);
                        dict["IsAbsoluteUri"] = new BooleanValue(uri.IsAbsoluteUri);
                        dict["IsDefaultPort"] = new BooleanValue(uri.IsDefaultPort);
                        dict["IsFile"] = new BooleanValue(uri.IsFile);
                        dict["IsLoopback"] = new BooleanValue(uri.IsLoopback);
                        dict["IsUnc"] = new BooleanValue(uri.IsUnc);
                        dict["LocalPath"] = new StringValue(uri.LocalPath);
                        dict["OriginalString"] = new StringValue(uri.OriginalString);
                        dict["PathAndQuery"] = new StringValue(uri.PathAndQuery);
                        dict["Port"] = new NumberValue(uri.Port);
                        dict["Query"] = new StringValue(uri.Query);
                        dict["Scheme"] = new StringValue(uri.Scheme);
                        dict["Segments"] = new ArrayValue(uri.Segments.Select(s => new StringValue(s)).ToList());
                        dict["UserEscaped"] = new BooleanValue(uri.UserEscaped);
                        dict["UserInfo"] = new StringValue(uri.UserInfo);
                        return new ObjectValue(dict);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to parse uri string '{uriString}': {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("htmlEncode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var html = (args[0] as StringValue).Value();

                    try
                    {
                        return new StringValue(WebUtility.HtmlEncode(html));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to encode html: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("htmlDecode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var html = (args[0] as StringValue).Value();

                    try
                    {
                        return new StringValue(WebUtility.HtmlDecode(html));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to decode html: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("urlEncode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var url = (args[0] as StringValue).Value();

                    try
                    {
                        return new StringValue(WebUtility.UrlEncode(url));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to encode url: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("urlDecode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var url = (args[0] as StringValue).Value();

                    try
                    {
                        return new StringValue(WebUtility.UrlDecode(url));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to decode url: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("fromJson",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var jsonString = (args[0] as StringValue).Value();
                    if (string.IsNullOrEmpty(jsonString))
                    {
                        throw new TemplateEvaluationException("fromJson function requires a non-empty string argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        var parsed = ParseToObject(jsonString);
                        if (parsed == null)
                        {
                            throw new TemplateEvaluationException(
                                $"Failed to deserialize object to JSON: Object must have a value",
                                context,
                                callSite);
                        }
                        return parsed;
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to parse JSON string: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("toJson",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(Value)),
                    new ParameterDefinition(typeof(BooleanValue), true, new BooleanValue(false))
                },
                (context, callSite, args) =>
                {
                    var obj = args[0];
                    var formatted = (args[1] as BooleanValue).Value();

                    if (obj == null)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to serialize object to JSON: Must have a value to serialize",
                            context,
                            callSite);
                    }

                    try
                    {
                        var json = TypeHelper.JsonSerialize(obj);

                        if (formatted)
                        {
                            return new StringValue(FormatJson(json));
                        }

                        return new StringValue(json);
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to serialize object to JSON: {ex.Message}",
                            context,
                            callSite);
                    }
                });
        }

        private Value ParseToObject(string jsonString)
        {
            var serializer = new JavaScriptSerializer();
            var deserializedObject = serializer.Deserialize<object>(jsonString);
            return ConvertToDictionary(deserializedObject) as Value;
        }

        private Value ConvertToDictionary(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            // Handle dictionary (objects in JSON)
            if (obj is Dictionary<string, object> dict)
            {
                var newDict = new Dictionary<string, Value>();
                foreach (var kvp in dict)
                {
                    var convertedValue = ConvertToDictionary(kvp.Value);
                    if (convertedValue != null)  // Skip null values
                    {
                        newDict[kvp.Key] = convertedValue;
                    }
                }
                return new ObjectValue(newDict);
            }

            // Handle array
            if (obj is System.Collections.ArrayList arrayList)
            {
                return new ArrayValue(arrayList.Cast<object>()
                               .Select(item => ConvertToDictionary(item))
                               .Where(item => item != null)
                               .ToList());  // Filter out null values
            }

            if (obj is object[] array)
            {
                return new ArrayValue(array.Cast<object>()
                               .Select(item => ConvertToDictionary(item))
                               .Where(item => item != null)
                               .ToList());  // Filter out null values
            }

            // Handle numbers - convert to decimal where possible
            if (TypeHelper.IsConvertibleToDecimal(obj))
            {
                return new NumberValue(Convert.ToDecimal(obj));
            }

            if (obj is string str)
            {
                return new StringValue(str);
            }

            if (obj is bool boolean)
            {
                return new BooleanValue(boolean);
            }

            if (obj is DateTime dateTime)
            {
                return new DateTimeValue(dateTime);
            }

            // Return other primitives as-is (string, bool)
            throw new Exception($"Unable to convert value to known datatype: {obj}");
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
            Func<ExecutionContext, AstNode, List<Value>, Value> implementation,
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
            List<Value> arguments,
            out FunctionDefinition matchingFunction,
            out List<Value> effectiveArguments)
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
                throw new InnerEvaluationException(
                    $"Ambiguous function call to '{name}'. Multiple overloads match the provided arguments.");
            }

            var bestMatch = scoredOverloads.First();
            matchingFunction = bestMatch.Function;
            effectiveArguments = bestMatch.EffectiveArgs;
            return true;
        }

        private List<Value> CreateEffectiveArguments(
            List<ParameterDefinition> parameters,
            List<Value> providedArgs)
        {
            var effectiveArgs = new List<Value>();

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
                    throw new InnerEvaluationException("Function missing required argument");
                }
            }

            return effectiveArgs;
        }

        private int ScoreTypeMatch(List<ParameterDefinition> parameters, List<Value> arguments)
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

        public void ValidateArguments(FunctionDefinition function, List<Value> arguments)
        {
            if (arguments.Count != function.Parameters.Count)
            {
                throw new InnerEvaluationException(
                    $"Function '{function.Name}' expects {function.Parameters.Count} arguments, but got {arguments.Count}");
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
                        throw new InnerEvaluationException($"Argument {i + 1} of function '{function.Name}' cannot be null");
                    }
                    continue;
                }

                var argumentType = argument.GetType();

                // Special handling for IEnumerable parameter type
                if (parameter.Type == typeof(ArrayValue))
                {
                    if (!(argument is ArrayValue))
                    {
                        throw new InnerEvaluationException($"Argument {i + 1} of function '{function.Name}' must be an array");
                    }
                    continue;
                }

                // Check if the argument can be converted to the expected type
                if (!parameter.Type.IsAssignableFrom(argumentType))
                {
                    throw new InnerEvaluationException(
                        $"Argument {i + 1} of function '{function.Name}' must be of type {parameter.Type.Name}");
                }
            }
        }
    }

    public class TypeHelper
    {
        public static bool UnboxBoolean(Value value, ExecutionContext context, AstNode astNode)
        {
            if (value is BooleanValue booleanValue)
            {
                return booleanValue.Value();
            }
            else
            {
                throw new TemplateEvaluationException(
                    $"Expected value of type boolean but found {value.GetType()}",
                    context,
                    astNode);
            }
        }

        public static decimal UnboxNumber(Value value, ExecutionContext context, AstNode astNode)
        {
            if (value is NumberValue booleanValue)
            {
                return booleanValue.Value();
            }
            else
            {
                throw new TemplateEvaluationException(
                    $"Expected value of type Number but found {value.GetType()}",
                    context,
                    astNode);
            }
        }

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
            else if ((evaluated is string || evaluated is char) && serializing)
            {
                return $"\"{evaluated.ToString()}\"";
            }
            else if (evaluated is string || evaluated is char)
            {
                return evaluated.ToString();
            }
            else if (evaluated is ValueType valueType)
            {
                return $"type<{valueType.ToString()}>";
            }
            else if (evaluated is IEnumerable<Value>)
            {
                return FormatArrayOutput(evaluated);
            }
            else if (evaluated is IDictionary<string, Value> dict)
            {
                return string.Concat("{",
                    string.Join(", ", dict.Keys.Select(key => string.Concat(key, ": ", FormatOutput(dict[key].Unbox(), true)))), "}");
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

        public static string FormatArrayOutput(IEnumerable<Value> array)
        {
            return string.Concat("[", string.Join(", ", array.Select(item => FormatOutput(item.Unbox(), true))), "]");
        }

        public static string JsonSerialize(Value value)
        {
            if (value is NumberValue numberValue)
            {
                return numberValue.Value().ToString();
            }
            else if (value is DateTimeValue dateTimeValue)
            {
                return $"\"{dateTimeValue.Value().ToString("o")}\""; // ISO 8601 format
            }
            else if (value is BooleanValue booleanValue)
            {
                return booleanValue.Value() ? "true" : "false";
            }
            else if (value is LambdaValue lambdaValue)
            {
                return $"\"func<lambda({string.Join(", ", lambdaValue.ParameterNames)})>\"";
            }
            else if (value is FunctionReferenceValue funcRefValue)
            {
                return $"\"func<{funcRefValue.Name}>\"";
            }
            else if (value is LazyValue)
            {
                return "\"value<lazy>\"";
            }
            else if (value is TypeValue typeValue)
            {
                return $"\"type<{typeValue.Value().ToString()}>\"";
            }
            else if (value is ObjectValue obj)
            {
                var dict = obj.Value();
                return string.Concat("{",
                    string.Join(",", dict.Keys.Select(key => string.Concat("\"", key, "\"", ":", JsonSerialize(dict[key])))), "}");
            }
            else if (value is ArrayValue arrayValue)
            {
                return string.Concat("[", string.Join(",", arrayValue.Value().Select(item => JsonSerialize(item))), "]");
            }
            else if (value is StringValue stringValue)
            {
                return new JavaScriptSerializer().Serialize(stringValue.Value());
            }
            else
            {
                return $"\"{value.Unbox().ToString()}\"";
            }
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

    public class ValueComparer : IComparer<Value>
    {
        private readonly Func<ExecutionContext, AstNode, List<Value>, Value> _comparer;
        private readonly ExecutionContext _context;
        private readonly AstNode _callSite;

        public ValueComparer(ExecutionContext context, AstNode callSite, Func<ExecutionContext, AstNode, List<Value>, Value> comparer)
        {
            _comparer = comparer;
            _context = context;
            _callSite = callSite;
        }

        public int Compare(Value x, Value y)
        {
            var comparison = _comparer(_context, _callSite, new List<Value> { x, y });
            if (comparison is NumberValue diff)
            {
                return Math.Sign(diff.Value());
            }
            else
            {
                throw new InnerEvaluationException(
                    $"Expected value of type number but found {comparison.GetType()}");
            }
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
        ArrayValue RetrieveMultiple(string fetchXml);
    }

    public class DataverseService : IDataverseService
    {
        private readonly IOrganizationService _organizationService;

        public DataverseService(IOrganizationService organizationService)
        {
            _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
        }

        public ArrayValue RetrieveMultiple(string fetchXml)
        {
            var fetch = new FetchExpression(fetchXml);
            var results = _organizationService.RetrieveMultiple(fetch);
            return ConvertToObjects(results);
        }

        private ArrayValue ConvertToObjects(EntityCollection entityCollection)
        {
            var dicts = new List<ObjectValue>();

            foreach (var entity in entityCollection.Entities)
            {
                var dict = new Dictionary<string, Value>();

                foreach (var attribute in entity.Attributes)
                {
                    // Skip null values
                    if (attribute.Value != null)
                    {
                        // Handle special types like EntityReference, OptionSetValue, etc.
                        var value = ConvertAttributeValue(attribute.Value);
                        if (value != null)
                        {
                            dict[attribute.Key] = value;
                        }
                    }
                }

                dicts.Add(new ObjectValue(dict));
            }

            return new ArrayValue(dicts);
        }

        private Value ConvertAttributeValue(object attributeValue)
        {
            if (attributeValue == null) return null;

            switch (attributeValue)
            {
                case EntityReference entityRef:
                    return new StringValue(entityRef.Id.ToString());

                case Guid guid:
                    return new StringValue(guid.ToString());

                case OptionSetValue optionSet:
                    return new NumberValue(Convert.ToDecimal(optionSet.Value));

                case Money money:
                    return new NumberValue(money.Value);

                case AliasedValue aliased:
                    return ConvertAttributeValue(aliased.Value);

                case string str:
                    return new StringValue(str);

                case bool boolean:
                    return new BooleanValue(boolean);

                case DateTime dateTime:
                    return new DateTimeValue(dateTime);

                default:
                    if (TypeHelper.IsConvertibleToDecimal(attributeValue))
                    {
                        return new NumberValue(Convert.ToDecimal(attributeValue));
                    }

                    throw new Exception($"Unable to convert value to known datatype: {attributeValue}");
            }
        }
    }
}
