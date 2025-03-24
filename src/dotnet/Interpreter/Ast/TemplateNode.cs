using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PowwowLang.Ast
{
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
                result.Append(child.Evaluate(context).Output());
            }
            return new Value(new StringValue(result.ToString()));
        }

        public override string Name()
        {
            return "<template>";
        }

        public override string ToStackString()
        {
            return $"<template{(!string.IsNullOrEmpty(Location.Source) ? $" {Location.Source}" : "")}>";
        }

        public override string ToString()
        {
            var childrenStr = string.Join(", ", _children.Select(child => child.ToString()));
            return $"TemplateNode({(!string.IsNullOrEmpty(Location.Source) ? $"source={Location.Source}" : "")}, children=[{childrenStr}])";
        }
    }
}
