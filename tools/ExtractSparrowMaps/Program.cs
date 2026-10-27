// タビスズメ (m8217 b0001) のテクスチャと、テクスチャの各画素が体のどこにあたるかを書き出す。
// tools/winter_texture.py が、これを使って冬羽のテクスチャを塗る。
//
// 使い方: dotnet run --project tools/ExtractSparrowMaps -- <ゲームの sqpack フォルダ> <出力フォルダ>
// 出力:
//   d.bgra / n.bgra / s.bgra  元のテクスチャ (256x256、BGRA 8bit)
//   maps.f32                  画素ごとに [ボーン番号, 位置 x, y, z, 法線 x, y, z] (float32、ボーン番号 -1 は未使用の画素)
//   bones.txt                 ボーン番号と名前
using System.Numerics;
using Lumina;
using Lumina.Data.Files;
using VT = Lumina.Models.Models.Vertex.VertexType;
using VU = Lumina.Models.Models.Vertex.VertexUsage;

const string Model = "chara/monster/m8217/obj/body/b0001/model/m8217b0001.mdl";
const string TexturePrefix = "chara/monster/m8217/obj/body/b0001/texture/v01_m8217b0001_";
const int Size = 256;

if (args.Length != 2)
{
    Console.Error.WriteLine("使い方: ExtractSparrowMaps <sqpack フォルダ> <出力フォルダ>");
    return 1;
}

var game = new GameData(args[0]);
var output = Directory.CreateDirectory(args[1]).FullName;

foreach (var kind in new[] { "d", "n", "s" })
{
    var tex = game.GetFile<TexFile>(TexturePrefix + kind + ".tex") ?? throw new FileNotFoundException(kind);
    if (tex.Header.Width != Size || tex.Header.Height != Size)
        throw new InvalidDataException($"{kind}: {tex.Header.Width}x{tex.Header.Height}");
    File.WriteAllBytes(Path.Combine(output, kind + ".bgra"), tex.ImageData[..(Size * Size * 4)]);
}

var mdl = new Penumbra.GameData.Files.MdlFile(game.GetFile(Model)!.Data);
var data = mdl.RemainingData;
var mesh = mdl.Meshes[mdl.Lods[0].MeshIndex];
var elements = mdl.VertexDeclarations[mdl.Lods[0].MeshIndex].VertexElements;
var boneTable = mdl.BoneTables[mesh.BoneTableIndex];
var bones = boneTable.BoneIndex.Take((int)boneTable.BoneCount).Select(i => mdl.Bones[(int)i]).ToArray();

int Offset(Lumina.Data.Parsing.MdlStructs.VertexElement e, int vertex) =>
    (int)(mdl.VertexOffset[0] + mesh.VertexBufferOffset(e.Stream)) + vertex * mesh.VertexBufferStride(e.Stream) + e.Offset;

Vector3 Read3(Lumina.Data.Parsing.MdlStructs.VertexElement e, int vertex)
{
    var o = Offset(e, vertex);
    return (VT)e.Type is VT.Single3 or VT.Single4
        ? new Vector3(BitConverter.ToSingle(data, o), BitConverter.ToSingle(data, o + 4), BitConverter.ToSingle(data, o + 8))
        : new Vector3((float)BitConverter.ToHalf(data, o), (float)BitConverter.ToHalf(data, o + 2), (float)BitConverter.ToHalf(data, o + 4));
}

var position = elements.First(e => (VU)e.Usage == VU.Position);
var normal = elements.First(e => (VU)e.Usage == VU.Normal);
var uvElement = elements.First(e => (VU)e.Usage == VU.UV);
var weights = elements.First(e => (VU)e.Usage == VU.BlendWeights);
var indices = elements.First(e => (VU)e.Usage == VU.BlendIndices);

var pos = new Vector3[mesh.VertexCount];
var nrm = new Vector3[mesh.VertexCount];
var uv = new Vector2[mesh.VertexCount];
var bone = new int[mesh.VertexCount];
for (var v = 0; v < mesh.VertexCount; v++)
{
    pos[v] = Read3(position, v);
    nrm[v] = Vector3.Normalize(Read3(normal, v));
    var u = Read3(uvElement, v);
    uv[v] = new Vector2(u.X, u.Y);
    // いちばん重みの大きいボーン
    var w = Offset(weights, v);
    var best = 0;
    for (var k = 1; k < 4; k++)
    {
        if (data[w + k] > data[w + best])
            best = k;
    }

    bone[v] = data[Offset(indices, v) + best];
}

// 三角形をテクスチャの上に描き写し、画素ごとにボーン・位置・法線を求める
var maps = new float[Size * Size * 7];
for (var i = 0; i < Size * Size; i++)
    maps[i * 7] = -1f;

var indexStart = (int)mdl.IndexOffset[0] + (int)mesh.StartIndex * 2;
for (var i = 0; i < mesh.IndexCount; i += 3)
{
    var t = new int[3];
    for (var k = 0; k < 3; k++)
        t[k] = BitConverter.ToUInt16(data, indexStart + (i + k) * 2);

    var a = uv[t[0]] * Size;
    var b = uv[t[1]] * Size;
    var c = uv[t[2]] * Size;
    var area = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
    if (MathF.Abs(area) < 1e-6f)
        continue;

    var minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
    var maxX = Math.Min(Size - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
    var minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
    var maxY = Math.Min(Size - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
    for (var y = minY; y <= maxY; y++)
    {
        for (var x = minX; x <= maxX; x++)
        {
            var px = x + 0.5f;
            var py = y + 0.5f;
            var w0 = ((b.X - px) * (c.Y - py) - (c.X - px) * (b.Y - py)) / area;
            var w1 = ((c.X - px) * (a.Y - py) - (a.X - px) * (c.Y - py)) / area;
            var w2 = 1f - w0 - w1;
            // 少しはみ出して描き、島の縁に隙間ができないようにする
            if (w0 < -0.02f || w1 < -0.02f || w2 < -0.02f)
                continue;

            var dominant = w0 >= w1 && w0 >= w2 ? t[0] : w1 >= w2 ? t[1] : t[2];
            var p = pos[t[0]] * w0 + pos[t[1]] * w1 + pos[t[2]] * w2;
            var n = Vector3.Normalize(nrm[t[0]] * w0 + nrm[t[1]] * w1 + nrm[t[2]] * w2);
            var o = (y * Size + x) * 7;
            maps[o] = bone[dominant];
            maps[o + 1] = p.X;
            maps[o + 2] = p.Y;
            maps[o + 3] = p.Z;
            maps[o + 4] = n.X;
            maps[o + 5] = n.Y;
            maps[o + 6] = n.Z;
        }
    }
}

var bytes = new byte[maps.Length * 4];
Buffer.BlockCopy(maps, 0, bytes, 0, bytes.Length);
File.WriteAllBytes(Path.Combine(output, "maps.f32"), bytes);
File.WriteAllLines(Path.Combine(output, "bones.txt"), bones.Select((name, i) => $"{i} {name}"));
Console.WriteLine($"{output} に書き出しました (頂点 {mesh.VertexCount}、三角形 {mesh.IndexCount / 3})");
return 0;
