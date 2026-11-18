using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.IO.Compression;
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

    // Dual-Projected Surface Nets (Watertight Manifold Isosurface Extractor with Sharp Feature Projection)
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

        // 3D Chamfered Stealth Block (octagonal XY + crisp 45-deg top/bottom Z bevels!)
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
        public static double Hexagon2D(double x, double y, double af)
        {
            double r = af * 0.5; // apothem (inradius)
            double px = Math.Abs(x);
            double py = Math.Abs(y);
            // Regular hexagon normal vectors: (1, 0) and (0.5, sqrt(3)/2)
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

        public static MeshData ExtractMesh(
            string name,
            Func<double, double, double, double> sdf,
            Vec3 minBound,
            Vec3 maxBound,
            double step)
        {
            int nx = (int)Math.Ceiling((maxBound.X - minBound.X) / step) + 2;
            int ny = (int)Math.Ceiling((maxBound.Y - minBound.Y) / step) + 2;
            int nz = (int)Math.Ceiling((maxBound.Z - minBound.Z) / step) + 2;

            double ox = minBound.X - step;
            double oy = minBound.Y - step;
            double oz = minBound.Z - step;

            float[,,] grid = new float[nx + 1, ny + 1, nz + 1];
            for (int ix = 0; ix <= nx; ix++)
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
            }

            int[,,] cellVert = new int[nx, ny, nz];
            for (int ix = 0; ix < nx; ix++)
                for (int iy = 0; iy < ny; iy++)
                    for (int iz = 0; iz < nz; iz++)
                        cellVert[ix, iy, iz] = -1;

            var mesh = new MeshData(name);

            int[,] edgeCorners = new int[,] {
                {0,1}, {2,3}, {4,5}, {6,7},
                {0,2}, {1,3}, {4,6}, {5,7},
                {0,4}, {1,5}, {2,6}, {3,7}
            };

            // Step 1: Create Dual/SurfaceNet vertices in active cells and project onto exact analytical zero-surface
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    for (int iz = 0; iz < nz; iz++)
                    {
                        float[] v = new float[8];
                        int mask = 0;
                        for (int c = 0; c < 8; c++)
                        {
                            int cx = ix + (c & 1);
                            int cy = iy + ((c >> 1) & 1);
                            int cz = iz + ((c >> 2) & 1);
                            v[c] = grid[cx, cy, cz];
                            if (v[c] < 0) mask |= (1 << c);
                        }
                        if (mask == 0 || mask == 255) continue;

                        double sumX = 0, sumY = 0, sumZ = 0;
                        int crossings = 0;
                        for (int e = 0; e < 12; e++)
                        {
                            int c0 = edgeCorners[e, 0];
                            int c1 = edgeCorners[e, 1];
                            if (((mask >> c0) & 1) != ((mask >> c1) & 1))
                            {
                                double v0 = v[c0];
                                double v1 = v[c1];
                                double t = v0 / (v0 - v1);
                                double x0 = ox + (ix + (c0 & 1)) * step;
                                double y0 = oy + (iy + ((c0 >> 1) & 1)) * step;
                                double z0 = oz + (iz + ((c0 >> 2) & 1)) * step;
                                double x1 = ox + (ix + (c1 & 1)) * step;
                                double y1 = oy + (iy + ((c1 >> 1) & 1)) * step;
                                double z1 = oz + (iz + ((c1 >> 2) & 1)) * step;
                                sumX += x0 + t * (x1 - x0);
                                sumY += y0 + t * (y1 - y0);
                                sumZ += z0 + t * (z1 - z0);
                                crossings++;
                            }
                        }

                        double px = sumX / crossings;
                        double py = sumY / crossings;
                        double pz = sumZ / crossings;

                        // Newton-Raphson projection onto exact SDF = 0 surface (clamped to cell bounds for manifold safety)
                        double cellMinX = ox + ix * step, cellMaxX = cellMinX + step;
                        double cellMinY = oy + iy * step, cellMaxY = cellMinY + step;
                        double cellMinZ = oz + iz * step, cellMaxZ = cellMinZ + step;

                        for (int iter = 0; iter < 3; iter++)
                        {
                            double val = sdf(px, py, pz);
                            if (Math.Abs(val) < 1e-5) break;
                            var g = Gradient(sdf, px, py, pz, step * 0.05);
                            px = Math.Max(cellMinX, Math.Min(cellMaxX, px - val * g.X));
                            py = Math.Max(cellMinY, Math.Min(cellMaxY, py - val * g.Y));
                            pz = Math.Max(cellMinZ, Math.Min(cellMaxZ, pz - val * g.Z));
                        }

                        cellVert[ix, iy, iz] = mesh.Vertices.Count;
                        mesh.Vertices.Add(new Vec3(px, py, pz));
                    }
                }
            }

            // Step 2: Connect quads across every sign-changing grid edge
            for (int ix = 1; ix < nx; ix++)
            {
                for (int iy = 1; iy < ny; iy++)
                {
                    for (int iz = 1; iz < nz; iz++)
                    {
                        float v0 = grid[ix, iy, iz];

                        // X-aligned edge: (ix, iy, iz) -> (ix+1, iy, iz)
                        if (ix + 1 <= nx)
                        {
                            float vx = grid[ix + 1, iy, iz];
                            if ((v0 < 0) != (vx < 0))
                            {
                                int i00 = cellVert[ix, iy - 1, iz - 1];
                                int i10 = cellVert[ix, iy, iz - 1];
                                int i11 = cellVert[ix, iy, iz];
                                int i01 = cellVert[ix, iy - 1, iz];
                                if (i00 >= 0 && i10 >= 0 && i11 >= 0 && i01 >= 0)
                                {
                                    if (v0 < 0)
                                    {
                                        mesh.Triangles.Add(new TriIdx(i00, i10, i11));
                                        mesh.Triangles.Add(new TriIdx(i00, i11, i01));
                                    }
                                    else
                                    {
                                        mesh.Triangles.Add(new TriIdx(i00, i11, i10));
                                        mesh.Triangles.Add(new TriIdx(i00, i01, i11));
                                    }
                                }
                            }
                        }

                        // Y-aligned edge: (ix, iy, iz) -> (ix, iy+1, iz)
                        if (iy + 1 <= ny)
                        {
                            float vy = grid[ix, iy + 1, iz];
                            if ((v0 < 0) != (vy < 0))
                            {
                                int i00 = cellVert[ix - 1, iy, iz - 1];
                                int i10 = cellVert[ix, iy, iz - 1];
                                int i11 = cellVert[ix, iy, iz];
                                int i01 = cellVert[ix - 1, iy, iz];
                                if (i00 >= 0 && i10 >= 0 && i11 >= 0 && i01 >= 0)
                                {
                                    if (v0 < 0)
                                    {
                                        mesh.Triangles.Add(new TriIdx(i00, i01, i11));
                                        mesh.Triangles.Add(new TriIdx(i00, i11, i10));
                                    }
                                    else
                                    {
                                        mesh.Triangles.Add(new TriIdx(i00, i11, i01));
                                        mesh.Triangles.Add(new TriIdx(i00, i10, i11));
                                    }
                                }
                            }
                        }

                        // Z-aligned edge: (ix, iy, iz) -> (ix, iy, iz+1)
                        if (iz + 1 <= nz)
                        {
                            float vz = grid[ix, iy, iz + 1];
                            if ((v0 < 0) != (vz < 0))
                            {
                                int i00 = cellVert[ix - 1, iy - 1, iz];
                                int i10 = cellVert[ix, iy - 1, iz];
                                int i11 = cellVert[ix, iy, iz];
                                int i01 = cellVert[ix - 1, iy, iz];
                                if (i00 >= 0 && i10 >= 0 && i11 >= 0 && i01 >= 0)
                                {
                                    if (v0 < 0)
                                    {
                                        mesh.Triangles.Add(new TriIdx(i00, i10, i11));
                                        mesh.Triangles.Add(new TriIdx(i00, i11, i01));
                                    }
                                    else
                                    {
                                        mesh.Triangles.Add(new TriIdx(i00, i11, i10));
                                        mesh.Triangles.Add(new TriIdx(i00, i01, i11));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return mesh;
        }
    }
}
