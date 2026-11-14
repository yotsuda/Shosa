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
    Amp,
    Bang,

    // Nodes
    CompilationUnit,
    Header,        // title "..." | tempo N | time N/N | fps N | music "..." (score NAME)?
    FigureDecl,    // figure NAME "label"?
    DancerDecl,    // dancer NAME RIG clause*          (one line)
    Clause,        // WORD value*                      (on a | at 0 1 | facing front | mirror | delay 2 | free | in A B)
    PoseDecl,      // pose NAME { track* }
    MoveDecl,      // move NAME { track* }
    SectionDecl,   // section NAME attribute* { (figureBlock | track)* }
    FigureBlock,   // FIGURE { track* }
    Track,         // TRACK { item* }
    BarLine,       // |
    Repeat,        // repeat N { item* }
    Overlay,       // sequence (& sequence)*
    Sequence,      // { item* }
    Event,         // target (duration | annotation | mark)*
    Target,        // word (. (word | N))* arguments?
    Duration,      // : N (/ N)? unit?                 (unit: s or f, written against the number)
    Annotation,    // @ word arguments?
    Arguments,     // ( (target | N)* )
    Mark,          // ! NAME
    FormDecl,      // form NAME? { formItem* }
    FormItem,      // NAME (* N)?
    ScoreDecl,     // score NAME? { scoreItem* }
    ScoreItem,     // form NAME | tempo N | title "..." | music "..." (score NAME)? | dancer NAME
    Error,         // tokens the parser could not place
}
