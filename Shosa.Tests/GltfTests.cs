using System.Buffers.Binary;
using System.Text;
using System.Text.Json.Nodes;
using Shosa.Core;
using Shosa.Core.Motion;
using Shosa.Core.Output;

namespace Shosa.Tests;

/// <summary>
/// The glTF and VRM Animation files say the same thing as the solver's joints, read back by
/// a reader written from the format's definition.
/// </summary>
public class GltfTests
{
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void A_reader_of_the_glb_file_finds_the_same_joints(string name)
    {
        var clip = Samples.Compile(name).Clip;
        var file = GltfFile.Parse(Gltf.Render(clip));
        // With the bones of the fingers when a hand's row is danced anywhere in the file (DESIGN.md 126).
        var skeleton = Skeleton.BonesWith(clip.Figures.Any(f => f.Hands is not null));
        int bones = skeleton.Length;

        // After the bones, a node for each figure, which is what the scene holds.
        Assert.Equal(clip.Figures.Count * (bones + 1), file.Json["nodes"]!.AsArray().Count);
        Assert.Equal(clip.Figures.Select(f => f.Id), file.Json["scenes"]![0]!["nodes"]!.AsArray().Select(n => (string)file.Json["nodes"]![(int)n!]!["name"]!));
        Assert.Equal(clip.Figures.Select(f => f.Id), file.Json["skins"]!.AsArray().Select(s => (string)s!["name"]!));

        var times = file.Floats((int)file.Json["animations"]![0]!["samplers"]![0]!["input"]!);
        Assert.Equal(clip.Figures[0].Frames.Count, times.Length);
        Assert.Equal(1.0 / clip.Fps, times[1], 6);

        for (int i = 0; i < clip.Figures.Count; i++)
        {
            var figure = clip.Figures[i];
            Assert.Equal(skeleton.Select(b => b.Name), Enumerable.Range(i * bones, bones).Select(n => (string)file.Json["nodes"]![n]!["name"]!));
            for (int f = 0; f < figure.Frames.Count; f++)
            {
                var (positions, turns) = file.Pose(f);
                var heads = positions[(i * bones)..((i + 1) * bones)];
                // glTF has no end sites: where a leaf bone ends is its rest offset, turned as the file says.
                var ends = Skeleton.Bones.Select((bone, b) => bone.End is { } end ? heads[b] + turns[i * bones + b].Rotate(end) : (Vec3?)null).ToArray();
                SkeletonTests.Compare(name, f, figure, heads, ends);
            }
        }
    }

    [Fact]
    public void A_skin_names_the_bones_and_binds_each_where_it_rests()
    {
        var file = GltfFile.Parse(Gltf.Render(ShosaCompilation.Compile(Samples.Walk).Clip));
        var skin = file.Json["skins"]![0]!;
        var inverseBind = file.Floats((int)skin["inverseBindMatrices"]!);
        var (rest, _) = file.Pose(-1);

        Assert.Equal(Enumerable.Range(0, Skeleton.Bones.Length), skin["joints"]!.AsArray().Select(j => (int)j!));
        for (int b = 0; b < Skeleton.Bones.Length; b++)
        {
            // Column-major: the last column moves the bone's resting place to the origin.
            var m = inverseBind[(b * 16)..(b * 16 + 16)];
            Assert.Equal([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0], m[..12]);
            Assert.True((rest[b] + new Vec3(m[12], m[13], m[14])).Length < 1e-6, Skeleton.Bones[b].Name);
        }
    }

    [Fact]
    public void Text_and_binary_hold_the_same_data()
    {
        var clip = ShosaCompilation.Compile(Samples.Walk).Clip;
        GltfFile binary = GltfFile.Parse(Gltf.Render(clip)), text = GltfFile.Parse(Gltf.Render(clip, binary: false));

        Assert.Equal(binary.Bin, text.Bin);
        text.Json["buffers"]![0]!.AsObject().Remove("uri");
        Assert.Equal(binary.Json.ToJsonString(), text.Json.ToJsonString());
    }

    [Fact]
    public void A_vrma_file_names_every_humanoid_bone_and_holds_one_figure()
    {
        var clip = Samples.Compile("duet").Clip;
        Assert.True(clip.Figures.Count > 1);

        foreach (var figure in clip.Figures)
        {
            var file = GltfFile.Parse(Gltf.RenderVrma(clip, figure));
            var extension = file.Json["extensions"]!["VRMC_vrm_animation"]!;
            var humanBones = extension["humanoid"]!["humanBones"]!.AsObject();

            Assert.Equal("1.0", (string)extension["specVersion"]!);
            Assert.Contains("VRMC_vrm_animation", file.Json["extensionsUsed"]!.AsArray().Select(e => (string)e!));
            Assert.Null(file.Json["skins"]);
            Assert.Equal(Skeleton.Bones.Length, file.Json["nodes"]!.AsArray().Count);
            Assert.Equal(Skeleton.Bones.Select(b => b.Name).Order(), humanBones.Select(b => b.Key).Order());
            foreach (var (bone, target) in humanBones)
                Assert.Equal(bone, (string)file.Json["nodes"]![(int)target!["node"]!]!["name"]!);

            // The hips move as this figure's pelvis does, not the other figure's.
            var (heads, _) = file.Pose(figure.Frames.Count - 1);
            Assert.True((heads[0] - figure.Joint(figure.Frames.Count - 1, Rig.Pelvis)).Length < 1e-5);
        }
    }

    [Fact]
    public void Rotations_are_unit_quaternions_that_do_not_jump_to_the_other_sign()
    {
        var clip = Samples.Compile("taiso").Clip;
        var file = GltfFile.Parse(Gltf.Render(clip));
        var animation = file.Json["animations"]![0]!;

        foreach (var channel in animation["channels"]!.AsArray())
        {
            if ((string)channel!["target"]!["path"]! != "rotation")
                continue;
            var q = file.Floats((int)animation["samplers"]![(int)channel["sampler"]!]!["output"]!);
            for (int f = 0; f < q.Length / 4; f++)
            {
                var now = q.AsSpan(f * 4, 4);
                Assert.Equal(1, Math.Sqrt(now[0] * now[0] + now[1] * now[1] + now[2] * now[2] + now[3] * now[3]), 5);
                if (f > 0)
                    Assert.True(now[0] * q[f * 4 - 4] + now[1] * q[f * 4 - 3] + now[2] * q[f * 4 - 2] + now[3] * q[f * 4 - 1] > 0, $"sign flips at frame {f}");
            }
        }
    }

    /// <summary>A glTF reader written from the format's definition, not from the writer.</summary>
    private sealed class GltfFile
    {
        public JsonNode Json { get; private init; } = null!;
        public byte[] Bin { get; private init; } = [];

        public static GltfFile Parse(byte[] bytes)
        {
            if (Encoding.ASCII.GetString(bytes, 0, 4) != "glTF")
            {
                var json = JsonNode.Parse(bytes)!;
                string uri = (string)json["buffers"]![0]!["uri"]!;
                return new GltfFile { Json = json, Bin = Convert.FromBase64String(uri[(uri.IndexOf(',') + 1)..]) };
            }

            Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
            Assert.Equal((uint)bytes.Length, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
            int textLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
            Assert.Equal("JSON", Encoding.ASCII.GetString(bytes, 16, 4));
            int binLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(20 + textLength));
            Assert.Equal("BIN\0", Encoding.ASCII.GetString(bytes, 24 + textLength, 4));
            Assert.Equal(0, textLength % 4);
            Assert.Equal(0, binLength % 4);
            Assert.Equal(bytes.Length, 28 + textLength + binLength);

            var file = new GltfFile
            {
                Json = JsonNode.Parse(bytes.AsSpan(20, textLength))!,
                Bin = bytes[(28 + textLength)..],
            };
            Assert.Equal(binLength, (int)file.Json["buffers"]![0]!["byteLength"]!);
            return file;
        }

        public float[] Floats(int accessor)
        {
            var a = Json["accessors"]![accessor]!;
            var view = Json["bufferViews"]![(int)a["bufferView"]!]!;
            Assert.Equal(5126, (int)a["componentType"]!);
            int width = (string)a["type"]! switch { "SCALAR" => 1, "VEC3" => 3, "VEC4" => 4, "MAT4" => 16, var other => throw new InvalidOperationException(other) };
            int offset = (int)(view["byteOffset"] ?? 0) + (int)(a["byteOffset"] ?? 0), count = (int)a["count"]! * width;
            Assert.True(count * 4 <= (int)view["byteLength"]!);

            var values = new float[count];
            for (int i = 0; i < count; i++)
                values[i] = BinaryPrimitives.ReadSingleLittleEndian(Bin.AsSpan(offset + i * 4));
            if (a["min"] is { } min)
            {
                Assert.Equal(values.Min(), (float)min[0]!);
                Assert.Equal(values.Max(), (float)a["max"]![0]!);
            }
            return values;
        }

        /// <summary>
        /// Where every node is in the world and how it is turned at one frame (the rest pose for
        /// frame -1): each node's own translation and rotation, the animation's where it has a
        /// channel for them, applied inside its parent's.
        /// </summary>
        public (Vec3[] Positions, Quat[] Turns) Pose(int frame)
        {
            var nodes = Json["nodes"]!.AsArray();
            var moves = new Vec3[nodes.Count];
            var local = new Quat[nodes.Count];
            for (int n = 0; n < nodes.Count; n++)
            {
                var t = nodes[n]!["translation"];
                moves[n] = t is null ? default : new Vec3((double)t[0]!, (double)t[1]!, (double)t[2]!);
                Assert.Null(nodes[n]!["rotation"]);
                local[n] = Quat.Identity;
            }

            if (frame >= 0)
            {
                var animation = Json["animations"]![0]!;
                foreach (var channel in animation["channels"]!.AsArray())
                {
                    var sampler = animation["samplers"]![(int)channel!["sampler"]!]!;
                    var v = Floats((int)sampler["output"]!);
                    int node = (int)channel["target"]!["node"]!;
                    if ((string)channel["target"]!["path"]! == "translation")
                        moves[node] = new Vec3(v[frame * 3], v[frame * 3 + 1], v[frame * 3 + 2]);
                    else
                        local[node] = new Quat(v[frame * 4], v[frame * 4 + 1], v[frame * 4 + 2], v[frame * 4 + 3]);
                }
            }

            var positions = new Vec3[nodes.Count];
            var turns = new Quat[nodes.Count];
            foreach (var root in Json["scenes"]![(int)Json["scene"]!]!["nodes"]!.AsArray())
                Place((int)root!, default, Quat.Identity);
            return (positions, turns);

            void Place(int node, Vec3 parentPosition, Quat parentTurn)
            {
                positions[node] = parentPosition + parentTurn.Rotate(moves[node]);
                turns[node] = parentTurn * local[node];
                foreach (var child in nodes[node]!["children"]?.AsArray() ?? [])
                    Place((int)child!, positions[node], turns[node]);
            }
        }
    }
}
