using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;

namespace DjiNeo2OrbeaMounts
{
    public static class HeroBannerGenerator
    {
        public static void Generate()
        {
            string baseDir = @"C:\Users\ahsan\.gemini\antigravity\scratch\dji-neo2-orbea-mounts";
            string refModelXml = Path.Combine(baseDir, @"reference\extracted\3D\Objects\object_16.model");
            string outPath = Path.Combine(baseDir, @"renders\00_Hero_Banner_Showcase.png");

            var cradleShell = PartDesigns.LoadReferenceCradleMesh(refModelXml);
            var cradleInsert = PartDesigns.BuildCradleMountingInsert(0.35);
            var universalCradle = cradleShell.Clone("Cradle");
            universalCradle.Append(cradleInsert);

            var padOnly = PartDesigns.BuildTpuVibrationIsolatorSystem(0.32, false);
            var tpuBelly = PartDesigns.BuildTpuBellySensorShield(0.28);
            var tpuLouver = PartDesigns.BuildTpuHeatsinkLouver(0.28);
            var tpuNose = PartDesigns.BuildTpuNoseVaultGimbalGuard(0.30);
            var tpuStrap = PartDesigns.BuildTpuTopFisheyeLensVisorStrap(0.28);

            var instNose = Program.PlaceNoseGuardInCradle(tpuNose);
            var instStrap = Program.CurveTopStrapForAssemblyRender(tpuStrap);
            var instIso = padOnly.Clone("Iso"); instIso.Translate(9.50, 0.0, -7.50);
            var instBelly = tpuBelly.Clone("Belly"); instBelly.Translate(50.50, 0.0, -1.60);
            var instLouver = tpuLouver.Clone("Louver"); instLouver.Translate(-31.25, 0.0, -1.60);

            // Panel 1: Stealth Number-Plate Assembly
            var closedClamp = PartDesigns.BuildHandlebarHingedClamshellSingle(37.0, true, 0.40);
            var npShield = PartDesigns.BuildOrbeaStealthNumberPlateShield(0.45);
            var npArms = PartDesigns.BuildOrbeaStealthNumberPlateDropArmsPair(0.38);

            var npParts = new List<ColoredMeshPart>
            {
                new ColoredMeshPart(Program.RotateClampToHandlebar(closedClamp, "CL", -31.5), Color.FromArgb(165, 95, 235)),
                new ColoredMeshPart(Program.RotateClampToHandlebar(closedClamp, "CR", +31.5), Color.FromArgb(165, 95, 235)),
                new ColoredMeshPart(Program.TransformDropArmToWorld(npArms, true, "AL", -31.5), Color.FromArgb(75, 165, 225)),
                new ColoredMeshPart(Program.TransformDropArmToWorld(npArms, false, "AR", +31.5), Color.FromArgb(75, 165, 225)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(npShield, "SH"), Color.FromArgb(235, 105, 35)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(padOnly, "IS", 9.50, 0.0, 6.0), Color.FromArgb(40, 195, 210)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(universalCradle, "CD", 0.0, 0.0, 13.5), Color.FromArgb(45, 125, 230)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(instNose, "NS", 0.0, 0.0, 13.5), Color.FromArgb(240, 110, 50)),
                new ColoredMeshPart(Program.TransformPlateSpaceToWorld(instStrap, "ST", 0.0, 0.0, 13.5), Color.FromArgb(225, 85, 165))
            };

            // Panel 2: Stealth Over-Stem Assembly (Nose-Forward over Chiseled Roost Prow!)
            var stemBridge = PartDesigns.BuildOrbeaStealthOverStemBridge(0.40);
            var osBridge = stemBridge.Clone("OSB"); osBridge.Translate(0, 0, 24.5);
            var osIso = padOnly.Clone("OSI"); osIso.Translate(0, 0, 35.0);
            var osCradle = Program.Rotate180Z(universalCradle, "OSC", +9.50, 0.0, 42.5);
            var osNose = Program.Rotate180Z(instNose, "OSN", +9.50, 0.0, 42.5);
            var osStrap = Program.Rotate180Z(instStrap, "OSS", +9.50, 0.0, 42.5);

            var osParts = new List<ColoredMeshPart>
            {
                new ColoredMeshPart(Program.RotateClampToHandlebar(closedClamp, "OCL", -30.5), Color.FromArgb(165, 95, 235)),
                new ColoredMeshPart(Program.RotateClampToHandlebar(closedClamp, "OCR", +30.5), Color.FromArgb(165, 95, 235)),
                new ColoredMeshPart(osBridge, Color.FromArgb(55, 190, 125)),
                new ColoredMeshPart(osIso, Color.FromArgb(40, 195, 210)),
                new ColoredMeshPart(osCradle, Color.FromArgb(45, 125, 230)),
                new ColoredMeshPart(osNose, Color.FromArgb(240, 110, 50)),
                new ColoredMeshPart(osStrap, Color.FromArgb(225, 85, 165))
            };

            // Panel 3: 360-Deg Lens-Armored Cradle
            var armorParts = new List<ColoredMeshPart>
            {
                new ColoredMeshPart(universalCradle, Color.FromArgb(45, 125, 230)),
                new ColoredMeshPart(instIso, Color.FromArgb(40, 195, 210)),
                new ColoredMeshPart(instBelly, Color.FromArgb(245, 175, 40)),
                new ColoredMeshPart(instLouver, Color.FromArgb(65, 205, 130)),
                new ColoredMeshPart(instNose, Color.FromArgb(240, 110, 50)),
                new ColoredMeshPart(instStrap, Color.FromArgb(225, 85, 165))
            };

            int width = 1600, height = 660;
            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using (var bgGrad = new LinearGradientBrush(new Rectangle(0, 0, width, height),
                    Color.FromArgb(13, 17, 23), Color.FromArgb(22, 29, 39), LinearGradientMode.Vertical))
                {
                    g.FillRectangle(bgGrad, 0, 0, width, height);
                }

                using (var headerBrush = new SolidBrush(Color.FromArgb(24, 31, 42)))
                using (var accentPen = new Pen(Color.FromArgb(235, 105, 35), 2.5f))
                {
                    g.FillRectangle(headerBrush, 0, 0, width, 96);
                    g.DrawLine(accentPen, 0, 96, width, 96);
                }

                using (var titleFont = new Font("Segoe UI", 22, FontStyle.Bold))
                using (var subFont = new Font("Segoe UI", 12, FontStyle.Regular))
                using (var whiteBrush = new SolidBrush(Color.FromArgb(245, 248, 252)))
                using (var orangeBrush = new SolidBrush(Color.FromArgb(255, 155, 85)))
                {
                    g.DrawString("DJI NEO 2 (w/ TRANSCEIVER)  x  2027 ORBEA WILD LT M-TEAM", titleFont, whiteBrush, 28, 16);
                    g.DrawString("Orbea Stealth Cockpit Mount Suite  |  Out-Front Number-Plate Fairing  +  Over-Stem OC Bridge  +  360-Deg TPU Lens & Gimbal Armor", subFont, orangeBrush, 30, 58);
                }

                RenderHeroCard(bmp, g, new Rectangle(24, 116, 504, 520),
                    "1. STEALTH NUMBER-PLATE MOUNT",
                    "Armored Chin Roost Fender | 64-Deg Rake | 72D + 90A Damping",
                    npParts, 34.0, 18.0);

                RenderHeroCard(bmp, g, new Rectangle(548, 116, 504, 520),
                    "2. STEALTH OC OVER-STEM MOUNT",
                    "Forward Roost Prow | Recessed 90A Isolator | Hinged 35mm Clamps",
                    osParts, 38.0, 26.0);

                RenderHeroCard(bmp, g, new Rectangle(1072, 116, 504, 520),
                    "3. 360-DEG LENS & GIMBAL ARMOR",
                    "Nose Gimbal/LiDAR Vault + Top Fisheye Visor + Belly Sensor Seal",
                    armorParts, 38.0, 28.0);

                bmp.Save(outPath, ImageFormat.Png);
            }
            Console.WriteLine("Saved Hero Banner: " + outPath);
        }

        private static void RenderHeroCard(Bitmap bmp, Graphics g, Rectangle rect, string title, string sub, List<ColoredMeshPart> parts, double yawDeg, double pitchDeg)
        {
            using (var cardBrush = new SolidBrush(Color.FromArgb(22, 27, 34)))
            using (var borderPen = new Pen(Color.FromArgb(48, 54, 61), 1.5f))
            {
                g.FillRectangle(cardBrush, rect);
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

            double pad = 34.0;
            double scale = Math.Min((rect.Width - 2 * pad) / Math.Max(1.0, maxU - minU), (rect.Height - 2 * pad - 50) / Math.Max(1.0, maxV - minV));
            double midU = 0.5 * (minU + maxU), midV = 0.5 * (minV + maxV);
            double cx = rect.Left + rect.Width * 0.5, cy = rect.Top + 26 + rect.Height * 0.5;

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
                    int yMin = Math.Max(54, (int)Math.Floor(Math.Min(sy0, Math.Min(sy1, sy2))));
                    int yMax = Math.Min(vh - 3, (int)Math.Ceiling(Math.Max(sy0, Math.Max(sy1, sy2))));
                    if (xMin > xMax || yMin > yMax) continue;
                    double denom = (sy1 - sy2) * (sx0 - sx2) + (sx2 - sx1) * (sy0 - sy2);
                    if (Math.Abs(denom) < 1e-6) continue;

                    double atten = 1.0 - 0.26 * (((a.Z + b.Z + c.Z) / 3.0 - minD) / dRange);
                    double diff = 0.62 * Math.Max(0.0, Vec3.Dot(n, light1)) + 0.22 * Math.Max(0.0, Vec3.Dot(n, light2));
                    double spec = Math.Pow(Math.Max(0.0, -n.Z), 22.0) * 0.25;
                    double shade = Math.Min(1.15, (0.24 + diff) * atten);
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

            using (var tFont = new Font("Segoe UI", 12, FontStyle.Bold))
            using (var sFont = new Font("Segoe UI", 9.5f, FontStyle.Regular))
            using (var wBrush = new SolidBrush(Color.FromArgb(235, 242, 250)))
            using (var gBrush = new SolidBrush(Color.FromArgb(145, 160, 180)))
            {
                g.DrawString(title, tFont, wBrush, rect.Left + 14, rect.Top + 10);
                g.DrawString(sub, sFont, gBrush, rect.Left + 14, rect.Top + 32);
            }
        }
    }
}
