# Template Processing Architecture

## Overview

The template processing system implements a text-based transformation engine that preserves literal text while interpreting specialized directives. All content outside of explicitly defined directives is emitted verbatim to the output stream.

## Template Structure

### Text Processing

The interpreter processes a template as a stream of characters. Any text encountered outside of directive delimiters is preserved exactly as written in the output, maintaining all whitespace, line endings, and special characters.

Example:
```
This text will appear exactly as written
in the output, preserving line breaks
    and indentation.
```

### Directive Syntax

Directives are specialized processing instructions enclosed in double curly braces. The formal syntax for directives is:

```
{{ <directive-content> }}
```

Directives may contain expressions, control structures, or other processing instructions that the interpreter evaluates during template processing.

Example:
```
Text before
{{ 2 + 2 }}
Text after
```

Yields:
```
Text before
4
Text after
```

## Whitespace Control

The templating system provides explicit whitespace control through modified directive delimiters. These controls allow for precise management of whitespace and newlines around directive boundaries.

### Standard Behavior

By default, all whitespace around directives is preserved:

```
Line one
{{ expression }}
Line two
```

Yields:
```
Line one
result
Line two
```

### Left Whitespace Control

A minus sign (`-`) immediately after the opening delimiter removes preceding whitespace and newlines:

```
Line one
{{- expression }}
Line two
```

Yields:
```
Line oneresult
Line two
```

### Right Whitespace Control

A minus sign (`-`) immediately before the closing delimiter removes following whitespace and newlines:

```
Line one
{{ expression -}}
Line two
```

Yields:
```
Line oneresultLine two
```

### Bidirectional Whitespace Control

Minus signs can be used on both sides to remove whitespace in both directions:

```
Line one
{{- expression -}}
Line two
```

Yields:
```
Line oneresultLine two
```

### Partial Line Whitespace Control

When text appears on the same line as a directive with whitespace control, only the whitespace between the text and the directive is eliminated. Whitespace on the opposite side of the text is preserved.

#### Leading Text Example

```
Line one   {{- expression }}
Line two
```

Yields:
```
Line oneresult
Line two
```

Note that the whitespace before "Line one" is preserved, while the whitespace between "one" and the directive is eliminated.

#### Trailing Text Example

```
Line one
{{ expression -}}   remaining text
Line two
```

Yields:
```
Line one
resultremaining text
Line two
```

Note that the whitespace after "remaining text" is preserved, while the whitespace between the directive and "remaining text" is eliminated.

#### Combined Example

```
Start   {{- expression -}}   End
Next line
```

Yields:
```
StartresultEnd
Next line
```

Note that only the whitespace between "Start" and the directive, and between the directive and "End" is eliminated. The whitespace before "Start" and after "End" remains intact.

## Important Considerations

1. **Whitespace Preservation**:
   - All whitespace in literal text segments is significant
   - Whitespace within directives does not affect the output
   - Whitespace control markers affect only the whitespace adjacent to directive boundaries

2. **Line Endings**:
   - Line endings in literal text are preserved
   - Line endings affected by whitespace control are removed entirely
   - Line ending normalization is not performed

3. **Directive Boundaries**:
   - Directive delimiters (`{{` and `}}`) must not contain whitespace
   - Whitespace control markers (`-`) must be adjacent to delimiters
   - Nested directives are not permitted within a single directive block

## Template Inclusion

The system provides a template inclusion mechanism through the `#include` directive, enabling modular template composition. Included templates are processed in their inclusion context, allowing for template reuse and composition.

### Include Directive Syntax

```
{{ #include templateName }}
```

The `templateName` must be a valid identifier that has been registered with the template registry.

### Example

Primary template:
```
Header text
{{ #include commonSection }}
Footer text
```

Included template ("commonSection"):
```
This is common content
with multiple lines
```

Yields:
```
Header text
This is common content
with multiple lines
Footer text
```

### Important Considerations

1. **Scope**:
   - Included templates share the execution context of their inclusion point
   - Variables and control structures from the parent template are accessible

2. **Recursion**:
   - Template inclusion may be nested
   - Recursive inclusion should be avoided

3. **Resolution**:
   - Template names must be registered before use
   - Templates are resolved at processing time
   - Template resolution failures are treated as fatal errors