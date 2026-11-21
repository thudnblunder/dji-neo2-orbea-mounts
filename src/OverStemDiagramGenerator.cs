using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Globalization;

namespace DjiNeo2OrbeaMounts
{
    public static class OverStemDiagramGenerator
    {
        private static MeshData BuildHandlebarRefMesh(double halfLen = 135.0)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + z * z) - 17.5;
                return Math.Max(r, Math.Abs(y) - halfLen);
            };
            return SdfMesher.ExtractMesh("Ref_Bar_35mm", sdf, new Vec3(-19, -halfLen - 2, -19), new Vec3(19, halfLen + 2, 19), 1.6);
        }

        private static MeshData BuildStemAndHeadTubeRefMesh()
        {
            double ux = -0.438371, uz = 0.898794;
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double dStem = SdfMesher.StealthBlock3D(x + 20.0, y, z, 36.0, 20.5, 19.5, 6.0, 2.0);
                double dx = x - (-40.0), dz = z - 0.0;
                double sAlong = dx * ux + dz * uz;
                double rx = dx - sAlong * ux, rz = dz - sAlong * uz;
                double rPerp = Math.Sqrt(rx * rx + y * y + rz * rz);
                double dHeadTube = Math.Max(rPerp - 27.0, Math.Max(-110.0 - sAlong, sAlong - 24.0));
                return Math.Min(dStem, dHeadTube);
            };
            return SdfMesher.ExtractMesh("Ref_Stem_HeadTube", sdf, new Vec3(-62, -30, -105), new Vec3(65, 30, 32), 2.0);
        }

        private static MeshData BuildDroneSilhouetteInCradleSpace()
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double dBody = SdfMesher.StealthBlock3D(x - 2.0, y, z - 22.8, 69.0, 17.5, 16.2, 6.0, 4.0);
                double dTx = SdfMesher.StealthBlock3D(x - 73.0, y, z - 32.0, 18.0, 13.5, 16.0, 4.0, 2.0);
                double ay = Math.Abs(y);
                double dDucts = 1e9;
                foreach (double cx in new double[] { -36.0, +36.0 })
                {
                    double r = Math.Sqrt((x - cx) * (x - cx) + (ay - 49.0) * (ay - 49.0));
                    double ring = Math.Max(Math.Max(28.0 - r, r - 36.5), Math.Abs(z - 23.0) - 12.0);
                    double hub = Math.Max(r - 7.0, Math.Abs(z - 23.0) - 9.0);
                    double strut = Math.Max(Math.Abs(x - cx) - 3.0, Math.Max(ay - 84.0, Math.Abs(z - 23.0) - 4.0));
                    dDucts = Math.Min(dDucts, Math.Min(ring, Math.Min(hub, strut)));
                }
                return Math.Min(Math.Min(dBody, dTx), dDucts);
            };
            return SdfMesher.ExtractMesh("Ref_DJI_Neo2_Drone", sdf, new Vec3(-76, -88, 5), new Vec3(95, 88, 52), 2.0);
        }

        private static MeshData BuildBrassInsertRing(double cx, double cy, double cz, double h = 5.5)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double r = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                return Math.Max(Math.Max(2.0 - r, r - 3.1), Math.Abs(z - cz) - h * 0.5);
            };
            return SdfMesher.ExtractMesh("BrassInsert", sdf, new Vec3(cx - 4, cy - 4, cz - h), new Vec3(cx + 4, cy + 4, cz + h), 0.7);
        }

        public static void Generate()
        {
            string baseDir = @"C:\Users\ahsan\.gemini\antigravity\scratch\dji-neo2-orbea-mounts";
            string refModelXml = Path.Combine(baseDir, @"reference\extracted\3D\Objects\object_16.model");
            string outPath = Path.Combine(baseDir, @"renders\12_OverStem_Exploded_And_Deployed_Guide.png");

            // Load/build fast preview parts
            var cradleShell = PartDesigns.LoadReferenceCradleMesh(refModelXml);
            var cradleInsert = PartDesigns.BuildCradleMountingInsert(0.80);
            var universalCradle = cradleShell.Clone("Cradle");
            universalCradle.Append(cradleInsert);

            var clampsSingle = PartDesigns.BuildHandlebarHingedClamshellSingle(37.0, true, 0.75);
            var tpuSleeves = PartDesigns.BuildHandlebarTpuSleeves(0.60);
            var singleSleeve = new MeshData("Sleeve");
            int halfSV = tpuSleeves.Vertices.Count / 2;
            int halfST = tpuSleeves.Triangles.Count / 2;
            for (int i = 0; i < halfSV; i++)
            {
                var v = tpuSleeves.Vertices[i];
                singleSleeve.Vertices.Add(new Vec3(v.X + 22.0, v.Y, v.Z));
            }
            for (int i = 0; i < halfST; i++) singleSleeve.Triangles.Add(tpuSleeves.Triangles[i]);

            var stemBridge = PartDesigns.BuildOrbeaStealthOverStemBridge(0.75);
            var padOnly = PartDesigns.BuildTpuVibrationIsolatorSystem(0.55, false);
            var tpuBelly = PartDesigns.BuildTpuBellySensorShield(0.55);
            var tpuLouver = PartDesigns.BuildTpuHeatsinkLouver(0.55);
            var tpuNose = PartDesigns.BuildTpuNoseVaultGimbalGuard(0.60);
            var tpuStrap = PartDesigns.BuildTpuTopFisheyeLensVisorStrap(0.55);

            var instNose = Program.PlaceNoseGuardInCradle(tpuNose);
            var instStrap = Program.CurveTopStrapForAssemblyRender(tpuStrap);
            var instBelly = tpuBelly.Clone("Belly"); instBelly.Translate(50.50, 0.0, -1.60);
            var instLouver = tpuLouver.Clone("Louver"); instLouver.Translate(-31.25, 0.0, -1.60);

            var barRef = BuildHandlebarRefMesh(135.0);
            var stemRef = BuildStemAndHeadTubeRefMesh();
            var droneRef = BuildDroneSilhouetteInCradleSpace();

            // Brass heat-set insert highlights floating right above clamp saddle holes in exploded view
            var brassInserts = new MeshData("BrassInserts");
            foreach (double cy in new double[] { -30.5, +30.5 })
                foreach (double cx in new double[] { -9.0, +9.0 })
                    brassInserts.Append(BuildBrassInsertRing(cx, cy, 42.5, 5.5));

            // ---------------------------------------------------------
            // PANEL 1: EXPLODED VERTICAL STACK-UP (OVER-STEM + HEAT-SET CLAMPS)
            // ---------------------------------------------------------
            var explodedParts = new List<ColoredMeshPart>
            {
                new ColoredMeshPart(barRef, Color.FromArgb(72, 78, 88)),
                new ColoredMeshPart(stemRef, Color.FromArgb(58, 64, 72)),
                // 03d TPU Grip Sleeves seated on bar
                new ColoredMeshPart(Program.RotateClampToHandlebar(singleSleeve, "SL", -30.5), Color.FromArgb(40, 195, 210)),
                new ColoredMeshPart(Program.RotateClampToHandlebar(singleSleeve, "SR", +30.5), Color.FromArgb(40, 195, 210)),
                // 03e Heat-Set Hinged Clamps popped slightly upward (+12mm Z)
                new ColoredMeshPart(TranslateCopy(Program.RotateClampToHandlebar(clampsSingle, "CL", -30.5), 0, 0, 12), Color.FromArgb(165, 95, 235)),
                new ColoredMeshPart(TranslateCopy(Program.RotateClampToHandlebar(clampsSingle, "CR", +30.5), 0, 0, 12), Color.FromArgb(165, 95, 235)),
                // Gold M4 Brass Heat-Set Inserts hovering right above clamp top saddles!
                new ColoredMeshPart(brassInserts, Color.FromArgb(245, 195, 55)),
                // 03a Stealth Over-Stem Bridge popped up (+52mm Z)
                new ColoredMeshPart(TranslateCopy(stemBridge, 0, 0, 52), Color.FromArgb(55, 190, 125)),
                // 04a 90A TPU Vibration Isolator Puck popped up (+84mm Z)
                new ColoredMeshPart(TranslateCopy(padOnly, 0, 0, 84), Color.FromArgb(40, 195, 210)),
                // 04b Belly Seal & 04c Heatsink Louver (+104mm Z, rotated 180 deg nose-forward!)
                new ColoredMeshPart(Program.Rotate180Z(instBelly, "BL", +9.50, 0.0, 106), Color.FromArgb(245, 175, 40)),
                new ColoredMeshPart(Program.Rotate180Z(instLouver, "LV", +9.50, 0.0, 106), Color.FromArgb(65, 205, 130)),
                // 01d 45-Deg Easy-PA Flex-Clip Cradle rotated Nose-Forward over Front Prow (+118mm Z)
                new ColoredMeshPart(Program.Rotate180Z(universalCradle, "CD", +9.50, 0.0, 118), Color.FromArgb(45, 125, 230)),
                // 04d Nose Vault Liner & 04e Top Fisheye Visor Strap popped above cradle (+138..148mm Z)
                new ColoredMeshPart(Program.Rotate180Z(instNose, "NS", +9.50, 0.0, 136), Color.FromArgb(240, 110, 50)),
                new ColoredMeshPart(Program.Rotate180Z(instStrap, "ST", +9.50, 0.0, 148), Color.FromArgb(225, 85, 165))
            };

            // ---------------------------------------------------------
            // PANEL 2 & 3: DEPLOYED OVER-STEM COCKPIT VIEW (WITH DRONE!)
            // ---------------------------------------------------------
            var deployedParts = new List<ColoredMeshPart>
            {
                new ColoredMeshPart(barRef, Color.FromArgb(72, 78, 88)),
                new ColoredMeshPart(stemRef, Color.FromArgb(58, 64, 72)),
                new ColoredMeshPart(Program.RotateClampToHandlebar(singleSleeve, "DSL", -30.5), Color.FromArgb(40, 195, 210)),
                new ColoredMeshPart(Program.RotateClampToHandlebar(singleSleeve, "DSR", +30.5), Color.FromArgb(40, 195, 210)),
                new ColoredMeshPart(Program.RotateClampToHandlebar(clampsSingle, "DCL", -30.5), Color.FromArgb(165, 95, 235)),
                new ColoredMeshPart(Program.RotateClampToHandlebar(clampsSingle, "DCR", +30.5), Color.FromArgb(165, 95, 235)),
                new ColoredMeshPart(TranslateCopy(stemBridge, 0, 0, 24.5), Color.FromArgb(55, 190, 125)),
                new ColoredMeshPart(TranslateCopy(padOnly, 0, 0, 24.5 + 10.5), Color.FromArgb(40, 195, 210)),
                new ColoredMeshPart(Program.Rotate180Z(universalCradle, "DCD", +9.50, 0.0, 24.5 + 18.0), Color.FromArgb(45, 125, 230)),
                new ColoredMeshPart(Program.Rotate180Z(instNose, "DNS", +9.50, 0.0, 24.5 + 18.0), Color.FromArgb(240, 110, 50)),
                new ColoredMeshPart(Program.Rotate180Z(instStrap, "DST", +9.50, 0.0, 24.5 + 18.0), Color.FromArgb(225, 85, 165)),
                new ColoredMeshPart(Program.Rotate180Z(droneRef, "DDRONE", +9.50, 0.0, 24.5 + 18.0), Color.FromArgb(165, 174, 186))
            };

            int width = 1600, height = 940;
            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.FromArgb(16, 20, 26));

                using (var headerBrush = new SolidBrush(Color.FromArgb(24, 31, 42)))
                using (var accentPen = new Pen(Color.FromArgb(55, 190, 125), 2.5f))
                {
                    g.FillRectangle(headerBrush, 0, 0, width, 86);
                    g.DrawLine(accentPen, 0, 86, width, 86);
                }

                using (var titleFont = new Font("Segoe UI", 20, FontStyle.Bold))
                using (var subFont = new Font("Segoe UI", 11.5f, FontStyle.Regular))
                using (var whiteBrush = new SolidBrush(Color.FromArgb(245, 248, 252)))
                using (var greenBrush = new SolidBrush(Color.FromArgb(95, 225, 160)))
                {
                    g.DrawString("HOW IT FITS: ORBEA STEALTH OVER-STEM MOUNT (HEAT-SET CLAMPS + VIBRATION ISOLATOR)", titleFont, whiteBrush, 24, 12);
                    g.DrawString("2027 Orbea Wild LT Cockpit: Ø35mm OC MC10 Carbon Bar + 40mm OC MC11 Stem + 03e Heat-Set Clamps + 04a 90A TPU Puck + 01d Easy-PA Flex-Clip Cradle", subFont, greenBrush, 26, 50);
                }

                // Top Left: Exploded Vertical Stack-Up
                RenderViewport(bmp, g, new Rectangle(20, 102, 770, 510),
                    "1. EXPLODED ASSEMBLY STACK-UP (BOTTOM-TO-TOP)",
                    explodedParts, yawDeg: 40.0, pitchDeg: 18.0);

                // Top Right: Deployed 3/4 Cockpit View w/ Drone Clipped In
                RenderViewport(bmp, g, new Rectangle(810, 102, 770, 510),
                    "2. DEPLOYED ON OVER-STEM COCKPIT (WITH DJI NEO 2 + TRANSCEIVER)",
                    deployedParts, yawDeg: 38.0, pitchDeg: 20.0);

                // Bottom Left: Deployed Side Profile (showing stem arch clearance & front roost prow!)
                RenderViewport(bmp, g, new Rectangle(20, 626, 770, 294),
                    "3. DEPLOYED SIDE PROFILE (46mm STEM ARCH CLEARANCE & FRONT ROOST PROW)",
                    deployedParts, yawDeg: 90.0, pitchDeg: 0.0);

                // Bottom Right: Color-Coded Legend & Exact Bolt / Heat-Set Guide
                DrawLegendBox(g, new Rectangle(810, 626, 770, 294));

                bmp.Save(outPath, ImageFormat.Png);
            }
            Console.WriteLine("Saved Over-Stem Guide Diagram: " + outPath);
        }

        private static MeshData TranslateCopy(MeshData src, double dx, double dy, double dz)
        {
            var m = src.Clone(src.Name);
            m.Translate(dx, dy, dz);
            return m;
        }

        private static void DrawLegendBox(Graphics g, Rectangle rect)
        {
            using (var bgBrush = new SolidBrush(Color.FromArgb(22, 27, 34)))
            using (var borderPen = new Pen(Color.FromArgb(48, 54, 61), 1.5f))
            {
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect);
            }
            using (var hFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (var bFont = new Font("Segoe UI", 9.8f, FontStyle.Regular))
            using (var wBrush = new SolidBrush(Color.FromArgb(235, 242, 250)))
            {
                g.DrawString("4. STEP-BY-STEP ASSEMBLY & HARDWARE KEY (OVER-STEM + HEAT-SET)", hFont, wBrush, rect.Left + 16, rect.Top + 12);

                var entries = new[]
                {
                    new { Col = Color.FromArgb(245, 195, 55), Txt = "Prep (Gold): Melt 8x M4 brass inserts into 03e clamps (4x top, 2x TOP of rear lug, 2x hinge)" },
                    new { Col = Color.FromArgb(40, 195, 210), Txt = "Step 1 (Cyan — 03d): Wrap 90A TPU grip sleeves onto Ø35mm bar on both sides of stem" },
                    new { Col = Color.FromArgb(165, 95, 235), Txt = "Step 2 (Purple — 03e): Close hinged clamshells over sleeves (2x M4x16 hinge, 2x M4x16 pinch)" },
                    new { Col = Color.FromArgb(55, 190, 125), Txt = "Step 3 (Green — 03a): Drop 6x M4 DIN 934 nuts under arch; bolt Bridge to clamps (4x M4x12)" },
                    new { Col = Color.FromArgb(40, 195, 210), Txt = "Step 4 (Cyan — 04a): Seat 90A TPU Shear/Bellows Isolator Puck in 3.5mm recessed pocket" },
                    new { Col = Color.FromArgb(45, 125, 230), Txt = "Step 5 (Blue — 01d): Bolt Easy-PA Flex-Clip Cradle Nose-Forward over prow (6x M4x16 Button)" },
                    new { Col = Color.FromArgb(240, 110, 50), Txt = "Step 6 (Optional 04b-d): Snap in Belly Seal, Heatsink Louver & Nose Gimbal/LiDAR Liner" },
                    new { Col = Color.FromArgb(225, 85, 165), Txt = "Step 7 (Pink — 04e): Clip DJI Neo 2 Nose-Forward & hook Top Fisheye Strap over side bosses" }
                };

                int y = rect.Top + 44;
                foreach (var e in entries)
                {
                    using (var swBrush = new SolidBrush(e.Col))
                    {
                        g.FillRectangle(swBrush, rect.Left + 18, y + 3, 14, 14);
                        g.DrawString(e.Txt, bFont, wBrush, rect.Left + 40, y);
                    }
                    y += 30;
                }
            }
        }

        private static void RenderViewport(Bitmap bmp, Graphics g, Rectangle rect, string label, List<ColoredMeshPart> parts, double yawDeg, double pitchDeg)
        {
            using (var bgBrush = new SolidBrush(Color.FromArgb(25, 30, 38)))
            using (var borderPen = new Pen(Color.FromArgb(52, 62, 76), 1.5f))
            {
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect);
            }

            double minX = 1e9, minY = 1e9, minZ = 1e9, maxX = -1e9, maxY = -1e9, maxZ = -1e9;
            foreach (var p in parts)
            {
                Vec3 pMin, pMax; p.Mesh.GetBounds(out pMin, out pMax);
                if (pMin.X < minX) minX = pMin.X; if (pMax.X > maxX) maxX = pMax.X;
                if (pMin.Y < minY) minY = pMin.Y; if (pMax.Y > maxY) maxY = pMax.Y;
                if (pMin.Z < minZ) minZ = pMin.Z; if (pMax.Z > maxZ) maxZ = pMax.Z;
            }
            Vec3 center = new Vec3(0.5 * (minX + maxX), 0.5 * (minY + maxY), 0.5 * (minZ + maxZ));
            double yaw = yawDeg * Math.PI / 180.0, pitch = pitchDeg * Math.PI / 180.0;
            Vec3 forward = new Vec3(-Math.Cos(pitch) * Math.Cos(yaw), -Math.Cos(pitch) * Math.Sin(yaw), -Math.Sin(pitch)).Normalized();
            Vec3 worldUp = new Vec3(0, 0, 1);
            Vec3 right = Vec3.Cross(forward, worldUp).Normalized();
            Vec3 up = Vec3.Cross(right, forward).Normalized();

            double minU = 1e9, maxU = -1e9, minV = 1e9, maxV = -1e9, minD = 1e9, maxD = -1e9;
            var allCam = new List<Vec3[]>();
            foreach (var part in parts)
            {
                var cv = new Vec3[part.Mesh.Vertices.Count];
                for (int i = 0; i < part.Mesh.Vertices.Count; i++)
                {
                    Vec3 p = part.Mesh.Vertices[i] - center;
                    double u = Vec3.Dot(p, right), v = Vec3.Dot(p, up), d = Vec3.Dot(p, forward);
                    cv[i] = new Vec3(u, v, d);
                    if (u < minU) minU = u; if (u > maxU) maxU = u;
                    if (v < minV) minV = v; if (v > maxV) maxV = v;
                    if (d < minD) minD = d; if (d > maxD) maxD = d;
                }
                allCam.Add(cv);
            }

            double pad = 28.0;
            double scale = Math.Min((rect.Width - 2 * pad) / Math.Max(1.0, maxU - minU), (rect.Height - 2 * pad - 24) / Math.Max(1.0, maxV - minV));
            double midU = 0.5 * (minU + maxU), midV = 0.5 * (minV + maxV);
            double cx = rect.Left + rect.Width * 0.5, cy = rect.Top + 14 + rect.Height * 0.5;

            int vw = rect.Width, vh = rect.Height;
            float[,] zbuf = new float[vw, vh];
            for (int x = 0; x < vw; x++) for (int y = 0; y < vh; y++) zbuf[x, y] = float.MaxValue;

            Vec3 light1 = new Vec3(-0.35, 0.45, -0.82).Normalized();
            Vec3 light2 = new Vec3(0.45, -0.30, -0.60).Normalized();
            double dRange = Math.Max(1.0, maxD - minD);

            for (int pIdx = 0; pIdx < parts.Count; pIdx++)
            {
                var part = parts[pIdx]; var cv = allCam[pIdx]; Color bc = part.PartColor;
                foreach (var tri in part.Mesh.Triangles)
                {
                    Vec3 a = cv[tri.V1], b = cv[tri.V2], c = cv[tri.V3];
                    Vec3 wa = part.Mesh.Vertices[tri.V1], wb = part.Mesh.Vertices[tri.V2], wc = part.Mesh.Vertices[tri.V3];
                    Vec3 wn = Vec3.Cross(wb - wa, wc - wa).Normalized();
                    Vec3 n = new Vec3(Vec3.Dot(wn, right), Vec3.Dot(wn, up), Vec3.Dot(wn, forward));
                    if (n.Z > 0.05) continue;

                    double sx0 = cx + (a.X - midU) * scale - rect.Left, sy0 = cy - (a.Y - midV) * scale - rect.Top;
                    double sx1 = cx + (b.X - midU) * scale - rect.Left, sy1 = cy - (b.Y - midV) * scale - rect.Top;
                    double sx2 = cx + (c.X - midU) * scale - rect.Left, sy2 = cy - (c.Y - midV) * scale - rect.Top;
                    int xMin = Math.Max(2, (int)Math.Floor(Math.Min(sx0, Math.Min(sx1, sx2))));
                    int xMax = Math.Min(vw - 3, (int)Math.Ceiling(Math.Max(sx0, Math.Max(sx1, sx2))));
                    int yMin = Math.Max(28, (int)Math.Floor(Math.Min(sy0, Math.Min(sy1, sy2))));
                    int yMax = Math.Min(vh - 3, (int)Math.Ceiling(Math.Max(sy0, Math.Max(sy1, sy2))));
                    if (xMin > xMax || yMin > yMax) continue;
                    double denom = (sy1 - sy2) * (sx0 - sx2) + (sx2 - sx1) * (sy0 - sy2);
                    if (Math.Abs(denom) < 1e-6) continue;

                    double atten = 1.0 - 0.25 * (((a.Z + b.Z + c.Z) / 3.0 - minD) / dRange);
                    double diff = 0.60 * Math.Max(0.0, Vec3.Dot(n, light1)) + 0.22 * Math.Max(0.0, Vec3.Dot(n, light2));
                    double spec = Math.Pow(Math.Max(0.0, -n.Z), 20.0) * 0.22;
                    double shade = Math.Min(1.15, (0.25 + diff) * atten);
                    Color col = Color.FromArgb(255,
                        (int)Math.Max(0, Math.Min(255, bc.R * shade + 255 * spec)),
                        (int)Math.Max(0, Math.Min(255, bc.G * shade + 255 * spec)),
                        (int)Math.Max(0, Math.Min(255, bc.B * shade + 255 * spec)));

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
                                if (z < zbuf[px, py]) { zbuf[px, py] = z; bmp.SetPixel(rect.Left + px, rect.Top + py, col); }
                            }
                        }
                    }
                }
            }

            using (var labelFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var labelBrush = new SolidBrush(Color.FromArgb(225, 234, 245)))
            {
                g.DrawString(label, labelFont, labelBrush, rect.Left + 12, rect.Top + 8);
            }
        }
    }
}
