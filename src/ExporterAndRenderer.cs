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
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture, @" <metadata name=""Title"">{0}</metadata>", title));
                    sw.WriteLine(@" <metadata name=""Application"">BambuStudio</metadata>");
                    sw.WriteLine(@" <resources>");

                    for (int i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        int objId = i + 1;
                        sw.WriteLine(string.Format(CultureInfo.InvariantCulture, @"  <object id=""{0}"" name=""{1}"" type=""model"">", objId, item.Mesh.Name));
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
                    yawDeg: 42.0, pitchDeg: 26.0);
                RenderViewport(bmp, g, new Rectangle(710, 92, 670, 410), "TOP ORTHOGRAPHIC VIEW (+Z)", parts,
                    yawDeg: 0.0, pitchDeg: 89.9);
                RenderViewport(bmp, g, new Rectangle(20, 518, 670, 410), "UNDERSIDE / MOUNTING VIEW (-Z)", parts,
                    yawDeg: 0.0, pitchDeg: -89.9);
                RenderViewport(bmp, g, new Rectangle(710, 518, 670, 410), "ISOMETRIC REAR / UNDERSIDE VIEW", parts,
                    yawDeg: -138.0, pitchDeg: 22.0);

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
        private static MeshData RotateFromPrintToHandlebar(MeshData src, string newName, double offsetY)
        {
            var m = new MeshData(newName);
            foreach (var v in src.Vertices)
            {
                m.Vertices.Add(new Vec3(v.X, (v.Z - 7.5) + offsetY, v.Y));
            }
            foreach (var t in src.Triangles)
            {
                m.Triangles.Add(new TriIdx(t.V1, t.V3, t.V2));
            }
            return m;
        }

        // Rotate Universal Cradle 90 deg onto its side to mount onto the Vertical L-Tower wall (Y_tower = -13.0mm, Z_tower = 100.0mm)
        // Mapping: (X, Y, Z) -> (X - 9.50, Z - 13.00, -Y + 100.00) has determinant +1 (preserves outward normals!)
        private static MeshData RotateCradleSidewaysOnTower(MeshData src, string newName)
        {
            var m = new MeshData(newName);
            foreach (var v in src.Vertices)
            {
                m.Vertices.Add(new Vec3(v.X - 9.50, v.Z - 13.00, -v.Y + 100.00));
            }
            m.Triangles.AddRange(src.Triangles);
            return m;
        }

        public static void Main(string[] args)
        {
            string baseDir = @"C:\Users\ahsan\.gemini\antigravity\scratch\dji-neo2-orbea-mounts";
            string refModelXml = Path.Combine(baseDir, @"reference\extracted\3D\Objects\object_16.model");
            string stlDir = Path.Combine(baseDir, "stl");
            string threeMfDir = Path.Combine(baseDir, "3mf");
            string rendersDir = Path.Combine(baseDir, "renders");

            Directory.CreateDirectory(stlDir);
            Directory.CreateDirectory(threeMfDir);
            Directory.CreateDirectory(rendersDir);

            Console.WriteLine("1. Loading reference DJI Neo 2 cradle shell and building Universal Cradle...");
            var cradleShell = PartDesigns.LoadReferenceCradleMesh(refModelXml);
            var cradleInsert = PartDesigns.BuildCradleMountingInsert(0.30);
            var universalCradle = cradleShell.Clone("01_DJI_Neo2_Universal_Cradle");
            universalCradle.Append(cradleInsert);
            SaveAndReport(universalCradle, Path.Combine(stlDir, "01_DJI_Neo2_Universal_Cradle.stl"));

            MeshRenderer.RenderMultiViewSheet(
                Path.Combine(rendersDir, "01_Cradle_And_Insert_Preview.png"),
                "01 - DJI Neo 2 Universal Cradle (Transceiver-Compatible)",
                "Flat Z=0 base | 15x15mm 4-Bolt Square + 30mm Centerline | Captive M4 DIN 934 Hex Nut Pockets",
                universalCradle,
                Color.FromArgb(45, 125, 230));

            Console.WriteLine("2. Building Side-Mount (Edgewise / Knee-Clearance) Downtube Bottle Cage + Fidlock L-Tower...");
            var downtubeSideTower = PartDesigns.BuildDowntubeSideMountLTower(0.42);
            SaveAndReport(downtubeSideTower, Path.Combine(stlDir, "02a_Orbea_Wild_Downtube_SideMount_LTower.stl"));

            MeshRenderer.RenderMultiViewSheet(
                Path.Combine(rendersDir, "02_Downtube_Fidlock_Mount_Preview.png"),
                "02a - Orbea Wild LT Side-Mount Downtube L-Tower (Bottle Boss + Fidlock TWIST)",
                "Holds Drone Edgewise Between Top Tube & Downtube (46mm Base Width) | Lower Prop-Guard Saddles | Fidlock Receiver",
                downtubeSideTower,
                Color.FromArgb(235, 110, 40));

            Console.WriteLine("3. Building Downtube Side-Mount TPU Prop-Guard Saddle Cushions (Pair)...");
            var downtubeTpuPads = PartDesigns.BuildDowntubeTpuSaddlePads(0.25);
            SaveAndReport(downtubeTpuPads, Path.Combine(stlDir, "02b_Downtube_TPU_Damping_Pads.stl"));

            Console.WriteLine("4. Building Integrated One-Piece Side-Mount Downtube L-Tower + Sideways Cradle...");
            var sidewaysCradle = RotateCradleSidewaysOnTower(universalCradle, "Sideways_Cradle");
            var integratedDowntube = downtubeSideTower.Clone("02c_Integrated_Downtube_SideMount_Cradle");
            integratedDowntube.Append(sidewaysCradle);
            SaveAndReport(integratedDowntube, Path.Combine(stlDir, "02c_Integrated_Downtube_SideMount_Cradle.stl"));

            // Also save the flat downtube mount as 02d in case user wants both options
            var downtubeFlat = PartDesigns.BuildDowntubeFlatFidlockMount(0.45);
            SaveAndReport(downtubeFlat, Path.Combine(stlDir, "02d_Orbea_Wild_Downtube_Flat_Fidlock_Mount.stl"));

            // Render Multi-Color Side-Mount Downtube Assembly
            MeshRenderer.RenderMultiColorSheet(
                Path.Combine(rendersDir, "05_Downtube_Assembly_Preview.png"),
                "Assembly 1 - Side-Mount Downtube L-Tower (Edgewise Between Top Tube & Downtube)",
                "Only 58mm Total Width (Zero Knee Interference!) | Orange: Fidlock/64mm L-Tower + Prop Saddles | Blue: Sideways Cradle",
                new List<ColoredMeshPart>
                {
                    new ColoredMeshPart(downtubeSideTower, Color.FromArgb(235, 110, 40)),
                    new ColoredMeshPart(sidewaysCradle, Color.FromArgb(45, 125, 230))
                });

            Console.WriteLine("5. Building Over-Stem Bridge Plate for OC Mountain Control MC11 40mm Stem...");
            var stemBridge = PartDesigns.BuildStemBridgePlate(0.38);
            SaveAndReport(stemBridge, Path.Combine(stlDir, "03a_Orbea_MC11_Stem_Bridge_Plate.stl"));

            MeshRenderer.RenderMultiViewSheet(
                Path.Combine(rendersDir, "03a_Stem_Bridge_Plate_Preview.png"),
                "03a - Orbea OC MC11 Over-Stem Bridge Plate",
                "46mm Stem Faceplate Arch | 50-72mm Slotted C-Clip Channels | 0 deg / 90 deg Captive M4 Cradle Mount",
                stemBridge,
                Color.FromArgb(60, 185, 120));

            Console.WriteLine("6. Building Swinging M4 Pin-Hinged Clamshell Handlebar Clamps (37.0mm Bore for TPU Sleeve)...");
            var hingedClampsForTpu = PartDesigns.BuildHandlebarHingedClamshellPair(37.0, "03b_Handlebar_35mm_Hinged_Clamshell_Clamps_For_TPU", 0.34);
            SaveAndReport(hingedClampsForTpu, Path.Combine(stlDir, "03b_Handlebar_35mm_Hinged_Clamshell_Clamps_For_TPU.stl"));

            MeshRenderer.RenderMultiViewSheet(
                Path.Combine(rendersDir, "03b_Handlebar_C_Clips_Preview.png"),
                "03b - Swinging M4 Pin-Hinged Clamshell Handlebar Clamps (Zero Plastic Flexing!)",
                "3-Knuckle Front M4 Bolt Hinge Swings Open 180 deg Over 35mm Bar | Rear M4 Pinch Bolt + Captive M4 Nut",
                hingedClampsForTpu,
                Color.FromArgb(170, 95, 235));

            Console.WriteLine("7. Building Single-Bolt Hook-In Clamshell Handlebar Clamps (37.0mm Bore for TPU Sleeve)...");
            var hookHingeClamps = PartDesigns.BuildHandlebarHookHingeClampsPair(37.0, "03c_Handlebar_35mm_HookHinge_Clamps_For_TPU", 0.34);
            SaveAndReport(hookHingeClamps, Path.Combine(stlDir, "03c_Handlebar_35mm_HookHinge_Clamps_For_TPU.stl"));

            Console.WriteLine("8. Building TPU Carbon Handlebar Grip Sleeves Pair (35.0mm ID / 37.0mm OD)...");
            var tpuSleeves = PartDesigns.BuildHandlebarTpuSleeves(0.28);
            SaveAndReport(tpuSleeves, Path.Combine(stlDir, "03d_Handlebar_35mm_TPU_Grip_Sleeves_Pair.stl"));

            MeshRenderer.RenderMultiViewSheet(
                Path.Combine(rendersDir, "04_TPU_Sleeves_And_Pads_Preview.png"),
                "03d - TPU Carbon Handlebar Grip Sleeves (35mm ID / 37mm OD Flanged)",
                "Protects OC MC10 Carbon Bar | High-Friction Rotational Grip | Damps High-Frequency Trail Vibration",
                tpuSleeves,
                Color.FromArgb(40, 195, 210));

            Console.WriteLine("9. Building Integrated One-Piece Stem Bridge + Cradle...");
            var integratedStem = stemBridge.Clone("03e_Integrated_Stem_Bridge_Cradle");
            var shiftedCradleForStem = universalCradle.Clone("Cradle_Upper");
            shiftedCradleForStem.Translate(-9.50, 0.0, 13.95);
            integratedStem.Append(shiftedCradleForStem);
            SaveAndReport(integratedStem, Path.Combine(stlDir, "03e_Integrated_Stem_Bridge_Cradle.stl"));

            Console.WriteLine("10. Building Swinging M4 Pin-Hinged Clamshell Handlebar Clamps (35.0mm Direct Bore)...");
            var hingedClampsDirect = PartDesigns.BuildHandlebarHingedClamshellPair(35.0, "03f_Handlebar_35mm_Hinged_Clamshell_Direct_35mm", 0.34);
            SaveAndReport(hingedClampsDirect, Path.Combine(stlDir, "03f_Handlebar_35mm_Hinged_Clamshell_Direct_35mm.stl"));

            Console.WriteLine("11. Building TPU Vibration-Isolator Sandwich Puck + 6x Flanged M4 Grommets...");
            var tpuIsolator = PartDesigns.BuildTpuVibrationIsolatorSystem(0.26);
            SaveAndReport(tpuIsolator, Path.Combine(stlDir, "04a_TPU_Vibration_Isolator_Puck_And_Grommets.stl"));

            Console.WriteLine("12. Building TPU Gimbal Lock Cushion & Downward Sensor Shield...");
            var tpuGimbalShield = PartDesigns.BuildTpuGimbalAndSensorShield(0.24);
            SaveAndReport(tpuGimbalShield, Path.Combine(stlDir, "04b_TPU_Gimbal_Lock_And_Sensor_Shield.stl"));

            Console.WriteLine("13. Building TPU Heatsink Window Mud-Guard Louver...");
            var tpuLouver = PartDesigns.BuildTpuHeatsinkLouver(0.24);
            SaveAndReport(tpuLouver, Path.Combine(stlDir, "04c_TPU_Heatsink_Mud_Louver.stl"));

            // Render sheet 07: All 3 TPU Gimbal/Sensor/Vibration Protection Parts laid out together
            var shieldForSheet = tpuGimbalShield.Clone("Shield");
            shieldForSheet.Translate(48.0, 5.0, 0.0);
            var louverForSheet = tpuLouver.Clone("Louver");
            louverForSheet.Translate(-50.0, 0.0, 0.0);
            MeshRenderer.RenderMultiColorSheet(
                Path.Combine(rendersDir, "07_TPU_Gimbal_And_Vibration_Protection_Preview.png"),
                "04a, 04b, 04c - TPU Vibration Isolator + Gimbal Lock Cushion + Sensor/Heatsink Shields",
                "Center: 04a Shear/Bellows Isolator + 6x M4 Grommets | Right: 04b Gimbal Lock & Sensor Shield | Left: 04c Heatsink Louver",
                new List<ColoredMeshPart>
                {
                    new ColoredMeshPart(tpuIsolator, Color.FromArgb(40, 195, 210)),
                    new ColoredMeshPart(shieldForSheet, Color.FromArgb(245, 165, 35)),
                    new ColoredMeshPart(louverForSheet, Color.FromArgb(65, 205, 130))
                });

            // Render sheet 08: Cradle with TPU Vibration Isolator + TPU Gimbal Lock/Sensor Shield + TPU Heatsink Louver installed!
            var installedIsolator = PartDesigns.BuildTpuVibrationIsolatorSystem(0.30);
            installedIsolator.Translate(9.50, 0.0, -7.50);
            var installedGimbalShield = tpuGimbalShield.Clone("Installed_Shield");
            installedGimbalShield.Translate(50.50, 0.0, -1.40);
            var installedLouver = tpuLouver.Clone("Installed_Louver");
            installedLouver.Translate(-31.25, 0.0, -1.40);
            MeshRenderer.RenderMultiColorSheet(
                Path.Combine(rendersDir, "08_Cradle_With_TPU_Armor_Assembly.png"),
                "Assembly 3 - DJI Neo 2 Cradle Equipped w/ Full TPU Gimbal, Sensor & Vibration Armor",
                "Blue: Cradle | Cyan: 04a TPU Isolator Sandwich | Gold: 04b Snap-In Gimbal Cushion & Sensor Shield | Green: 04c Heatsink Louver",
                new List<ColoredMeshPart>
                {
                    new ColoredMeshPart(universalCradle, Color.FromArgb(45, 125, 230)),
                    new ColoredMeshPart(installedIsolator, Color.FromArgb(40, 195, 210)),
                    new ColoredMeshPart(installedGimbalShield, Color.FromArgb(245, 165, 35)),
                    new ColoredMeshPart(installedLouver, Color.FromArgb(65, 205, 130))
                });

            // Build Multi-Color 3D Stem Assembly Render using Closed Hinged Clamshell
            var closedClampUnit = PartDesigns.BuildHandlebarHingedClamshellSingle(37.0, assembledClosed: true, step: 0.38);
            var clampAssyL = RotateFromPrintToHandlebar(closedClampUnit, "Clamp_L", -30.5);
            var clampAssyR = RotateFromPrintToHandlebar(closedClampUnit, "Clamp_R", +30.5);
            var bridgeAssy = stemBridge.Clone("Bridge_Assy");
            bridgeAssy.Translate(0, 0, 24.5);
            var cradleAssy = universalCradle.Clone("Cradle_Assy");
            cradleAssy.Translate(-9.50, 0.0, 24.5 + 14.0);

            MeshRenderer.RenderMultiColorSheet(
                Path.Combine(rendersDir, "06_Stem_Assembly_Preview.png"),
                "Assembly 2 - Over-Stem Handlebar Mount w/ Swinging Pin-Hinged Clamshell Clamps",
                "Purple: Hinged Clamshell 35mm Clamps (Zero Flexing!) | Green: Stem Bridge | Blue: DJI Neo 2 Cradle",
                new List<ColoredMeshPart>
                {
                    new ColoredMeshPart(clampAssyL, Color.FromArgb(170, 95, 235)),
                    new ColoredMeshPart(clampAssyR, Color.FromArgb(170, 95, 235)),
                    new ColoredMeshPart(bridgeAssy, Color.FromArgb(60, 185, 120)),
                    new ColoredMeshPart(cradleAssy, Color.FromArgb(45, 125, 230))
                });

            Console.WriteLine("14. Packaging ready-to-slice Bambu Studio .3mf project files...");
            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate1_Structural_Mounts_ASA_PETG_Nylon.3mf"),
                "DJI Neo 2 - Orbea Wild LT Structural Mounts (ASA / Easy Nylon / PETG)",
                new List<PlateItem>
                {
                    new PlateItem(universalCradle, -5.0, 74.0, 0.0),
                    new PlateItem(downtubeSideTower, -58.0, -25.0, 0.0),
                    new PlateItem(stemBridge, 58.0, -5.0, 0.0),
                    new PlateItem(hingedClampsForTpu, 52.0, -76.0, 0.0)
                });

            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate2_Flexible_Dampers_TPU.3mf"),
                "DJI Neo 2 - Orbea Wild LT Complete TPU Protection & Vibration Isolation Suite (TPU 95A)",
                new List<PlateItem>
                {
                    new PlateItem(tpuSleeves, -45.0, 42.0, 0.0),
                    new PlateItem(downtubeTpuPads, 45.0, 42.0, 0.0),
                    new PlateItem(tpuIsolator, -40.0, -22.0, 0.0),
                    new PlateItem(tpuGimbalShield, 28.0, -18.0, 0.0),
                    new PlateItem(tpuLouver, 72.0, -18.0, 0.0)
                });

            ThreeMfWriter.Save3mf(
                Path.Combine(threeMfDir, "Plate3_Integrated_OnePiece_Variants.3mf"),
                "DJI Neo 2 - Orbea Wild LT Integrated One-Piece Mounts",
                new List<PlateItem>
                {
                    new PlateItem(integratedDowntube, 0.0, -48.0, 0.0),
                    new PlateItem(integratedStem, 0.0, 52.0, 0.0)
                });

            Console.WriteLine("ALL UPDATED STL, 3MF, AND RENDER FILES GENERATED SUCCESSFULLY.");
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
