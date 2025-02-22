# Data Types Reference

## Overview

This document specifies the fundamental data types supported by the templating language.

## String Type

A string represents an immutable sequence of Unicode characters. String literals must be enclosed in double quotation marks (`"`). The implementation supports standard escape sequences for special characters.

### String Literal Syntax
```
"<character sequence>"
```

### Supported Escape Sequences
- `\"` - Double quote
- `\\` - Backslash
- `\n` - Line feed
- `\r` - Carriage return
- `\t` - Horizontal tab

### Examples
```
{{ "Example string literal" }}
{{ "Line termination demonstration\nSecond line" }}
{{ "String with \"quoted\" content" }}
```

## Boolean Type

The boolean type represents a logical value that can be either `true` or `false`. Boolean values are used in conditional expressions and logical operations.

### Logical Operators
- `&&` - Logical AND
- `||` - Logical OR
- `!`  - Logical NOT

### Examples
```
{{ true }}                 // Boolean literal
{{ false }}                // Boolean literal
{{ true && false }}        // Logical AND operation
{{ true || false }}        // Logical OR operation
{{ !true }}               // Logical NOT operation
```

## Number Type

The number type implements a decimal floating-point numeric representation. All numeric values are stored as decimals, providing exact decimal arithmetic where applicable.

### Numeric Literal Syntax
```
[±]<digits>[.<digits>]
```

### Arithmetic Operators
- `+` - Addition
- `-` - Subtraction
- `*` - Multiplication
- `/` - Division

### Examples
```
{{ 42 }}                   // Integer literal
{{ 3.14159 }}             // Decimal literal
{{ -273.15 }}             // Negative number
{{ 2.5e+3 }}              // Scientific notation
{{ (10 + 5) * 2 }}        // Arithmetic expression
```

## Object Type

An object type represents a collection of key-value pairs where each key is a unique identifier and each value can be any valid type. Objects are constructed using the `obj()` syntax.

### Object Literal Syntax
```
obj(
    <identifier>: <expression>[,
    <identifier>: <expression>]*
)
```

### Examples
```
{{ obj(
    identifier: "UUID-001",
    magnitude: 42.5,
    activated: true,
    metadata: obj(
        timestamp: "2024-02-22T10:30:00Z",
        version: "1.0.0"
    )
) }}
```

### Property Access
Properties are accessed using dot notation:
```
{{ entity.identifier }}
{{ entity.metadata.version }}
```

## Array Type

An array type represents an ordered sequence of values. Arrays may contain elements of heterogeneous types and support nested structures.

### Array Literal Syntax
```
[<expression>[, <expression>]*]
```

### Examples
```
{{ [1, 2, 3, 4, 5] }}     // Homogeneous numeric array
{{ ["alpha", "beta"] }}    // String array
{{ [                      // Heterogeneous array
    42,
    "text",
    true,
    obj(id: 1),
    [1, 2, 3]
] }}
```

### Array Iteration
Arrays support iteration through the `#for` directive:
```
{{ #for element in [1, 2, 3] }}
    {{ element }}
{{ /for }}
```

## Type Conversion Functions

The language provides a set of built-in functions for explicit type conversion:

### String Conversion
```
{{ string(42) }}          // Yields: "42"
{{ string(true) }}        // Yields: "true"
```

### Numeric Conversion
```
{{ number("42") }}        // Yields: 42
{{ numeric("42") }}       // Yields: true
```

## Type System Constraints

1. **Null Values**: The type system does not implement null values. All variables must be explicitly initialized with a value of their declared type.

2. **Type Safety**: Implicit type conversion is not supported; all type conversions must be explicit through the provided conversion functions.

3. **String Delimiters**: String literals must use double quotation marks (`"`). Single quotation marks are not supported.

4. **Identifier Rules**: Object property identifiers must conform to the following rules:
   - Must begin with a letter or underscore
   - May contain letters, numbers, and underscores
   - Are case-sensitive
   - Cannot be reserved keywords

All behavior not explicitly specified here should be considered undefined and should not be relied upon.