#:project ../Shosa.Core/Shosa.Core.csproj
using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Semantics;

// dotnet run tools\flips.cs -- FILE.shosa ...
// Every bone that turns 45 degrees or more in a frame, and every joint the rotations put 1 mm or more from where the
// solver had it: what the tests No_bone_flips_between_two_frames and Rotations_put_every_joint_back_where_the_solver_had_it
// stop at the first of. DESIGN.md 129.
foreach (string file in args)
{
    string path = Path.GetFullPath(file);
    var clip = ShosaCompilation.Compile(File.ReadAllText(path), null, null, UsingSource.For(path)).Clip;
    foreach (var figure in clip.Figures)
    {
        for (int f = 1; f < figure.Rotations.Count; f++)
        {
            for (int b = 0; b < Skeleton.Bones.Length; b++)
            {
                Quat x = figure.Rotation(f - 1, b), y = figure.Rotation(f, b);
                double dot = Math.Abs(x.X * y.X + x.Y * y.Y + x.Z * y.Z + x.W * y.W);
                double deg = 2 * Math.Acos(Math.Min(1, dot)) * 180 / Math.PI;
                if (deg >= 45)
                    Console.WriteLine($"{Path.GetFileNameWithoutExtension(file)} {figure.Id} frame {f} sec {f / (double)clip.Fps:0.00} {Skeleton.Bones[b].Name} {deg:0}");
            }
            var local = Enumerable.Range(0, Skeleton.Bones.Length).Select(b => figure.Rotation(f, b)).ToArray();
            var (heads, _) = Skeleton.Positions(figure.Joint(f, Rig.Pelvis), local);
            for (int b = 0; b < Skeleton.Bones.Length; b++)
            {
                if (Skeleton.Bones[b].Joint >= 0 && (heads[b] - figure.Joint(f, Skeleton.Bones[b].Joint)).Length >= 1e-3)
                    Console.WriteLine($"{Path.GetFileNameWithoutExtension(file)} {figure.Id} frame {f} {Skeleton.Bones[b].Name} is {(heads[b] - figure.Joint(f, Skeleton.Bones[b].Joint)).Length * 1000:0.0} mm off");
            }
        }
    }
}
