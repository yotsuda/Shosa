namespace Shosa.Core.Syntax;

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;
    public bool Contains(int position) => position >= Start && position < End;
}

public readonly record struct TextChange(TextSpan Span, string NewText);

public enum DiagnosticSeverity { Info, Warning, Error }

/// <summary>
/// One finding, addressed by a source span. Codes are grouped by the layer that found it:
/// SH0xxx syntax, SH1xxx meaning (binding), SH2xxx movement (the solver's physical checks).
/// </summary>
public sealed record Diagnostic(string Code, DiagnosticSeverity Severity, TextSpan Span, string Message)
{
    public Diagnostic Shift(int delta) => this with { Span = new TextSpan(Span.Start + delta, Span.Length) };
}
