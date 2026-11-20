using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using Shosa.Core.Motion;

namespace Shosa.Core.Output;

/// <summary>
/// The movement as glTF 2.0: the bones of <see cref="Skeleton"/> as nodes, and one animation
/// that moves the hips and turns every bone. Shosa's frame is already glTF's (metres, y up,
/// right-handed, the figure facing +z), so positions and rotations are written as they are.
///
/// Two flavours come out of the same nodes:
/// plain glTF holds every figure, each with a skin, because Blender and Godot only make a
/// skeleton out of nodes a skin names (there is no mesh: the skin exists to say "these are bones");
/// VRM Animation (<c>.vrma</c>) holds one figure and says which node is which humanoid bone,
/// so the movement can be put on any VRM model.
/// </summary>
public static class Gltf
{
    /// <summary>Every figure in one file: <c>.glb</c> when binary, else <c>.gltf</c> text with the data embedded.</summary>
    public static byte[] Render(MotionClip clip, bool binary = true) => Render([clip], binary);

    /// <summary>
    /// Several clips in one file: one skeleton for each dancer of any of them, one animation per
    /// clip, named after the clip. An animation's <c>extras</c> carry what the engine needs beside the
    /// curves: whether it loops, the velocity an `inPlace` clip had taken out, and the marks.
    /// </summary>
    public static byte[] Render(IReadOnlyList<MotionClip> clips, bool binary = true) =>
        Pack(clips, clips.Select(c => (IReadOnlyList<ClipFigure>)c.Figures).ToList(), vrma: false, binary);

    /// <summary>One figure as a VRM Animation, which is a <c>.glb</c> with the humanoid named.</summary>
    /// <summary>
    /// What an engine needs of a clip besides the curves, in the one shape both the extras of a
    /// glTF animation and FILE.events.json have: <c>loop</c>; <c>velocity</c> (only `inPlace`:
    /// metres a second in glTF's axes, [x, 0, z], for each figure); <c>marks</c> (only if there
    /// are any: <c>time</c> in seconds, <c>beat</c>, <c>name</c>, <c>figure</c>, <c>section</c>, <c>play</c>).
    /// </summary>
    public static JsonObject Extras(MotionClip clip, IReadOnlyList<ClipFigure> figures)
    {
        var extras = new JsonObject { ["loop"] = clip.Loop };
        if (clip.Inplace)
        {
            extras["velocity"] = new JsonObject(figures.Where(f => f.Velocity is not null)
                .Select(f => KeyValuePair.Create(f.Id, (JsonNode?)new JsonArray(f.Velocity![0], 0, f.Velocity[1]))));
        }
        var marks = clip.Marks.Where(m => figures.Any(f => f.Id == m.Figure)).ToList();
        if (marks.Count > 0)
        {
            extras["marks"] = new JsonArray([.. marks.Select(m => (JsonNode)new JsonObject
            {
                ["time"] = m.Seconds, ["beat"] = m.Beat, ["name"] = m.Name, ["figure"] = m.Figure, ["section"] = m.Section, ["play"] = m.Play,
            })]);
        }
        return extras;
    }

    /// <summary>
    /// The text of FILE.events.json, written beside a BVH or a VRM Animation (neither can carry
    /// extras): the score and its length, then <see cref="Extras"/> field for field. Null for a
    /// clip with nothing to say (no marks, not `inPlace`).
    /// </summary>
    public static string? EventsJson(MotionClip clip)
    {
        if (clip.Marks.Count == 0 && !clip.Inplace)
            return null;
        var events = new JsonObject { ["score"] = clip.Name, ["seconds"] = Math.Round(clip.SecondsAt(clip.TotalBeats), 4) };
        foreach (var (key, value) in Extras(clip, clip.Figures))
            events[key] = value?.DeepClone();
        return events.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    }

    public static byte[] RenderVrma(MotionClip clip, ClipFigure figure) => Pack([clip], [[figure]], vrma: true, binary: true);

    /// <param name="cast">For each clip, its figures: the same ones in the same order in every clip.</param>
    private static byte[] Pack(IReadOnlyList<MotionClip> clips, IReadOnlyList<IReadOnlyList<ClipFigure>> cast, bool vrma, bool binary)
    {
        var bin = new MemoryStream();
        JsonArray nodes = [], roots = [], skins = [], accessors = [], views = [], samplers = [], channels = [], animations = [];
        // A skeleton for everyone who dances in any of the clips, by name: a clip moves its own.
        var figures = cast.SelectMany(c => c).GroupBy(f => f.Id).Select(g => g.First()).ToList();
        var firstNode = new Dictionary<string, int>();

        int Accessor(float[] values, string type, int width, bool bounds = false)
        {
            var bytes = new byte[values.Length * 4];
            for (int i = 0; i < values.Length; i++)
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), values[i]);
            views.Add(new JsonObject { ["buffer"] = 0, ["byteOffset"] = bin.Length, ["byteLength"] = bytes.Length });
            bin.Write(bytes);

            var accessor = new JsonObject
            {
                ["bufferView"] = views.Count - 1, ["componentType"] = 5126, ["count"] = values.Length / width, ["type"] = type,
            };
            if (bounds)
            {
                accessor["min"] = new JsonArray { values.Min() };
                accessor["max"] = new JsonArray { values.Max() };
            }
            accessors.Add(accessor);
            return accessors.Count - 1;
        }

        void Channel(int time, int output, int node, string path)
        {
            samplers.Add(new JsonObject { ["input"] = time, ["output"] = output, ["interpolation"] = "LINEAR" });
            channels.Add(new JsonObject
            {
                ["sampler"] = samplers.Count - 1,
                ["target"] = new JsonObject { ["node"] = node, ["path"] = path },
            });
        }

        var bones = Skeleton.Bones;
        var humanBones = new JsonObject();
        var expressions = new JsonObject();

        var figureRoots = new List<int>();

        foreach (var figure in figures)
        {
            int first = nodes.Count;
            firstNode[figure.Id] = first;
            var rest = new Vec3[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                var bone = bones[b];
                rest[b] = bone.Parent < 0 ? bone.Offset : rest[bone.Parent] + bone.Offset;
                var node = new JsonObject
                {
                    ["name"] = bone.Name,
                    ["translation"] = new JsonArray { (float)bone.Offset.X, (float)bone.Offset.Y, (float)bone.Offset.Z },
                };
                var children = new JsonArray();
                for (int c = b + 1; c < bones.Length; c++)
                {
                    if (bones[c].Parent == b)
                        children.Add(first + c);
                }
                if (children.Count > 0)
                    node["children"] = children;
                nodes.Add(node);
                if (bone.Parent < 0)
                    figureRoots.Add(first + b);
                humanBones[bone.Name] = new JsonObject { ["node"] = first + b };
            }

            if (!vrma)
            {
                // Nothing is turned at rest, so a bone's inverse bind matrix only moves it back to the origin.
                var inverseBind = new float[bones.Length * 16];
                for (int b = 0; b < bones.Length; b++)
                {
                    int at = b * 16;
                    inverseBind[at] = inverseBind[at + 5] = inverseBind[at + 10] = inverseBind[at + 15] = 1;
                    (inverseBind[at + 12], inverseBind[at + 13], inverseBind[at + 14]) = ((float)-rest[b].X, (float)-rest[b].Y, (float)-rest[b].Z);
                }
                skins.Add(new JsonObject
                {
                    ["name"] = figure.Id,
                    ["joints"] = new JsonArray([.. Enumerable.Range(first, bones.Length).Select(i => (JsonNode)i)]),
                    ["inverseBindMatrices"] = Accessor(inverseBind, "MAT4", 16),
                });
            }

        }

        // One animation per clip, on the bones made above.
        for (int c = 0; c < clips.Count; c++)
        {
            var clip = clips[c];
            int frames = cast[c].Count == 0 ? 0 : cast[c].Max(f => f.Rotations.Count);
            if (frames == 0)
                continue;
            // The last frame is the pose at the very end (Rig.BeatOfFrame), seldom a whole frame after the one before:
            // keyed at its own time, a loop is as long as the score and does not stand still at the join (trial 28).
            double end = clip.SecondsAt(clip.TotalBeats);
            int time = Accessor([.. Enumerable.Range(0, frames).Select(f => (float)Math.Min((double)f / clip.Fps, end))], "SCALAR", 1, bounds: true);
            samplers = [];
            channels = [];
            foreach (var figure in cast[c])
            {
                Animate(figure, firstNode[figure.Id], frames, time);
                if (vrma && figure.Mouth is { } mouth)
                    Sing(mouth, frames, time);
            }

            var extras = Extras(clip, cast[c]);
            string name = clip.Name.Length > 0 ? clip.Name : clip.Title.Length > 0 ? clip.Title : "shosa";
            animations.Add(new JsonObject { ["name"] = name, ["samplers"] = samplers, ["channels"] = channels, ["extras"] = extras });
        }

        void Animate(ClipFigure figure, int first, int frames, int time)
        {
            var moves = new float[frames * 3];
            for (int f = 0; f < frames; f++)
            {
                var hips = figure.Joint(f, Rig.Pelvis);
                (moves[f * 3], moves[f * 3 + 1], moves[f * 3 + 2]) = ((float)hips.X, (float)hips.Y, (float)hips.Z);
            }
            Channel(time, Accessor(moves, "VEC3", 3), first, "translation");

            for (int b = 0; b < bones.Length; b++)
            {
                var turns = new float[frames * 4];
                var previous = Quat.Identity;
                for (int f = 0; f < frames; f++)
                {
                    // q and -q are the same rotation: stay on the side of the frame before, for readers that blend the four numbers.
                    var q = figure.Rotation(f, b);
                    if (q.X * previous.X + q.Y * previous.Y + q.Z * previous.Z + q.W * previous.W < 0)
                        q = new Quat(-q.X, -q.Y, -q.Z, -q.W);
                    previous = q;
                    double length = Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
                    (turns[f * 4], turns[f * 4 + 1], turns[f * 4 + 2], turns[f * 4 + 3]) =
                        ((float)(q.X / length), (float)(q.Y / length), (float)(q.Z / length), (float)(q.W / length));
                }
                Channel(time, Accessor(turns, "VEC4", 4), first + b, "rotation");
            }
        }

        // The mouth of a figure that sings (DESIGN.md 99): VRM Animation keys an expression as the x of a node's
        // translation, one node for each of the five mouth expressions, outside the humanoid.
        void Sing(List<float[]> mouth, int frames, int time)
        {
            for (int shape = 0; shape < Mouth.Shapes.Length; shape++)
            {
                int node = nodes.Count;
                nodes.Add(new JsonObject { ["name"] = "Expression_" + Mouth.Shapes[shape] });
                roots.Add(node);
                expressions[Mouth.Shapes[shape]] = new JsonObject { ["node"] = node };
                var weights = new float[frames * 3];
                for (int f = 0; f < frames; f++)
                    weights[f * 3] = mouth[Math.Min(f, mouth.Count - 1)][shape];
                Channel(time, Accessor(weights, "VEC3", 3), node, "translation");
            }
        }

        // Each figure's bones hang from a node of its own, named after the figure and placed after
        // all the bones: Blender makes one armature of skeletons whose roots share a parent.
        // (A VRM Animation is one humanoid, and its hips stay at the top.)
        for (int i = 0; i < figureRoots.Count; i++)
        {
            if (vrma)
            {
                roots.Add(figureRoots[i]);
                continue;
            }
            roots.Add(nodes.Count);
            nodes.Add(new JsonObject { ["name"] = figures[i].Id, ["children"] = new JsonArray { figureRoots[i] } });
        }

        var buffer = new JsonObject { ["byteLength"] = bin.Length };
        var json = new JsonObject
        {
            ["asset"] = new JsonObject { ["version"] = "2.0", ["generator"] = "Shosa" },
            ["scene"] = 0,
            ["scenes"] = new JsonArray { new JsonObject { ["nodes"] = roots } },
            ["nodes"] = nodes,
        };
        if (skins.Count > 0)
            json["skins"] = skins;
        if (animations.Count > 0)
            json["animations"] = animations;
        if (bin.Length > 0)
        {
            json["accessors"] = accessors;
            json["bufferViews"] = views;
            json["buffers"] = new JsonArray { buffer };
        }
        if (vrma)
        {
            json["extensionsUsed"] = new JsonArray { "VRMC_vrm_animation" };
            var animation = new JsonObject
            {
                ["specVersion"] = "1.0",
                ["humanoid"] = new JsonObject { ["humanBones"] = humanBones },
            };
            if (expressions.Count > 0)
                animation["expressions"] = new JsonObject { ["preset"] = expressions };
            json["extensions"] = new JsonObject { ["VRMC_vrm_animation"] = animation };
        }

        if (!binary)
        {
            buffer["uri"] = "data:application/octet-stream;base64," + Convert.ToBase64String(bin.ToArray());
            return Encoding.UTF8.GetBytes(json.ToJsonString());
        }

        // GLB: a header, then the JSON padded with spaces and the data, each to a multiple of four bytes.
        var text = Encoding.UTF8.GetBytes(json.ToJsonString());
        int textLength = (text.Length + 3) & ~3, binLength = (int)bin.Length;
        var glb = new byte[12 + 8 + textLength + (binLength > 0 ? 8 + binLength : 0)];
        var span = glb.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, 0x46546C67); // "glTF"
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 2);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)glb.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)textLength);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], 0x4E4F534A); // "JSON"
        span.Slice(20, textLength).Fill((byte)' ');
        text.CopyTo(span[20..]);
        if (binLength > 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[(20 + textLength)..], (uint)binLength);
            BinaryPrimitives.WriteUInt32LittleEndian(span[(24 + textLength)..], 0x004E4942); // "BIN"
            bin.ToArray().CopyTo(span[(28 + textLength)..]);
        }
        return glb;
    }
}
