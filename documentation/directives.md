# Template Language Directives Reference

## Overview

This document specifies all available directives in the template language.

## Variable Behavior and Constraints

The template language implements immutable variables with strict initialization and naming rules.

### Variable Constraints

1. **Single Assignment**:
   - Variables may only be declared and initialized once
   - Reassignment is not permitted
   - Attempting to reassign a variable results in a runtime error

2. **Naming Requirements**:
   - Names must begin with a letter or underscore
   - Names may contain letters, numbers, and underscores
   - Names are case-sensitive
   - Reserved words may not be used as variable names

3. **Uniqueness**:
   - Variable names must be unique within their scope
   - Names cannot conflict with:
     - Other variables in the same scope
     - Function names
     - Iterator variables
     - Parent scope variables
     - Field names in the data context

### Examples of Variable Constraints

Valid variable declarations:
```
{{ #let userId = 42 }}
{{ userId }}
{{ #let _internalFlag = true }}
{{ _internalFlag }}
{{ #let computed_value = 2 + 2 }}
{{ computed_value }}
```

Yields:
```
42
true
4
```

Invalid variable declarations (will raise errors):
```
{{ #let user = "John" }}
{{ #let user = "Jane" }}  // Error: Variable 'user' already defined

{{ #let if = true }}      // Error: 'if' is a reserved word

{{ #for item in items }}
    {{ #let item = "x" }} // Error: 'item' conflicts with iterator
{{ /for }}
```
## Variable Assignment Directives

### Let Directive (#let)

The let directive creates immutable variables within the current scope.

#### Syntax
```
{{ #let variableName = expression }}
```

#### Assignment Rules
1. Expression is evaluated immediately
2. Value is immutable after assignment
3. Variable is accessible in current and nested scopes
4. Name must not conflict with any existing identifiers

#### Examples with Compound Expressions

Template:
```
{{ #let basePrice = 100 }}
{{ #let taxRate = 0.2 }}
{{ #let total = basePrice * (1 + taxRate) }}
Base Price: ${{ basePrice }}
Tax Rate: {{ taxRate * 100 }}%
Total: ${{ total }}

{{ #let userType = if(user.age >= 18, "adult", "minor") }}
User Type: {{ userType }}

{{ #let complexObj = obj(
    id: 123,
    values: [1, 2, 3],
    metadata: obj(
        created: now(),
        source: "system"
    )
) }}
Object ID: {{ complexObj.id }}
First Value: {{ complexObj.values[0] }}
Source: {{ complexObj.metadata.source }}
```

Given context:
```
{
    user: {
        age: 20
    }
}
```

Yields (example timestamp):
```
Base Price: $100
Tax Rate: 20%
Total: $120

User Type: adult

Object ID: 123
First Value: 1
Source: system
```

### Capture Directive (#capture)

The capture directive accumulates template output into an immutable variable.

#### Syntax
```
{{ #capture variableName }}
    content
{{ /capture }}
```

#### Capture Rules
1. Captured content may contain any valid template constructs
2. Variable name follows standard naming constraints
3. Captured content is processed before assignment
4. Nested captures must use unique variable names

#### Example with Mixed Content

Given context:
```
{
    user: {
        name: "John Smith",
        subscription: {
            active: true,
            expiryDate: "2025-12-31",
            benefits: [
                { name: "Premium Support" },
                { name: "Cloud Storage" },
                { name: "API Access" }
            ]
        }
    }
}
```

Template:
```
{{ #capture emailBody }}
    {{ #let recipient = user.name }}
    Dear {{ recipient }},

    {{ #if user.subscription.active }}
        Your subscription is active until {{ user.subscription.expiryDate }}
    {{ #else }}
        Your subscription has expired
    {{ /if }}

    {{ #for benefit in user.subscription.benefits }}
        - {{ benefit.name }}
    {{ /for }}

    Best regards,
    The Team
{{ /capture }}

{{ emailBody }}
```

Yields:
```
Dear John Smith,

Your subscription is active until 2025-12-31

    - Premium Support
    - Cloud Storage
    - API Access

Best regards,
The Team
```

## Control Flow Directives

### Conditional Directive (#if)

The conditional directive enables branching logic based on boolean expressions.

#### Syntax
```
{{ #if condition }}
    content
[{{ #elseif condition }}
    alternative content]*
[{{ #else }}
    default content]
{{ /if }}
```

Square brackets indicate optional elements. The asterisk indicates that the element may be repeated.

#### Evaluation Rules
1. Conditions are evaluated in order
2. First true condition's content is rendered
3. If no conditions are true and else is present, else content is rendered
4. If no conditions are true and no else is present, no content is rendered

#### Example with Complex Conditions

Given context:
```
{
    user: {
        testScore: 95,
        age: 20
    }
}
```

Template:
```
{{ #let score = user.testScore }}
{{ #let age = user.age }}

{{ #if score >= 90 && age >= 18 }}
    Outstanding adult performance
{{ #elseif score >= 90 }}
    Outstanding youth performance
{{ #elseif score >= 70 }}
    Satisfactory performance
{{ #else }}
    Performance needs improvement
{{ /if }}
```

Yields:
```
Outstanding adult performance
```

### Iteration Directive (#for)

The iteration directive implements collection traversal with controlled scope and variable creation.

#### Syntax
```
{{ #for iterator in collection }}
    content
{{ /for }}
```

#### Iterator Scope Rules
1. Iterator variable is only accessible within the loop body
2. Iterator variable name must not conflict with existing variables
3. Nested loops must use unique iterator names
4. Collection must evaluate to an iterable value

#### Example with Nested Iteration

Given context:
```
{
    categories: [
        {
            name: "Electronics",
            products: [
                {
                    name: "Laptop",
                    price: 999.99,
                    inStock: true,
                    quantity: 5
                },
                {
                    name: "Smartphone",
                    price: 499.99,
                    inStock: false
                }
            ]
        },
        {
            name: "Books",
            products: [
                {
                    name: "Programming Guide",
                    price: 29.99,
                    inStock: true,
                    quantity: 12
                }
            ]
        }
    ]
}
```

Template:
```
{{ #for category in categories }}
    Category: {{ category.name }}
    {{ #for product in category.products }}
        - {{ product.name }}: ${{ product.price }}
        {{ #if product.inStock }}
            (In Stock: {{ product.quantity }})
        {{ #else }}
            (Out of Stock)
        {{ /if }}
    {{ /for }}
{{ /for }}
```

Yields:
```
Category: Electronics
    - Laptop: $999.99
        (In Stock: 5)
    - Smartphone: $499.99
        (Out of Stock)
Category: Books
    - Programming Guide: $29.99
        (In Stock: 12)
```


## Template Composition Directives

### Include Directive (#include)

The include directive implements template composition through reference.

#### Syntax
```
{{ #include templateName }}
```

#### Include Processing Rules
1. Template name must be registered before use
2. Included templates inherit parent context
3. Circular inclusion should be avoided
4. Whitespace control applies to inclusion boundaries

#### Example with Context Inheritance

Template "header":
```
<header>
    <h1>{{ pageTitle }}</h1>
</header>
```

Template "layout":
```
<div class="layout">
    <div class="content">{{ content }}</div>
</div>
```

Template "footer":
```
<footer>
    <p>Page: {{ pageTitle }}</p>
</footer>
```

Main template:
```
{{ #let pageTitle = "Welcome" }}

{{ #include header }}

{{ #capture content }}
    Main page content
{{ /capture }}

{{ #include layout }}

{{ #include footer }}
```

Yields:
```
<header>
    <h1>Welcome</h1>
</header>

<div class="layout">
    <div class="header">
        <header>
            <h1>Welcome</h1>
        </header>
    </div>
    <div class="content">
        Main page content
    </div>
</div>

<footer>
    <p>Page: Welcome</p>
</footer>
```

### Literal Directive (#literal)

The literal directive preserves content without interpretation.

#### Syntax
```
{{ #literal }}
    content
{{ /literal }}
```

#### Literal Processing Rules
1. No directive interpretation occurs
2. Whitespace is preserved exactly
3. Content is emitted verbatim

#### Example Preserving Template Syntax

Template:
```
Here is an example of a template loop:
{{ #literal }}
    Example template code:
    {{ #for item in items }}
        - {{ item.name }}
    {{ /for }}
{{ /literal }}
```

Yields:
```
Here is an example of a template loop:
    Example template code:
    {{ #for item in items }}
        - {{ item.name }}
    {{ /for }}
```

Note that the template syntax within the literal block is preserved exactly as written, without interpretation.

## Comment Directive

Comments provide documentation without affecting output.

#### Syntax
```
{{ * comment content * }}
```

#### Comment Rules
1. Comments cannot be nested
2. Comments are removed from output
3. Multi-line comments are supported
4. Comments can contain any text including directive syntax

#### Example
```
{{ * 
    This section implements the user profile display.
    Variables required:
    - user.name
    - user.email
    - user.roles
* }}
{{ #if user.authenticated }}
    Welcome, {{ user.name }}
{{ /if }}
```

## Expression Directives

Expressions evaluate to values that can be rendered or used in other directives.

### Simple Expression

#### Syntax
```
{{ expression }}
```

#### Expression Components
- Variables
- Literals
- Operators
- Function calls
- Object/array access
- Object/array creation

#### Examples of Complex Expressions

Given context:
```
{
    basePrice: 100,
    shipping: 10,
    taxRate: 0.2,
    user: {
        addresses: [
            { zipCode: "12345" },
            { zipCode: "67890" }
        ]
    },
    shipping: {
        addressIndex: 1
    },
    order: {
        id: 42,
        date: "2025-02-22T10:30:00Z"
    }
}
```

Template:
```
Total: ${{ (basePrice + shipping) * (1 + taxRate) }}

Shipping to: {{ user.addresses[shipping.addressIndex].zipCode }}

Order Reference: {{ concat(
    string(order.id),
    "-",
    format(datetime(order.date), "YYYYMMDD")
) }}
```

Yields:
```
Total: $132

Shipping to: 67890

Order Reference: 42-20250222
```

## Important Considerations

1. **Directive Nesting**:
   - Control flow directives may be nested to any depth
   - Each directive must be properly closed in LIFO order
   - Variable scope follows nesting structure

2. **Scope Rules**:
   - Variables are accessible in their declaration scope and nested scopes
   - Inner scope variables cannot be accessed from outer scopes
   - Variables cannot be redeclared in nested scopes

3. **Error Handling**:
   - Syntax errors terminate processing
   - Runtime errors terminate processing
   - Variable conflicts terminate processing
   - Include resolution failures terminate processing

All behavior not explicitly specified here should be considered undefined and should not be relied upon.