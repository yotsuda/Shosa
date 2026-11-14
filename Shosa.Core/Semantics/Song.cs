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
}

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
public sealed record MusicIr(string File, string? Score, IReadOnlyList<string>? Order, double? Tempo);
