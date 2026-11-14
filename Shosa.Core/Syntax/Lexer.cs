namespace Shosa.Core.Syntax;

/// <summary>
/// Text to tokens. Every character lands in a token or in a token's trivia, so the token
/// stream reproduces the text exactly. No state is carried from one line to the next
/// (comments and strings both end at the line break), which keeps a re-lex local to an edit.
/// </summary>
internal sealed class Lexer(string text)
{
    public List<Diagnostic> Diagnostics { get; } = [];

    public List<GreenToken> Lex()
    {
        var tokens = new List<GreenToken>();
        int pos = 0;
        while (true)
        {
            int leadingStart = pos;
            ScanLeadingTrivia(ref pos);
            string leading = text[leadingStart..pos];

            if (pos >= text.Length)
            {
                tokens.Add(new GreenToken(SyntaxKind.EndOfFile, leading, "", ""));
                return tokens;
            }

            int tokenStart = pos;
            var kind = ScanToken(ref pos);
            string tokenText = text[tokenStart..pos];

            int trailingStart = pos;
            ScanTrailingTrivia(ref pos);
            tokens.Add(new GreenToken(kind, leading, tokenText, text[trailingStart..pos]));
        }
    }

    private bool AtComment(int pos) => pos + 1 < text.Length && text[pos] == '/' && text[pos + 1] == '/';

    private void SkipComment(ref int pos)
    {
        while (pos < text.Length && text[pos] != '\n' && text[pos] != '\r')
            pos++;
    }

    private void ScanLeadingTrivia(ref int pos)
    {
        while (pos < text.Length)
        {
            if (char.IsWhiteSpace(text[pos]))
                pos++;
            else if (AtComment(pos))
                SkipComment(ref pos);
            else
                break;
        }
    }

    private void ScanTrailingTrivia(ref int pos)
    {
        while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\t'))
            pos++;
        if (AtComment(pos))
            SkipComment(ref pos);
        if (pos < text.Length && text[pos] == '\r')
            pos++;
        if (pos < text.Length && text[pos] == '\n')
            pos++;
    }

    private SyntaxKind ScanToken(ref int pos)
    {
        char c = text[pos];
        switch (c)
        {
            case '{': pos++; return SyntaxKind.OpenBrace;
            case '}': pos++; return SyntaxKind.CloseBrace;
            case '|': pos++; return SyntaxKind.Bar;
            case '.': pos++; return SyntaxKind.Dot;
            case ':': pos++; return SyntaxKind.Colon;
            case '@': pos++; return SyntaxKind.At;
            case '/': pos++; return SyntaxKind.Slash;
            case '*': pos++; return SyntaxKind.Star;
            case '(': pos++; return SyntaxKind.OpenParen;
            case ')': pos++; return SyntaxKind.CloseParen;
        }

        if (c == '"')
        {
            int start = pos++;
            while (pos < text.Length && text[pos] != '"' && text[pos] != '\n' && text[pos] != '\r')
                pos++;
            if (pos < text.Length && text[pos] == '"')
                pos++;
            else
                Diagnostics.Add(new Diagnostic("SH0001", DiagnosticSeverity.Error, new TextSpan(start, pos - start),
                    "This string is not closed: add a \" before the end of the line."));
            return SyntaxKind.String;
        }

        if (char.IsAsciiDigit(c) || (c == '-' && pos + 1 < text.Length && char.IsAsciiDigit(text[pos + 1])))
        {
            pos++;
            while (pos < text.Length && char.IsAsciiDigit(text[pos]))
                pos++;
            if (pos + 1 < text.Length && text[pos] == '.' && char.IsAsciiDigit(text[pos + 1]))
            {
                pos++;
                while (pos < text.Length && char.IsAsciiDigit(text[pos]))
                    pos++;
            }
            return SyntaxKind.Number;
        }

        if (char.IsLetter(c) || c == '_')
        {
            pos++;
            while (pos < text.Length && (char.IsLetterOrDigit(text[pos]) || text[pos] == '_'))
                pos++;
            return SyntaxKind.Identifier;
        }

        Diagnostics.Add(new Diagnostic("SH0002", DiagnosticSeverity.Error, new TextSpan(pos, 1),
            $"'{c}' is not a character this language uses."));
        pos++;
        return SyntaxKind.BadToken;
    }
}
