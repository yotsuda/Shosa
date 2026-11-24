using System.Diagnostics.Tracing;
using Shosa.Core.Motion;
using Shosa.Core.Music;
using Shosa.Core.Semantics;
using Shosa.Core.Syntax;

/// <summary>
/// What solving a file allocates, by type: the runtime's allocation ticks (one for about every 100 KB a thread
/// allocates, naming the type that crossed the line), counted while the file's clips are solved on one processor.
/// </summary>
internal static class Allocations
{
    private sealed class Ticks : EventListener
    {
        public readonly Dictionary<string, long> Bytes = [];
        public volatile bool On;

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
                EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1);   // GC
        }

        protected override void OnEventWritten(EventWrittenEventArgs data)
        {
            if (!On || data.EventName is null || !data.EventName.StartsWith("GCAllocationTick") || data.PayloadNames is null || data.Payload is null)
                return;
            int type = data.PayloadNames.IndexOf("TypeName"), amount = data.PayloadNames.IndexOf("AllocationAmount64");
            if (type < 0 || amount < 0)
                return;
            string name = data.Payload[type] as string ?? "?";
            lock (Bytes)
                Bytes[name] = Bytes.GetValueOrDefault(name) + Convert.ToInt64(data.Payload[amount]);
        }
    }

    public static int Run(string[] args)
    {
        string file = Path.GetFullPath(args[1]);
        int times = Array.IndexOf(args, "--times") is >= 0 and var i && i + 1 < args.Length ? int.Parse(args[i + 1]) : 20;
        var score = Binder.Bind(SyntaxTree.Parse(File.ReadAllText(file)), null, SongReader.For(file)).Item1;
        Solver.FramesAtOnce = int.MaxValue;   // on this thread: its allocations are the ones counted
        foreach (var ir in score.Clips)
            Solver.Solve(ir, []);
        using var ticks = new Ticks();
        long before = GC.GetAllocatedBytesForCurrentThread();
        ticks.On = true;
        int frames = 0;
        for (int n = 0; n < times; n++)
        {
            foreach (var ir in score.Clips)
                frames += Solver.Solve(ir, []).Figures.Sum(f => f.Frames.Count);
        }
        ticks.On = false;
        long all = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine(FormattableString.Invariant($"{Path.GetFileName(file)}: {all / 1024.0 / 1024.0 / times:0.0} MB a solve, {(double)all / Math.Max(1, frames):0} bytes a frame of a figure ({frames / times} of them)"));
        long ticked = ticks.Bytes.Values.Sum();
        foreach (var (name, bytes) in ticks.Bytes.OrderByDescending(b => b.Value).Take(25))
            Console.WriteLine(FormattableString.Invariant($"  {100.0 * bytes / Math.Max(1, ticked),5:0.0}%  {(double)bytes / ticked * all / Math.Max(1, frames),6:0} B  {name}"));
        return 0;
    }
}