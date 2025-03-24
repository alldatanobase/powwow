using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Types;
using System.Collections.Generic;
using System.Linq;

namespace PowwowLang.Ast
{
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
                _arguments.Select(arg => new Value(new LazyValue(arg, currentContext))).ToList() :
                _arguments.Select(arg => arg.Evaluate(currentContext)).ToList();

            // Now handle the callable based on its type
            if (callable.ValueOf() is LambdaValue lambdaFunc)
            {
                // Direct lambda invocation
                // Lambda establishes its own new child context
                return lambdaFunc.Value()(parentContext, this, args);
            }
            else if (callable.ValueOf() is FunctionReferenceValue functionInfo)
            {
                var registry = context.GetFunctionRegistry();

                // First check if this is a parameter that contains a function in any parent context
                if (context is LambdaExecutionContext lambdaContext &&
                    lambdaContext.TryGetParameterFromAnyContext(functionInfo.Name, out var paramValue))
                {
                    if (paramValue.ValueOf() is LambdaValue paramFunc)
                    {
                        // Lambda establishes its own new child context
                        return paramFunc.Value()(parentContext, this, args);
                    }
                    else if (paramValue.ValueOf() is FunctionReferenceValue paramFuncInfo)
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
                    if (variableValue.ValueOf() is LambdaValue variableFunc)
                    {
                        // Lambda establishes its own new child context
                        return variableFunc.Value()(parentContext, this, args);
                    }

                    if (variableValue.ValueOf() is FunctionReferenceValue variableFuncInfo)
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
            if (callable.ValueOf() is FunctionReferenceValue functionInfo)
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

        public override string Name()
        {
            return "<invocation>";
        }
    }
}
