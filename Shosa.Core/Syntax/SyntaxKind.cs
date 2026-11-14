namespace Shosa.Core.Syntax;

public enum SyntaxKind
{
    None,

    // Tokens
    EndOfFile,
    BadToken,
    Identifier,
    Number,
    String,
    OpenBrace,
    CloseBrace,
    Bar,
    Dot,
    Colon,
    At,
    Slash,
    Star,
    OpenParen,
    CloseParen,

    // Nodes
    CompilationUnit,
    Header,        // title "..." | tempo N | time N/N
    FigureDecl,    // figure NAME "label"? { property* }
    Property,      // NAME value*
    PhraseDecl,    // phrase NAME { figureBlock* }
    FigureBlock,   // FIGURE { track* }
    Track,         // CHANNEL { item* }
    BarLine,       // |
    Repeat,        // repeat N { item* }
    Event,         // target duration? annotation*
    Target,        // word (. word)*
    Duration,      // : N (/ N)?
    Annotation,    // @ word ( ( N ) )?
    FormDecl,      // form { formItem* }
    FormItem,      // NAME (* N)?
    Error,         // tokens the parser could not place
}
