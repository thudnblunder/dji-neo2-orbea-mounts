using System;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Collections.Generic;
using System.Globalization;

namespace DjiNeo2OrbeaMounts
{
    public struct QuantVert : IEquatable<QuantVert>
    {
        public long X, Y, Z;
        public QuantVert(double x, double y, double z, double invTol)
        {
            X = (long)Math.Round(x * invTol);
            Y = (long)Math.Round(y * invTol);
            Z = (long)Math.Round(z * invTol);
        }
        public bool Equals(QuantVert o) { return X == o.X && Y == o.Y && Z == o.Z; }
        public override bool Equals(object obj) { return obj is QuantVert && Equals((QuantVert)obj); }
        public override int GetHashCode()
        {
            unchecked
            {
                long h = (X * 73856093L) ^ (Y * 19349663L) ^ (Z * 83492791L);
                return (int)h ^ (int)(h >> 32);
            }
        }
    }

    public struct AuditEdgeKey : IEquatable<AuditEdgeKey>
    {
        public int Lo, Hi;
        public AuditEdgeKey(int lo, int hi) { Lo = lo; Hi = hi; }
        public bool Equals(AuditEdgeKey o) { return Lo == o.Lo && Hi == o.Hi; }
        public override bool Equals(object obj) { return obj is AuditEdgeKey && Equals((AuditEdgeKey)obj); }
        public override int GetHashCode() { unchecked { return (Lo * 73856093) ^ (Hi * 19349663); } }
    }

    public static class MeshAuditor
    {
        public class AuditResult
        {
            public string Name;
            public int Verts;
            public int Tris;
            public int DupVerts;
            public int DegenTris;
            public int ZeroAreaTris;
            public int OpenEdges;
            public int NonManifoldEdges;
            public int ReversedEdges;
            public int PinchVerts;
            public int FoldedEdges;
            public int Shells;
            public int NegVolShells;
            public double TotalVolumeCm3;
            public double MinX, MaxX, MinY, MaxY, MinZ, MaxZ;
            public bool Passed
            {
                get
                {
                    return DegenTris == 0 && ZeroAreaTris == 0 && OpenEdges == 0 &&
                           NonManifoldEdges == 0 && ReversedEdges == 0 &&
                           PinchVerts == 0 && FoldedEdges == 0 && NegVolShells == 0 &&
                           Shells > 0 && TotalVolumeCm3 > 0.0;
                }
            }
        }

        public static bool AuditAll(string baseDir)
        {
            string threeMfDir = Path.Combine(baseDir, "3mf");
            string stlDir = Path.Combine(baseDir, "stl");
            bool allPassed = true;

            Console.WriteLine("================================================================================================================");
            Console.WriteLine("  FULL MESH & BUILD-PLATE TOPOLOGY AUDIT (ALL 5 .3MF PLATES @ F4 & 0.002mm BAMBU WELD TOLERANCE + ALL 15 STLs)");
            Console.WriteLine("================================================================================================================");

            var mfFiles = Directory.GetFiles(threeMfDir, "*.3mf");
            Array.Sort(mfFiles);
            foreach (var mf in mfFiles)
            {
                if (!Audit3mfFile(mf)) allPassed = false;
            }

            Console.WriteLine("\n--- BINARY STL TOPOLOGY VERIFICATION (15 PARTS @ 0.002mm BAMBU WELD TOLERANCE) ---");
            var stlFiles = Directory.GetFiles(stlDir, "*.stl");
            Array.Sort(stlFiles);
            foreach (var stl in stlFiles)
            {
                if (!AuditStlFile(stl)) allPassed = false;
            }

            Console.WriteLine("\n================================================================================================================");
            Console.WriteLine(allPassed
                ? "  AUDIT SUMMARY: 100% PASS -- ZERO NON-MANIFOLD EDGES, ZERO REVERSED FACES, ZERO DEGENERATES, ALL PLATES VALID!"
                : "  AUDIT SUMMARY: FAILED -- DEFECTS DETECTED (SEE ABOVE).");
            Console.WriteLine("================================================================================================================");
            return allPassed;
        }

        public static bool Audit3mfFile(string path)
        {
            Console.WriteLine("\nPLATE: " + Path.GetFileName(path));
            bool plateOk = true;

            var objVerts = new Dictionary<string, List<double>>();
            var objTris = new Dictionary<string, List<int>>();
            var objNames = new Dictionary<string, string>();
            var buildItems = new List<Tuple<string, double, double, double>>();

            using (var za = ZipFile.OpenRead(path))
            {
                string[] reqEntries = new string[] { "[Content_Types].xml", "_rels/.rels", "3D/3dmodel.model" };
                foreach (var req in reqEntries)
                {
                    var e = za.GetEntry(req);
                    if (e == null)
                    {
                        Console.WriteLine("  [FAIL] Missing required 3MF archive entry: " + req);
                        return false;
                    }
                    if (req != "3D/3dmodel.model")
                    {
                        var doc = new XmlDocument();
                        using (var s = e.Open()) doc.Load(s);
                    }
                }

                var modelEntry = za.GetEntry("3D/3dmodel.model");
                using (var xr = XmlReader.Create(modelEntry.Open()))
                {
                    string curId = null;
                    List<double> curV = null;
                    List<int> curT = null;
                    while (xr.Read())
                    {
                        if (xr.NodeType == XmlNodeType.Element)
                        {
                            if (xr.Name == "object")
                            {
                                curId = xr.GetAttribute("id");
                                objNames[curId] = xr.GetAttribute("name") ?? ("obj_" + curId);
                                curV = new List<double>(150000);
                                curT = new List<int>(300000);
                                objVerts[curId] = curV;
                                objTris[curId] = curT;
                            }
                            else if (curId != null && xr.Name == "vertex")
                            {
                                curV.Add(double.Parse(xr.GetAttribute("x"), CultureInfo.InvariantCulture));
                                curV.Add(double.Parse(xr.GetAttribute("y"), CultureInfo.InvariantCulture));
                                curV.Add(double.Parse(xr.GetAttribute("z"), CultureInfo.InvariantCulture));
                            }
                            else if (curId != null && xr.Name == "triangle")
                            {
                                curT.Add(int.Parse(xr.GetAttribute("v1")));
                                curT.Add(int.Parse(xr.GetAttribute("v2")));
                                curT.Add(int.Parse(xr.GetAttribute("v3")));
                            }
                            else if (xr.Name == "item")
                            {
                                string oid = xr.GetAttribute("objectid");
                                string tf = xr.GetAttribute("transform") ?? "1 0 0 0 1 0 0 0 1 0 0 0";
                                string[] parts = tf.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                double tx = double.Parse(parts[9], CultureInfo.InvariantCulture);
                                double ty = double.Parse(parts[10], CultureInfo.InvariantCulture);
                                double tz = double.Parse(parts[11], CultureInfo.InvariantCulture);
                                buildItems.Add(Tuple.Create(oid, tx, ty, tz));
                            }
                        }
                        else if (xr.NodeType == XmlNodeType.EndElement && xr.Name == "object")
                        {
                            curId = null;
                        }
                    }
                }
            }

            var placedBoxes = new List<Tuple<string, double[], double[]>>();
            foreach (var bi in buildItems)
            {
                string oid = bi.Item1;
                string name = objNames[oid];
                var verts = objVerts[oid];
                var tris = objTris[oid];

                var resF4 = AnalyzeFlatMesh(name, verts, tris, 0.0001);
                var resTol2 = AnalyzeFlatMesh(name, verts, tris, 0.0020);

                double pMinX = resF4.MinX + bi.Item2, pMaxX = resF4.MaxX + bi.Item2;
                double pMinY = resF4.MinY + bi.Item3, pMaxY = resF4.MaxY + bi.Item3;
                double pMinZ = resF4.MinZ + bi.Item4, pMaxZ = resF4.MaxZ + bi.Item4;
                placedBoxes.Add(Tuple.Create(name, new double[] { pMinX, pMinY, pMinZ }, new double[] { pMaxX, pMaxY, pMaxZ }));

                bool boundsOk = (pMinX >= 0.0 && pMaxX <= 256.0 && pMinY >= 0.0 && pMaxY <= 256.0 && pMinZ >= -0.02 && pMinZ <= 0.02 && pMaxZ <= 256.0);
                bool objOk = resF4.Passed && resTol2.Passed && boundsOk;
                if (!objOk) plateOk = false;

                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  [{0}] {1,-48} | T={2,7:N0} | deg={3}/{4} zA={5} open={6}/{7} nonM={8}/{9} rev={10}/{11} pinch={12} fold={13} | sh={14} negV={15} | vol={16,5:F1}cm3",
                    objOk ? "PASS" : "FAIL", name, resF4.Tris,
                    resF4.DegenTris, resTol2.DegenTris, resF4.ZeroAreaTris,
                    resF4.OpenEdges, resTol2.OpenEdges,
                    resF4.NonManifoldEdges, resTol2.NonManifoldEdges,
                    resF4.ReversedEdges, resTol2.ReversedEdges,
                    resF4.PinchVerts, resF4.FoldedEdges, resF4.Shells, resF4.NegVolShells, resF4.TotalVolumeCm3));
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "         Plate Pos: X=[{0:F1}..{1:F1}] Y=[{2:F1}..{3:F1}] Z=[{4:F3}..{5:F1}] mm {6}",
                    pMinX, pMaxX, pMinY, pMaxY, pMinZ, pMaxZ, boundsOk ? "(On Bed & Inside 256x256)" : "[OUT OF BOUNDS / FLOATING!]"));
            }

            for (int i = 0; i < placedBoxes.Count; i++)
            {
                for (int j = i + 1; j < placedBoxes.Count; j++)
                {
                    var aMin = placedBoxes[i].Item2; var aMax = placedBoxes[i].Item3;
                    var bMin = placedBoxes[j].Item2; var bMax = placedBoxes[j].Item3;
                    bool overlapX = aMin[0] < bMax[0] && bMin[0] < aMax[0];
                    bool overlapY = aMin[1] < bMax[1] && bMin[1] < aMax[1];
                    if (overlapX && overlapY)
                    {
                        Console.WriteLine(string.Format("  [WARN/FAIL] Plate overlap between {0} and {1}!", placedBoxes[i].Item1, placedBoxes[j].Item1));
                        plateOk = false;
                    }
                }
            }

            return plateOk;
        }

        public static bool AuditStlFile(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            uint triCount = BitConverter.ToUInt32(data, 80);
            var verts = new List<double>((int)triCount * 3);
            var tris = new List<int>((int)triCount * 3);
            var vMap = new Dictionary<QuantVert, int>((int)triCount);

            int pos = 84;
            for (uint i = 0; i < triCount; i++)
            {
                pos += 12;
                for (int k = 0; k < 3; k++)
                {
                    float fx = BitConverter.ToSingle(data, pos); pos += 4;
                    float fy = BitConverter.ToSingle(data, pos); pos += 4;
                    float fz = BitConverter.ToSingle(data, pos); pos += 4;
                    var q = new QuantVert(fx, fy, fz, 10000.0);
                    int vi;
                    if (!vMap.TryGetValue(q, out vi))
                    {
                        vi = verts.Count / 3;
                        verts.Add(fx); verts.Add(fy); verts.Add(fz);
                        vMap[q] = vi;
                    }
                    tris.Add(vi);
                }
                pos += 2;
            }

            var res = AnalyzeFlatMesh(Path.GetFileName(path), verts, tris, 0.0020);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  [{0}] {1,-54} | T={2,7:N0} | deg={3} zA={4} open={5} nonM={6} rev={7} pinch={8} fold={9} | sh={10,2} negV={11} | minZ={12:F3}",
                res.Passed ? "PASS" : "FAIL", res.Name, res.Tris,
                res.DegenTris, res.ZeroAreaTris, res.OpenEdges, res.NonManifoldEdges,
                res.ReversedEdges, res.PinchVerts, res.FoldedEdges, res.Shells, res.NegVolShells, res.MinZ));
            return res.Passed;
        }

        // Ultra-fast flat-array topology analyzer (O(T) time, zero per-edge/per-vertex heap objects!)
        public static AuditResult AnalyzeFlatMesh(string name, List<double> rawVerts, List<int> rawTris, double mergeTol)
        {
            int rawVCount = rawVerts.Count / 3;
            int rawTCount = rawTris.Count / 3;
            var res = new AuditResult { Name = name, Verts = rawVCount, Tris = rawTCount };
            res.MinX = res.MinY = res.MinZ = 1e9;
            res.MaxX = res.MaxY = res.MaxZ = -1e9;

            double invTol = 1.0 / mergeTol;
            var vMap = new Dictionary<QuantVert, int>(rawVCount);
            double[] vx = new double[rawVCount];
            double[] vy = new double[rawVCount];
            double[] vz = new double[rawVCount];
            int[] remap = new int[rawVCount];
            int uVCount = 0;

            for (int i = 0; i < rawVCount; i++)
            {
                double x = rawVerts[i * 3];
                double y = rawVerts[i * 3 + 1];
                double z = rawVerts[i * 3 + 2];
                if (x < res.MinX) res.MinX = x; if (x > res.MaxX) res.MaxX = x;
                if (y < res.MinY) res.MinY = y; if (y > res.MaxY) res.MaxY = y;
                if (z < res.MinZ) res.MinZ = z; if (z > res.MaxZ) res.MaxZ = z;

                var q = new QuantVert(x, y, z, invTol);
                int idx;
                if (!vMap.TryGetValue(q, out idx))
                {
                    idx = uVCount++;
                    vx[idx] = x; vy[idx] = y; vz[idx] = z;
                    vMap[q] = idx;
                }
                else res.DupVerts++;
                remap[i] = idx;
            }

            int[] tA = new int[rawTCount];
            int[] tB = new int[rawTCount];
            int[] tC = new int[rawTCount];
            float[] nX = new float[rawTCount];
            float[] nY = new float[rawTCount];
            float[] nZ = new float[rawTCount];
            int vTCount = 0;

            for (int i = 0; i < rawTCount; i++)
            {
                int a = remap[rawTris[i * 3]];
                int b = remap[rawTris[i * 3 + 1]];
                int c = remap[rawTris[i * 3 + 2]];
                if (a == b || b == c || c == a) { res.DegenTris++; continue; }

                double ux = vx[b] - vx[a], uy = vy[b] - vy[a], uz = vz[b] - vz[a];
                double wx = vx[c] - vx[a], wy = vy[c] - vy[a], wz = vz[c] - vz[a];
                double nx = uy * wz - uz * wy, ny = uz * wx - ux * wz, nz = ux * wy - uy * wx;
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-11) { res.ZeroAreaTris++; continue; }

                tA[vTCount] = a; tB[vTCount] = b; tC[vTCount] = c;
                nX[vTCount] = (float)(nx / len);
                nY[vTCount] = (float)(ny / len);
                nZ[vTCount] = (float)(nz / len);
                vTCount++;
            }

            // Flat half-edge & undirected-edge maps + Union-Find for O(T) shell volumes
            int[] parent = new int[vTCount];
            for (int i = 0; i < vTCount; i++) parent[i] = i;

            // Pack (firstTri, count) into edge arrays indexed by unique undirected edge id
            int maxEdges = (vTCount * 3) / 2 + 1024;
            var undirMap = new Dictionary<AuditEdgeKey, int>(maxEdges);
            int[] eCount = new int[vTCount * 3];
            int[] eTri0 = new int[vTCount * 3];
            int[] eDir0 = new int[vTCount * 3]; // +1 if first occurrence was lo->hi, -1 if hi->lo
            int[] eOppHalf = new int[vTCount * 3]; // half-edge twin (for O(V) pinch vertex check!)
            for (int i = 0; i < vTCount * 3; i++) eOppHalf[i] = -1;
            int[] eHalf0 = new int[vTCount * 3];
            int edgeTotal = 0;

            int[] vDeg = new int[uVCount];
            int[] vStartHalf = new int[uVCount];
            for (int i = 0; i < uVCount; i++) vStartHalf[i] = -1;

            for (int ti = 0; ti < vTCount; ti++)
            {
                int a = tA[ti], b = tB[ti], c = tC[ti];
                vDeg[a]++; vDeg[b]++; vDeg[c]++;
                vStartHalf[a] = ti * 3;
                vStartHalf[b] = ti * 3 + 1;
                vStartHalf[c] = ti * 3 + 2;

                for (int k = 0; k < 3; k++)
                {
                    int u = (k == 0) ? a : ((k == 1) ? b : c);
                    int w = (k == 0) ? b : ((k == 1) ? c : a);
                    int he = ti * 3 + k;
                    int lo = u < w ? u : w;
                    int hi = u < w ? w : u;
                    int dir = u < w ? 1 : -1;
                    var uk = new AuditEdgeKey(lo, hi);

                    int eid;
                    if (!undirMap.TryGetValue(uk, out eid))
                    {
                        eid = edgeTotal++;
                        undirMap[uk] = eid;
                        eCount[eid] = 1;
                        eTri0[eid] = ti;
                        eDir0[eid] = dir;
                        eHalf0[eid] = he;
                    }
                    else
                    {
                        int cOld = eCount[eid];
                        eCount[eid] = cOld + 1;
                        int t0 = eTri0[eid];
                        Union(parent, t0, ti);
                        if (cOld == 1)
                        {
                            if (eDir0[eid] == dir) res.ReversedEdges++;
                            int h0 = eHalf0[eid];
                            eOppHalf[h0] = he;
                            eOppHalf[he] = h0;
                            float dot = nX[t0] * nX[ti] + nY[t0] * nY[ti] + nZ[t0] * nZ[ti];
                            if (dot < -0.96f) res.FoldedEdges++;
                        }
                        else if (eDir0[eid] == dir)
                        {
                            res.ReversedEdges++;
                        }
                    }
                }
            }

            for (int eid = 0; eid < edgeTotal; eid++)
            {
                if (eCount[eid] == 1) res.OpenEdges++;
                else if (eCount[eid] > 2) res.NonManifoldEdges++;
            }

            // O(V) zero-allocation pinch-vertex check by walking the single half-edge fan cycle around every vertex
            if (res.OpenEdges == 0 && res.NonManifoldEdges == 0 && res.ReversedEdges == 0)
            {
                for (int vi = 0; vi < uVCount; vi++)
                {
                    int deg = vDeg[vi];
                    if (deg == 0) continue;
                    int startHe = vStartHalf[vi]; // outgoing half-edge from vi
                    int curHe = startHe;
                    int steps = 0;
                    do
                    {
                        // Move to previous edge in same triangle (incoming to vi), then cross to its twin (outgoing from vi)
                        int triBase = (curHe / 3) * 3;
                        int local = curHe - triBase;
                        int prevHe = triBase + ((local + 2) % 3);
                        int twin = eOppHalf[prevHe];
                        if (twin < 0) break;
                        curHe = twin;
                        steps++;
                    } while (curHe != startHe && steps <= deg);

                    if (steps != deg || curHe != startHe) res.PinchVerts++;
                }
            }

            // Compute signed volume per connected shell via Union-Find roots
            var shellVol = new Dictionary<int, double>(16);
            for (int ti = 0; ti < vTCount; ti++)
            {
                int root = Find(parent, ti);
                int a = tA[ti], b = tB[ti], c = tC[ti];
                double v6 = vx[a] * (vy[b] * vz[c] - vz[b] * vy[c])
                          - vy[a] * (vx[b] * vz[c] - vz[b] * vx[c])
                          + vz[a] * (vx[b] * vy[c] - vy[b] * vx[c]);
                double cur;
                shellVol.TryGetValue(root, out cur);
                shellVol[root] = cur + v6;
            }

            double totalVol6 = 0;
            res.Shells = shellVol.Count;
            foreach (var kv in shellVol)
            {
                totalVol6 += kv.Value;
                if (kv.Value <= 0.0) res.NegVolShells++;
            }
            res.TotalVolumeCm3 = (totalVol6 / 6.0) / 1000.0;
            return res;
        }

        private static int Find(int[] parent, int x)
        {
            int r = x;
            while (parent[r] != r) r = parent[r];
            while (parent[x] != r) { int nxt = parent[x]; parent[x] = r; x = nxt; }
            return r;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);
            if (ra != rb) parent[rb] = ra;
        }
    }
}
