// shosa-music — reads a Lily# song for Shosa.
//
// This program is free software: you can redistribute it and/or modify it under the terms of
// the GNU General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. It links Lily# (LilySharp.Core), which
// is under the same licence. It is distributed WITHOUT ANY WARRANTY; see the GNU General Public
// License for more details: <https://www.gnu.org/licenses/>.

using LilySharp.Core.Editing;
using LilySharp.Core.Midi;
using LilySharp.Core.Parser;
using LilySharp.Core.Svg.Collector;
using LilySharp.Core.Syntax;

namespace Shosa.Music;

/// <summary>One play of a section, in the song's beats (the lower number of its time), upbeat included.</summary>
/// <param name="Pickup">Beats of it that come before its first bar line (an upbeat); 0 when it starts on a bar.</param>
public sealed record Play(string Name, double Beats, double Pickup);

/// <summary>
/// What a dance needs to know of a song: how fast, in what time, which sections, in what order
/// and how long each is as it is played. The same shape as Shosa.Core's SongInfo — it travels
/// between the two as JSON.
/// </summary>
public sealed class Timeline
{
    public string? Error { get; set; }
    /// <summary>Beats per minute at the start, a beat being the lower number of the time.</summary>
    public double Tempo { get; set; }
    public int BeatsPerBar { get; set; } = 4;
    public int BeatUnit { get; set; } = 4;
    /// <summary>Every section the song has, played or not.</summary>
    public List<string> Sections { get; set; } = [];
    /// <summary>The sections as they are played: the order asked for, or the score's own.</summary>
    public List<Play> Plays { get; set; } = [];
    /// <summary>The song's scores ("" is the unnamed one).</summary>
    public List<string> Scores { get; set; } = [];
    /// <summary>Every tempo as the song is played, the first at beat 0, each in the beat of the time in force there.</summary>
    public List<TempoAt> Tempos { get; set; } = [];
    /// <summary>Every time as the song is played, the first at beat 0.</summary>
    public List<MeterAt> Meters { get; set; } = [];
}

public sealed record TempoAt(double Beat, double Bpm);
public sealed record MeterAt(double Beat, int BeatsPerBar, int BeatUnit);

/// <summary>A song as it will sound: the order and the tempo may be the dance's rather than its own.</summary>
public sealed class SongFile
{
    private const string OrderForm = "ShosaOrder__";

    public Timeline Timeline { get; } = new();
    public MidiFile? Midi { get; private set; }

    /// <param name="score">One of the song's scores; null or "" for its default.</param>
    /// <param name="order">Section names to play instead of the score's own form.</param>
    /// <param name="tempo">Beats per minute to start at instead of the song's own; a song that changes tempo keeps its proportions.</param>
    public static SongFile Load(string path, string? score, IReadOnlyList<string>? order, double? tempo = null)
    {
        var song = new SongFile();
        try
        {
            song.Read(path, score, order, tempo);
        }
        catch (Exception ex)
        {
            song.Timeline.Error = $"{Path.GetFileName(path)} could not be read: {ex.Message}";
        }
        return song;
    }

    private void Read(string path, string? score, IReadOnlyList<string>? order, double? tempo)
    {
        string name = Path.GetFileName(path);
        if (!File.Exists(path))
        {
            Timeline.Error = $"There is no file {name} beside this one.";
            return;
        }

        // As lysc reads a file: `using` first, then the staves the singers need.
        string source = File.ReadAllText(path);
        if (UsingExpander.HasUsings(source))
            source = UsingExpander.Expand(source, path, p => File.Exists(p) ? File.ReadAllText(p) : null, out _);
        var tree = SyntaxTree.Parse(source);
        if (SingerParts.Wanted(tree))
        {
            source = SingerParts.Expand(source, tree, out _);
            tree = SyntaxTree.Parse(source);
        }
        if (tree.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error) is { } error)
        {
            int line = 1 + source.AsSpan(0, Math.Min(error.Span.Start, source.Length)).Count('\n');
            Timeline.Error = $"{name} has errors (line {line}: {error.Message}). Check it with lysc first.";
            return;
        }

        Timeline.Sections = tree.GetNodes<SectionDeclarationSyntax>().Select(s => s.SectionName).Distinct().ToList();
        Timeline.Scores = RenderSpecParser.FindAll(tree).Select(s => s.Name ?? "").Distinct().ToList();
        if (Timeline.Sections.Count == 0)
        {
            Timeline.Error = $"{name} has no section to dance to.";
            return;
        }

        // The order the dance asks for is written into the song as one more form: Lily# plays a
        // form of its own tree, and this way every rule of how sections follow one another is its.
        if (order is not null)
        {
            var known = order.Where(Timeline.Sections.Contains).ToList();
            if (known.Count == 0)
            {
                Timeline.Error = $"None of the sections asked for is in {name}. It has: {string.Join(", ", Timeline.Sections)}.";
                return;
            }
            tree = SyntaxTree.Parse(source + $"\nform {OrderForm} {{ {string.Join(' ', known)} }}\n");
        }

        RenderSpec? spec = null;
        if (!string.IsNullOrEmpty(score))
        {
            spec = RenderSpecParser.FindByName(tree, score);
            if (spec is null)
            {
                Timeline.Error = $"{name} has no score named '{score}'. It has: {string.Join(", ", Timeline.Scores.Select(s => s == "" ? "(the unnamed one)" : s))}.";
                return;
            }
        }

        var form = order is null ? spec?.Form : tree.GetNodes<FormDeclarationSyntax>().Last(f => f.NameText == OrderForm);
        var midi = new MidiExporter { Form = form, Score = spec }.Export(tree);
        Midi = midi;
        Measure(midi);
        if (tempo is { } asked && Timeline.Tempo > 0)
            Retime(midi, asked / Timeline.Tempo);
    }

    /// <summary>
    /// The times of the song in MIDI ticks, each with the beat it begins at. A beat is the note
    /// of the lower number of the time in force, so the ticks of a beat change with the time.
    /// </summary>
    private readonly List<(int Tick, double Beat, double TicksPerBeat, int Numerator, int Denominator)> _meters = [];

    private double Beat(int tick)
    {
        var meter = _meters.Count == 0 ? (Tick: 0, Beat: 0.0, TicksPerBeat: 480.0, Numerator: 4, Denominator: 4) : _meters.LastOrDefault(m => m.Tick <= tick, _meters[0]);
        return Math.Round(meter.Beat + (tick - meter.Tick) / meter.TicksPerBeat, 4);
    }

    private void Measure(MidiFile midi)
    {
        // Every track says the same times and tempos: one of each tick, and only real changes.
        double TicksPerBeat(int denominator) => midi.TicksPerQuarterNote * 4.0 / denominator;
        _meters.Add((0, 0, TicksPerBeat(4), 4, 4));
        foreach (var change in midi.Tracks.SelectMany(t => t.TimeSignatures).OrderBy(t => t.Tick))
        {
            var last = _meters[^1];
            if ((change.Numerator, change.Denominator) == (last.Numerator, last.Denominator))
                continue;
            var next = (change.Tick, Beat(change.Tick), TicksPerBeat(change.Denominator), change.Numerator, change.Denominator);
            if (change.Tick <= last.Tick)
                _meters[^1] = (last.Tick, last.Beat, next.Item3, next.Numerator, next.Denominator);
            else
                _meters.Add(next);
        }
        (Timeline.BeatsPerBar, Timeline.BeatUnit) = (_meters[0].Numerator, _meters[0].Denominator);
        Timeline.Meters = _meters.Select(m => new MeterAt(m.Beat, m.Numerator, m.Denominator)).ToList();

        // A tempo is counted in the beat of its time: where the time changes its beat, the
        // number changes though the music goes on as fast as before.
        var written = midi.Tracks.SelectMany(t => t.TempoChanges).OrderBy(t => t.Tick).ToList();
        double microsecondsPerQuarter = 500000;
        foreach (int tick in written.Select(t => t.Tick).Concat(_meters.Select(m => m.Tick)).Append(0).Distinct().Order())
        {
            foreach (var change in written.Where(t => t.Tick == tick))
                microsecondsPerQuarter = change.MicrosecondsPerBeat;
            int unit = _meters.Last(m => m.Tick <= tick).Denominator;
            double bpm = Math.Round(60e6 / microsecondsPerQuarter * unit / 4, 3);
            if (Timeline.Tempos.Count == 0 || Timeline.Tempos[^1].Bpm != bpm)
                Timeline.Tempos.Add(new TempoAt(Beat(tick), bpm));
        }
        Timeline.Tempo = Timeline.Tempos[0].Bpm;

        // The piece ends at its last bar line: a fermata or a ringing note may sound past it,
        // and a closing rest stops short of it.
        var starts = midi.SectionStarts.ToList();
        int closing = midi.BarTicks.Count > 0 ? midi.BarTicks.Max : 0;
        int end = starts.Count > 0 && closing > starts[^1].Key ? closing : midi.EndTick;
        for (int i = 0; i < starts.Count; i++)
        {
            int start = starts[i].Key, next = i + 1 < starts.Count ? starts[i + 1].Key : end;
            // An upbeat: the section's first bar line comes before a whole bar has gone by.
            var meter = _meters.Last(m => m.Tick <= start);
            int line = midi.BarTicks.Where(b => b > start && b < next).DefaultIfEmpty(start).First();
            bool upbeat = line - start < meter.Numerator * meter.TicksPerBeat - 0.5;
            Timeline.Plays.Add(new Play(starts[i].Value, Math.Round(Beat(next) - Beat(start), 4), upbeat ? Math.Round(Beat(line) - Beat(start), 4) : 0));
        }
    }

    /// <summary>
    /// The notes of the song as it is played, in the order they start, each as
    /// [beat it starts at, beats it lasts, pitch (a MIDI number; .5 is a quarter tone),
    /// velocity (1-127), timbre (Lily#'s preview families, 0 piano … 8 voice; 9 is the drum kit,
    /// whose pitch is the General MIDI drum), program (the General MIDI instrument of its part,
    /// 0-127, as the exported MIDI has it; -1 when the part names none), channel (0-15; 9 is the
    /// drums)]. The last two are for a player with a sound font; one without reads the timbre.
    /// Beats are the timeline's, so a player that keeps the dance's beat keeps the music with it.
    /// </summary>
    public List<double[]> Notes()
    {
        if (Midi is null)
            return [];
        return Midi.Tracks.SelectMany(t => t.Notes.Select(n => (Note: n, Program: t.Program ?? -1)))
            .Where(n => n.Note.Velocity > 0 && n.Note.DurationTicks > 0)
            .OrderBy(n => n.Note.StartTick).ThenBy(n => n.Note.Pitch)
            .Select(x =>
            {
                var n = x.Note;
                return new double[]
                {
                    Beat(n.StartTick), Math.Round(Beat(n.StartTick + n.DurationTicks) - Beat(n.StartTick), 4),
                    n.Pitch + n.QuarterBend * 0.5, n.Velocity, n.Channel == 9 ? 9 : n.Timbre, x.Program, n.Channel,
                };
            })
            .ToList();
    }

    /// <summary>Every tempo of the song times the same factor: the ticks, and so the beats, stay where they are.</summary>
    private static void Retime(MidiFile midi, double factor)
    {
        foreach (var track in midi.Tracks)
        {
            for (int i = 0; i < track.TempoChanges.Count; i++)
                track.TempoChanges[i] = new TempoChange(track.TempoChanges[i].Tick, (int)Math.Round(track.TempoChanges[i].MicrosecondsPerBeat / factor));
        }
    }
}
