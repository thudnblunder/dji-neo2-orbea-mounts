using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.IO.Compression;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DjiNeo2OrbeaMounts
{
    public class PlateItem
    {
        public MeshData Mesh;
        public double OffsetX, OffsetY, OffsetZ;
        public PlateItem(MeshData mesh, double ox, double oy, double oz = 0.0)
        {
            Mesh = mesh; OffsetX = ox; OffsetY = oy; OffsetZ = oz;
        }
    }

    public class ColoredMeshPart
    {
        public MeshData Mesh;
        public Color PartColor;
        public ColoredMeshPart(MeshData mesh, Color color)
        {
            Mesh = mesh; PartColor = color;
        }
    }

    public static class ThreeMfWriter
    {
        public static void Save3mf(string path, string title, List<PlateItem> items)
        {
            if (File.Exists(path)) File.Delete(path);
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var ctEntry = archive.CreateEntry("[Content_Types].xml", CompressionLevel.Fastest);
                using (var sw = new StreamWriter(ctEntry.Open(), new UTF8Encoding(false)))
                {
                    sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
 <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml"" />
 <Default Extension=""model"" ContentType=""application/vnd.ms-package.3dmanufacturing-3dmodel+xml"" />
</Types>");
                }

                var relsEntry = archive.CreateEntry("_rels/.rels", CompressionLevel.Fastest);
                using (var sw = new StreamWriter(relsEntry.Open(), new UTF8Encoding(false)))
                {
                    sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
 <Relationship Target=""/3D/3dmodel.model"" Id=""rel-1"" Type=""http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel"" />
</Relationships>");
                }

                var modelEntry = archive.CreateEntry("3D/3dmodel.model", CompressionLevel.Optimal);
                using (var sw = new StreamWriter(modelEntry.Open(), new UTF8Encoding(false)))
                {
                    sw.WriteLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
                    sw.WriteLine(@"<model unit=""millimeter"" xml:lang=""en-US"" xmlns=""http://schemas.microsoft.com/3dmanufacturing/core/2015/02"">");
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture, @" <metadata name=""Title"">{0}</metadata>", System.Security.SecurityElement.Escape(title)));
                    sw.WriteLine(@" <metadata name=""Application"">BambuStudio</metadata>");
                    sw.WriteLine(@" <resources>");

                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        int objId = i + 1;
                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture, @"  <object id=""{0}"" name=""{1}"" type=""model"">", objId, System.Security.SecurityElement.Escape(item.Mesh.Name)));
                        sw.WriteLine(@"   <mesh>");
                        sw.WriteLine(@"    <vertices>");
                        foreach (var v in item.Mesh.Vertices)
                        {
                            sw.WriteLine(string.Format(CultureInfo.InvariantCulture, @"     <vertex x=""{0:F4}"" y=""{1:F4}"" z=""{2:F4}"" />", v.X, v.Y, v.Z));
                        }
                        sw.WriteLine(@"    </vertices>");
                        sw.WriteLine(@"    <triangles>");
                        foreach (var t in item.Mesh.Triangles)
                        {
                            sw.WriteLine(string.Format(CultureInfo.InvariantCulture, @"     <triangle v1=""{0}"" v2=""{1}"" v3=""{2}"" />", t.V1, t.V2, t.V3));
                        }
                        sw.WriteLine(@"    </triangles>");
                        sw.WriteLine(@"   </mesh>");
                        sw.WriteLine(@"  </object>");
                    }

                    sw.WriteLine(@" </resources>");
                    sw.WriteLine(@" <build>");
                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        int objId = i + 1;
                        double tx = 128.0 + item.OffsetX;
                        double ty = 128.0 + item.OffsetY;
                        double tz = item.OffsetZ;
                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            @"  <item objectid=""{0}"" transform=""1 0 0 0 1 0 0 0 1 {1:F3} {2:F3} {3:F3}"" />",
                            objId, tx, ty, tz));
                    }
                    sw.WriteLine(@" </build>");
                    sw.WriteLine(@"</model>");
                }
            }
        }
    }

    public static class MeshRenderer
    {
        public static void RenderMultiViewSheet(
            string outputPath,
            string title,
            string subtitle,
            MeshData mesh,
            Color baseColor)
        {
            RenderMultiColorSheet(
                outputPath,
                title,
                subtitle,
                new List<ColoredMeshPart> { new ColoredMeshPart(mesh, baseColor) });
        }

        public static void RenderMultiColorSheet(
            string outputPath,
            string title,
            string subtitle,
            List<ColoredMeshPart> parts)
        {
            int width = 1400;
            int height = 950;
            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.FromArgb(18, 22, 28));

                using (var headerBrush = new SolidBrush(Color.FromArgb(28, 34, 44)))
                {
                    g.FillRectangle(headerBrush, 0, 0, width, 78);
                }
                using (var titleFont = new Font("Segoe UI", 18, FontStyle.Bold))
                using (var subFont = new Font("Segoe UI", 11, FontStyle.Regular))
                using (var whiteBrush = new SolidBrush(Color.FromArgb(240, 246, 252)))
                using (var accentBrush = new SolidBrush(Color.FromArgb(88, 166, 255)))
                {
                    g.DrawString(title, titleFont, whiteBrush, 24, 14);
                    g.DrawString(subtitle, subFont, accentBrush, 26, 46);
                }

                RenderViewport(bmp, g, new Rectangle(20, 92, 670, 410), "ISOMETRIC TOP-FRONT VIEW", parts,
                    yawDeg: 40.0, pitchDeg: 25.0);
                RenderViewport(bmp, g, new Rectangle(710, 92, 670, 410), "TOP / FRONT ORTHOGRAPHIC VIEW (+Z)", parts,
                    yawDeg: 0.0, pitchDeg: 89.9);
                RenderViewport(bmp, g, new Rectangle(20, 518, 670, 410), "UNDERSIDE / REAR VIEW (-Z)", parts,
                    yawDeg: 0.0, pitchDeg: -89.9);
                RenderViewport(bmp, g, new Rectangle(710, 518, 670, 410), "ISOMETRIC REAR / SIDE VIEW", parts,
                    yawDeg: -140.0, pitchDeg: 22.0);

                bmp.Save(outputPath, ImageFormat.Png);
            }
        }

        private static void RenderViewport(
            Bitmap bmp,
            Graphics g,
            Rectangle rect,
            string label,
            List<ColoredMeshPart> parts,
            double yawDeg,
            double pitchDeg)
        {
            using (var bgBrush = new SolidBrush(Color.FromArgb(25, 30, 38)))
            using (var borderPen = new Pen(Color.FromArgb(52, 62, 76), 1.5f))
            {
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect);
            }

            double minX = 1e9, minY = 1e9, minZ = 1e9;
            double maxX = -1e9, maxY = -1e9, maxZ = -1e9;
            int totalTris = 0;
            foreach (var p in parts)
            {
                Vec3 pMin, pMax;
                p.Mesh.GetBounds(out pMin, out pMax);
                if (pMin.X < minX) minX = pMin.X; if (pMax.X > maxX) maxX = pMax.X;
                if (pMin.Y < minY) minY = pMin.Y; if (pMax.Y > maxY) maxY = pMax.Y;
                if (pMin.Z < minZ) minZ = pMin.Z; if (pMax.Z > maxZ) maxZ = pMax.Z;
                totalTris += p.Mesh.Triangles.Count;
            }
            Vec3 minB = new Vec3(minX, minY, minZ);
            Vec3 maxB = new Vec3(maxX, maxY, maxZ);
            Vec3 center = (minB + maxB) * 0.5;
            Vec3 span = maxB - minB;

            double yaw = yawDeg * Math.PI / 180.0;
            double pitch = pitchDeg * Math.PI / 180.0;

            Vec3 forward = new Vec3(
                -Math.Cos(pitch) * Math.Cos(yaw),
                -Math.Cos(pitch) * Math.Sin(yaw),
                -Math.Sin(pitch)).Normalized();

            Vec3 worldUp;
            if (Math.Abs(Math.Sin(pitch)) > 0.99)
                worldUp = new Vec3(1, 0, 0);
            else if (pitch < 0)
                worldUp = new Vec3(0, 0, -1);
            else
                worldUp = new Vec3(0, 0, 1);

            Vec3 right = Vec3.Cross(forward, worldUp).Normalized();
            Vec3 up = Vec3.Cross(right, forward).Normalized();

            double minU = 1e9, maxU = -1e9, minV = 1e9, maxV = -1e9;
            double minDepth = 1e9, maxDepth = -1e9;
            List<Vec3[]> allCamVerts = new List<Vec3[]>();

            foreach (var part in parts)
            {
                Vec3[] camVerts = new Vec3[part.Mesh.Vertices.Count];
                for (int i = 0; i < part.Mesh.Vertices.Count; i++)
                {
                    Vec3 p = part.Mesh.Vertices[i] - center;
                    double u = Vec3.Dot(p, right);
                    double v = Vec3.Dot(p, up);
                    double depth = Vec3.Dot(p, forward);
                    camVerts[i] = new Vec3(u, v, depth);
                    if (u < minU) minU = u; if (u > maxU) maxU = u;
                    if (v < minV) minV = v; if (v > maxV) maxV = v;
                    if (depth < minDepth) minDepth = depth; if (depth > maxDepth) maxDepth = depth;
                }
                allCamVerts.Add(camVerts);
            }

            double pad = 42.0;
            double scaleU = (rect.Width - 2 * pad) / Math.Max(1.0, maxU - minU);
            double scaleV = (rect.Height - 2 * pad - 24) / Math.Max(1.0, maxV - minV);
            double scale = Math.Min(scaleU, scaleV);
            double midU = 0.5 * (minU + maxU);
            double midV = 0.5 * (minV + maxV);
            double cx = rect.Left + rect.Width * 0.5;
            double cy = rect.Top + 14 + rect.Height * 0.5;

            int vw = rect.Width;
            int vh = rect.Height;
            float[,] zbuf = new float[vw, vh];
            for (int x = 0; x < vw; x++)
                for (int y = 0; y < vh; y++)
                    zbuf[x, y] = float.MaxValue;

            Vec3 light1 = new Vec3(-0.35, 0.45, -0.82).Normalized();
            Vec3 light2 = new Vec3(0.45, -0.30, -0.60).Normalized();
            double depthRange = Math.Max(1.0, maxDepth - minDepth);

            for (int pIdx = 0; pIdx < parts.Count; pIdx++)
            {
                var part = parts[pIdx];
                var camVerts = allCamVerts[pIdx];
                Color baseColor = part.PartColor;

                foreach (var tri in part.Mesh.Triangles)
                {
                    Vec3 a = camVerts[tri.V1];
                    Vec3 b = camVerts[tri.V2];
                    Vec3 c = camVerts[tri.V3];

                    Vec3 wa = part.Mesh.Vertices[tri.V1];
                    Vec3 wb = part.Mesh.Vertices[tri.V2];
                    Vec3 wc = part.Mesh.Vertices[tri.V3];
                    Vec3 wn = Vec3.Cross(wb - wa, wc - wa).Normalized();
                    Vec3 n = new Vec3(Vec3.Dot(wn, right), Vec3.Dot(wn, up), Vec3.Dot(wn, forward));

                    if (n.Z > 0.05) continue;

                    double sx0 = cx + (a.X - midU) * scale - rect.Left;
                    double sy0 = cy - (a.Y - midV) * scale - rect.Top;
                    double sx1 = cx + (b.X - midU) * scale - rect.Left;
                    double sy1 = cy - (b.Y - midV) * scale - rect.Top;
                    double sx2 = cx + (c.X - midU) * scale - rect.Left;
                    double sy2 = cy - (c.Y - midV) * scale - rect.Top;

                    int xMin = Math.Max(2, (int)Math.Floor(Math.Min(sx0, Math.Min(sx1, sx2))));
                    int xMax = Math.Min(vw - 3, (int)Math.Ceiling(Math.Max(sx0, Math.Max(sx1, sx2))));
                    int yMin = Math.Max(28, (int)Math.Floor(Math.Min(sy0, Math.Min(sy1, sy2))));
                    int yMax = Math.Min(vh - 3, (int)Math.Ceiling(Math.Max(sy0, Math.Max(sy1, sy2))));
                    if (xMin > xMax || yMin > yMax) continue;

                    double denom = (sy1 - sy2) * (sx0 - sx2) + (sx2 - sx1) * (sy0 - sy2);
                    if (Math.Abs(denom) < 1e-6) continue;

                    double avgDepthNorm = ((a.Z + b.Z + c.Z) / 3.0 - minDepth) / depthRange;
                    double depthAtten = 1.0 - 0.28 * avgDepthNorm;

                    double diff1 = Math.Max(0.0, Vec3.Dot(n, light1));
                    double diff2 = Math.Max(0.0, Vec3.Dot(n, light2));
                    double spec = Math.Pow(Math.Max(0.0, -n.Z), 22.0) * 0.25;
                    double shade = Math.Min(1.15, (0.24 + 0.60 * diff1 + 0.20 * diff2) * depthAtten);

                    int cr = (int)Math.Max(0, Math.Min(255, baseColor.R * shade + 255 * spec));
                    int cg = (int)Math.Max(0, Math.Min(255, baseColor.G * shade + 255 * spec));
                    int cb = (int)Math.Max(0, Math.Min(255, baseColor.B * shade + 255 * spec));
                    Color col = Color.FromArgb(255, cr, cg, cb);

                    for (int py = yMin; py <= yMax; py++)
                    {
                        double dy = py + 0.5;
                        for (int px = xMin; px <= xMax; px++)
                        {
                            double dx = px + 0.5;
                            double w0 = ((sy1 - sy2) * (dx - sx2) + (sx2 - sx1) * (dy - sy2)) / denom;
                            double w1 = ((sy2 - sy0) * (dx - sx2) + (sx0 - sx2) * (dy - sy2)) / denom;
                            double w2 = 1.0 - w0 - w1;
                            if (w0 >= 0 && w1 >= 0 && w2 >= 0)
                            {
                                float z = (float)(w0 * a.Z + w1 * b.Z + w2 * c.Z);
                                if (z < zbuf[px, py])
                                {
                                    zbuf[px, py] = z;
                                    bmp.SetPixel(rect.Left + px, rect.Top + py, col);
                                }
                            }
                        }
                    }
                }
            }

            using (var labelFont = new Font("Segoe UI", 10, FontStyle.Bold))
            using (var dimFont = new Font("Consolas", 9, FontStyle.Regular))
            using (var labelBrush = new SolidBrush(Color.FromArgb(210, 220, 232)))
            using (var dimBrush = new SolidBrush(Color.FromArgb(140, 155, 175)))
            {
                g.DrawString(label, labelFont, labelBrush, rect.Left + 12, rect.Top + 8);
                string dimText = string.Format(CultureInfo.InvariantCulture,
                    "Dims: {0:F1} x {1:F1} x {2:F1} mm | {3:N0} tris",
                    span.X, span.Y, span.Z, totalTris);
                g.DrawString(dimText, dimFont, dimBrush, rect.Right - 310, rect.Top + 10);
            }
        }
    }

    public class Program
    {
        public static MeshData RotateClampToHandlebar(MeshData src, string newName, double offsetY)
        {
            var m = new MeshData(newName);
            foreach (var v in src.Vertices) m.Vertices.Add(new Vec3(v.X, (v.Z - 7.5) + offsetY, v.Y));
            foreach (var t in src.Triangles) m.Triangles.Add(new TriIdx(t.V1, t.V3, t.V2));
            return m;
        }

        // Rotate 180 deg around Z: (X, Y, Z) -> (-X + dx, -Y + dy, Z + dz), determinant +1 (preserves outward normals!)
        public static MeshData Rotate180Z(MeshData src, string newName, double dx, double dy, double dz)
        {
            var m = new MeshData(newName);
            foreach (var v in src.Vertices) m.Vertices.Add(new Vec3(-v.X + dx, -v.Y + dy, v.Z + dz));
            m.Triangles.AddRange(src.Triangles);
            return m;
        }

        // Transform a single Drop Arm from print space (X, Y, Z in [0..13]) to handlebar world space (saddle at Z_world = +24.5, Y_world = offsetY)
        public static MeshData TransformDropArmToWorld(MeshData pairMesh, bool takeFirstHalf, string newName, double offsetY)
        {
            var m = new MeshData(newName);
            int halfV = pairMesh.Vertices.Count / 2;
            int halfT = pairMesh.Triangles.Count / 2;
            int vStart = takeFirstHalf ? 0 : halfV;
            double shiftBackY = takeFirstHalf ? -22.0 : 22.0;
            for (int i = 0; i < halfV; i++)
            {
                var v = pairMesh.Vertices[vStart + i];
                double yLocal = v.Y + shiftBackY;
                m.Vertices.Add(new Vec3(v.X, (v.Z - 7.5) + offsetY, yLocal + 24.5));
            }
            int tStart = takeFirstHalf ? 0 : halfT;
            for (int i = 0; i < halfT; i++)
            {
                var t = pairMesh.Triangles[tStart + i];
                m.Triangles.Add(new TriIdx(t.V1 - vStart, t.V3 - vStart, t.V2 - vStart));
            }
            return m;
        }

        // Transform Plate-local coordinates (X_plate = up along 64-deg plate, Y_plate = lateral, Z_plate = forward normal) to Bike World Space
        // Anchor: (X_plate = +31.0, Z_plate = 0.0) -> (X_world = +50.0, Z_world = +12.5)
        public static MeshData TransformPlateSpaceToWorld(MeshData src, string newName, double dXPlate = 0.0, double dYPlate = 0.0, double dZPlate = 0.0)
        {
            double ux = -0.438371, uz = 0.898794;
            double nx = 0.898794,  nz = 0.438371;
            var m = new MeshData(newName);
            foreach (var v in src.Vertices)
            {
                double xp = v.X + dXPlate;
                double yp = v.Y + dYPlate;
                double zp = v.Z + dZPlate;
                double xw = 50.0 + (xp - 31.0) * ux + zp * nx;
                double yw = yp;
                double zw = 12.5 + (xp - 31.0) * uz + zp * nz;
                m.Vertices.Add(new Vec3(xw, yw, zw));
            }
            // Basis [u, (0,1,0), n] has determinant -1 -> swap V2 and V3
            foreach (var t in src.Triangles) m.Triangles.Add(new TriIdx(t.V1, t.V3, t.V2));
            return m;
        }

        // Deform the flat-printed 04e Top-Fisheye Visor Strap into its curved installed arch over the drone!
        public static MeshData CurveTopStrapForAssemblyRender(MeshData flatStrap)
        {
            var m = new MeshData("Installed_Top_Fisheye_Visor");
            foreach (var v in flatStrap.Vertices)
            {
                // In flatStrap: X in [-17..+12], Y in [-50..+57], Z in [0..5.4]
                // Curve Y into an arch over the drone at X_cradle = +60.6, Y_cradle in [-23..+23], Z_cradle in [14..46]!
                double u = Math.Max(-1.25, Math.Min(1.35, v.Y / 41.0));
                double angle = u * (Math.PI * 0.56); // +-100 deg arch
                double rad = 24.5 + v.Z;
                double yc = Math.Sin(angle) * 22.8;
                double zc = 20.5 + Math.Cos(angle) * rad;
                double xc = 60.5 + v.X;
                m.Vertices.Add(new Vec3(xc, yc, zc));
            }
            m.Triangles.AddRange(flatStrap.Triangles);
            return m;
        }

        // Orient 04d Nose-Vault Gimbal Guard into the Closed Nose Cup (X_cradle in [-69.5, -50.5], Z_cradle in [7.2, 35.2])
        public static MeshData PlaceNoseGuardInCradle(MeshData noseGuard)
        {
            var m = new MeshData("Installed_Nose_Guard");
            foreach (var v in noseGuard.Vertices)
            {
                // Print space: X in [-14..+14] (height), Y in [-17.4..+17.4] (width), Z in [0..19] (depth from front nose wall)
                // Cradle space: X_cradle = -69.5 + Z_print, Y_cradle = Y_print, Z_cradle = 21.5 + X_print
                // Basis [(0,0,1), (0,1,0), (1,0,0)] has determinant -1 -> swap V2, V3
                m.Vertices.Add(new Vec3(-69.5 + v.Z, v.Y, 21.5 + v.X));
            }
            foreach (var t in noseGuard.Triangles) m.Triangles.Add(new TriIdx(t.V1, t.V3, t.V2));
            return m;
        }

        public static void Main(string[] args)
        {
            string baseDir = @"C:\Users\ahsan\.gemini\antigravity\scratch\dji-neo2-orbea-mounts";
            string refModelXml = Path.Combine(baseDir, @"reference\extracted\3D\Objects\object_16.model");
            string stlDir = Path.Combine(baseDir, "stl");
            string threeMfDir = Path.Combine(baseDir, "3mf");
            string rendersDir = Path.Combine(baseDir, "renders");

            // Clean old files so only the sleek Number-Plate & Over-Stem system remains!
            if (Directory.Exists(stlDir)) Directory.Delete(stlDir, true);
            if (Directory.Exists(threeMfDir)) Directory.Delete(threeMfDir, true);
            Directory.CreateDirectory(stlDir);
            Directory.CreateDirectory(threeMfDir);
            Directory.CreateDirectory(rendersDir);

            bool doRenders = args != null && Array.IndexOf(args, "--renders") >= 0;

            Console.WriteLine("1. Building Watertight Boolean-Unioned DJI Neo 2 Cradles (01a Flat, 01b 45-Deg Easy-PA, 01c 72D TPU)...");
            var cradleShell = PartDesigns.LoadReferenceCradleMesh(refModelXml);

            var universalCradle = PartDesigns.BuildCradleBooleanUnion(
                "01a_DJI_Neo2_Universal_Cradle", cradleShell, PartDesigns.CradleMountingInsertSdf, 0.65);
            SaveAndReport(universalCradle, Path.Combine(stlDir, "01a_DJI_Neo2_Universal_Cradle.stl"));

            var cradle45WithFin = PartDesigns.BuildCradle45DegBooleanUnion(
                "01b_DJI_Neo2_Universal_Cradle_45Deg_EasyPA", cradleShell, includeAnchorFin: true, step: 0.65);
            SaveAndReport(cradle45WithFin, Path.Combine(stlDir, "01b_DJI_Neo2_Universal_Cradle_45Deg_EasyPA.stl"));

            var cradle72D = PartDesigns.BuildCradleBooleanUnion(
                "01c_DJI_Neo2_72D_TPU_Cradle_For_PETG_Insert", cradleShell, PartDesigns.Cradle72DMountingInsertSdf, 0.65);
            SaveAndReport(cradle72D, Path.Combine(stlDir, "01c_DJI_Neo2_72D_TPU_Cradle_For_PETG_Insert.stl"));

            var cradle45EasyPaFlex = PartDesigns.BuildCradle45DegBooleanUnion(
                "01d_DJI_Neo2_Cradle_45Deg_EasyPA_FlexClip", cradleShell, includeAnchorFin: true, step: 0.65, applyRearFlexClipTrim: true);
            SaveAndReport(cradle45EasyPaFlex, Path.Combine(stlDir, "01d_DJI_Neo2_Cradle_45Deg_EasyPA_FlexClip.stl"));

            var cradleFlatEasyPaFlex = PartDesigns.BuildCradleBooleanUnion(
                "01e_DJI_Neo2_Cradle_Flat_EasyPA_FlexClip", cradleShell, PartDesigns.CradleMountingInsertSdf, 0.50, applyRearFlexClipTrim: false, applyFlatEasyPaV2: true);
            SaveAndReport(cradleFlatEasyPaFlex, Path.Combine(stlDir, "01e_DJI_Neo2_Cradle_Flat_EasyPA_FlexClip.stl"));

            var cradleFlatEasyPaReinforced = PartDesigns.BuildCradleBooleanUnion(
                "01f_DJI_Neo2_Cradle_Flat_EasyPA_Reinforced", cradleShell, PartDesigns.CradleMountingInsertSdf, 0.50, applyRearFlexClipTrim: false, applyFlatEasyPaV2: false, applyFlatEasyPaReinforced: true);
            SaveAndReport(cradleFlatEasyPaReinforced, Path.Combine(stlDir, "01f_DJI_Neo2_Cradle_Flat_EasyPA_Reinforced.stl"));

            // 01g: Dedicated Flat 72D TPU Cradle (tapered 4.5->3.2mm arms give ideal firm snap-in retention & unbreakable Z-bonding in Elegoo 72D TPU!)
            var cradleFlat72DTpu = cradleFlatEasyPaReinforced.Clone("01g_DJI_Neo2_Cradle_Flat_72D_TPU");
            SaveAndReport(cradleFlat72DTpu, Path.Combine(stlDir, "01g_DJI_Neo2_Cradle_Flat_72D_TPU.stl"));

            if (doRenders)
            {
                var cradle45Only = PartDesigns.BuildCradle45DegBooleanUnion(
                    "Cradle_45Deg", cradleShell, includeAnchorFin: false, step: 0.70);
                var anchorFin45 = PartDesigns.BuildCradle45DegSnapOffAnchorFin(0.65);
                MeshRenderer.RenderMultiViewSheet(
                    Path.Combine(rendersDir, "01_Cradle_And_Insert_Preview.png"),
                    "01a - DJI Neo 2 Universal Cradle (Transceiver-Compatible)",
                    "Flat Z=0 Base | 15x15mm 4-Bolt Square + 30mm Centerline | Captive M4 DIN 934 Hex Nut Traps",
                    universalCradle,
                    Color.FromArgb(45, 125, 230));

                MeshRenderer.RenderMultiColorSheet(
                    Path.Combine(rendersDir, "09_Cradle_45Deg_EasyPA_Preview.png"),
                    "01b - 45-Deg Sunlu Easy-PA Cradle w/ Snap-Off A-Frame Bed Anchor Fin",
                    "Blue: 47.1 deg Self-Supporting Cradle (Max Hook & Cup Fiber Strength) | Gold: Perforated Snap-Off Stabilization Foot",
                    new List<ColoredMeshPart>
                    {
                        new ColoredMeshPart(cradle45Only, Color.FromArgb(45, 125, 230)),
                        new ColoredMeshPart(anchorFin45, Color.FromArgb(245, 165, 35))
                    });

                MeshRenderer.RenderMultiViewSheet(
                    Path.Combine(rendersDir, "13_Cradle_Flat_EasyPA_FlexClip_V2_Preview.png"),
                    "01e V2 - Flat 72D TPU Flex-Clip Cradle (2.8mm Uniform Arms)",
                    "29-Deg Self-Supporting Nose Chin | Lump-Free 2.8mm Rear Flex Arms | Flush 14x3.5mm FPV LiPo Velcro Strap Loops",
                    cradleFlatEasyPaFlex,
                    Color.FromArgb(65, 165, 245));

                MeshRenderer.RenderMultiViewSheet(
                    Path.Combine(rendersDir, "14_Cradle_Flat_EasyPA_Reinforced_Preview.png"),
                    "01f - Flat Sunlu Easy-PA Reinforced Cradle (Tapered 4.5->3.2mm Arms)",
                    "29-Deg Self-Supporting Nose Chin | Lump-Free Tapered 4.5->3.2mm Reinforced Arms (+125% Bend Strength) | Flush 14x3.5mm FPV Strap Loops",
                    cradleFlatEasyPaReinforced,
                    Color.FromArgb(95, 195, 140));
            }

            Console.WriteLine("2. Building Mount 1: Orbea Wild LT 'Stealth Fairing' Number-Plate Shield (02a) + Swept Drop Arms (02b)...");
            var numberPlateShield = PartDesigns.BuildOrbeaStealthNumberPlateShield(0.90);
            numberPlateShield.SnapBottomToZero();
            SaveAndReport(numberPlateShield, Path.Combine(stlDir, "02a_Orbea_Stealth_NumberPlate_Shield.stl"));

            var numberPlateDropArms = PartDesigns.BuildOrbeaStealthNumberPlateDropArmsPair(0.72);
            numberPlateDropArms.SnapBottomToZero();
            SaveAndReport(numberPlateDropArms, Path.Combine(stlDir, "02b_Orbea_Stealth_NumberPlate_Drop_Arms_Pair.stl"));

            var numberPlateShieldPause = PartDesigns.BuildOrbeaStealthNumberPlateShieldPauseEmbed(0.90);
            numberPlateShieldPause.SnapBottomToZero();
            SaveAndReport(numberPlateShieldPause, Path.Combine(stlDir, "02c_Orbea_Stealth_NumberPlate_Shield_PauseEmbed.stl"));

            if (doRenders)
            {
                MeshRenderer.RenderMultiViewSheet(
                    Path.Combine(rendersDir, "02_NumberPlate_Shield_And_Arms_Preview.png"),
                    "02a - Orbea Wild LT 'Stealth Fairing' Number-Plate Shield & Chin Roost Fender",
                    "Faceted 45-deg Stealth Winglets | Armored Lower Gimbal/LiDAR Chin Scoop | Recessed 90A TPU Isolator Seat | Zero Supports",
                    numberPlateShield,
                    Color.FromArgb(235, 105, 35));

                MeshRenderer.RenderMultiViewSheet(
                    Path.Combine(rendersDir, "02b_NumberPlate_Drop_Arms_Preview.png"),
                    "02b - Swept Out-Front Faceted Twin-Truss Handlebar Drop Arms (Pair)",
                    "64-deg Head-Tube Rake | 50mm Forward SIC Cable Tunnel Clearance | Tuned 72D TPU / Easy-PA Truss Cutouts",
                    numberPlateDropArms,
                    Color.FromArgb(75, 165, 225));
            }

            Console.WriteLine("3. Building Mount 2: Orbea Wild LT 'Stealth OC' Over-Stem Bridge (03a/03i) + Faceted Clamshells (03b/03c/03e/03f/03g/03h)...");
            var stemBridge = PartDesigns.BuildOrbeaStealthOverStemBridge(0.48, includeFrontProw: true, meshName: "03a_Orbea_Stealth_OverStem_Bridge");
            stemBridge.SnapBottomToZero();
            SaveAndReport(stemBridge, Path.Combine(stlDir, "03a_Orbea_Stealth_OverStem_Bridge.stl"));

            var stemBridgeCompact = PartDesigns.BuildOrbeaStealthOverStemBridge(0.48, includeFrontProw: false, meshName: "03i_Orbea_Stealth_OverStem_Bridge_Compact");
            stemBridgeCompact.SnapBottomToZero();
            SaveAndReport(stemBridgeCompact, Path.Combine(stlDir, "03i_Orbea_Stealth_OverStem_Bridge_Compact.stl"));

            var hingedClampsForTpu = PartDesigns.BuildHandlebarHingedClamshellPair(37.0, "03b_Orbea_Stealth_35mm_Hinged_Clamshells_For_TPU", 0.70);
            hingedClampsForTpu.SnapBottomToZero();
            SaveAndReport(hingedClampsForTpu, Path.Combine(stlDir, "03b_Orbea_Stealth_35mm_Hinged_Clamshells_For_TPU.stl"));

            var hingedClampsDirect = PartDesigns.BuildHandlebarHingedClamshellPair(35.0, "03c_Orbea_Stealth_35mm_Hinged_Clamshells_Direct_35mm", 0.70);
            hingedClampsDirect.SnapBottomToZero();
            SaveAndReport(hingedClampsDirect, Path.Combine(stlDir, "03c_Orbea_Stealth_35mm_Hinged_Clamshells_Direct_35mm.stl"));

            var heatSetClampsForTpu = PartDesigns.BuildHandlebarHingedClamshellPairHeatSet(37.0, "03e_Orbea_Stealth_Clamshells_HeatSet_M4_For_TPU", 0.70);
            heatSetClampsForTpu.SnapBottomToZero();
            SaveAndReport(heatSetClampsForTpu, Path.Combine(stlDir, "03e_Orbea_Stealth_Clamshells_HeatSet_M4_For_TPU.stl"));

            var heatSetClampsDirect = PartDesigns.BuildHandlebarHingedClamshellPairHeatSet(35.0, "03f_Orbea_Stealth_Clamshells_HeatSet_M4_Direct_35mm", 0.70);
            heatSetClampsDirect.SnapBottomToZero();
            SaveAndReport(heatSetClampsDirect, Path.Combine(stlDir, "03f_Orbea_Stealth_Clamshells_HeatSet_M4_Direct_35mm.stl"));

            var pauseNylocClampsForTpu = PartDesigns.BuildHandlebarHingedClamshellPairPauseNyloc(37.0, "03g_Orbea_Stealth_Clamshells_PauseNyloc_M4_For_TPU", 0.48);
            pauseNylocClampsForTpu.SnapBottomToZero();
            SaveAndReport(pauseNylocClampsForTpu, Path.Combine(stlDir, "03g_Orbea_Stealth_Clamshells_PauseNyloc_M4_For_TPU.stl"));

            var pauseNylocClampsDirect = PartDesigns.BuildHandlebarHingedClamshellPairPauseNyloc(35.0, "03h_Orbea_Stealth_Clamshells_PauseNyloc_M4_Direct_35mm", 0.48);
            pauseNylocClampsDirect.SnapBottomToZero();
            SaveAndReport(pauseNylocClampsDirect, Path.Combine(stlDir, "03h_Orbea_Stealth_Clamshells_PauseNyloc_M4_Direct_35mm.stl"));

            var tpuSleeves = PartDesigns.BuildHandlebarTpuSleeves(0.50);
            tpuSleeves.SnapBottomToZero();
            SaveAndReport(tpuSleeves, Path.Combine(stlDir, "03d_Handlebar_35mm_TPU_Grip_Sleeves_Pair.stl"));

            if (doRenders)
            {
                MeshRenderer.RenderMultiViewSheet(
                    Path.Combine(rendersDir, "03a_Stem_Bridge_Plate_Preview.png"),
                    "03a - Orbea Wild LT 'Stealth OC' Over-Stem Bridge w/ Front Roost Prow",
                    "Chiseled Forward Mud-Deflector Prow | Recessed 3.5mm TPU Isolator Pocket | 46mm OC MC11 Stem Arch",
                    stemBridge,
                    Color.FromArgb(55, 190, 125));

                MeshRenderer.RenderMultiViewSheet(
                    Path.Combine(rendersDir, "03b_Handlebar_C_Clips_Preview.png"),
                    "03b - Faceted OC-Styled Swinging M4 Pin-Hinged 35mm Handlebar Clamps",
                    "Zero Plastic Flexing Over 35mm Carbon Bar | 3-Knuckle M4 Bolt Hinge | Shared by Both Number-Plate & Over-Stem Mounts!",
                    hingedClampsForTpu,
                    Color.FromArgb(165, 95, 235));

                MeshRenderer.RenderMultiViewSheet(
                    Path.Combine(rendersDir, "04_TPU_Sleeves_And_Pads_Preview.png"),
                    "03d - Flanged 90A TPU Grip Sleeves for OC MC10 35.0mm Carbon Handlebar",
                    "35mm ID / 37mm OD w/ Retaining Lips | Protects Carbon Finish | High-Friction Anti-Slip & First-Stage Bar Damping",
                    tpuSleeves,
                    Color.FromArgb(40, 195, 210));
            }

            Console.WriteLine("4. Building 360-Deg 90A TPU Lens, Gimbal, Sensor & Vibration Armor Suite (04a - 04e) + PETG Inserts (05)...");
            var tpuIsolator = PartDesigns.BuildTpuVibrationIsolatorSystem(0.55, true);
            tpuIsolator.SnapBottomToZero();
            SaveAndReport(tpuIsolator, Path.Combine(stlDir, "04a_TPU_Vibration_Isolator_Puck_And_Grommets.stl"));

            var tpuBellyShield = PartDesigns.BuildTpuBellySensorShield(0.45);
            tpuBellyShield.SnapBottomToZero();
            SaveAndReport(tpuBellyShield, Path.Combine(stlDir, "04b_TPU_Belly_Sensor_Shield.stl"));

            var tpuHeatsinkLouver = PartDesigns.BuildTpuHeatsinkLouver(0.45);
            tpuHeatsinkLouver.SnapBottomToZero();
            SaveAndReport(tpuHeatsinkLouver, Path.Combine(stlDir, "04c_TPU_Heatsink_Mud_Louver.stl"));

            var tpuNoseGuard = PartDesigns.BuildTpuNoseVaultGimbalGuard(0.55);
            tpuNoseGuard.SnapBottomToZero();
            SaveAndReport(tpuNoseGuard, Path.Combine(stlDir, "04d_TPU_Nose_Vault_Gimbal_And_LiDAR_Guard.stl"));

            var tpuTopVisorStrap = PartDesigns.BuildTpuTopFisheyeLensVisorStrap(0.50);
            tpuTopVisorStrap.SnapBottomToZero();
            SaveAndReport(tpuTopVisorStrap, Path.Combine(stlDir, "04e_TPU_Top_Fisheye_Lens_Visor_Strap.stl"));

            var tpuSelfLockingStrap = PartDesigns.BuildTpu90ASelfLockingCradleStrap(0.36);
            tpuSelfLockingStrap.SnapBottomToZero();
            SaveAndReport(tpuSelfLockingStrap, Path.Combine(stlDir, "04f_TPU_90A_SelfLocking_Cradle_Strap.stl"));

            var tpuDecoupledPuck = PartDesigns.BuildTpuDecoupledShearIsolatorPuck(0.36);
            tpuDecoupledPuck.SnapBottomToZero();
            SaveAndReport(tpuDecoupledPuck, Path.Combine(stlDir, "04g_TPU_Decoupled_Shear_Isolator_Puck.stl"));

            var petgInsertKit = PartDesigns.BuildPetgReinforcementInsertKit(0.48);
            petgInsertKit.SnapBottomToZero();
            SaveAndReport(petgInsertKit, Path.Combine(stlDir, "05_PETG_Reinforcement_Inserts_For_72D_TPU.stl"));

            if (doRenders)
            {
            // Render Sheet 07: Complete 5-Part 90A TPU Lens & Vibration Armor Kit laid out cleanly
            var s04b = tpuBellyShield.Clone("b"); s04b.Translate(-52.0, 18.0, 0.0);
            var s04c = tpuHeatsinkLouver.Clone("c"); s04c.Translate(-52.0, -18.0, 0.0);
            var s04d = tpuNoseGuard.Clone("d"); s04d.Translate(48.0, 0.0, 0.0);
            var s04e = tpuTopVisorStrap.Clone("e"); s04e.Translate(84.0, 0.0, 0.0);
            MeshRenderer.RenderMultiColorSheet(
                Path.Combine(rendersDir, "07_TPU_Gimbal_And_Vibration_Protection_Preview.png"),
                "04a - 04e: Complete 360-Deg 90A TPU Lens, Gimbal & Vibration Armor Suite",
                "Cyan: 04a Shear Isolator | Gold: 04b Belly Sensor Plug | Green: 04c Louver | Orange: 04d Nose Gimbal/LiDAR Liner | Pink: 04e Top Fisheye Visor Strap",
                new List<ColoredMeshPart>
                {
                    new ColoredMeshPart(tpuIsolator, Color.FromArgb(40, 195, 210)),
                    new ColoredMeshPart(s04b, Color.FromArgb(245, 175, 40)),
                    new ColoredMeshPart(s04c, Color.FromArgb(65, 205, 130)),
                    new ColoredMeshPart(s04d, Color.FromArgb(240, 110, 50)),
                    new ColoredMeshPart(s04e, Color.FromArgb(225, 85, 165))
                });

            MeshRenderer.RenderMultiViewSheet(
                Path.Combine(rendersDir, "10_PETG_Inserts_For_72D_TPU_Preview.png"),
                "05 - Rigid PETG Fastener Reinforcement Inserts for 72D TPU Parts",
                "Unified 6-Nut Cradle Carrier Plate (Snaps into 01c) + 8x Press-In Flanged Hex-Collar M4 Bolt Bushings",
                petgInsertKit,
                Color.FromArgb(230, 75, 95));

            // Render Sheet 08: Universal Cradle Equipped w/ All 5 TPU Lens & Vibration Armor Pieces!
            var padOnly = PartDesigns.BuildTpuVibrationIsolatorSystem(0.55, false);
            var instIsolator = padOnly.Clone("InstIso"); instIsolator.Translate(9.50, 0.0, -7.50);
            var instBelly = tpuBellyShield.Clone("InstBelly"); instBelly.Translate(50.50, 0.0, -1.60);
            var instLouver = tpuHeatsinkLouver.Clone("InstLouver"); instLouver.Translate(-31.25, 0.0, -1.60);
            var instNose = PlaceNoseGuardInCradle(tpuNoseGuard);
            var instTopStrap = CurveTopStrapForAssemblyRender(tpuTopVisorStrap);

            MeshRenderer.RenderMultiColorSheet(
                Path.Combine(rendersDir, "08_Cradle_With_TPU_Armor_Assembly.png"),
                "360-Deg Lens-Armored Cradle Assembly (Every Camera, LiDAR & Vision Lens Shielded)",
                "Blue: Cradle | Orange: Nose Gimbal/LiDAR Vault | Pink: Top Fisheye Visor Strap | Gold: Belly Sensor Seal | Cyan: Shear Isolator",
                new List<ColoredMeshPart>
                {
                    new ColoredMeshPart(universalCradle, Color.FromArgb(45, 125, 230)),
                    new ColoredMeshPart(instIsolator, Color.FromArgb(40, 195, 210)),
                    new ColoredMeshPart(instBelly, Color.FromArgb(245, 175, 40)),
                    new ColoredMeshPart(instLouver, Color.FromArgb(65, 205, 130)),
                    new ColoredMeshPart(instNose, Color.FromArgb(240, 110, 50)),
                    new ColoredMeshPart(instTopStrap, Color.FromArgb(225, 85, 165))
                });

            // Render Sheet 05: Complete Assembly 1 - Orbea Stealth Number-Plate Mount on Handlebar Clamps!
            var closedClamp = PartDesigns.BuildHandlebarHingedClamshellSingle(37.0, assembledClosed: true, step: 0.70);
            var npClampL = RotateClampToHandlebar(closedClamp, "NP_Clamp_L", -31.5);
            var npClampR = RotateClampToHandlebar(closedClamp, "NP_Clamp_R", +31.5);
            var npArmL = TransformDropArmToWorld(numberPlateDropArms, true, "NP_Arm_L", -31.5);
            var npArmR = TransformDropArmToWorld(numberPlateDropArms, false, "NP_Arm_R", +31.5);
            var npShieldWorld = TransformPlateSpaceToWorld(numberPlateShield, "NP_Shield");
            var npIsoWorld = TransformPlateSpaceToWorld(padOnly, "NP_Iso", 9.50, 0.0, 6.0);
            var npCradleWorld = TransformPlateSpaceToWorld(universalCradle, "NP_Cradle", 0.0, 0.0, 13.5);
            var npNoseWorld = TransformPlateSpaceToWorld(instNose, "NP_Nose", 0.0, 0.0, 13.5);
            var npStrapWorld = TransformPlateSpaceToWorld(instTopStrap, "NP_Strap", 0.0, 0.0, 13.5);

            MeshRenderer.RenderMultiColorSheet(
                Path.Combine(rendersDir, "05_NumberPlate_Assembly_Preview.png"),
                "Assembly 1 - Orbea Wild LT 'Stealth Fairing' Out-Front Number-Plate Mount",
                "64-deg Head-Tube Rake | Armored Lower Chin Roost Fender + Side Winglets | Dual-Stage 72D + 90A Damping | Full SIC Cable Clearance",
                new List<ColoredMeshPart>
                {
                    new ColoredMeshPart(npClampL, Color.FromArgb(165, 95, 235)),
                    new ColoredMeshPart(npClampR, Color.FromArgb(165, 95, 235)),
                    new ColoredMeshPart(npArmL, Color.FromArgb(75, 165, 225)),
                    new ColoredMeshPart(npArmR, Color.FromArgb(75, 165, 225)),
                    new ColoredMeshPart(npShieldWorld, Color.FromArgb(235, 105, 35)),
                    new ColoredMeshPart(npIsoWorld, Color.FromArgb(40, 195, 210)),
                    new ColoredMeshPart(npCradleWorld, Color.FromArgb(45, 125, 230)),
                    new ColoredMeshPart(npNoseWorld, Color.FromArgb(240, 110, 50)),
                    new ColoredMeshPart(npStrapWorld, Color.FromArgb(225, 85, 165))
                });

            // Render Sheet 06: Complete Assembly 2 - Restyled Orbea Stealth Over-Stem Mount (Oriented Nose-Forward over the Front Roost Prow!)
            var osClampL = RotateClampToHandlebar(closedClamp, "OS_Clamp_L", -30.5);
            var osClampR = RotateClampToHandlebar(closedClamp, "OS_Clamp_R", +30.5);
            var osBridge = stemBridge.Clone("OS_Bridge"); osBridge.Translate(0, 0, 24.5);
            var osIso = padOnly.Clone("OS_Iso"); osIso.Translate(0, 0, 24.5 + 10.5);
            var osCradle = Rotate180Z(universalCradle, "OS_Cradle", +9.50, 0.0, 24.5 + 18.0);
            var osNose = Rotate180Z(instNose, "OS_Nose", +9.50, 0.0, 24.5 + 18.0);
            var osStrap = Rotate180Z(instTopStrap, "OS_Strap", +9.50, 0.0, 24.5 + 18.0);

            MeshRenderer.RenderMultiColorSheet(
                Path.Combine(rendersDir, "06_OverStem_Assembly_Preview.png"),
                "Assembly 2 - Orbea Wild LT 'Stealth OC' Over-Stem Cockpit Mount",
                "Nose-Forward Over Chiseled Roost Prow | Recessed 90A TPU Shear Isolator | Zero-Flex Clamshells + 360-Deg Lens Armor",
                new List<ColoredMeshPart>
                {
                    new ColoredMeshPart(osClampL, Color.FromArgb(165, 95, 235)),
                    new ColoredMeshPart(osClampR, Color.FromArgb(165, 95, 235)),
                    new ColoredMeshPart(osBridge, Color.FromArgb(55, 190, 125)),
                    new ColoredMeshPart(osIso, Color.FromArgb(40, 195, 210)),
                    new ColoredMeshPart(osCradle, Color.FromArgb(45, 125, 230)),
                    new ColoredMeshPart(osNose, Color.FromArgb(240, 110, 50)),
                    new ColoredMeshPart(osStrap, Color.FromArgb(225, 85, 165))
                });
            }

            Console.WriteLine("5. Packaging Bambu Studio .3mf build plates for Bambu Lab P2S...");
            // Plate 1: Number-Plate Shield + Swept Drop Arms (Elegoo 72D TPU or Sunlu Easy-PA / ASA) - ZERO SUPPORTS!
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate1_NumberPlate_Stealth_Mount_72D_Or_EasyPA.3mf"),
                "Orbea Wild LT - Stealth Number-Plate Shield & Swept Drop Arms (Zero Supports)",
                new List<PlateItem>
                {
                    new PlateItem(numberPlateShield, -42.0, 0.0, 0.0),
                    new PlateItem(numberPlateDropArms, 64.0, 0.0, 0.0)
                });

            // Plate 1b: Stealth Number-Plate Shield w/ Fully-Embedded Print-Pause M4 DIN 934 Nuts + Drop Arms
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate1b_NumberPlate_Shield_PauseEmbed_Nuts.3mf"),
                "Orbea Wild LT - Stealth Number-Plate Shield (Pause-Embed M4 Nuts: Z=4.6mm & Z=8.4mm) & Drop Arms",
                new List<PlateItem>
                {
                    new PlateItem(numberPlateShieldPause, -42.0, 0.0, 0.0),
                    new PlateItem(numberPlateDropArms, 64.0, 0.0, 0.0)
                });

            // Plate 2: Over-Stem Stealth Bridge + Hinged 35mm Handlebar Clamshells (For 1mm TPU Sleeves)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate2_OverStem_Stealth_Bridge_And_Clamps.3mf"),
                "Orbea Wild LT - Stealth OC Over-Stem Bridge & 35mm Hinged Clamshells (For TPU Sleeves)",
                new List<PlateItem>
                {
                    new PlateItem(stemBridge, 0.0, 48.0, 0.0),
                    new PlateItem(hingedClampsForTpu, 0.0, -32.0, 0.0)
                });

            // Plate 2b: Standalone Direct-Fit 35.0mm Hinged Clamshells (No TPU Sleeves Needed)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate2b_Direct_35mm_Clamps_No_TPU_Sleeves.3mf"),
                "Orbea Wild LT - Direct-Fit 35.0mm Hinged Handlebar Clamshells (No TPU Sleeves)",
                new List<PlateItem>
                {
                    new PlateItem(hingedClampsDirect, 0.0, 0.0, 0.0)
                });

            // Plate 2c: M4 Heat-Set Brass Insert Hinged Clamshells (Both 37mm For TPU & 35mm Direct-Fit — delete whichever pair you don't need!)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate2c_HeatSet_M4_Clamps_35mm_And_37mm.3mf"),
                "Orbea Wild LT - M4 Heat-Set Brass Insert Handlebar Clamshells (37mm For TPU & 35mm Direct)",
                new List<PlateItem>
                {
                    new PlateItem(heatSetClampsForTpu, 0.0, 42.0, 0.0),
                    new PlateItem(heatSetClampsDirect, 0.0, -42.0, 0.0)
                });

            // Plate 2d: Pause-Embed M4 Nyloc (DIN 985) Hinged Clamshells (1 Single Pause @ Z=11.80mm | Both 37mm For TPU & 35mm Direct — delete whichever pair you don't need!)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate2d_PauseEmbed_Nyloc_Clamps_35mm_And_37mm.3mf"),
                "Orbea Wild LT - Pause-Embed M4 Nyloc Clamshells (Pause @ Z=11.80mm | 37mm For TPU & 35mm Direct)",
                new List<PlateItem>
                {
                    new PlateItem(pauseNylocClampsForTpu, 0.0, 42.0, 0.0),
                    new PlateItem(pauseNylocClampsDirect, 0.0, -42.0, 0.0)
                });

            // Plate 2e: Trimmed Compact Flush-Front Over-Stem Bridge (03i — No Front Beak, Outboard ±22.5mm Slots, Crisp 6.90mm Hex Pockets)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate2e_OverStem_Bridge_Compact.3mf"),
                "Orbea Wild LT - Compact Flush-Front Over-Stem Bridge (No Front Beak, Outboard ±22.5mm Slots)",
                new List<PlateItem>
                {
                    new PlateItem(stemBridgeCompact, 0.0, 0.0, 0.0)
                });

            // Plate 3: Complete 90A TPU 360-Deg Lens, Gimbal & Vibration Armor Suite (All 6 TPU Parts on 1 Plate!)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate3_90A_TPU_Lens_Gimbal_And_Vibration_Armor.3mf"),
                "DJI Neo 2 - Complete 90A TPU Lens, Gimbal & Vibration Armor Suite",
                new List<PlateItem>
                {
                    new PlateItem(tpuSleeves, -52.0, 48.0, 0.0),
                    new PlateItem(tpuIsolator, -45.0, -18.0, 0.0),
                    new PlateItem(tpuBellyShield, 15.0, 48.0, 0.0),
                    new PlateItem(tpuHeatsinkLouver, 15.0, 12.0, 0.0),
                    new PlateItem(tpuNoseGuard, 18.0, -32.0, 0.0),
                    new PlateItem(tpuTopVisorStrap, 66.0, 0.0, 0.0)
                });

            // Plate 3b: Standalone 90A TPU Self-Locking Cradle Strap w/ Molded Mushroom-Head Hook Boss & 4-Position Eyelet Ladder (Zero Supports!)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate3b_90A_TPU_SelfLocking_Cradle_Strap.3mf"),
                "DJI Neo 2 - 90A TPU Self-Locking Cradle Strap (Molded Mushroom Boss + 4-Position Eyelet Ladder, Zero Supports)",
                new List<PlateItem>
                {
                    new PlateItem(tpuSelfLockingStrap, 0.0, 0.0, 0.0)
                });

            // Plate 3c: True 100% Decoupled 10.0mm 90A TPU Shear-Isolator Puck (4x Cradle Bolts + 2x Bridge Bolts, Zero Steel-to-Steel Path!)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate3c_90A_TPU_Decoupled_Shear_Isolator_Puck.3mf"),
                "DJI Neo 2 - 10mm Decoupled 90A TPU Shear-Isolator Puck (Zero Steel-to-Steel Path, Zero Supports)",
                new List<PlateItem>
                {
                    new PlateItem(tpuDecoupledPuck, 0.0, 0.0, 0.0)
                });

            // Plate 4: 45-Deg Sunlu Easy-PA Cradle w/ Snap-Off A-Frame Anchor Fin (+ Flat 72D Cradle Option)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate4_Cradles_45Deg_EasyPA_And_Flat_72D.3mf"),
                "DJI Neo 2 - 45-Deg Easy-PA Cradle & Flat 72D TPU Cradle",
                new List<PlateItem>
                {
                    new PlateItem(cradle45WithFin, 0.0, -38.0, 0.0),
                    new PlateItem(cradle72D, 0.0, 42.0, 0.0)
                });

            // Plate 4b: 45-Deg Sunlu Easy-PA / PETG Flex-Clip Cradle (2.8mm Thinned Outer Rear Arms, ~60% Easier Snap-In, Same Grip)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate4b_Cradle_45Deg_EasyPA_FlexClip.3mf"),
                "DJI Neo 2 - 45-Deg Easy-PA / PETG Flex-Clip Cradle (Thinned Rear Arms, Zero Supports)",
                new List<PlateItem>
                {
                    new PlateItem(cradle45EasyPaFlex, 0.0, 0.0, 0.0)
                });

            // Plate 4c: FLAT 72D TPU Flex-Clip Cradle (2.8mm Uniform Rear Flex Arms, 29-Deg Nose Chin, 14x3.5mm FPV Velcro Strap Loops)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate4c_Cradle_Flat_EasyPA_FlexClip.3mf"),
                "DJI Neo 2 - Flat 72D TPU Flex-Clip Cradle (29-Deg Nose Chin, Smooth 2.8mm Arms, 14x3.5mm FPV Strap Loops)",
                new List<PlateItem>
                {
                    new PlateItem(cradleFlatEasyPaFlex, 0.0, 0.0, 0.0)
                });

            // Plate 4d: FLAT Sunlu Easy-PA Reinforced Cradle (Tapered 4.5->3.2mm Lump-Free Arms, 29-Deg Nose Chin, 14x3.5mm FPV Velcro Strap Loops)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate4d_Cradle_Flat_EasyPA_Reinforced.3mf"),
                "DJI Neo 2 - Flat Easy-PA Reinforced Cradle (Tapered 4.5->3.2mm Arms, 29-Deg Nose Chin, 14x3.5mm FPV Strap Loops)",
                new List<PlateItem>
                {
                    new PlateItem(cradleFlatEasyPaReinforced, 0.0, 0.0, 0.0)
                });

            // Plate 4e: DEDICATED FLAT 72D TPU CRADLE (Tapered 4.5->3.2mm Unbreakable Arms, 29-Deg Nose Chin, 14x3.5mm Strap Loops for 04f 90A Strap)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate4e_Cradle_Flat_72D_TPU.3mf"),
                "DJI Neo 2 - Flat Elegoo 72D TPU Cradle (Tapered 4.5->3.2mm Arms, 29-Deg Nose Chin, 14x3.5mm Strap Loops)",
                new List<PlateItem>
                {
                    new PlateItem(cradleFlat72DTpu, 0.0, 0.0, 0.0)
                });

            // 90A TPU Exo-Cradles (01h Hybrid + 05c Rigid Spine, 01i All-90A One-Piece) & 04h Nose-to-Tail Over-Top Strap!
            Console.WriteLine("6. Building 90A TPU Exo-Cradles (01h Hybrid + 05c Rigid Spine, 01i One-Piece) & 04h Nose-to-Tail Strap...");
            var cradle90AHybrid = PartDesigns.BuildCradle90ATpuExo("01h_DJI_Neo2_90A_TPU_ExoCradle_Hybrid", cradleShell, hybridWithSpineRecess: true, step: 0.36);
            SaveAndReport(cradle90AHybrid, Path.Combine(stlDir, "01h_DJI_Neo2_90A_TPU_ExoCradle_Hybrid.stl"));

            var rigidSpinePlate = PartDesigns.BuildRigidBackboneSpinePlate(0.28);
            rigidSpinePlate.SnapBottomToZero();
            SaveAndReport(rigidSpinePlate, Path.Combine(stlDir, "05c_Rigid_Backbone_Spine_72D_Or_PETG.stl"));

            var cradle90AOnePiece = PartDesigns.BuildCradle90ATpuExo("01i_DJI_Neo2_90A_TPU_ExoCradle_OnePiece", cradleShell, hybridWithSpineRecess: false, step: 0.36);
            SaveAndReport(cradle90AOnePiece, Path.Combine(stlDir, "01i_DJI_Neo2_90A_TPU_ExoCradle_OnePiece.stl"));

            var tpuNoseToTailStrap = PartDesigns.BuildTpu90ANoseToTailStrap(0.36);
            tpuNoseToTailStrap.SnapBottomToZero();
            SaveAndReport(tpuNoseToTailStrap, Path.Combine(stlDir, "04h_TPU_90A_NoseToTail_OverTop_Strap.stl"));

            var tpuGimbalLockClip = PartDesigns.BuildTpu90AGimbalLockClip(0.24);
            tpuGimbalLockClip.SnapBottomToZero();
            SaveAndReport(tpuGimbalLockClip, Path.Combine(stlDir, "04i_TPU_90A_Gimbal_Lock_Clip.stl"));

            // Plate 3d: 90A TPU Nose-to-Tail Over-Top Strap (Pre-Rotated 45-Deg for 256x256 P2S Bed!)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate3d_90A_TPU_NoseToTail_OverTop_Strap.3mf"),
                "DJI Neo 2 - 90A TPU Nose-to-Tail Over-Top Strap (45-Deg Diagonal, Zero Supports)",
                new List<PlateItem>
                {
                    new PlateItem(tpuNoseToTailStrap, 0.0, 0.0, 0.0)
                });

            // Plate 3e: Low-Profile 90A TPU Snap-In Gimbal Lock Clip (Fits Inside 01h/01i/01g Cradle Nose Vault, Zero Supports!)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate3e_90A_TPU_Gimbal_Lock_Clip.3mf"),
                "DJI Neo 2 - Low-Profile 90A TPU Snap-In Gimbal Lock Clip (Non-Contact Lens Dome, Zero Supports)",
                new List<PlateItem>
                {
                    new PlateItem(tpuGimbalLockClip, 0.0, 0.0, 0.0)
                });

            // Plate 4f: Hybrid 90A TPU Exo-Cradle (Integrated Gimbal Vault, Belly/Heatsink Armor, Nose-to-Tail Strap Loops, Underside Spine Recess)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate4f_Cradle_90A_TPU_ExoCradle_Hybrid.3mf"),
                "DJI Neo 2 - Hybrid 90A TPU Exo-Cradle (Integrated Gimbal/Belly Armor + Underside Recess for 05c Spine, Zero Supports)",
                new List<PlateItem>
                {
                    new PlateItem(cradle90AHybrid, 0.0, 0.0, 0.0)
                });

            // Plate 4g: All-90A One-Piece Thick-Keel Exo-Cradle (Integrated Gimbal/Belly Armor + Stiffening Side-Keels, Zero Supports)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate4g_Cradle_90A_TPU_ExoCradle_OnePiece.3mf"),
                "DJI Neo 2 - All-90A One-Piece Thick-Keel Exo-Cradle (Integrated Gimbal/Belly Armor + Side-Keels, Zero Supports)",
                new List<PlateItem>
                {
                    new PlateItem(cradle90AOnePiece, 0.0, 0.0, 0.0)
                });

            // Plate 5b: Rigid 72D TPU / PETG Backbone Spine Plate for 01h Hybrid 90A Exo-Cradle (4x 6.90mm Zero-Spin M4 Nyloc Pockets!)
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate5b_Rigid_Backbone_Spine_72D_Or_PETG.3mf"),
                "DJI Neo 2 - Rigid 72D TPU / PETG Backbone Spine Plate for 01h Hybrid 90A Exo-Cradle",
                new List<PlateItem>
                {
                    new PlateItem(rigidSpinePlate, 0.0, 0.0, 0.0)
                });

            // Plate 5: Rigid PETG Fastener Reinforcement Inserts for 72D TPU Parts
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate5_PETG_Inserts_For_72D_TPU.3mf"),
                "DJI Neo 2 - Rigid PETG Fastener Reinforcement Inserts",
                new List<PlateItem>
                {
                    new PlateItem(petgInsertKit, 0.0, 0.0, 0.0)
                });

            Console.WriteLine("ALL STEALTH NUMBER-PLATE & OVER-STEM FILES GENERATED SUCCESSFULLY.");
        }

        private static void SaveAndReport(MeshData m, string path)
        {
            m.SaveBinaryStl(path);
            Vec3 minB, maxB;
            m.GetBounds(out minB, out maxB);
            Vec3 span = maxB - minB;
            double volCm3 = m.ComputeSignedVolume() / 1000.0;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "   Saved {0}: {1:N0} verts, {2:N0} tris | Bounds: {3:F2} x {4:F2} x {5:F2} mm | Vol: {6:F2} cm^3",
                Path.GetFileName(path), m.Vertices.Count, m.Triangles.Count, span.X, span.Y, span.Z, volCm3));
        }
    }
}
