namespace Shosa.Core.Semantics;

/// <summary>
/// One play of a section of the song, in beats, its upbeat included. A beat is the note of the
/// lower number of the time in force, so a section that changes time counts each bar in its own.
/// </summary>
/// <param name="Pickup">Beats of it before its first bar line (an upbeat). The dance of the section begins after them.</param>
public sealed record SongPlay(string Name, double Beats, double Pickup = 0);

/// <summary>
/// What a dance takes from the song it names with `music`: how fast, in what time, which
/// sections there are, and how long each lasts as the song is played. Plain data: the binder
/// does not know where it comes from (a Lily# file, read by <see cref="Music.SongReader"/>; a
/// test writes one by hand).
/// </summary>
public sealed class SongInfo
{
    /// <summary>Why the song could not be read, in words for the author of the dance; null when it was.</summary>
    public string? Error { get; set; }
    /// <summary>Beats per minute at the start; a beat is the lower number of the time.</summary>
    public double Tempo { get; set; } = 90;
    public int BeatsPerBar { get; set; } = 4;
    public int BeatUnit { get; set; } = 4;
    /// <summary>Every section the song has, played or not.</summary>
    public List<string> Sections { get; set; } = [];
    /// <summary>The sections as they are played: the order that was asked for, or the song's own.</summary>
    public List<SongPlay> Plays { get; set; } = [];
    /// <summary>The song's scores ("" is the unnamed one).</summary>
    public List<string> Scores { get; set; } = [];
    /// <summary>
    /// Every tempo of the song as it is played, the first at beat 0, each in the beat of the
    /// time in force there. Empty means <see cref="Tempo"/> throughout.
    /// </summary>
    public List<TempoIr> Tempos { get; set; } = [];
    /// <summary>Every time of the song as it is played, the first at beat 0. Empty means the one above throughout.</summary>
    public List<MeterIr> Meters { get; set; } = [];
    /// <summary>
    /// The recording the song was made from, when it has one beside it (`NAME.orecchiante.json`) and it is
    /// played in its own order: <see cref="Tempos"/> are then the recording's, a tempo for every beat (DESIGN.md 97).
    /// </summary>
    public SongRecording? Recording { get; set; }
    /// <summary>Why the recording beside the song is not followed, in words for the author of the dance; null when it is, or there is none.</summary>
    public string? RecordingError { get; set; }
    /// <summary>
    /// What the song's `singer` lines sing, in the beats of <see cref="Plays"/>: for each, the part and its
    /// syllables (DESIGN.md 99). Null when the Lily# that read the song does not say (older than request 6);
    /// empty when nothing is sung.
    /// </summary>
    public List<SongSung>? Sung { get; set; }

    public SongInfo Copy() => (SongInfo)MemberwiseClone();
}

/// <summary>One `singer` line of a song: the part it sings and the syllables sung, in order.</summary>
/// <param name="Part">The Lily# part named after `on`.</param>
/// <param name="Singer">The singer's name; null when the line gives none.</param>
/// <param name="Syllables">Each [beat it starts at, beats the voice sounds, "text"], as lysc prints them.</param>
public sealed record SongSung(string Part, string? Singer, List<System.Text.Json.JsonElement[]> Syllables)
{
    /// <summary>The syllables as (beat, beats, text); a malformed one is left out.</summary>
    public IEnumerable<(double Beat, double Beats, string Text)> Read() =>
        ReadWords().Select(s => (s.Beat, s.Beats, s.Text));

    /// <summary>The syllables with whether the next one goes on the same word (`Lit- tle`: a fourth element
    /// true, Lily# 0.18.0, Shosa's request 8); false where Lily# does not say.</summary>
    public IEnumerable<(double Beat, double Beats, string Text, bool Joins)> ReadWords()
    {
        foreach (var s in Syllables ?? [])
        {
            if (s is [{ ValueKind: System.Text.Json.JsonValueKind.Number } beat, { ValueKind: System.Text.Json.JsonValueKind.Number } beats, { ValueKind: System.Text.Json.JsonValueKind.String } text, ..]
                && beats.GetDouble() > 0)
                yield return (beat.GetDouble(), beats.GetDouble(), text.GetString()!, s.Length > 3 && s[3].ValueKind == System.Text.Json.JsonValueKind.True);
        }
    }
}

/// <summary>A recording a song keeps time with: the sound file, and the second of it at which beat 0 of the song falls.</summary>
/// <param name="Audio">The sound file, a full path.</param>
/// <param name="Offset">Seconds into the recording of the song's first bar line.</param>
/// <param name="Duration">How long the recording is, seconds (0 if not known).</param>
/// <param name="Silent">
/// The recording is not heard, and its bars still keep the time: the file beside the song says `"silent": true`
/// (Lily#'s Arrange an Accompaniment… writes it when an accompaniment takes the recording's place). The preview
/// and the video then play the stems or the song's notes, never the recording (DESIGN.md 124). Left out of the IR when false.
/// </param>
public sealed record SongRecording(
    string Audio, double Offset, double Duration,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    bool Silent = false);

/// <summary>
/// The notes of a song as a clip plays it, for a preview that sounds them on the dance's clock.
/// Each is [beat it starts at, beats it lasts, pitch (a MIDI number; .5 is a quarter tone),
/// velocity (1-127), timbre (0 piano, 1 flute, 2 clarinet, 3 strings, 4 guitar, 5 bass, 6 brass,
/// 7 organ, 8 voice; 9 the drum kit, whose pitch is the General MIDI drum), program (the General
/// MIDI instrument of its part, -1 for none), channel (0-15; 9 the drums)], in starting order.
/// The beats are the clip's. A player with a sound font plays the program; one without, the timbre.
/// </summary>
public sealed class SongNotes
{
    /// <summary>Why there are none, in words for the author of the dance; null when the song was read.</summary>
    public string? Error { get; set; }
    public List<double[]> Notes { get; set; } = [];
}

/// <summary>
/// Reads the song a file names. <paramref name="file"/> is as written after `music`;
/// <paramref name="score"/> is the song's score (null for its default); <paramref name="order"/>
/// is the sections the dance's own `form` plays (null to take the song's order). Names in the
/// order that the song does not have are left out of <see cref="SongInfo.Plays"/>.
/// Null means there is nothing here that reads songs.
/// </summary>
public delegate SongInfo? SongSource(string file, string? score, IReadOnlyList<string>? order);

/// <summary>The song a clip is danced to and how it is to sound, for whoever plays or renders the two together.</summary>
/// <param name="File">As written after `music`, relative to the file.</param>
/// <param name="Score">The song's score; null for its default.</param>
/// <param name="Order">The sections to play when the dance has a `form` of its own; null for the song's order.</param>
/// <param name="Tempo">Beats per minute when the dance sets its own `tempo`; null for the song's.</param>
/// <param name="Recording">The recording the dance keeps time with, played instead of the song's notes; null for none.</param>
public sealed record MusicIr(string File, string? Score, IReadOnlyList<string>? Order, double? Tempo, SongRecording? Recording = null);
