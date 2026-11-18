using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Globalization;

namespace DjiNeo2OrbeaMounts
{
    public static class NumberPlateDiagramGenerator
    {
        // Build simple reference bike cockpit meshes (35mm Handlebar, 40mm OC MC11 Stem, 64-deg Head Tube)
        private static MeshData BuildHandlebarRefMesh(double halfLen = 135.0)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + z * z) - 17.5; // Ø35.0mm bar along Y axis centered at (0, Y, 0)
                return Math.Max(r, Math.Abs(y) - halfLen);
            };
            return SdfMesher.ExtractMesh("Ref_Bar_35mm", sdf, new Vec3(-19, -halfLen - 2, -19), new Vec3(19, halfLen + 2, 19), 1.4);
        }

        private static MeshData BuildStemAndHeadTubeRefMesh()
        {
            // OC MC11 40mm Stem (bar at X=0, Z=0; steerer at X=-40, Z=0) + 64-deg Head Tube (Ø56mm going down-forward from steerer!)
            double ux = -0.438371, uz = 0.898794; // upward along 64-deg headtube
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                // Stem body: X in [-56, +18], Y in [-21, +21], Z in [-20, +20]
                double dStem = SdfMesher.StealthBlock3D(x + 20.0, y, z, 36.0, 20.5, 19.5, 6.0, 2.0);
                // Trim front of stem cleanly around bar (X <= +20)
                // Head tube / steerer stack below stem: centered at (X=-40, Y=0, Z=0) extending down along -u for 130mm!
                double dx = x - (-40.0), dz = z - 0.0;
                double sAlong = dx * ux + dz * uz;
                double rx = dx - sAlong * ux, rz = dz - sAlong * uz;
                double rPerp = Math.Sqrt(rx * rx + y * y + rz * rz);
                double dHeadTube = Math.Max(rPerp - 27.0, Math.Max(-145.0 - sAlong, sAlong - 24.0));
                return Math.Min(dStem, dHeadTube);
            };
            return SdfMesher.ExtractMesh("Ref_Stem_HeadTube", sdf, new Vec3(-62, -30, -140), new Vec3(110, 30, 32), 1.8);
        }

        // Simplified DJI Neo 2 + Digital Transceiver silhouette in Cradle Local Space (to show deployed fitment!)
        private static MeshData BuildDroneSilhouetteInCradleSpace()
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                // Main fuselage: X in [-68, +72], Y in [-18, +18], Z in [6.5, 39.0]
                double dBody = SdfMesher.StealthBlock3D(x - 2.0, y, z - 22.8, 69.0, 17.5, 16.2, 6.0, 4.0);
                // Digital Transceiver module on top-rear (X in [+55, +92], Y in [-13.5, +13.5], Z in [16, 48])
                double dTx = SdfMesher.StealthBlock3D(x - 73.0, y, z - 32.0, 18.0, 13.5, 16.0, 4.0, 2.0);
                // 4 Ducted Propeller Guards (approximate outer shrouds: X_centers = -36 and +36, Y_centers = +-49, R_outer = 37mm, R_inner = 28mm, Z in [10, 36])
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
            return SdfMesher.ExtractMesh("Ref_DJI_Neo2_Drone", sdf, new Vec3(-76, -88, 5), new Vec3(95, 88, 52), 1.8);
        }

        public static void Generate()
        {
            string baseDir = @"C:\Users\ahsan\.gemini\antigravity\scratch\dji-neo2-orbea-mounts";
            string refModelXml = Path.Combine(baseDir, @"reference\extracted\3D\Objects\object_16.model");
            string outPath = Path.Combine(baseDir, @"renders\11_NumberPlate_Exploded_And_Deployed_Guide.png");

            // Load/build all parts
            var cradleShell = PartDesigns.LoadReferenceCradleMesh(refModelXml);
            var cradleInsert = PartDesigns.BuildCradleMountingInsert(0.35);
            var universalCradle = cradleShell.Clone("Cradle");
            universalCradle.Append(cradleInsert);

            var clampsSingle = PartDesigns.BuildHandlebarHingedClamshellSingle(37.0, true, 0.38);
            var tpuSleeves = PartDesigns.BuildHandlebarTpuSleeves(0.32);
            // Extract single TPU sleeve (left half centered at X=-22 -> shift +22)
            var singleSleeve = new MeshData("Sleeve");
            int halfSV = tpuSleeves.Vertices.Count / 2;
            int halfST = tpuSleeves.Triangles.Count / 2;
            for (int i = 0; i < halfSV; i++)
            {
                var v = tpuSleeves.Vertices[i];
                singleSleeve.Vertices.Add(new Vec3(v.X + 22.0, v.Y, v.Z));
            }
            for (int i = 0; i < halfST; i++) singleSleeve.Triangles.Add(tpuSleeves.Triangles[i]);

            var npShield = PartDesigns.BuildOrbeaStealthNumberPlateShield(0.42);
            var npArms = PartDesigns.BuildOrbeaStealthNumberPlateDropArmsPair(0.36);
            var padOnly = PartDesigns.BuildTpuVibrationIsolatorSystem(0.30, false);
            var tpuBelly = PartDesigns.BuildTpuBellySensorShield(0.28);
            var tpuLouver = PartDesigns.BuildTpuHeatsinkLouver(0.28);
            var tpuNose = PartDesigns.BuildTpuNoseVaultGimbalGuard(0.28);
            var tpuStrap = PartDesigns.BuildTpuTopFisheyeLensVisorStrap(0.28);

            var instNose = Program.PlaceNoseGuardInCradle(tpuNose);
            var instStrap = Program.CurveTopStrapForAssemblyRender(tpuStrap);
            var instBelly = tpuBelly.Clone("Belly"); instBelly.Translate(50.50, 0.0, -1.60);
            var instLouver = tpuLouver.Clone("Louver"); instLouver.Translate(-31.25, 0.0, -1.60);

            var barRef = BuildHandlebarRefMesh(135.0);
            var stemRef = BuildStemAndHeadTubeRefMesh();
            var droneRef = BuildDroneSilhouetteInCradleSpace();

            // ---------------------------------------------------------
            // PANEL 1: EXPLODED STEP-BY-STEP ASSEMBLY (along 64-deg plate axis)
            // ---------------------------------------------------------
            var explodedParts = new List<ColoredMeshPart>
            {
                new ColoredMeshPart(barRef, Color.FromArgb(72, 78, 88)),
                new ColoredMeshPart(stemRef, Color.FromArgb(58, 64, 72)),
                // TPU Sleeves popped slightly outward laterally
                new ColoredMeshPart(Program.RotateClampToHandlebar(singleSleeve, "SL", -31.5), Color.FromArgb(40, 195, 210)),
                new ColoredMeshPart(Program.RotateClampToHandlebar(singleSleeve, "SR", +31.5), Color.FromArgb(40, 195, 210)),
                // Hinged Clamps popped slightly upward/forward
                new ColoredMeshPart(TranslateCopy(Program.RotateClampToHandlebar(clampsSingle, "CL", -31.5), 14, 0, 14), Color.FromArgb(165, 95, 235)),
                new ColoredMeshPart(TranslateCopy(Program.RotateClampToHandlebar(clampsSingle, "CR", +31.5), 14, 0, 14), Color.FromArgb(165, 95, 235)),
                // Drop Arms popped forward-up (+28, +22)
                new ColoredMeshPart(TranslateCopy(Program.TransformDropArmToWorld(npArms, true, "AL", -31.5), 34, 0, 22), Color.FromArgb(75, 165, 225)),
                new ColoredMeshPart(TranslateCopy(Program.TransformDropArmToWorld(npArms, false, "AR", +31.5), 34, 0, 22), Color.FromArgb(75, 165, 225)),
                // Number Plate Shield popped forward along normal (+58mm)
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(npShield, "SH", 0, 0, 58.0), Color.FromArgb(235, 105, 35)),
                // 90A TPU Vibration Isolator Puck popped forward (+92mm)
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(padOnly, "IS", 9.50, 0.0, 92.0), Color.FromArgb(40, 195, 210)),
                // Cradle + Belly/Heatsink/Nose/Top Strap exploded along normal (+124..+160mm)
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(instBelly, "BL", 0, 0, 114.0), Color.FromArgb(245, 175, 40)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(instLouver, "LV", 0, 0, 114.0), Color.FromArgb(65, 205, 130)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(universalCradle, "CD", 0, 0, 126.0), Color.FromArgb(45, 125, 230)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(instNose, "NS", 0, 0, 152.0), Color.FromArgb(240, 110, 50)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(instStrap, "ST", 0, 0, 162.0), Color.FromArgb(225, 85, 165))
            };

            // ---------------------------------------------------------
            // PANEL 2: DEPLOYED ON 2027 ORBEA WILD LT COCKPIT (WITH DRONE!)
            // ---------------------------------------------------------
            var deployedParts = new List<ColoredMeshPart>
            {
                new ColoredMeshPart(barRef, Color.FromArgb(72, 78, 88)),
                new ColoredMeshPart(stemRef, Color.FromArgb(58, 64, 72)),
                new ColoredMeshPart(Program.RotateClampToHandlebar(singleSleeve, "DSL", -31.5), Color.FromArgb(40, 195, 210)),
                new ColoredMeshPart(Program.RotateClampToHandlebar(singleSleeve, "DSR", +31.5), Color.FromArgb(40, 195, 210)),
                new ColoredMeshPart(Program.RotateClampToHandlebar(clampsSingle, "DCL", -31.5), Color.FromArgb(165, 95, 235)),
                new ColoredMeshPart(Program.RotateClampToHandlebar(clampsSingle, "DCR", +31.5), Color.FromArgb(165, 95, 235)),
                new ColoredMeshPart(Program.TransformDropArmToWorld(npArms, true, "DAL", -31.5), Color.FromArgb(75, 165, 225)),
                new ColoredMeshPart(Program.TransformDropArmToWorld(npArms, false, "DAR", +31.5), Color.FromArgb(75, 165, 225)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(npShield, "DSH"), Color.FromArgb(235, 105, 35)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(padOnly, "DIS", 9.50, 0.0, 6.0), Color.FromArgb(40, 195, 210)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(universalCradle, "DCD", 0.0, 0.0, 13.5), Color.FromArgb(45, 125, 230)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(instNose, "DNS", 0.0, 0.0, 13.5), Color.FromArgb(240, 110, 50)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(instStrap, "DST", 0.0, 0.0, 13.5), Color.FromArgb(225, 85, 165)),
                // Deployed DJI Neo 2 + Transceiver clipped in!
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(droneRef, "DDRONE", 0.0, 0.0, 13.5), Color.FromArgb(165, 174, 186))
            };

            // ---------------------------------------------------------
            // PANEL 3: DEPLOYED SIDE PROFILE (SHOWING 64-DEG RAKE, SIC CABLE TUNNEL & CHIN CLEARANCE)
            // ---------------------------------------------------------
            int width = 1600, height = 940;
            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.FromArgb(16, 20, 26));

                using (var headerBrush = new SolidBrush(Color.FromArgb(24, 31, 42)))
                using (var accentPen = new Pen(Color.FromArgb(235, 105, 35), 2.5f))
                {
                    g.FillRectangle(headerBrush, 0, 0, width, 86);
                    g.DrawLine(accentPen, 0, 86, width, 86);
                }

                using (var titleFont = new Font("Segoe UI", 20, FontStyle.Bold))
                using (var subFont = new Font("Segoe UI", 11.5f, FontStyle.Regular))
                using (var whiteBrush = new SolidBrush(Color.FromArgb(245, 248, 252)))
                using (var orangeBrush = new SolidBrush(Color.FromArgb(255, 155, 85)))
                {
                    g.DrawString("HOW IT FITS: ORBEA STEALTH NUMBER-PLATE MOUNT (EXPLODED + DEPLOYED ON BIKE)", titleFont, whiteBrush, 24, 12);
                    g.DrawString("Shown on 2027 Orbea Wild LT Size L Cockpit: Ø35mm Carbon Bar + 40mm OC MC11 Stem + 64° Head Tube + DJI Neo 2 w/ Transceiver", subFont, orangeBrush, 26, 50);
                }

                // Left Half: Exploded Order of Assembly
                RenderViewport(bmp, g, new Rectangle(20, 102, 770, 510),
                    "1. EXPLODED ASSEMBLY ORDER (BACK-TO-FRONT STACK-UP)",
                    explodedParts, yawDeg: 42.0, pitchDeg: 18.0);

                // Right Half: Deployed 3/4 Cockpit View w/ Drone Clipped In
                RenderViewport(bmp, g, new Rectangle(810, 102, 770, 510),
                    "2. DEPLOYED ON BIKE COCKPIT (WITH DJI NEO 2 + TRANSCEIVER)",
                    deployedParts, yawDeg: 38.0, pitchDeg: 16.0);

                // Bottom Left: Deployed Side Profile (showing SIC cable tunnel & 64-deg parallel rake!)
                RenderViewport(bmp, g, new Rectangle(20, 626, 770, 294),
                    "3. DEPLOYED SIDE PROFILE (64° PARALLEL RAKE & SIC CABLE TUNNEL)",
                    deployedParts, yawDeg: 90.0, pitchDeg: 0.0);

                // Bottom Right: Color-Coded Legend & Step-by-Step Bolt Guide
                DrawLegendBox(g, new Rectangle(810, 626, 770, 294));

                bmp.Save(outPath, ImageFormat.Png);
            }
            Console.WriteLine("Saved Number-Plate Guide Diagram: " + outPath);
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
            using (var bFont = new Font("Segoe UI", 10.0f, FontStyle.Regular))
            using (var wBrush = new SolidBrush(Color.FromArgb(235, 242, 250)))
            {
                g.DrawString("4. STEP-BY-STEP ASSEMBLY & COLOR KEY", hFont, wBrush, rect.Left + 16, rect.Top + 12);

                var entries = new[]
                {
                    new { Col = Color.FromArgb(72, 78, 88),   Txt = "Bike Cockpit Reference: Ø35mm OC MC10 Carbon Bar + 40mm OC MC11 Stem + 64° Head Tube" },
                    new { Col = Color.FromArgb(40, 195, 210), Txt = "Step 1 (Cyan — 03d): Wrap 90A TPU grip sleeves around carbon bar on both sides of stem" },
                    new { Col = Color.FromArgb(165, 95, 235), Txt = "Step 2 (Purple — 03b): Swing M4-hinged clamshells shut around sleeves (snug rear M4 pinch bolt)" },
                    new { Col = Color.FromArgb(75, 165, 225), Txt = "Step 3 (Sky Blue — 02b): Bolt Drop Arms onto clamp tops (4x M4x16) sweeping forward over SIC cables" },
                    new { Col = Color.FromArgb(235, 105, 35), Txt = "Step 4 (Orange — 02a): Bolt Number-Plate Shield onto front of Drop Arms (4x M4x16 from rear)" },
                    new { Col = Color.FromArgb(40, 195, 210), Txt = "Step 5 (Cyan — 04a): Nest 90A TPU Shear Isolator Puck into shield pocket; bolt Cradle (Blue) Nose-Down" },
                    new { Col = Color.FromArgb(240, 110, 50), Txt = "Step 6 (Orange/Gold/Green — 04b-d): Snap in Belly Seal, Louver & Nose Gimbal/LiDAR Vault" },
                    new { Col = Color.FromArgb(225, 85, 165), Txt = "Step 7 (Pink — 04e): Clip DJI Neo 2 in Nose-Down & hook Top Fisheye Visor Strap over side bosses" }
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
