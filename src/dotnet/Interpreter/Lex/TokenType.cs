namespace PowwowLang.Lex
{
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
}
