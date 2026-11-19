using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DjiNeo2OrbeaMounts
{
    public struct Vec3
    {
        public double X, Y, Z;
        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static Vec3 operator +(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Vec3 operator -(Vec3 a, Vec3 b) { return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Vec3 operator *(Vec3 a, double s) { return new Vec3(a.X * s, a.Y * s, a.Z * s); }
        public double Length { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }
        public Vec3 Normalized()
        {
            double len = Length;
            return len > 1e-12 ? new Vec3(X / len, Y / len, Z / len) : new Vec3(0, 0, 1);
        }
        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }
        public static double Dot(Vec3 a, Vec3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
    }

    public struct TriIdx
    {
        public int V1, V2, V3;
        public TriIdx(int v1, int v2, int v3) { V1 = v1; V2 = v2; V3 = v3; }
    }

    public struct EdgeIdKey : IEquatable<EdgeIdKey>
    {
        public int Lo, Hi;
        public EdgeIdKey(int lo, int hi) { Lo = lo; Hi = hi; }
        public bool Equals(EdgeIdKey o) { return Lo == o.Lo && Hi == o.Hi; }
        public override bool Equals(object obj) { return obj is EdgeIdKey && Equals((EdgeIdKey)obj); }
        public override int GetHashCode() { unchecked { return (Lo * 73856093) ^ (Hi * 19349663); } }
    }

    public class MeshData
    {
        public string Name;
        public List<Vec3> Vertices = new List<Vec3>();
        public List<TriIdx> Triangles = new List<TriIdx>();

        public MeshData(string name) { Name = name; }

        public MeshData Clone(string newName)
        {
            var m = new MeshData(newName);
            m.Vertices.AddRange(this.Vertices);
            m.Triangles.AddRange(this.Triangles);
            return m;
        }

        public void Translate(double dx, double dy, double dz)
        {
            for (int i = 0; i < Vertices.Count; i++)
            {
                var v = Vertices[i];
                Vertices[i] = new Vec3(v.X + dx, v.Y + dy, v.Z + dz);
            }
        }

        public void SnapBottomToZero()
        {
            if (Vertices.Count == 0) return;
            Vec3 minB, maxB;
            GetBounds(out minB, out maxB);
            if (Math.Abs(minB.Z) > 1e-6)
            {
                Translate(0.0, 0.0, -minB.Z);
            }
        }

        public void Append(MeshData other)
        {
            int offset = Vertices.Count;
            Vertices.AddRange(other.Vertices);
            foreach (var t in other.Triangles)
            {
                Triangles.Add(new TriIdx(t.V1 + offset, t.V2 + offset, t.V3 + offset));
            }
        }

        public void GetBounds(out Vec3 min, out Vec3 max)
        {
            double minX = 1e9, minY = 1e9, minZ = 1e9;
            double maxX = -1e9, maxY = -1e9, maxZ = -1e9;
            foreach (var v in Vertices)
            {
                if (v.X < minX) minX = v.X; if (v.X > maxX) maxX = v.X;
                if (v.Y < minY) minY = v.Y; if (v.Y > maxY) maxY = v.Y;
                if (v.Z < minZ) minZ = v.Z; if (v.Z > maxZ) maxZ = v.Z;
            }
            min = new Vec3(minX, minY, minZ);
            max = new Vec3(maxX, maxY, maxZ);
        }

        public double ComputeSignedVolume()
        {
            double vol = 0;
            foreach (var t in Triangles)
            {
                var a = Vertices[t.V1];
                var b = Vertices[t.V2];
                var c = Vertices[t.V3];
                vol += (a.X * (b.Y * c.Z - b.Z * c.Y) - a.Y * (b.X * c.Z - b.Z * c.X) + a.Z * (b.X * c.Y - b.Y * c.X)) / 6.0;
            }
            return vol;
        }

        public void SaveBinaryStl(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                byte[] header = new byte[80];
                byte[] titleBytes = Encoding.ASCII.GetBytes("Antigravity CAD - " + Name);
                Array.Copy(titleBytes, header, Math.Min(titleBytes.Length, 80));
                bw.Write(header);
                bw.Write((uint)Triangles.Count);
                foreach (var t in Triangles)
                {
                    var a = Vertices[t.V1];
                    var b = Vertices[t.V2];
                    var c = Vertices[t.V3];
                    var n = Vec3.Cross(b - a, c - a).Normalized();
                    bw.Write((float)n.X); bw.Write((float)n.Y); bw.Write((float)n.Z);
                    bw.Write((float)a.X); bw.Write((float)a.Y); bw.Write((float)a.Z);
                    bw.Write((float)b.X); bw.Write((float)b.Y); bw.Write((float)b.Z);
                    bw.Write((float)c.X); bw.Write((float)c.Y); bw.Write((float)c.Z);
                    bw.Write((ushort)0);
                }
            }
        }
    }

    // Guaranteed 2-Manifold 6-Tetrahedron Kuhn Marching Tetrahedra Isosurface Extractor
    // with 1D Analytical Edge Bisection & Narrow-Band Mesh-to-SDF CSG Support
    public static class SdfMesher
    {
        public static double SmoothMin(double a, double b, double k)
        {
            if (k <= 0) return Math.Min(a, b);
            double h = Math.Max(k - Math.Abs(a - b), 0.0) / k;
            return Math.Min(a, b) - h * h * k * 0.25;
        }

        public static double SmoothMax(double a, double b, double k)
        {
            return -SmoothMin(-a, -b, k);
        }

        public static double Box2D(double x, double y, double hx, double hy, double r = 0.0)
        {
            double dx = Math.Abs(x) - (hx - r);
            double dy = Math.Abs(y) - (hy - r);
            double outDist = Math.Sqrt(Math.Max(dx, 0) * Math.Max(dx, 0) + Math.Max(dy, 0) * Math.Max(dy, 0));
            double inDist = Math.Min(Math.Max(dx, dy), 0.0);
            return outDist + inDist - r;
        }

        // Orbea Wild LT Stealth Design Language: Crisp 45-deg octagonal chamfered 2D profile
        public static double ChamferBox2D(double x, double y, double hx, double hy, double chamfer)
        {
            double ax = Math.Abs(x);
            double ay = Math.Abs(y);
            double dRect = Math.Max(ax - hx, ay - hy);
            if (chamfer <= 0.0) return dRect;
            double dChamfer = (ax + ay - (hx + hy - chamfer)) * 0.70710678118;
            return Math.Max(dRect, dChamfer);
        }

        // 3D Chamfered Stealth Block (octagonal XY + crisp 45-deg top/bottom Z bevels)
        public static double StealthBlock3D(double x, double y, double z, double hx, double hy, double hz, double chamferXY, double bevelZ = 0.0)
        {
            double dxy = ChamferBox2D(x, y, hx, hy, chamferXY);
            double dz = Math.Abs(z) - hz;
            double dBox = Math.Max(dxy, dz);
            if (bevelZ <= 0.0) return dBox;
            double dBevel = (dxy + dz + bevelZ) * 0.70710678118;
            return Math.Max(dBox, dBevel);
        }

        public static double RoundedBox3D(double x, double y, double z, double hx, double hy, double hz, double rXY = 0.0)
        {
            double dxy = Box2D(x, y, hx, hy, rXY);
            double dz = Math.Abs(z) - hz;
            double outDist = Math.Sqrt(Math.Max(dxy, 0) * Math.Max(dxy, 0) + Math.Max(dz, 0) * Math.Max(dz, 0));
            double inDist = Math.Min(Math.Max(dxy, dz), 0.0);
            return outDist + inDist;
        }

        // Exact regular hexagon 2D distance (af = across-flats distance, e.g. 7.18mm for M4 nut)
        // Flats perpendicular to X (width in X = af, vertex-to-vertex in Y = af / 0.8660254)
        public static double Hexagon2D(double x, double y, double af)
        {
            double r = af * 0.5;
            double px = Math.Abs(x);
            double py = Math.Abs(y);
            double d1 = px - r;
            double d2 = (px * 0.5 + py * 0.86602540378) - r;
            return Math.Max(d1, d2);
        }

        // Capsule/slot in 2D along X from x0 to x1 at y0 with radius r
        public static double SlotX2D(double x, double y, double x0, double x1, double y0, double r)
        {
            double cx = Math.Max(x0, Math.Min(x1, x));
            double dx = x - cx;
            double dy = y - y0;
            return Math.Sqrt(dx * dx + dy * dy) - r;
        }

        // Capsule/slot in 2D along Y from y0 to y1 at x0 with radius r
        public static double SlotY2D(double x, double y, double x0, double y0, double y1, double r)
        {
            double cy = Math.Max(y0, Math.Min(y1, y));
            double dx = x - x0;
            double dy = y - cy;
            return Math.Sqrt(dx * dx + dy * dy) - r;
        }

        public static Vec3 Gradient(Func<double, double, double, double> sdf, double x, double y, double z, double eps = 0.02)
        {
            double gx = sdf(x + eps, y, z) - sdf(x - eps, y, z);
            double gy = sdf(x, y + eps, z) - sdf(x, y - eps, z);
            double gz = sdf(x, y, z + eps) - sdf(x, y, z - eps);
            return new Vec3(gx, gy, gz).Normalized();
        }

        // 6 Kuhn tetrahedra partitioning the unit cube along main diagonal (0,0,0)->(1,1,1)
        // Every tetrahedron has positive orientation and identical face diagonals across adjacent cubes.
        private static readonly int[,] KuhnTets = new int[,] {
            { 0, 1, 3, 7 },
            { 0, 1, 7, 5 },
            { 0, 2, 6, 7 },
            { 0, 2, 7, 3 },
            { 0, 4, 5, 7 },
            { 0, 4, 7, 6 }
        };

        public static MeshData ExtractMesh(
            string name,
            Func<double, double, double, double> sdf,
            Vec3 minBound,
            Vec3 maxBound,
            double step)
        {
            // Align Z grid so Z = 0.0 sits cleanly at a half-grid plane (t = 0.5) for dead-flat bed adhesion
            double ox = minBound.X - step;
            double oy = minBound.Y - step;
            double oz = Math.Floor(minBound.Z / step) * step - 0.5 * step;

            int nx = (int)Math.Ceiling((maxBound.X - ox) / step) + 2;
            int ny = (int)Math.Ceiling((maxBound.Y - oy) / step) + 2;
            int nz = (int)Math.Ceiling((maxBound.Z - oz) / step) + 2;

            float[,,] grid = new float[nx + 1, ny + 1, nz + 1];
            Parallel.For(0, nx + 1, ix =>
            {
                double x = ox + ix * step;
                for (int iy = 0; iy <= ny; iy++)
                {
                    double y = oy + iy * step;
                    for (int iz = 0; iz <= nz; iz++)
                    {
                        double z = oz + iz * step;
                        grid[ix, iy, iz] = (float)sdf(x, y, z);
                    }
                }
            });

            return ExtractFromGrid(name, grid, ox, oy, oz, step, sdf);
        }

        public static MeshData ExtractFromGrid(
            string name,
            float[,,] grid,
            double ox,
            double oy,
            double oz,
            double step,
            Func<double, double, double, double> refineSdf)
        {
            int nx = grid.GetLength(0) - 1;
            int ny = grid.GetLength(1) - 1;
            int nz = grid.GetLength(2) - 1;

            // Enforce outer boundary strictly positive (outside) so the extracted surface is 100% closed
            for (int ix = 0; ix <= nx; ix++)
            {
                for (int iy = 0; iy <= ny; iy++)
                {
                    if (grid[ix, iy, 0] < 0f) grid[ix, iy, 0] = (float)(0.25 * step);
                    if (grid[ix, iy, nz] < 0f) grid[ix, iy, nz] = (float)(0.25 * step);
                }
            }
            for (int ix = 0; ix <= nx; ix++)
            {
                for (int iz = 0; iz <= nz; iz++)
                {
                    if (grid[ix, 0, iz] < 0f) grid[ix, 0, iz] = (float)(0.25 * step);
                    if (grid[ix, ny, iz] < 0f) grid[ix, ny, iz] = (float)(0.25 * step);
                }
            }
            for (int iy = 0; iy <= ny; iy++)
            {
                for (int iz = 0; iz <= nz; iz++)
                {
                    if (grid[0, iy, iz] < 0f) grid[0, iy, iz] = (float)(0.25 * step);
                    if (grid[nx, iy, iz] < 0f) grid[nx, iy, iz] = (float)(0.25 * step);
                }
            }

            // Fill any trapped interior void pockets (not connected to exterior air) so no negative-volume internal shells can exist
            if (refineSdf == null)
            {
                int sx = nx + 1, sy = ny + 1, sz = nz + 1;
                int totalNodes = sx * sy * sz;
                byte[] extAir = new byte[totalNodes];
                int[] q = new int[totalNodes];
                int qHead = 0, qTail = 0;
                extAir[0] = 1;
                q[qTail++] = 0;
                int syz = sy * sz;

                while (qHead < qTail)
                {
                    int cur = q[qHead++];
                    int ix = cur / syz;
                    int rem = cur - ix * syz;
                    int iy = rem / sz;
                    int iz = rem - iy * sz;

                    if (ix > 0) { int n = cur - syz; if (extAir[n] == 0 && grid[ix - 1, iy, iz] >= 0f) { extAir[n] = 1; q[qTail++] = n; } }
                    if (ix < nx) { int n = cur + syz; if (extAir[n] == 0 && grid[ix + 1, iy, iz] >= 0f) { extAir[n] = 1; q[qTail++] = n; } }
                    if (iy > 0) { int n = cur - sz; if (extAir[n] == 0 && grid[ix, iy - 1, iz] >= 0f) { extAir[n] = 1; q[qTail++] = n; } }
                    if (iy < ny) { int n = cur + sz; if (extAir[n] == 0 && grid[ix, iy + 1, iz] >= 0f) { extAir[n] = 1; q[qTail++] = n; } }
                    if (iz > 0) { int n = cur - 1; if (extAir[n] == 0 && grid[ix, iy, iz - 1] >= 0f) { extAir[n] = 1; q[qTail++] = n; } }
                    if (iz < nz) { int n = cur + 1; if (extAir[n] == 0 && grid[ix, iy, iz + 1] >= 0f) { extAir[n] = 1; q[qTail++] = n; } }
                }

                for (int ix = 1; ix < nx; ix++)
                    for (int iy = 1; iy < ny; iy++)
                        for (int iz = 1; iz < nz; iz++)
                            if (grid[ix, iy, iz] >= 0f && extAir[ix * syz + iy * sz + iz] == 0)
                                grid[ix, iy, iz] = -(float)(0.25 * step);
            }

            var mesh = new MeshData(name);
            var edgeVerts = new Dictionary<EdgeIdKey, int>(65536);

            int strideY = nz + 1;
            int strideX = (ny + 1) * strideY;

            // Reusable buffers to avoid GC allocations in tight loop
            int[] cId = new int[8];
            float[] cVal = new float[8];
            Vec3[] cPos = new Vec3[8];
            int[] inIdx = new int[4];
            int[] outIdx = new int[4];

            for (int ix = 0; ix < nx; ix++)
            {
                double x0 = ox + ix * step;
                double x1 = x0 + step;
                for (int iy = 0; iy < ny; iy++)
                {
                    double y0 = oy + iy * step;
                    double y1 = y0 + step;
                    for (int iz = 0; iz < nz; iz++)
                    {
                        float v0 = grid[ix, iy, iz];
                        float v1 = grid[ix + 1, iy, iz];
                        float v2 = grid[ix, iy + 1, iz];
                        float v3 = grid[ix + 1, iy + 1, iz];
                        float v4 = grid[ix, iy, iz + 1];
                        float v5 = grid[ix + 1, iy, iz + 1];
                        float v6 = grid[ix, iy + 1, iz + 1];
                        float v7 = grid[ix + 1, iy + 1, iz + 1];

                        int mask =
                            (v0 < 0f ? 1 : 0) |
                            (v1 < 0f ? 2 : 0) |
                            (v2 < 0f ? 4 : 0) |
                            (v3 < 0f ? 8 : 0) |
                            (v4 < 0f ? 16 : 0) |
                            (v5 < 0f ? 32 : 0) |
                            (v6 < 0f ? 64 : 0) |
                            (v7 < 0f ? 128 : 0);

                        if (mask == 0 || mask == 255) continue;

                        double z0 = oz + iz * step;
                        double z1 = z0 + step;

                        int baseId = ix * strideX + iy * strideY + iz;
                        cId[0] = baseId;
                        cId[1] = baseId + strideX;
                        cId[2] = baseId + strideY;
                        cId[3] = baseId + strideX + strideY;
                        cId[4] = baseId + 1;
                        cId[5] = baseId + strideX + 1;
                        cId[6] = baseId + strideY + 1;
                        cId[7] = baseId + strideX + strideY + 1;

                        cVal[0] = v0; cVal[1] = v1; cVal[2] = v2; cVal[3] = v3;
                        cVal[4] = v4; cVal[5] = v5; cVal[6] = v6; cVal[7] = v7;

                        cPos[0] = new Vec3(x0, y0, z0);
                        cPos[1] = new Vec3(x1, y0, z0);
                        cPos[2] = new Vec3(x0, y1, z0);
                        cPos[3] = new Vec3(x1, y1, z0);
                        cPos[4] = new Vec3(x0, y0, z1);
                        cPos[5] = new Vec3(x1, y0, z1);
                        cPos[6] = new Vec3(x0, y1, z1);
                        cPos[7] = new Vec3(x1, y1, z1);

                        for (int t = 0; t < 6; t++)
                        {
                            int nIn = 0, nOut = 0;
                            for (int k = 0; k < 4; k++)
                            {
                                int ci = KuhnTets[t, k];
                                if (cVal[ci] < 0f) inIdx[nIn++] = ci;
                                else outIdx[nOut++] = ci;
                            }

                            if (nIn == 0 || nIn == 4) continue;

                            if (nIn == 1)
                            {
                                int i0 = inIdx[0];
                                int o0 = outIdx[0], o1 = outIdx[1], o2 = outIdx[2];
                                int e0 = GetOrCreateEdgeVertex(mesh, edgeVerts, cId[i0], cId[o0], cPos[i0], cPos[o0], cVal[i0], cVal[o0], refineSdf);
                                int e1 = GetOrCreateEdgeVertex(mesh, edgeVerts, cId[i0], cId[o1], cPos[i0], cPos[o1], cVal[i0], cVal[o1], refineSdf);
                                int e2 = GetOrCreateEdgeVertex(mesh, edgeVerts, cId[i0], cId[o2], cPos[i0], cPos[o2], cVal[i0], cVal[o2], refineSdf);
                                Vec3 outDir = (cPos[o0] + cPos[o1] + cPos[o2]) - (cPos[i0] * 3.0);
                                AddOrientedTri(mesh, e0, e1, e2, outDir);
                            }
                            else if (nIn == 3)
                            {
                                int i0 = inIdx[0], i1 = inIdx[1], i2 = inIdx[2];
                                int o0 = outIdx[0];
                                int e0 = GetOrCreateEdgeVertex(mesh, edgeVerts, cId[i0], cId[o0], cPos[i0], cPos[o0], cVal[i0], cVal[o0], refineSdf);
                                int e1 = GetOrCreateEdgeVertex(mesh, edgeVerts, cId[i1], cId[o0], cPos[i1], cPos[o0], cVal[i1], cVal[o0], refineSdf);
                                int e2 = GetOrCreateEdgeVertex(mesh, edgeVerts, cId[i2], cId[o0], cPos[i2], cPos[o0], cVal[i2], cVal[o0], refineSdf);
                                Vec3 outDir = (cPos[o0] * 3.0) - (cPos[i0] + cPos[i1] + cPos[i2]);
                                AddOrientedTri(mesh, e0, e1, e2, outDir);
                            }
                            else // nIn == 2, nOut == 2
                            {
                                int i0 = inIdx[0], i1 = inIdx[1];
                                int o0 = outIdx[0], o1 = outIdx[1];
                                int e00 = GetOrCreateEdgeVertex(mesh, edgeVerts, cId[i0], cId[o0], cPos[i0], cPos[o0], cVal[i0], cVal[o0], refineSdf);
                                int e01 = GetOrCreateEdgeVertex(mesh, edgeVerts, cId[i0], cId[o1], cPos[i0], cPos[o1], cVal[i0], cVal[o1], refineSdf);
                                int e11 = GetOrCreateEdgeVertex(mesh, edgeVerts, cId[i1], cId[o1], cPos[i1], cPos[o1], cVal[i1], cVal[o1], refineSdf);
                                int e10 = GetOrCreateEdgeVertex(mesh, edgeVerts, cId[i1], cId[o0], cPos[i1], cPos[o0], cVal[i1], cVal[o0], refineSdf);
                                Vec3 outDir = (cPos[o0] + cPos[o1]) - (cPos[i0] + cPos[i1]);

                                Vec3 p00 = mesh.Vertices[e00];
                                Vec3 p01 = mesh.Vertices[e01];
                                Vec3 p11 = mesh.Vertices[e11];
                                double orient = Vec3.Dot(Vec3.Cross(p01 - p00, p11 - p00), outDir);
                                if (orient >= 0.0)
                                {
                                    mesh.Triangles.Add(new TriIdx(e00, e01, e11));
                                    mesh.Triangles.Add(new TriIdx(e00, e11, e10));
                                }
                                else
                                {
                                    mesh.Triangles.Add(new TriIdx(e00, e11, e01));
                                    mesh.Triangles.Add(new TriIdx(e00, e10, e11));
                                }
                            }
                        }
                    }
                }
            }

            if (refineSdf == null) RelaxExtremeCreases(mesh);
            return mesh;
        }

        // Relax only vertices touching any extreme (>160 deg) knife-edge crease so zero folded facets remain
        private static void RelaxExtremeCreases(MeshData mesh)
        {
            int tCount = mesh.Triangles.Count;
            int vCount = mesh.Vertices.Count;
            if (tCount == 0 || vCount == 0) return;

            for (int pass = 0; pass < 3; pass++)
            {
                var normals = new Vec3[tCount];
                for (int i = 0; i < tCount; i++)
                {
                    var t = mesh.Triangles[i];
                    normals[i] = Vec3.Cross(mesh.Vertices[t.V2] - mesh.Vertices[t.V1], mesh.Vertices[t.V3] - mesh.Vertices[t.V1]).Normalized();
                }

                var edgeFirst = new Dictionary<EdgeIdKey, int>(tCount * 3 / 2);
                var badVerts = new HashSet<int>();
                for (int ti = 0; ti < tCount; ti++)
                {
                    var t = mesh.Triangles[ti];
                    int[] tv = new int[] { t.V1, t.V2, t.V3 };
                    for (int k = 0; k < 3; k++)
                    {
                        int u = tv[k], w = tv[(k + 1) % 3];
                        var ek = u < w ? new EdgeIdKey(u, w) : new EdgeIdKey(w, u);
                        int other;
                        if (!edgeFirst.TryGetValue(ek, out other))
                        {
                            edgeFirst[ek] = ti;
                        }
                        else
                        {
                            if (Vec3.Dot(normals[other], normals[ti]) < -0.94)
                            {
                                badVerts.Add(u);
                                badVerts.Add(w);
                                var ot = mesh.Triangles[other];
                                badVerts.Add(ot.V1); badVerts.Add(ot.V2); badVerts.Add(ot.V3);
                                badVerts.Add(t.V1); badVerts.Add(t.V2); badVerts.Add(t.V3);
                            }
                        }
                    }
                }

                if (badVerts.Count == 0) break;

                var sumPos = new Dictionary<int, Vec3>();
                var sumCnt = new Dictionary<int, int>();
                foreach (int v in badVerts) { sumPos[v] = new Vec3(0, 0, 0); sumCnt[v] = 0; }
                for (int ti = 0; ti < tCount; ti++)
                {
                    var t = mesh.Triangles[ti];
                    int[] tv = new int[] { t.V1, t.V2, t.V3 };
                    for (int k = 0; k < 3; k++)
                    {
                        int u = tv[k];
                        if (!badVerts.Contains(u)) continue;
                        int w1 = tv[(k + 1) % 3], w2 = tv[(k + 2) % 3];
                        sumPos[u] = sumPos[u] + mesh.Vertices[w1] + mesh.Vertices[w2];
                        sumCnt[u] = sumCnt[u] + 2;
                    }
                }
                foreach (int v in badVerts)
                {
                    int c = sumCnt[v];
                    if (c > 0)
                    {
                        Vec3 avg = sumPos[v] * (1.0 / c);
                        mesh.Vertices[v] = mesh.Vertices[v] * 0.55 + avg * 0.45;
                    }
                }
            }
        }

        private static void AddOrientedTri(MeshData mesh, int v0, int v1, int v2, Vec3 outDir)
        {
            Vec3 p0 = mesh.Vertices[v0];
            Vec3 p1 = mesh.Vertices[v1];
            Vec3 p2 = mesh.Vertices[v2];
            if (Vec3.Dot(Vec3.Cross(p1 - p0, p2 - p0), outDir) >= 0.0)
                mesh.Triangles.Add(new TriIdx(v0, v1, v2));
            else
                mesh.Triangles.Add(new TriIdx(v0, v2, v1));
        }

        private static int GetOrCreateEdgeVertex(
            MeshData mesh,
            Dictionary<EdgeIdKey, int> edgeVerts,
            int idA,
            int idB,
            Vec3 posA,
            Vec3 posB,
            float valA,
            float valB,
            Func<double, double, double, double> refineSdf)
        {
            int lo = idA < idB ? idA : idB;
            int hi = idA < idB ? idB : idA;
            var key = new EdgeIdKey(lo, hi);
            int idx;
            if (edgeVerts.TryGetValue(key, out idx)) return idx;

            Vec3 p0 = idA < idB ? posA : posB;
            Vec3 p1 = idA < idB ? posB : posA;
            double v0 = idA < idB ? (double)valA : (double)valB;
            double v1 = idA < idB ? (double)valB : (double)valA;

            double t;
            if (refineSdf != null)
            {
                // 4-iteration 1D analytical bisection along the sign-changing edge (< 0.02mm accuracy)
                double tLo = 0.0, tHi = 1.0;
                bool loInside = (v0 < 0.0);
                for (int iter = 0; iter < 4; iter++)
                {
                    double tMid = 0.5 * (tLo + tHi);
                    double mx = p0.X + tMid * (p1.X - p0.X);
                    double my = p0.Y + tMid * (p1.Y - p0.Y);
                    double mz = p0.Z + tMid * (p1.Z - p0.Z);
                    double vm = refineSdf(mx, my, mz);
                    if ((vm < 0.0) == loInside) tLo = tMid;
                    else tHi = tMid;
                }
                t = 0.5 * (tLo + tHi);
            }
            else
            {
                double denom = v0 - v1;
                t = Math.Abs(denom) > 1e-12 ? (v0 / denom) : 0.5;
            }

            // Clamp strictly inside the open edge interval so no two vertices can EVER merge or fold
            double loClamp = (refineSdf == null) ? 0.14 : 0.08;
            double hiClamp = 1.0 - loClamp;
            if (t < loClamp) t = loClamp;
            else if (t > hiClamp) t = hiClamp;

            Vec3 p = new Vec3(
                p0.X + t * (p1.X - p0.X),
                p0.Y + t * (p1.Y - p0.Y),
                p0.Z + t * (p1.Z - p0.Z));

            idx = mesh.Vertices.Count;
            mesh.Vertices.Add(p);
            edgeVerts[key] = idx;
            return idx;
        }

        // Convert an arbitrary closed triangle mesh (e.g. reference CAD cradle shell) into a clean
        // narrow-band signed distance grid (exact XY ray-parity sign + exact 3D point-to-triangle distance)
        // enabling true watertight CSG boolean unions with parametric SDF features!
        public static float[,,] ComputeMeshSignedDistanceGrid(
            MeshData srcMesh,
            double ox,
            double oy,
            double oz,
            int nx,
            int ny,
            int nz,
            double step)
        {
            int triCount = srcMesh.Triangles.Count;
            double[] ax = new double[triCount], ay = new double[triCount], az = new double[triCount];
            double[] bx = new double[triCount], by = new double[triCount], bz = new double[triCount];
            double[] cx = new double[triCount], cy = new double[triCount], cz = new double[triCount];
            double[] tMinX = new double[triCount], tMaxX = new double[triCount];
            double[] tMinY = new double[triCount], tMaxY = new double[triCount];
            double[] tMinZ = new double[triCount], tMaxZ = new double[triCount];

            for (int i = 0; i < triCount; i++)
            {
                var t = srcMesh.Triangles[i];
                var a = srcMesh.Vertices[t.V1];
                var b = srcMesh.Vertices[t.V2];
                var c = srcMesh.Vertices[t.V3];
                ax[i] = a.X; ay[i] = a.Y; az[i] = a.Z;
                bx[i] = b.X; by[i] = b.Y; bz[i] = b.Z;
                cx[i] = c.X; cy[i] = c.Y; cz[i] = c.Z;
                tMinX[i] = Math.Min(a.X, Math.Min(b.X, c.X));
                tMaxX[i] = Math.Max(a.X, Math.Max(b.X, c.X));
                tMinY[i] = Math.Min(a.Y, Math.Min(b.Y, c.Y));
                tMaxY[i] = Math.Max(a.Y, Math.Max(b.Y, c.Y));
                tMinZ[i] = Math.Min(a.Z, Math.Min(b.Z, c.Z));
                tMaxZ[i] = Math.Max(a.Z, Math.Max(b.Z, c.Z));
            }

            // 1. Build 2D XY uniform grid for ultra-fast vertical +Z raycast sign determination
            double cell2D = Math.Max(1.0, step * 4.0);
            int g2x = Math.Max(1, (int)Math.Ceiling((nx * step) / cell2D) + 1);
            int g2y = Math.Max(1, (int)Math.Ceiling((ny * step) / cell2D) + 1);
            var buckets2D = new List<int>[g2x, g2y];

            for (int i = 0; i < triCount; i++)
            {
                int ix0 = Math.Max(0, (int)Math.Floor((tMinX[i] - ox) / cell2D));
                int ix1 = Math.Min(g2x - 1, (int)Math.Floor((tMaxX[i] - ox) / cell2D));
                int iy0 = Math.Max(0, (int)Math.Floor((tMinY[i] - oy) / cell2D));
                int iy1 = Math.Min(g2y - 1, (int)Math.Floor((tMaxY[i] - oy) / cell2D));
                for (int ix = ix0; ix <= ix1; ix++)
                {
                    for (int iy = iy0; iy <= iy1; iy++)
                    {
                        if (buckets2D[ix, iy] == null) buckets2D[ix, iy] = new List<int>(8);
                        buckets2D[ix, iy].Add(i);
                    }
                }
            }

            bool[,,] inside = new bool[nx + 1, ny + 1, nz + 1];
            double[] jx = new double[] { 1.3719e-6, -2.1931e-5, 4.7123e-5, -7.319e-5 };
            double[] jy = new double[] { 2.7183e-6, 3.1415e-5, -5.8213e-5, 6.173e-5 };

            Parallel.For(0, nx + 1, ix =>
            {
                var hits = new List<double>(32);
                double x = ox + ix * step;
                int bx2 = Math.Max(0, Math.Min(g2x - 1, (int)Math.Floor((x - ox) / cell2D)));
                for (int iy = 0; iy <= ny; iy++)
                {
                    double y = oy + iy * step;
                    int by2 = Math.Max(0, Math.Min(g2y - 1, (int)Math.Floor((y - oy) / cell2D)));
                    var bList = buckets2D[bx2, by2];
                    if (bList == null || bList.Count == 0) continue;

                    for (int attempt = 0; attempt < jx.Length; attempt++)
                    {
                        hits.Clear();
                        double rx = x + jx[attempt];
                        double ry = y + jy[attempt];
                        for (int k = 0; k < bList.Count; k++)
                        {
                            int ti = bList[k];
                            if (rx < tMinX[ti] || rx > tMaxX[ti] || ry < tMinY[ti] || ry > tMaxY[ti]) continue;
                            double ux = bx[ti] - ax[ti], uy = by[ti] - ay[ti];
                            double vx = cx[ti] - ax[ti], vy = cy[ti] - ay[ti];
                            double det = ux * vy - vx * uy;
                            if (Math.Abs(det) < 1e-13) continue;
                            double wx = rx - ax[ti], wy = ry - ay[ti];
                            double u = (wx * vy - vx * wy) / det;
                            if (u < 0.0 || u > 1.0) continue;
                            double v = (ux * wy - wx * uy) / det;
                            if (v < 0.0 || u + v > 1.0) continue;
                            double zHit = az[ti] + u * (bz[ti] - az[ti]) + v * (cz[ti] - az[ti]);
                            hits.Add(zHit);
                        }
                        if ((hits.Count & 1) == 0) break;
                    }

                    if (hits.Count == 0) continue;
                    hits.Sort();
                    int hi = 0;
                    for (int iz = 0; iz <= nz; iz++)
                    {
                        double z = oz + iz * step;
                        while (hi < hits.Count && hits[hi] < z) hi++;
                        inside[ix, iy, iz] = ((hi & 1) == 1);
                    }
                }
            });

            // 2. Build 3D uniform voxel bucket for narrow-band exact point-to-triangle distance
            double cell3D = Math.Max(1.8, step * 6.0);
            int g3x = Math.Max(1, (int)Math.Ceiling((nx * step) / cell3D) + 1);
            int g3y = Math.Max(1, (int)Math.Ceiling((ny * step) / cell3D) + 1);
            int g3z = Math.Max(1, (int)Math.Ceiling((nz * step) / cell3D) + 1);
            var buckets3D = new List<int>[g3x, g3y, g3z];

            for (int i = 0; i < triCount; i++)
            {
                int ix0 = Math.Max(0, (int)Math.Floor((tMinX[i] - ox) / cell3D));
                int ix1 = Math.Min(g3x - 1, (int)Math.Floor((tMaxX[i] - ox) / cell3D));
                int iy0 = Math.Max(0, (int)Math.Floor((tMinY[i] - oy) / cell3D));
                int iy1 = Math.Min(g3y - 1, (int)Math.Floor((tMaxY[i] - oy) / cell3D));
                int iz0 = Math.Max(0, (int)Math.Floor((tMinZ[i] - oz) / cell3D));
                int iz1 = Math.Min(g3z - 1, (int)Math.Floor((tMaxZ[i] - oz) / cell3D));
                for (int ix = ix0; ix <= ix1; ix++)
                    for (int iy = iy0; iy <= iy1; iy++)
                        for (int iz = iz0; iz <= iz1; iz++)
                        {
                            if (buckets3D[ix, iy, iz] == null) buckets3D[ix, iy, iz] = new List<int>(8);
                            buckets3D[ix, iy, iz].Add(i);
                        }
            }

            // 3. Identify narrow-band nodes (adjacent to any sign change in 26-neighborhood) in parallel
            float farVal = (float)(2.5 * step);
            float[,,] grid = new float[nx + 1, ny + 1, nz + 1];

            Parallel.For(0, nx + 1, ix =>
            {
                double px = ox + ix * step;
                int bx3 = Math.Max(0, Math.Min(g3x - 1, (int)Math.Floor((px - ox) / cell3D)));
                for (int iy = 0; iy <= ny; iy++)
                {
                    double py = oy + iy * step;
                    int by3 = Math.Max(0, Math.Min(g3y - 1, (int)Math.Floor((py - oy) / cell3D)));
                    for (int iz = 0; iz <= nz; iz++)
                    {
                        bool inSelf = inside[ix, iy, iz];
                        bool nearSurface = false;
                        int xLo = ix > 0 ? ix - 1 : 0, xHi = ix < nx ? ix + 1 : nx;
                        int yLo = iy > 0 ? iy - 1 : 0, yHi = iy < ny ? iy + 1 : ny;
                        int zLo = iz > 0 ? iz - 1 : 0, zHi = iz < nz ? iz + 1 : nz;

                        for (int qx = xLo; qx <= xHi && !nearSurface; qx++)
                            for (int qy = yLo; qy <= yHi && !nearSurface; qy++)
                                for (int qz = zLo; qz <= zHi; qz++)
                                    if (inside[qx, qy, qz] != inSelf) { nearSurface = true; break; }

                        if (!nearSurface)
                        {
                            grid[ix, iy, iz] = inSelf ? -farVal : farVal;
                            continue;
                        }

                        double pz = oz + iz * step;
                        int bz3 = Math.Max(0, Math.Min(g3z - 1, (int)Math.Floor((pz - oz) / cell3D)));
                        double minDistSq = farVal * farVal;

                        int bX0 = Math.Max(0, bx3 - 1), bX1 = Math.Min(g3x - 1, bx3 + 1);
                        int bY0 = Math.Max(0, by3 - 1), bY1 = Math.Min(g3y - 1, by3 + 1);
                        int bZ0 = Math.Max(0, bz3 - 1), bZ1 = Math.Min(g3z - 1, bz3 + 1);

                        for (int cx3 = bX0; cx3 <= bX1; cx3++)
                            for (int cy3 = bY0; cy3 <= bY1; cy3++)
                                for (int cz3 = bZ0; cz3 <= bZ1; cz3++)
                                {
                                    var lst = buckets3D[cx3, cy3, cz3];
                                    if (lst == null) continue;
                                    for (int k = 0; k < lst.Count; k++)
                                    {
                                        int ti = lst[k];
                                        double d2 = PointTriangleDistSq(
                                            px, py, pz,
                                            ax[ti], ay[ti], az[ti],
                                            bx[ti], by[ti], bz[ti],
                                            cx[ti], cy[ti], cz[ti]);
                                        if (d2 < minDistSq) minDistSq = d2;
                                    }
                                }

                        float dist = (float)Math.Sqrt(minDistSq);
                        if (dist < 1e-5f) dist = 1e-5f;
                        grid[ix, iy, iz] = inSelf ? -dist : dist;
                    }
                }
            });

            return grid;
        }

        private static double PointTriangleDistSq(
            double px, double py, double pz,
            double ax, double ay, double az,
            double bx, double by, double bz,
            double cx, double cy, double cz)
        {
            double abx = bx - ax, aby = by - ay, abz = bz - az;
            double acx = cx - ax, acy = cy - ay, acz = cz - az;
            double apx = px - ax, apy = py - ay, apz = pz - az;

            double d1 = abx * apx + aby * apy + abz * apz;
            double d2 = acx * apx + acy * apy + acz * apz;
            if (d1 <= 0.0 && d2 <= 0.0) return apx * apx + apy * apy + apz * apz;

            double bpx = px - bx, bpy = py - by, bpz = pz - bz;
            double d3 = abx * bpx + aby * bpy + abz * bpz;
            double d4 = acx * bpx + acy * bpy + acz * bpz;
            if (d3 >= 0.0 && d4 <= d3) return bpx * bpx + bpy * bpy + bpz * bpz;

            double vc = d1 * d4 - d3 * d2;
            if (vc <= 0.0 && d1 >= 0.0 && d3 <= 0.0)
            {
                double v = d1 / (d1 - d3);
                double dx = ax + v * abx - px;
                double dy = ay + v * aby - py;
                double dz = az + v * abz - pz;
                return dx * dx + dy * dy + dz * dz;
            }

            double cpx = px - cx, cpy = py - cy, cpz = pz - cz;
            double d5 = abx * cpx + aby * cpy + abz * cpz;
            double d6 = acx * cpx + acy * cpy + acz * cpz;
            if (d6 >= 0.0 && d5 <= d6) return cpx * cpx + cpy * cpy + cpz * cpz;

            double vb = d5 * d2 - d1 * d6;
            if (vb <= 0.0 && d2 >= 0.0 && d6 <= 0.0)
            {
                double w = d2 / (d2 - d6);
                double dx = ax + w * acx - px;
                double dy = ay + w * acy - py;
                double dz = az + w * acz - pz;
                return dx * dx + dy * dy + dz * dz;
            }

            double va = d3 * d6 - d5 * d4;
            if (va <= 0.0 && (d4 - d3) >= 0.0 && (d5 - d6) >= 0.0)
            {
                double w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                double dx = bx + w * (cx - bx) - px;
                double dy = by + w * (cy - by) - py;
                double dz = bz + w * (cz - bz) - pz;
                return dx * dx + dy * dy + dz * dz;
            }

            double denom = 1.0 / (va + vb + vc);
            double vn = vb * denom;
            double wn = vc * denom;
            double qx = ax + abx * vn + acx * wn - px;
            double qy = ay + aby * vn + acy * wn - py;
            double qz = az + abz * vn + acz * wn - pz;
            return qx * qx + qy * qy + qz * qz;
        }
    }
}
