# Powwow Grammar

Powwow is a templating language that mixes plain text with directives enclosed in double curly braces.

## 1. Basic Structure

```
Template ::= (Text | Directive | Comment | Whitespace | Newline)*
```

## 2. Directives and Whitespace Handling

```
Directive ::= DirectiveStart Expression DirectiveEnd
            | ControlDirective

DirectiveStart ::= "{{" Whitespace* | "{{-" Whitespace*  
DirectiveEnd ::= Whitespace* "}}" | Whitespace* "-}}"

Whitespace ::= " " | "\t" | other whitespace characters
```

The `-` in `{{-` or `-}}` indicates whitespace trimming, removing preceding or following whitespace.

## 3. Variable Declaration and Mutation

```
LetStatement ::= DirectiveStart "let" Variable "=" Expression DirectiveEnd
MutationStatement ::= DirectiveStart "mut" Variable ["." Field]* "=" Expression DirectiveEnd
```

Examples:
```
{{ let x = 42 }}
{{ mut user.name = "John" }}
```

## 4. Control Flow

### 4.1 Conditional Statements

```
IfStatement ::= DirectiveStart "if" Expression DirectiveEnd Template
                [DirectiveStart "elseif" Expression DirectiveEnd Template]*
                [DirectiveStart "else" DirectiveEnd Template]
                DirectiveStart "/if" DirectiveEnd
```

Example:
```
{{ if age >= 18 }}
  Adult content
{{ elseif age >= 13 }}
  Teen content
{{ else }}
  Children content
{{ /if }}
```

### 4.2 Loops

```
ForStatement ::= DirectiveStart "for" Variable "in" Expression DirectiveEnd 
                 Template 
                 DirectiveStart "/for" DirectiveEnd
```

Example:
```
{{ for item in items }}
  - {{ item.name }}: {{ item.price }}
{{ /for }}
```

## 5. Comments

```
Comment ::= DirectiveStart "*" [any characters except "*" followed by DirectiveEnd] "*" DirectiveEnd
```

Example:
```
{{* This is a comment and won't be rendered *}}
```

## 6. Capture and Literal Blocks

```
CaptureStatement ::= DirectiveStart "capture" Variable DirectiveEnd 
                     Template 
                     DirectiveStart "/capture" DirectiveEnd

LiteralStatement ::= DirectiveStart "literal" DirectiveEnd 
                     [any characters] 
                     DirectiveStart "/literal" DirectiveEnd
```

Examples:
```
{{ capture header }}
  <h1>Welcome, {{ user.name }}!</h1>
{{ /capture }}

{{ literal }}
  {{ This will be rendered as-is, not processed }}
{{ /literal }}
```

## 7. Include Statements

```
IncludeStatement ::= DirectiveStart "include" Variable DirectiveEnd
```

Example:
```
{{ include header }}
```

## 8. Expressions

```
Expression ::= OrExpression

OrExpression ::= AndExpression ["||" AndExpression]*
AndExpression ::= ComparisonExpression ["&&" ComparisonExpression]*
ComparisonExpression ::= AdditiveExpression [ComparisonOperator AdditiveExpression]*
AdditiveExpression ::= MultiplicativeExpression [("+"|"-") MultiplicativeExpression]*
MultiplicativeExpression ::= UnaryExpression [("*"|"/") UnaryExpression]*
UnaryExpression ::= ["!"] PrimaryExpression

ComparisonOperator ::= "==" | "!=" | "<" | "<=" | ">" | ">="
```

### 8.1 Primary Expressions

```
PrimaryExpression ::= LiteralExpression
                    | Variable
                    | FieldAccess
                    | FunctionCall
                    | GroupExpression
                    | ArrayExpression
                    | ObjectExpression
                    | LambdaExpression
                    | TypeLiteral

LiteralExpression ::= StringLiteral
                    | NumberLiteral
                    | BooleanLiteral

TypeLiteral ::= "String" | "Number" | "Boolean" | "Array" | "Object" | "Function" | "DateTime"

Variable ::= Identifier
FieldAccess ::= Expression "." Identifier
FunctionCall ::= (Variable | FieldAccess) "(" [ExpressionList] ")"
GroupExpression ::= "(" Expression ")"
```

### 8.2 Complex Expressions

```
ArrayExpression ::= "[" [ExpressionList] "]"
ObjectExpression ::= "obj(" [ObjectFieldList] ")"
LambdaExpression ::= "(" [ParameterList] ")" "=>" [StatementList] Expression

ExpressionList ::= Expression ["," Expression]*
ObjectFieldList ::= Identifier ":" Expression ["," Identifier ":" Expression]*
ParameterList ::= Identifier ["," Identifier]*
StatementList ::= (LetStatement | MutationStatement) ["," (LetStatement | MutationStatement)]* ","
```

## 9. Literal Expressions

```
StringLiteral ::= '"' [StringCharacter]* '"'
StringCharacter ::= any character except " and \ | '\' EscapeSequence
EscapeSequence ::= '"' | '\\' | 'n' | 'r' | 't'

NumberLiteral ::= ['-'] Digit+ ['.' Digit+]
Digit ::= '0' | '1' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9'

BooleanLiteral ::= 'true' | 'false'
```

### 9.1 StringLiteral

A string literal is enclosed in double quotes and can contain:
- Any character except unescaped double quotes or backslashes
- Escaped sequences for special characters:
  - `\"` for a double quote
  - `\\` for a backslash
  - `\n` for a newline
  - `\r` for a carriage return
  - `\t` for a tab

Examples:
```
"Hello, World!"
"Line 1\nLine 2"
"This has a \"quoted\" word"
```

### 9.2 NumberLiteral

A number literal can be:
- An optional minus sign (`-`) for negative numbers
- One or more digits
- Optionally followed by a decimal point and one or more digits for the fractional part

Examples:
```
42
-7
3.14
-0.5
```

### 9.3 BooleanLiteral

A boolean literal is either `true` or `false` (case-sensitive).

Examples:
```
true
false
```

All other grammar rules remain as previously defined.

## 10. Types

### 10.1 Type System

The language has a first-class type system with the following type literals:
- `String`: String type
- `Number`: Numeric type
- `Boolean`: Boolean type
- `Array`: Array type
- `Object`: Object type
- `Function`: Function type
- `DateTime`: Date and time type

Type literals can be used in expressions:
```
{{ let stringType = String }}
{{ let isStringType = typeof("hello") == String }}
```

## Examples

### Working with Types

```
{{ let str = "hello" }}
{{ let strType = typeof(str) }}
{{ if strType == String }}
  This is a string!
{{ /if }}

{{ let numType = Number }}
{{ let x = 42 }}
{{ let isNumType = typeof(x) == numType }}
```

### Variable Declaration and Expressions
```
{{ let name = "World" }}
{{ let greeting = concat("Hello, ", name) }}
{{ greeting }}
```

### Conditionals
```
{{ let score = 85 }}
{{ if score >= 90 }}
  A grade
{{ elseif score >= 80 }}
  B grade
{{ else }}
  Lower grade
{{ /if }}
```

### Loops and Arrays
```
{{ let fruits = ["Apple", "Banana", "Cherry"] }}
<ul>
{{ for fruit in fruits }}
  <li>{{ fruit }}</li>
{{ /for }}
</ul>
```

### Functions and Lambdas
```
{{ let numbers = [1, 2, 3, 4, 5] }}
{{ let doubled = map(numbers, (x) => x * 2) }}
{{ join(doubled, ", ") }}
```

### Objects
```
{{ let user = obj(name: "John", age: 30) }}
Name: {{ user.name }}, Age: {{ user.age }}
```