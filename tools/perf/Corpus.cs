using System.Globalization;
using Shosa.Core;
using Shosa.Core.Music;
using Shosa.Core.Semantics;

/// <summary>
/// Every number the solver gives for a set of files, kept and compared: for a change that is meant to make the solver
/// quicker and nothing else (DESIGN.md 120).
///
///   Shosa.Perf --dump FOLDER FILE.shosa…      keeps what each file solves to (FOLDER\NAME.bin, NAME.txt its diagnostics)
///   Shosa.Perf --compare FOLDER FILE.shosa…   solves them again and says what is not as kept
/// </summary>
internal static class Corpus
{
    public static int Run(string[] args)
    {
        bool dump = args[0] == "--dump";
        string folder = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(folder);
        int different = 0;
        foreach (string file in args.Skip(2).Select(Path.GetFullPath))
        {
            string name = Path.GetFileName(Path.GetDirectoryName(file)) + "." + Path.GetFileNameWithoutExtension(file);
            var (numbers, said, bound) = Solve(file);
            string bin = Path.Combine(folder, name + ".bin"), txt = Path.Combine(folder, name + ".txt"), ir = Path.Combine(folder, name + ".ir.json");
            if (dump)
            {
                var bytes = new byte[numbers.Count * sizeof(double)];
                Buffer.BlockCopy(numbers.ToArray(), 0, bytes, 0, bytes.Length);
                File.WriteAllBytes(bin, bytes);
                File.WriteAllText(txt, said);
                File.WriteAllText(ir, bound);
                Console.WriteLine($"{name}: {numbers.Count} numbers kept");
                continue;
            }
            if (!File.Exists(bin))
            {
                Console.WriteLine($"{name}: nothing kept to compare with");
                different++;
                continue;
            }
            var keptBytes = File.ReadAllBytes(bin);
            var kept = new double[keptBytes.Length / sizeof(double)];
            Buffer.BlockCopy(keptBytes, 0, kept, 0, keptBytes.Length);
            int differ = 0;
            double most = 0;
            for (int i = 0; i < Math.Min(kept.Length, numbers.Count); i++)
            {
                if (kept[i].Equals(numbers[i]))
                    continue;
                differ++;
                most = Math.Max(most, Math.Abs(kept[i] - numbers[i]));
            }
            bool sameText = File.ReadAllText(txt) == said;
            // What the binder gave (kept since DESIGN.md 120.14: a folder kept before it has none, and says so).
            bool sameBound = File.Exists(ir) && File.ReadAllText(ir) == bound;
            bool same = differ == 0 && kept.Length == numbers.Count && sameText && sameBound;
            if (!same)
                different++;
            Console.WriteLine(same ? $"{name}: the same ({numbers.Count} numbers)"
                : string.Create(CultureInfo.InvariantCulture, $"{name}: DIFFERENT: {differ} of {numbers.Count} numbers (kept {kept.Length}), the most by {most:0.######}{(sameText ? "" : "; the diagnostics too")}{(sameBound ? "" : File.Exists(ir) ? "; what the binder gave too" : "; nothing kept of what the binder gave")}"));
        }
        return different == 0 ? 0 : 1;
    }

    private static (List<double> Numbers, string Said, string Bound) Solve(string file)
    {
        var compilation = ShosaCompilation.Compile(File.ReadAllText(file), null, SongReader.For(file), UsingSource.For(file));
        var numbers = new List<double>();
        foreach (var clip in compilation.Clips)
        {
            numbers.Add(clip.Figures.Count);
            foreach (var figure in clip.Figures)
            {
                numbers.Add(figure.Frames.Count);
                foreach (var row in figure.Frames) numbers.AddRange(row.Select(v => (double)v));
                foreach (var row in figure.Rotations) numbers.AddRange(row.Select(v => (double)v));
                numbers.AddRange(figure.Yaws);
                numbers.AddRange(figure.Facing);
                numbers.AddRange(figure.Velocity ?? []);
                foreach (var row in figure.Mouth ?? []) numbers.AddRange(row.Select(v => (double)v));
                numbers.AddRange((figure.Mic?.Up ?? []).Select(v => (double)v));
                if (figure.Juggle is { } juggle)
                {
                    foreach (var row in juggle.Frames) numbers.AddRange(row.Select(v => (double)v));
                    numbers.AddRange(juggle.Weight.Select(v => (double)v));
                    foreach (var row in juggle.Flights) numbers.AddRange(row.Select(v => (double)v));
                    foreach (var row in juggle.Styles) numbers.AddRange(row.Select(v => (double)v));
                    numbers.AddRange(juggle.Mills.Select(v => (double)v));
                    foreach (var list in new[] { juggle.Pull, juggle.Wheel, juggle.Plane })
                        foreach (var row in list ?? []) numbers.AddRange(row.Select(v => (double)v));
                    numbers.AddRange((juggle.Follow ?? []).Select(v => (double)v));
                }
            }
            foreach (var mark in clip.Marks)
            {
                numbers.Add(mark.Beat);
                numbers.Add(mark.Seconds);
            }
        }
        string said = string.Join("\n", compilation.Diagnostics.Select(d => $"{d.Code} {d.Severity} {d.Span.Start}+{d.Span.Length} {d.Message}"));
        // Everything the binder gave, with where in the text each word is: for a change that is meant to make the binder quicker.
        string bound = System.Text.Json.JsonSerializer.Serialize(compilation.Score, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
        });
        return (numbers, said, bound);
    }
}
