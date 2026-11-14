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
    Percent,
    Tilde,
    OpenBracket,
    CloseBracket,
    Comma,
    EqualsSign,

    // Nodes
    // A tempo is written the same wherever it is: tempo N | tempo N% | tempo NOTE = N (NOTE: 4, 8, 4. …)
    CompilationUnit,
    Header,        // title "..." | tempo … | time N/N | fps N | music "..." (score NAME)?
    FigureDecl,    // figure NAME "label"?
    DancerDecl,    // dancer NAME RIG clause*          (one line)
    Clause,        // WORD value*                      (on a | at 0 1 | facing front | mirror | delay 2 | free | in A B)
    PoseDecl,      // pose NAME { track* }
    MoveDecl,      // move NAME { track* }
    SectionDecl,   // section NAME attribute* { sectionItem* (figureBlock | track)* }
    SectionItem,   // tempo N | time N/N | upbeat N (/ N)?   (the lines a section begins with)
    FigureBlock,   // FIGURE { track* }
    Track,         // TRACK { item* }
    BarLine,       // |
    BarRepeat,     // % N?                             (the bar, or the N bars, before it again)
    Overlay,       // sequence (& sequence)*           (one sequence alone: a group, which `q` performs again)
    Sequence,      // { item* }
    Verse,         // verse N ((,)? N)* sequence       (verse 2 { … }  verse 1,3 { … }  verse 1-2 { … }: only on those plays of the section)
    Event,         // target (duration | annotation | mark)* ~?    or, after a tie:  duration ~?
    Target,        // word (. (word | N))* arguments?
    Duration,      // : N (/ N)? unit?                 (unit: s or f, written against the number)
    Annotation,    // @ word arguments?
    Arguments,     // ( (target | N)* )
    Mark,          // ! NAME
    FormDecl,      // form NAME? { (formItem | formChange | formSign | formEnding)* }
    FormItem,      // NAME
    FormChange,    // tempo N | time N/N                (for the section named after it, this time it is played)
    FormSign,      // |:   or   :| (* N)?   or   :|:
    FormEnding,    // [ N (N)? . (formItem | formChange)* ]   ([1. B]  [1-2. B C])
    ScoreDecl,     // score NAME? { scoreItem* }
    ScoreItem,     // form NAME | tempo N | title "..." | music "..." (score NAME)? | dancer NAME
    Error,         // tokens the parser could not place
}
