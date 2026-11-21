using System;
using System.Xml;
using System.Globalization;
using System.Collections.Generic;

namespace DjiNeo2OrbeaMounts
{
    public static class PartDesigns
    {
        public static MeshData LoadReferenceCradleMesh(string modelXmlPath)
        {
            var mesh = new MeshData("DJI_Neo2_Cradle_Shell");
            using (XmlReader reader = XmlReader.Create(modelXmlPath))
            {
                bool inObj2 = false;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "object")
                        {
                            inObj2 = (reader.GetAttribute("id") == "2");
                        }
                        else if (inObj2 && reader.Name == "vertex")
                        {
                            double x = double.Parse(reader.GetAttribute("x"), CultureInfo.InvariantCulture);
                            double y = double.Parse(reader.GetAttribute("y"), CultureInfo.InvariantCulture);
                            double z = double.Parse(reader.GetAttribute("z"), CultureInfo.InvariantCulture) + 22.37105;
                            mesh.Vertices.Add(new Vec3(x, y, z));
                        }
                        else if (inObj2 && reader.Name == "triangle")
                        {
                            int v1 = int.Parse(reader.GetAttribute("v1"));
                            int v2 = int.Parse(reader.GetAttribute("v2"));
                            int v3 = int.Parse(reader.GetAttribute("v3"));
                            mesh.Triangles.Add(new TriIdx(v1, v2, v3));
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "object" && inObj2)
                    {
                        break;
                    }
                }
            }
            return mesh;
        }

        private static double M4HoleAndHexNutSdf(double x, double y, double z, double hx, double hy, double zNutMin, double zNutMax)
        {
            double dx = x - hx;
            double dy = y - hy;
            double dHole = Math.Sqrt(dx * dx + dy * dy) - 2.15;
            double dHex2D = SdfMesher.Hexagon2D(dx, dy, 7.18);
            double dHexZ = Math.Max(zNutMin - z, z - zNutMax);
            double dHex3D = Math.Max(dHex2D, dHexZ);
            return Math.Min(dHole, dHex3D);
        }

        // Same M4 nut pocket rotated 30 deg so hexagon flats face +-Y (compact Y extent: +-3.59mm instead of +-4.15mm)
        private static double M4HoleAndHexNutYFlatSdf(double x, double y, double z, double hx, double hy, double zNutMin, double zNutMax)
        {
            double dx = x - hx;
            double dy = y - hy;
            double dHole = Math.Sqrt(dx * dx + dy * dy) - 2.15;
            double dHex2D = SdfMesher.Hexagon2D(dy, dx, 7.18);
            double dHexZ = Math.Max(zNutMin - z, z - zNutMax);
            double dHex3D = Math.Max(dHex2D, dHexZ);
            return Math.Min(dHole, dHex3D);
        }

        // =========================================================================
        // 01. UNIVERSAL CRADLE INSERT, 45-DEG EASY-PA CRADLE, & 72D POCKETED CRADLE
        // =========================================================================
        public static double CradleMountingInsertSdf(double x, double y, double z)
        {
            double w1 = SdfMesher.RoundedBox3D(x - (-4.125), y, z - 1.425, 10.075, 11.50, 1.425, 0.8);
            double w2 = SdfMesher.RoundedBox3D(x - 23.00, y, z - 1.425, 9.95, 11.50, 1.425, 0.8);
            double deck = SdfMesher.StealthBlock3D(x - 9.50, y, z - 4.125, 25.00, 13.20, 1.425, 3.0, 0.6);
            double body = Math.Min(Math.Min(w1, w2), deck);

            double holes = 1e9;
            holes = Math.Min(holes, M4HoleAndHexNutSdf(x, y, z, 2.00, -7.50, 2.05, 6.00));
            holes = Math.Min(holes, M4HoleAndHexNutSdf(x, y, z, 2.00,  7.50, 2.05, 6.00));
            holes = Math.Min(holes, M4HoleAndHexNutSdf(x, y, z, 17.00, -7.50, 2.05, 6.00));
            holes = Math.Min(holes, M4HoleAndHexNutSdf(x, y, z, 17.00,  7.50, 2.05, 6.00));
            holes = Math.Min(holes, M4HoleAndHexNutSdf(x, y, z, -5.50, 0.00, 2.05, 6.00));
            holes = Math.Min(holes, M4HoleAndHexNutSdf(x, y, z, 24.50, 0.00, 2.05, 6.00));

            return Math.Max(body, -holes);
        }

        public static MeshData BuildCradleMountingInsert(double step = 0.30)
        {
            return SdfMesher.ExtractMesh("Cradle_Mounting_Insert", CradleMountingInsertSdf, new Vec3(-17.0, -15.0, -0.5), new Vec3(36.0, 15.0, 6.2), step);
        }

        public static double Cradle45DegAnchorFinSdf(double x, double y, double z)
        {
            double ay = Math.Abs(y);
            double zSpine = 1.075354 * (x + 23.410);
            double basePad = SdfMesher.StealthBlock3D(x - 4.25, y, z - 0.7, 23.75, 24.0, 0.7, 5.0, 0.0);
            double baseCut = SdfMesher.ChamferBox2D(x - 3.0, y, 14.0, 11.5, 3.0);
            basePad = Math.Max(basePad, -baseCut);

            double distToSpine = zSpine - z;
            double halfThickY = distToSpine < 2.2 ? Math.Max(0.50, 0.50 + (distToSpine / 2.2) * 0.80) : 1.30;
            double dFinY = Math.Abs(ay - 15.5) - halfThickY;
            double dFinX = Math.Max(-18.5 - x, x - 25.0);
            double dFinZ = Math.Max(0.0 - z, -0.20 - distToSpine);
            double fins = Math.Max(Math.Max(dFinY, dFinX), dFinZ);

            double dCross = Math.Max(Math.Max(Math.Abs(x - 19.0) - 2.0, ay - 16.0), Math.Max(0.0 - z, z - 20.0));
            double body = Math.Min(Math.Min(basePad, fins), dCross);

            double cut = 1e9;
            for (int i = -1; i <= 2; i++)
            {
                double cx = -7.0 + i * 9.5;
                cut = Math.Min(cut, Math.Max(Math.Abs(x - cx) - 2.4, Math.Abs(distToSpine - 1.1) - 1.3));
            }
            cut = Math.Min(cut, Math.Max(Math.Abs(x - 9.0) - 6.5, Math.Max(5.0 - z, (zSpine - 6.0) - z)));
            return Math.Max(body, -cut);
        }

        public static double Cradle72DMountingInsertSdf(double x, double y, double z)
        {
            double w1 = SdfMesher.RoundedBox3D(x - (-4.125), y, z - 1.425, 10.075, 11.50, 1.425, 0.8);
            double w2 = SdfMesher.RoundedBox3D(x - 23.00, y, z - 1.425, 9.95, 11.50, 1.425, 0.8);
            // Widened deck (hy = 14.20) frames the 12.50mm PETG carrier pocket cleanly with solid 1.70mm TPU rims
            double deck = SdfMesher.StealthBlock3D(x - 9.50, y, z - 4.125, 25.00, 14.20, 1.425, 3.0, 0.6);
            double body = Math.Min(Math.Min(w1, w2), deck);

            // Continuous CSG snap-lip pocket (zero SDF discontinuity!)
            double dPocketLower = Math.Max(SdfMesher.Box2D(x - 9.50, y, 21.50, 12.50, 3.0), Math.Max(1.95 - z, z - 5.10));
            double dPocketMouth = Math.Max(SdfMesher.Box2D(x - 9.50, y, 21.15, 12.15, 2.8), 4.95 - z);
            double dCarrierPocket3D = Math.Min(dPocketLower, dPocketMouth);

            double holes = 1e9;
            holes = Math.Min(holes, Math.Sqrt((x - 2.00) * (x - 2.00) + (y + 7.50) * (y + 7.50)) - 2.15);
            holes = Math.Min(holes, Math.Sqrt((x - 2.00) * (x - 2.00) + (y - 7.50) * (y - 7.50)) - 2.15);
            holes = Math.Min(holes, Math.Sqrt((x - 17.00) * (x - 17.00) + (y + 7.50) * (y + 7.50)) - 2.15);
            holes = Math.Min(holes, Math.Sqrt((x - 17.00) * (x - 17.00) + (y - 7.50) * (y - 7.50)) - 2.15);
            holes = Math.Min(holes, Math.Sqrt((x + 5.50) * (x + 5.50) + y * y) - 2.15);
            holes = Math.Min(holes, Math.Sqrt((x - 24.50) * (x - 24.50) + y * y) - 2.15);

            return Math.Max(body, -Math.Min(dCarrierPocket3D, holes));
        }

        // True Watertight CSG Boolean Union of Reference Cradle Shell + Flat Mounting Insert (01a or 01c)
        public static MeshData BuildCradleBooleanUnion(
            string name,
            MeshData refCradleShell,
            Func<double, double, double, double> insertSdf,
            double step = 0.30)
        {
            Vec3 minB, maxB;
            refCradleShell.GetBounds(out minB, out maxB);
            double ox = minB.X - 2.0 * step;
            double oy = minB.Y - 2.0 * step;
            double oz = Math.Floor((minB.Z - step) / step) * step - 0.5 * step;

            int nx = (int)Math.Ceiling((maxB.X + 2.0 * step - ox) / step) + 2;
            int ny = (int)Math.Ceiling((maxB.Y + 2.0 * step - oy) / step) + 2;
            int nz = (int)Math.Ceiling((maxB.Z + 2.0 * step - oz) / step) + 2;

            float[,,] grid = SdfMesher.ComputeMeshSignedDistanceGrid(refCradleShell, ox, oy, oz, nx, ny, nz, step);

            for (int ix = 0; ix <= nx; ix++)
            {
                double x = ox + ix * step;
                if (x < -17.5 || x > 36.5) continue;
                for (int iy = 0; iy <= ny; iy++)
                {
                    double y = oy + iy * step;
                    if (y < -15.5 || y > 15.5) continue;
                    for (int iz = 0; iz <= nz; iz++)
                    {
                        double z = oz + iz * step;
                        if (z < -1.0 || z > 6.8) continue;
                        float dIns = (float)insertSdf(x, y, z);
                        if (dIns < grid[ix, iy, iz]) grid[ix, iy, iz] = dIns;
                    }
                }
            }

            var result = SdfMesher.ExtractFromGrid(name, grid, ox, oy, oz, step, null);
            result.SnapBottomToZero();
            return result;
        }

        // Flex-Clip outer shave for rigid filaments (Sunlu Easy-PA / PETG):
        // Smoothly trims outer excess wall of twin rear spring-clip arms (X > 50.5, Z > 5) from |Y|=20.8..21.8 down to |Y|=19.4mm
        // (~2.8mm arm thickness -> ~60% lower snap-in force) while keeping inner |Y|=16.6mm drone grip & Ø10.5mm strap bosses 100% intact!
        public static double RearFlexClipTrimSdf(double x, double y, double z)
        {
            double ay = Math.Abs(y);
            double dBox = SdfMesher.SmoothMax(SdfMesher.SmoothMax(50.5 - x, 5.0 - z, 1.5), 19.4 - ay, 1.0);
            double rBoss = Math.Sqrt((x - 60.6) * (x - 60.6) + (z - 19.2) * (z - 19.2));
            double dKeepBoss = rBoss - 5.45;
            return SdfMesher.SmoothMax(dBox, -dKeepBoss, 1.2);
        }

        // True Watertight CSG Boolean Union of 45-Deg Easy-PA Cradle + Insert + Snap-Off Anchor Fin (01b or 01d Flex-Clip)
        public static MeshData BuildCradle45DegBooleanUnion(
            string name,
            MeshData refCradleShell,
            bool includeAnchorFin,
            double step = 0.30,
            bool applyRearFlexClipTrim = false)
        {
            double c = 0.680983722;
            double s = 0.732298553;

            // Rotate reference cradle shell to 45 deg and record exact minZ for orthonormal inverse mapping
            var rotShell = new MeshData("RotShell");
            double minZ = 1e9;
            var tempVerts = new List<Vec3>(refCradleShell.Vertices.Count);
            foreach (var v in refCradleShell.Vertices)
            {
                double zOrig = v.Z - 22.37105;
                double xr = c * v.X - s * zOrig;
                double yr = v.Y;
                double zr = s * v.X + c * zOrig;
                if (zr < minZ) minZ = zr;
                tempVerts.Add(new Vec3(xr, yr, zr));
            }
            foreach (var v in tempVerts) rotShell.Vertices.Add(new Vec3(v.X, v.Y, v.Z - minZ));
            rotShell.Triangles.AddRange(refCradleShell.Triangles);

            Vec3 minB, maxB;
            rotShell.GetBounds(out minB, out maxB);
            if (includeAnchorFin)
            {
                if (-21.0 < minB.X) minB.X = -21.0; if (29.5 > maxB.X) maxB.X = 29.5;
                if (-25.5 < minB.Y) minB.Y = -25.5; if (25.5 > maxB.Y) maxB.Y = 25.5;
                if (0.0 < minB.Z) minB.Z = 0.0; if (56.0 > maxB.Z) maxB.Z = 56.0;
            }

            double ox = minB.X - 2.0 * step;
            double oy = minB.Y - 2.0 * step;
            double oz = Math.Floor((minB.Z - step) / step) * step - 0.5 * step;

            int nx = (int)Math.Ceiling((maxB.X + 2.0 * step - ox) / step) + 2;
            int ny = (int)Math.Ceiling((maxB.Y + 2.0 * step - oy) / step) + 2;
            int nz = (int)Math.Ceiling((maxB.Z + 2.0 * step - oz) / step) + 2;

            float[,,] grid = SdfMesher.ComputeMeshSignedDistanceGrid(rotShell, ox, oy, oz, nx, ny, nz, step);

            for (int ix = 0; ix <= nx; ix++)
            {
                double x = ox + ix * step;
                for (int iy = 0; iy <= ny; iy++)
                {
                    double y = oy + iy * step;
                    for (int iz = 0; iz <= nz; iz++)
                    {
                        double z = oz + iz * step;
                        // Inverse-rotate into flat cradle space to union CradleMountingInsertSdf & optional Flex-Clip trim
                        double zrRaw = z + minZ;
                        double xFlat = c * x + s * zrRaw;
                        double zFlat = (-s * x + c * zrRaw) + 22.37105;

                        if (applyRearFlexClipTrim && xFlat >= 48.0 && Math.Abs(y) >= 17.5 && zFlat >= 2.0)
                        {
                            float dTrim = -(float)RearFlexClipTrimSdf(xFlat, y, zFlat);
                            if (dTrim > grid[ix, iy, iz]) grid[ix, iy, iz] = dTrim;
                        }

                        if (xFlat >= -17.5 && xFlat <= 36.5 && y >= -15.5 && y <= 15.5 && zFlat >= -1.0 && zFlat <= 6.8)
                        {
                            float dIns = (float)CradleMountingInsertSdf(xFlat, y, zFlat);
                            if (dIns < grid[ix, iy, iz]) grid[ix, iy, iz] = dIns;
                        }
                        if (includeAnchorFin && x >= -21.0 && x <= 29.5 && y >= -25.5 && y <= 25.5 && z >= -1.0 && z <= 56.0)
                        {
                            float dFin = (float)Cradle45DegAnchorFinSdf(x, y, z);
                            if (dFin < grid[ix, iy, iz]) grid[ix, iy, iz] = dFin;
                        }
                    }
                }
            }

            var result = SdfMesher.ExtractFromGrid(name, grid, ox, oy, oz, step, null);
            result.SnapBottomToZero();
            return result;
        }

        public static MeshData RotateCradleTo45DegPrintOrientation(MeshData flatCradle, string newName)
        {
            double c = 0.680983722;
            double s = 0.732298553;
            var rotated = new MeshData(newName);
            double minZ = 1e9;
            var tempVerts = new List<Vec3>(flatCradle.Vertices.Count);
            foreach (var v in flatCradle.Vertices)
            {
                double zOrig = v.Z - 22.37105;
                double xr = c * v.X - s * zOrig;
                double yr = v.Y;
                double zr = s * v.X + c * zOrig;
                if (zr < minZ) minZ = zr;
                tempVerts.Add(new Vec3(xr, yr, zr));
            }
            foreach (var v in tempVerts) rotated.Vertices.Add(new Vec3(v.X, v.Y, v.Z - minZ));
            rotated.Triangles.AddRange(flatCradle.Triangles);
            return rotated;
        }

        public static MeshData BuildCradle45DegSnapOffAnchorFin(double step = 0.36)
        {
            return SdfMesher.ExtractMesh("SnapOff_45Deg_Anchor_Fin", Cradle45DegAnchorFinSdf, new Vec3(-21.0, -25.5, -0.5), new Vec3(29.5, 25.5, 56.0), step);
        }

        public static MeshData BuildCradle72DMountingInsertForPetgCarrier(double step = 0.28)
        {
            return SdfMesher.ExtractMesh("Cradle_72D_Pocket_Insert", Cradle72DMountingInsertSdf, new Vec3(-17.0, -15.5, -0.5), new Vec3(36.0, 15.5, 6.2), step);
        }

        // =========================================================================
        // 02. MOUNT 1: ORBEA WILD LT "STEALTH FAIRING" NUMBER-PLATE MOUNT SYSTEM
        // =========================================================================
        // 02a: Faceted Stealth Number-Plate Shield + Armored Lower Chin Roost Fender
        // Prints 100% FLAT on its back (Z = 0.00mm) on the build plate with ZERO supports!
        // Coordinate system in print space:
        //   - X in [-80.0, +76.0]: Vertical axis along the plate (-X = bottom Chin Roost Fender over Nose/Gimbal, +X = top near Transceiver)
        //   - Y in [-58.0, +58.0]: Lateral width across front of head tube (116mm wide with faceted Orbea stealth winglets!)
        //   - Z in [0.0, +28.0]: Thickness (+Z faces forward toward the drone cradle; Z=0 faces rearward toward the head tube/cable tunnel)
        //   - Cradle + 90A TPU Vibration Isolator Puck center at (X = +9.50mm, Y = 0.00mm), so Nose Cup (X_cradle = -73..-45) nests right inside the Lower Chin Fender!
        public static MeshData BuildOrbeaStealthNumberPlateShield(double step = 0.42)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ay = Math.Abs(y);

                // 1) Central Structural Spine & Isolator Housing: X in [-79.0, +72.0], Y in [-25.5, +25.5], Z in [0.0, 9.5]
                // Chiseled octagonal Orbea stealth silhouette with 12mm corner chamfers and 1.8mm top bevels
                double dSpine = SdfMesher.StealthBlock3D(x + 3.5, y, z - 4.75, 75.5, 25.5, 4.75, 11.0, 1.5);

                // 2) Swept Faceted Orbea Side Roost Winglets: Y in [20.0, 57.0], X in [-56.0, +54.0]
                // Tapered trapezoidal wings (narrowing in X as |Y| increases, rising slightly forward in Z from Z=0..5.8mm to Z=2.2..7.5mm at outer tips!)
                double wingU = Math.Max(0.0, Math.Min(1.0, (ay - 20.0) / 37.0));
                double wingHalfX = 55.0 - 20.0 * wingU; // tapers from 110mm tall at spine to 70mm tall at wingtips!
                double wingShiftX = 4.0 * wingU;        // slight aggressive upward sweep like an ODI/Orbea number plate!
                double dWing2D = Math.Max(Math.Max(20.0 - ay, ay - 57.0), Math.Abs(x - wingShiftX) - wingHalfX);
                // Crisp 45-deg corner cuts on wingtips
                double dWingCorner = (Math.Abs(x - wingShiftX) + (ay - 20.0) - (wingHalfX + 37.0 - 14.0)) * 0.70710678;
                dWing2D = Math.Max(dWing2D, dWingCorner);
                // Keep Z = 0 flat on bed at inner wing root, beveling outer wing face at Z_top = 6.2mm with 2.0mm stealth facet!
                double dWingZ = Math.Max(0.0 - z, z - 6.2);
                double dWingBevel = (dWing2D + (z - 6.2) + 2.2) * 0.70710678;
                double wings3D = Math.Max(Math.Max(dWing2D, dWingZ), dWingBevel);

                // 3) Strut-Arm Bolting Bosses on Upper Wings: centered at Y = +-31.5mm, X in [+14.0, +48.0], Z in [0.0, 9.5mm]
                // Gives solid 9.5mm thickness for captive M4 hex nuts where the 02b Swept Handlebar Drop Arms bolt on!
                double strutPad = SdfMesher.StealthBlock3D(x - 31.0, ay - 31.5, z - 4.75, 17.5, 8.5, 4.75, 4.0, 1.2);

                // 4) Armored Faceted Lower Chin Roost Fender / Nose Vault Scoop: X in [-80.5, -46.0], Y in [-27.5, +27.5], Z in [0.0, 27.5]
                // Wraps around the bottom AND left/right sides of the DJI Neo 2's Closed Nose Cup (protecting 2-axis Gimbal + Forward LiDAR from tire roost!)
                // Outer faceted shell with aggressive 45-deg prow chamfers (self-supporting from Z=0 up to Z=27.5!)
                double dChin2D = SdfMesher.ChamferBox2D(x - (-63.5), y, 17.0, 27.5, 9.5);
                // Slant the front-bottom prow at 45 deg so rocks glance off and it prints cleanly from Z=0!
                double dChinProw = ((-79.5 - x) + (z - 9.5)) * 0.70710678;
                double chinOuter = Math.Max(Math.Max(dChin2D, Math.Max(0.0 - z, z - 27.5)), dChinProw);
                // Hollow out interior pocket of Chin Vault (X > -74.5, |Y| < 23.2, Z > 9.5) so the Universal Cradle's Closed Nose Cup drops cleanly inside!
                double chinCavity = Math.Max(SdfMesher.ChamferBox2D(x - (-56.0), y, 18.8, 23.2, 5.0), 9.5 - z);
                double chinFender = Math.Max(chinOuter, -chinCavity);

                double body = Math.Min(Math.Min(dSpine, wings3D), Math.Min(strutPad, chinFender));
                body = Math.Max(body, 0.0 - z);

                // Subtract features:
                double cut = 1e9;

                // (A) Recessed 3.5mm Deep Stadium Pocket on Front Face (Z in [6.0, 10.0], centered at X = +9.50, Y = 0.00)
                // Captures the 48.0 x 26.0 x 7.5mm 90A TPU Vibration Isolator Puck (04a)!
                double dPuckPocket = Math.Max(SdfMesher.Box2D(x - 9.50, y, 24.3, 13.3, 4.1), 6.0 - z);
                cut = Math.Min(cut, dPuckPocket);

                // (B) 6 x Cradle + TPU Isolator M4 Mounting Holes + Rear Captive M4 Hex Nut Pockets (Z in [-0.5, 3.6], leaving 2.4mm solid clamping wall at Z in [3.6, 6.0]!)
                cut = Math.Min(cut, M4HoleAndHexNutSdf(x, y, z, 2.00, -7.50, -0.5, 3.6));
                cut = Math.Min(cut, M4HoleAndHexNutSdf(x, y, z, 2.00,  7.50, -0.5, 3.6));
                cut = Math.Min(cut, M4HoleAndHexNutSdf(x, y, z, 17.00, -7.50, -0.5, 3.6));
                cut = Math.Min(cut, M4HoleAndHexNutSdf(x, y, z, 17.00,  7.50, -0.5, 3.6));
                cut = Math.Min(cut, M4HoleAndHexNutSdf(x, y, z, -5.50, 0.00, -0.5, 3.6));
                cut = Math.Min(cut, M4HoleAndHexNutSdf(x, y, z, 24.50, 0.00, -0.5, 3.6));

                // (C) Twin Out-Front Drop-Arm Mounting Slots at Y = +-31.5mm (2 slotted M4 mounts per side: X in [20..26] and X in [36..42])
                // Gives +-3.0mm vertical height adjustment, with front recessed M4 hex nut pockets (Z in [6.0, 10.0]) so bolts enter cleanly from the rear arms!
                for (int sIdx = 0; sIdx < 2; sIdx++)
                {
                    double sx0 = sIdx == 0 ? 20.0 : 36.0;
                    double sx1 = sx0 + 6.0;
                    double dArmSlot = SdfMesher.SlotX2D(x, ay, sx0, sx1, 31.5, 2.15);
                    // Hex-width anti-rotation nut channel on front face (width in Y = 7.18mm -> ry = 3.59mm, Z in [6.0, 10.0])!
                    double dNutTrack2D = Math.Max(Math.Max((sx0 - 3.6) - x, x - (sx1 + 3.6)), Math.Abs(ay - 31.5) - 3.59);
                    double dNutTrack3D = Math.Max(dNutTrack2D, 6.0 - z);
                    cut = Math.Min(cut, Math.Min(dArmSlot, dNutTrack3D));
                }

                // (D) Orbea-Styled Angular Stealth Aero Slashes in Side Winglets (at Y = +-44.5mm, X in [-32, +2])
                double slashX = x + 15.0 - (ay - 44.5) * 0.7;
                double dSlash = SdfMesher.ChamferBox2D(slashX, ay - 45.0, 14.0, 3.2, 2.0);
                cut = Math.Min(cut, dSlash);

                // (E) Weight-saving hexagonal/stealth relief windows under the solid cradle zones (at X = +52.0, Y = 0 and X = -31.0, Y = 0)
                // Wait: at X = -31.0 (heatsink) we keep a louvered/angled mud deflector rather than an open hole so tire spray NEVER hits the drone!
                // At top X = +53.0 (behind the sealed belly sensor plug), add a recessed stealth pocket on the back (Z in [-0.5, 4.5]) to save weight while keeping 100% mud seal!
                double dRearRelief1 = Math.Max(SdfMesher.ChamferBox2D(x - 51.0, y, 11.0, 12.0, 4.0), z - 4.5);
                double dRearRelief2 = Math.Max(SdfMesher.ChamferBox2D(x + 31.0, y, 10.0, 12.0, 4.0), z - 4.5);
                cut = Math.Min(cut, Math.Min(dRearRelief1, dRearRelief2));

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh(
                "02a_Orbea_Stealth_NumberPlate_Shield",
                sdf,
                new Vec3(-82.0, -59.0, -0.6),
                new Vec3(74.0, 59.0, 28.5),
                step);
        }

        // 02c: Stealth Number-Plate Shield Variant w/ Fully-Embedded Print-Pause M4 DIN 934 Captive Nuts (Zero Exposed Nuts!)
        //   - Pause 1 at Z = 4.60 mm (Layer 23 @ 0.20mm): drop 6x M4 DIN 934 nuts into cradle pockets (Z = 1.00..4.60, 0.40mm headroom)
        //   - Pause 2 at Z = 8.40 mm (Layer 42 @ 0.20mm): drop 4x M4 DIN 934 nuts into strut-arm pockets (Z = 4.80..8.40, 4.8mm solid rear wall!)
        public static MeshData BuildOrbeaStealthNumberPlateShieldPauseEmbed(double step = 0.90)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ay = Math.Abs(y);

                double dSpine = SdfMesher.StealthBlock3D(x + 3.5, y, z - 4.75, 75.5, 25.5, 4.75, 11.0, 1.5);

                double wingU = Math.Max(0.0, Math.Min(1.0, (ay - 20.0) / 37.0));
                double wingHalfX = 55.0 - 20.0 * wingU;
                double wingShiftX = 4.0 * wingU;
                double dWing2D = Math.Max(Math.Max(20.0 - ay, ay - 57.0), Math.Abs(x - wingShiftX) - wingHalfX);
                double dWingCorner = (Math.Abs(x - wingShiftX) + (ay - 20.0) - (wingHalfX + 37.0 - 14.0)) * 0.70710678;
                dWing2D = Math.Max(dWing2D, dWingCorner);
                double dWingZ = Math.Max(0.0 - z, z - 6.2);
                double dWingBevel = (dWing2D + (z - 6.2) + 2.2) * 0.70710678;
                double wings3D = Math.Max(Math.Max(dWing2D, dWingZ), dWingBevel);

                double strutPad = SdfMesher.StealthBlock3D(x - 31.0, ay - 31.5, z - 4.75, 17.5, 8.5, 4.75, 4.0, 1.2);

                double dChin2D = SdfMesher.ChamferBox2D(x - (-63.5), y, 17.0, 27.5, 9.5);
                double dChinProw = ((-79.5 - x) + (z - 9.5)) * 0.70710678;
                double chinOuter = Math.Max(Math.Max(dChin2D, Math.Max(0.0 - z, z - 27.5)), dChinProw);
                double chinCavity = Math.Max(SdfMesher.ChamferBox2D(x - (-56.0), y, 18.8, 23.2, 5.0), 9.5 - z);
                double chinFender = Math.Max(chinOuter, -chinCavity);

                double body = Math.Min(Math.Min(dSpine, wings3D), Math.Min(strutPad, chinFender));
                body = Math.Max(body, 0.0 - z);

                double cut = 1e9;

                // (A) Recessed 3.5mm Deep Stadium Pocket on Front Face (Z in [6.0, 10.0]) for 04a 90A TPU Puck
                double dPuckPocket = Math.Max(SdfMesher.Box2D(x - 9.50, y, 24.3, 13.3, 4.1), 6.0 - z);
                cut = Math.Min(cut, dPuckPocket);

                // (B) 6x Embedded Cradle M4 DIN 934 Hex Pockets (Z in [1.00, 4.60] -> Pause 1 at Z = 4.60mm!)
                // Bolt bores enter from front (+Z down to Z = 1.00) so the rear Z = 0..1.00 skin stays 100% sealed!
                double[] cxArr = new double[] { 2.00, 2.00, 17.00, 17.00, -5.50, 24.50 };
                double[] cyArr = new double[] { -7.50, 7.50, -7.50, 7.50, 0.00, 0.00 };
                for (int i = 0; i < 6; i++)
                {
                    double dx = x - cxArr[i], dy = y - cyArr[i];
                    double dBore = Math.Max(Math.Sqrt(dx * dx + dy * dy) - 2.15, 1.00 - z);
                    double dHex = Math.Max(SdfMesher.Hexagon2D(dx, dy, 7.25), Math.Max(1.00 - z, z - 4.60));
                    cut = Math.Min(cut, Math.Min(dBore, dHex));
                }

                // (C) 4x Embedded Strut-Arm M4 DIN 934 Hex Pockets at Y = +-31.5, X = 23.0 & 39.0 (Z in [4.80, 8.40] -> Pause 2 at Z = 8.40mm!)
                // Bolt bores enter from rear (Z = -0.5 up to Z = 8.40) against 4.80mm solid rear wall; front Z = 8.40..9.50 skin stays 100% sealed!
                for (int sIdx = 0; sIdx < 2; sIdx++)
                {
                    double sx = sIdx == 0 ? 23.0 : 39.0;
                    double dx = x - sx, dy = ay - 31.5;
                    double dBore = Math.Max(Math.Sqrt(dx * dx + dy * dy) - 2.15, z - 8.40);
                    double dHex = Math.Max(SdfMesher.Hexagon2D(dx, dy, 7.25), Math.Max(4.80 - z, z - 8.40));
                    cut = Math.Min(cut, Math.Min(dBore, dHex));
                }

                // (D) Orbea-Styled Angular Stealth Aero Slashes in Side Winglets
                double slashX = x + 15.0 - (ay - 44.5) * 0.7;
                double dSlash = SdfMesher.ChamferBox2D(slashX, ay - 45.0, 14.0, 3.2, 2.0);
                cut = Math.Min(cut, dSlash);

                // (E) Rear Weight-Saving Relief Pockets
                double dRearRelief1 = Math.Max(SdfMesher.ChamferBox2D(x - 51.0, y, 11.0, 12.0, 4.0), z - 4.5);
                double dRearRelief2 = Math.Max(SdfMesher.ChamferBox2D(x + 31.0, y, 10.0, 12.0, 4.0), z - 4.5);
                cut = Math.Min(cut, Math.Min(dRearRelief1, dRearRelief2));

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh(
                "02c_Orbea_Stealth_NumberPlate_Shield_PauseEmbed",
                sdf,
                new Vec3(-82.0, -59.0, -0.6),
                new Vec3(74.0, 59.0, 28.5),
                step);
        }

        // 02b: Swept Out-Front Faceted Twin-Truss Handlebar Drop Arms (Pair)
        // Printed FLAT on their sides (Z in [0.0, 13.0mm] is lateral arm thickness!) with ZERO supports and 100% continuous perimeter strength!
        // In side-view print plane (X, Y):
        //   - Top Handlebar Clamp Pad sits horizontally on top of the 03b Clamshell Saddle: X in [-15.0, +15.0] (bolts at X = -9.0 and +9.0), Y in [0.0, +7.5]
        //   - Arm sweeps forward (+X to +54mm, creating a 38mm+ open SIC cable tunnel!) and angles down at 64 deg (matching the 2027 Orbea Wild LT head-tube angle!)
        //   - Front Plate Mounting Flange tilts at 64 deg from horizontal (26 deg from vertical) so the Number Plate sits parallel to the head tube!
        public static MeshData BuildOrbeaStealthNumberPlateDropArmsPair(double step = 0.34)
        {
            // Unit vector along the 64-deg raked number plate (tilted back 26 deg from vertical):
            // Down-and-back along headtube: (-sin 26, -cos 26) = (-0.43837, -0.89879)
            // Forward outward normal of plate: (+cos 26, +sin 26) = (+0.89879, +0.43837)
            double ux = 0.438371, uy = 0.898794;   // along plate upward (+X_plate)
            double nx = 0.898794, ny = -0.438371;  // forward normal (+Z_plate, pointing forward and slightly down? Wait: if top tilts back toward rider (-X), forward normal tilts UP (+Y)!)
            // Let's verify! On a bike with a 64-deg head tube:
            // Going UP the head tube moves UP (+Y) and BACKWARD toward the rider (-X)!
            // Yes! Upward along the head tube is (-cos 64, +sin 64) = (-0.43837, +0.89879)!
            // And FORWARD perpendicular to the head tube is (+sin 64, +cos 64) = (+0.89879, +0.43837)!
            ux = -0.438371; uy = 0.898794;
            nx = 0.898794;  ny = 0.438371;

            // Let's place the center of the two plate-bolting holes (X_plate = +31.0) at (px0 = +50.0, py0 = -12.0) relative to the top clamp saddle (0, 0)!
            // Wait: at X_plate = +31.0 -> (50.0, -12.0), where is X_plate = 0 (plate center)?
            // Plate center = (50, -12) - 31 * (-0.438, +0.899) = (+63.6, -39.9mm)!
            // And where is the bottom of the Chin Fender (X_plate = -80.5)?
            // (50, -12) - 111.5 * (-0.438, +0.899) = (+98.8, -112.2mm below saddle = only ~136mm below bar center -> >60mm ABOVE Fox 38 fork crown bottom!)
            // And at X_plate = +31.0, (px0 = +50.0) is 50mm forward of the handlebar axis -> massive 35mm+ clearance for SIC headset cables and stem faceplate!
            double px0 = 50.0, py0 = -12.0;

            Func<double, double, double, double> singleArmSdf = (x, y, z) =>
            {
                // 1) Top Saddle Mounting Pad: X in [-15.5, +16.0], Y in [0.0, 9.5] (beefed up to 9.5mm tall, zero top-edge scalloping!)
                double dTopPad2D = SdfMesher.ChamferBox2D(x - 0.25, y - 4.75, 15.75, 4.75, 2.0);

                // 2) Front Raked Plate Flange: centered at (px0, py0), length +-18.5mm along (ux, uy), thickness 9.0mm behind plate backplane
                double dxp = x - px0, dyp = y - py0;
                double sAlong = dxp * ux + dyp * uy;      // coordinate along plate [-18.5, +18.5]
                double sNormal = dxp * nx + dyp * ny;     // coordinate along plate normal [-9.0, 0.0] (0.0 mates flush against Z_plate = 0!)
                double dFlange2D = SdfMesher.ChamferBox2D(sAlong, sNormal + 4.5, 18.5, 4.5, 2.0);

                // 3) Swept Dual-Chord Truss Strut connecting Top Pad to Front Raked Flange
                // Upper tension chord: from (8.0, 5.2) to (u1x, u1y), radius 4.2mm (8.4mm thick chord!)
                double u1x = px0 + 11.5 * ux - 4.2 * nx, u1y = py0 + 11.5 * uy - 4.2 * ny;
                double dChordTop = Capsule2D(x, y, 8.0, 5.2, u1x, u1y, 4.2);

                // Lower compression arch chord: from (-4.0, 4.2) -> mid-arch (22.0, -5.5) -> lower flange (l1x, l1y), radius 4.2mm
                double l1x = px0 - 11.5 * ux - 4.2 * nx, l1y = py0 - 11.5 * uy - 4.2 * ny;
                double dChordBotA = Capsule2D(x, y, -4.0, 4.2, 22.0, -5.5, 4.2);
                double dChordBotB = Capsule2D(x, y, 22.0, -5.5, l1x, l1y, 4.2);

                // Web fill between chords
                double dWeb = Triangle2D(x, y, 6.0, 4.8, u1x, u1y, l1x, l1y);
                double profile2D = SdfMesher.SmoothMin(Math.Min(dTopPad2D, dFlange2D), Math.Min(dChordTop, Math.Min(dChordBotA, dChordBotB)), 2.2);
                profile2D = Math.Min(profile2D, dWeb);

                // Continuous CSG trim flush against top of C-clip saddle (Y >= 0 for X < 14.5) and flush against back of Number Plate (sNormal <= 0.0)!
                double dSaddleClear = Math.Max(x - 14.5, y - 0.0);
                profile2D = Math.Max(profile2D, -dSaddleClear);
                profile2D = Math.Max(profile2D, sNormal);

                // Extrude Z in [0.0, 15.0mm] (matches exact 15.0mm width of 03b handlebar clamps!) with 1.0mm Orbea stealth bevels
                double dz = Math.Abs(z - 7.5) - 7.5;
                double dBevel = (profile2D + dz + 1.0) * 0.70710678;
                double body3D = Math.Max(Math.Max(profile2D, dz), dBevel);

                // Subtract features:
                double cut = 1e9;

                // (A) Centered Stealth Truss Slot along the arm's neutral axis from (24.0, -2.5) to (36.5, -9.5), radius 2.8mm
                // Guaranteed >= 4.5mm solid upper tension chord and >= 4.5mm solid lower compression arch with ZERO thin slivers!
                double dTrussWindow = Capsule2D(x, y, 24.0, -2.5, 36.5, -9.5, 2.8);
                dTrussWindow = Math.Max(dTrussWindow, profile2D + 4.5);
                cut = Math.Min(cut, dTrussWindow);

                // (B) Top Saddle M4 Bolt Slots (at X = -9.0 and X = +9.0, running along Y, centered at Z = 7.5mm, slotted +-1.2mm in Z)
                // Clean through-bores (d = 4.3mm -> 5.3mm solid walls on both sides in Z, zero top-edge notches! Mount with M4x16 button-head + washer on top)
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    double bx = sx * 9.0;
                    double dBore = Math.Max(SdfMesher.SlotY2D(x, z, bx, 6.3, 8.7, 2.15), Math.Max(-1.0 - y, y - 10.5));
                    cut = Math.Min(cut, dBore);
                }

                // (C) Front Plate Mounting M4 Bolt Holes (at sAlong = -8.0 and +8.0, centered at Z = 7.5mm)
                // Punch BOTH the M4 bore (r = 2.15mm) and rear socket-head / hex-wrench access tunnel (r = 3.90mm) ALL THE WAY OUT THE BACK (sNormal to -38.0mm)!
                // Leaves a solid 4.5mm clamping flange at the front (sNormal in [-4.5, 0.0]) and 100% unobstructed rear bolt & Allen-key access!
                for (int ss = -1; ss <= 1; ss += 2)
                {
                    double sa = ss * 8.0;
                    double dRad = Math.Sqrt((sAlong - sa) * (sAlong - sa) + (z - 7.5) * (z - 7.5));
                    double dPlateBore = Math.Max(dRad - 2.15, Math.Max(-38.0 - sNormal, sNormal - 1.0));
                    double dPlateCbore = Math.Max(dRad - 3.90, Math.Max(-38.0 - sNormal, sNormal - (-4.5)));
                    cut = Math.Min(cut, Math.Min(dPlateBore, dPlateCbore));
                }

                return Math.Max(body3D, -cut);
            };

            var single = SdfMesher.ExtractMesh("Stealth_Drop_Arm", singleArmSdf,
                new Vec3(-17.0, -32.0, -0.6), new Vec3(62.0, 11.0, 15.6), step);

            var pair = new MeshData("02b_Orbea_Stealth_NumberPlate_Drop_Arms_Pair");
            var armL = single.Clone("Drop_Arm_L"); armL.Translate(0.0, 22.0, 0.0);
            var armR = single.Clone("Drop_Arm_R"); armR.Translate(0.0, -22.0, 0.0);
            pair.Append(armL);
            pair.Append(armR);
            return pair;
        }

        private static double Capsule2D(double px, double py, double ax, double ay, double bx, double by, double r)
        {
            double vx = bx - ax, vy = by - ay;
            double wx = px - ax, wy = py - ay;
            double t = Math.Max(0.0, Math.Min(1.0, (wx * vx + wy * vy) / Math.Max(1e-9, vx * vx + vy * vy)));
            double dx = px - (ax + t * vx), dy = py - (ay + t * vy);
            return Math.Sqrt(dx * dx + dy * dy) - r;
        }

        private static double Triangle2D(double px, double py, double x0, double y0, double x1, double y1, double x2, double y2)
        {
            // Signed distance to CCW/CW triangle via half-planes
            double area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
            double s = area >= 0 ? 1.0 : -1.0;
            double e0 = s * ((px - x0) * (y1 - y0) - (py - y0) * (x1 - x0)) / Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            double e1 = s * ((px - x1) * (y2 - y1) - (py - y1) * (x2 - x1)) / Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            double e2 = s * ((px - x2) * (y0 - y2) - (py - y2) * (x0 - x2)) / Math.Sqrt((x0 - x2) * (x0 - x2) + (y0 - y2) * (y0 - y2));
            return Math.Max(e0, Math.Max(e1, e2));
        }

        // =========================================================================
        // 03. MOUNT 2: ORBEA WILD LT "STEALTH OC" OVER-STEM BRIDGE & CLAMSHELLS
        // =========================================================================
        // Restyled in angular Orbea Wild LT / OC MC11 stealth facets with:
        //   - Recessed 3.5mm top pocket for the 04a 90A TPU Vibration Isolator Puck (lowering stack height & hiding the puck!)
        //   - Integrated Front Chiseled Aero Roost Deflector Prow (X in [+25, +43mm]) to shield the cradle from upward tire spray!
        public static MeshData BuildOrbeaStealthOverStemBridge(double step = 0.38)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);

                // Octagonal stealth wing footprint: X in [-27, +27] at center, tapering crisply at wings Y in [-42, +42]
                double halfX = ay <= 21.0 ? 27.5 : Math.Max(15.5, 27.5 - (ay - 21.0) * 0.58);
                double dMain2D = SdfMesher.ChamferBox2D(x, y, halfX, 41.5, 7.5);

                // Top profile with sharp angular step from center isolator bezel (Z = 14.0) down to faceted clamp wings (Z = 8.5)
                double zTop = ay <= 20.5 ? 14.0 : (ay >= 24.5 ? 8.5 : 14.0 - (ay - 20.5) * (5.5 / 4.0));
                // Bottom profile: 46mm wide central arch (Z = 6.0) clearing the OC MC11 stem faceplate
                double zBot = ay >= 23.0 ? 0.0 : (ay <= 19.5 ? 6.0 : 6.0 * (23.0 - ay) / 3.5);

                double dMainZ = Math.Max(zBot - z, z - zTop);
                double dMainBevel = (dMain2D + (z - zTop) + 1.6) * 0.70710678;
                double mainBody = Math.Max(Math.Max(dMain2D, dMainZ), dMainBevel);

                // Integrated Front Stealth Roost Deflector Prow (X in [+22.0, +42.0], Y in [-21.0, +21.0], sweeping up to Z = 16.5mm!)
                // Echoes the sharp angular prow of the 2027 Orbea Wild LT head tube and shields the front nose/sensors from upward fork spray!
                double prowU = Math.Max(0.0, Math.Min(1.0, (x - 22.0) / 20.0));
                double prowHalfY = 21.0 - 7.5 * prowU;
                double dProw2D = Math.Max(Math.Max(22.0 - x, x - 42.0), ay - prowHalfY);
                double zProwBot = 6.0 + 6.5 * prowU;
                double zProwTop = 14.0 + 2.5 * prowU - 0.18 * ay; // V-creased stealth top ridge!
                double prow3D = Math.Max(dProw2D, Math.Max(zProwBot - z, z - zProwTop));

                double body = Math.Min(mainBody, prow3D);

                double cut = 1e9;

                // (A) Recessed 3.5mm Deep Stadium Pocket on Top Deck (Z in [10.5, 15.0]) for the 04a 90A TPU Vibration Isolator Puck!
                double dPuckPocket = Math.Max(SdfMesher.Box2D(x, y, 24.3, 13.3, 4.1), 10.5 - z);
                cut = Math.Min(cut, dPuckPocket);

                // (B) Slotted Lateral M4 Mounting Channels on Left & Right Wings (X = +-9.0, Y in [25.0..36.0] -> 50 to 72mm span!)
                double dWingSlot = SdfMesher.SlotY2D(ax, ay, 9.0, 25.0, 36.0, 2.15);
                double dWingCbore = Math.Max(SdfMesher.SlotY2D(ax, ay, 9.0, 25.0, 36.0, 4.15), 4.5 - z);
                cut = Math.Min(cut, Math.Min(dWingSlot, dWingCbore));

                // (C) Center Cradle + TPU Isolator M4 Mounting Holes + Underside Arch Captive M4 Hex Nut Pockets (Z in [5.5, 8.6], leaving 1.9mm solid bridge below Z=10.5!)
                cut = Math.Min(cut, M4HoleAndHexNutSdf(ax, ay, z, 7.50, 7.50, 5.5, 8.6));
                cut = Math.Min(cut, M4HoleAndHexNutSdf(ax, y, z, 15.00, 0.00, 5.5, 8.6));

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh("03a_Orbea_Stealth_OverStem_Bridge", sdf, new Vec3(-29.0, -43.0, -0.6), new Vec3(43.5, 43.0, 17.5), step);
        }

        // 03b & 03f: Restyled Faceted "Orbea OC" Swinging M4 Pin-Hinged Clamshell Handlebar Clamps (Zero Plastic Flexing!)
        public static MeshData BuildHandlebarHingedClamshellSingle(double boreDiameter, bool assembledClosed, double step = 0.34)
        {
            double rIn = boreDiameter * 0.5;
            double rOut = rIn + 5.6;
            double ySaddleTop = rIn + 6.0;
            double pivotX = -(rIn + 5.2);
            double pivotY = 0.0;
            double pinchX = +(rIn + 6.5);

            Func<double, double, double, double> upperHalfSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                // Octagonal/faceted outer profile blended onto ring for crisp OC MC11 CNC aesthetic!
                double dOct = SdfMesher.ChamferBox2D(x, y, rOut, rOut, rOut * 0.52);
                double dRing2D = Math.Max(rIn - r, Math.Max(r - (rOut + 0.6), dOct));

                double dSaddle2D = SdfMesher.ChamferBox2D(x, y - (ySaddleTop * 0.5 + 3.0), 14.8, ySaddleTop * 0.5 - 3.0, 3.2);
                dSaddle2D = Math.Max(dSaddle2D, rIn - r);

                double dHingeCyl2D = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 5.2;
                double dHingeWeb2D = Math.Max(SdfMesher.ChamferBox2D(x - (pivotX * 0.5 - 4.0), y - 2.5, 6.0, 4.5, 1.8), rIn - r);
                double dFront2D = SdfMesher.SmoothMin(dHingeCyl2D, dHingeWeb2D, 1.2);

                double dRearLug2D = Math.Max(SdfMesher.ChamferBox2D(x - (rIn + 5.2), y - 5.1, 6.5, 4.4, 2.2), rIn - r);

                double upperMain2D = Math.Max(SdfMesher.SmoothMin(Math.Min(dRing2D, dSaddle2D), dRearLug2D, 1.4), 0.6 - y);
                double profile2D = Math.Max(Math.Min(upperMain2D, dFront2D), rIn - r);

                double dz = Math.Abs(z - 7.5) - 7.5;
                double dSideBevel = (profile2D + dz + 1.0) * 0.70710678;
                double body3D = Math.Max(Math.Max(profile2D, dz), dSideBevel);

                double cut = 1e9;
                // 100% Support-Free Half-Lap Hinge: upper jaw knuckle occupies Z = 0.0..7.35mm (flat on build plate!)
                double dKnuckleCut = Math.Max(Math.Max(x - (-rIn + 0.5), y - 5.5), 7.35 - z);
                cut = Math.Min(cut, dKnuckleCut);

                double dHingePinBore = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 2.15;
                double dHingeNut = Math.Max(SdfMesher.Hexagon2D(x - pivotX, y - pivotY, 7.18), z - 3.4);
                cut = Math.Min(cut, Math.Min(dHingePinBore, dHingeNut));

                for (int sx = -1; sx <= 1; sx += 2)
                {
                    double bx = sx * 9.0;
                    double dTopBore = Math.Max(Math.Sqrt((x - bx) * (x - bx) + (z - 7.5) * (z - 7.5)) - 2.15, Math.Max((rIn - 1.0) - y, y - (ySaddleTop + 1.0)));
                    double dNutSlot = Math.Max(Math.Max(Math.Abs(x - bx) - 3.60, Math.Abs(y - (ySaddleTop - 4.45)) - 1.75), z - 11.1);
                    cut = Math.Min(cut, Math.Min(dTopBore, dNutSlot));
                }

                double dPinchBore = Math.Sqrt((x - pinchX) * (x - pinchX) + (z - 7.5) * (z - 7.5)) - 2.15;
                double dPinchHex = Math.Max(SdfMesher.Hexagon2D(x - pinchX, z - 7.5, 7.18), 5.2 - y);
                cut = Math.Min(cut, Math.Min(dPinchBore, dPinchHex));

                return Math.Max(body3D, -cut);
            };

            Func<double, double, double, double> lowerHalfSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                double dOct = SdfMesher.ChamferBox2D(x, y, rOut, rOut, rOut * 0.52);
                double dRing2D = Math.Max(rIn - r, Math.Max(r - (rOut + 0.6), dOct));

                double dHingeCyl2D = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 5.2;
                double dHingeWeb2D = Math.Max(SdfMesher.ChamferBox2D(x - (pivotX * 0.5 - 4.0), y + 2.5, 6.0, 4.5, 1.8), rIn - r);
                double dFront2D = SdfMesher.SmoothMin(dHingeCyl2D, dHingeWeb2D, 1.2);

                double dRearLug2D = Math.Max(SdfMesher.ChamferBox2D(x - (rIn + 5.2), y + 5.1, 6.5, 4.4, 2.2), rIn - r);

                double lowerMain2D = Math.Max(SdfMesher.SmoothMin(dRing2D, dRearLug2D, 1.4), y - (-0.6));
                double profile2D = Math.Max(Math.Min(lowerMain2D, dFront2D), rIn - r);

                double dz = Math.Abs(z - 7.5) - 7.5;
                double dSideBevel = (profile2D + dz + 1.0) * 0.70710678;
                double body3D = Math.Max(Math.Max(profile2D, dz), dSideBevel);

                double cut = 1e9;
                // Matching Half-Lap Hinge: assembled lower knuckle occupies Z = 7.65..15.0mm (0.30mm smooth clearance!)
                // For printing (!assembledClosed), lowerMesh is rotated 180 deg around Y so Z=7.65..15.0 sits FLAT on Z=0.0..7.35!
                double dLowerKnuckleCut = Math.Max(Math.Max(x - (-rIn + 0.5), -5.5 - y), z - 7.65);
                cut = Math.Min(cut, dLowerKnuckleCut);

                double dHingePinBore = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 2.15;
                cut = Math.Min(cut, dHingePinBore);

                double dPinchBore = Math.Sqrt((x - pinchX) * (x - pinchX) + (z - 7.5) * (z - 7.5)) - 2.15;
                double dPinchHead = Math.Max(Math.Sqrt((x - pinchX) * (x - pinchX) + (z - 7.5) * (z - 7.5)) - 4.15, y - (-5.2));
                cut = Math.Min(cut, Math.Min(dPinchBore, dPinchHead));

                return Math.Max(body3D, -cut);
            };

            var upperMesh = SdfMesher.ExtractMesh("Upper_Saddle", upperHalfSdf,
                new Vec3(pivotX - 6.0, -6.0, -0.5), new Vec3(pinchX + 6.5, ySaddleTop + 1.0, 15.5), step);
            var lowerMesh = SdfMesher.ExtractMesh("Lower_Swinging_Jaw", lowerHalfSdf,
                new Vec3(pivotX - 6.0, -(rOut + 1.5), -0.5), new Vec3(pinchX + 6.5, 6.0, 15.5), step);

            if (!assembledClosed)
            {
                // Proper 180-deg rigid rotation around Y-axis: (x, y, z) -> (-x, y, 15.0 - z), det = +1
                // Puts the lower jaw's 7.35mm hinge knuckle 100% FLAT on the build plate (Z = 0.0) for ZERO supports!
                for (int i = 0; i < lowerMesh.Vertices.Count; i++)
                {
                    var v = lowerMesh.Vertices[i];
                    lowerMesh.Vertices[i] = new Vec3(-v.X, v.Y - 12.5, 15.0 - v.Z);
                }
            }

            var combined = new MeshData("Hinged_Clamshell_Unit");
            combined.Append(upperMesh);
            combined.Append(lowerMesh);
            return combined;
        }

        public static MeshData BuildHandlebarHingedClamshellPair(double boreDiameter, string meshName, double step = 0.34)
        {
            var single = BuildHandlebarHingedClamshellSingle(boreDiameter, assembledClosed: false, step: step);
            var pair = new MeshData(meshName);
            var left = single.Clone(meshName + "_L"); left.Translate(-38.0, 0, 0);
            var right = single.Clone(meshName + "_R"); right.Translate(38.0, 0, 0);
            pair.Append(left);
            pair.Append(right);
            return pair;
        }

        // 03e & 03f: M4 Heat-Set Brass Insert Variant of Hinged Clamshell Clamps (For Sunlu Easy-PA / ASA / PETG)
        // Universal Ø5.60mm pilot bores (CNC Kitchen / Ruthex / Bambu Maker's Supply M4x4, M4x5, M4x6 brass inserts):
        //   - Top Saddle (2x): Ø5.60mm x 6.4mm deep from top face (zero side-entry slots; >=1.94mm solid wall above bar!)
        //   - Rear Pinch (1x): Reverse-installed from TOP of upper lug (Ø5.60mm from Y=+3.0..+9.5) so bolt pulls insert against 2.3mm solid Easy-PA!
        //   - Front Hinge (1x): Ø5.60mm x 4.6mm deep from outer Z=15.0 knuckle face (fits M4x4mm brass insert flush!)
        public static MeshData BuildHandlebarHingedClamshellPairHeatSet(double boreDiameter, string meshName, double step = 0.70)
        {
            double rIn = boreDiameter * 0.5;
            double rOut = rIn + 5.6;
            double ySaddleTop = rIn + 6.0;
            double pivotX = -(rIn + 5.2);
            double pivotY = 0.0;
            double pinchX = +(rIn + 6.5);

            Func<double, double, double, double> upperHalfSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                double dOct = SdfMesher.ChamferBox2D(x, y, rOut, rOut, rOut * 0.52);
                double dRing2D = Math.Max(rIn - r, Math.Max(r - (rOut + 0.6), dOct));

                double dSaddle2D = SdfMesher.ChamferBox2D(x, y - (ySaddleTop * 0.5 + 3.0), 14.8, ySaddleTop * 0.5 - 3.0, 3.2);
                dSaddle2D = Math.Max(dSaddle2D, rIn - r);

                double dHingeCyl2D = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 5.2;
                double dHingeWeb2D = Math.Max(SdfMesher.ChamferBox2D(x - (pivotX * 0.5 - 4.0), y - 2.5, 6.0, 4.5, 1.8), rIn - r);
                double dFront2D = SdfMesher.SmoothMin(dHingeCyl2D, dHingeWeb2D, 1.2);

                double dRearLug2D = Math.Max(SdfMesher.ChamferBox2D(x - (rIn + 5.2), y - 5.1, 6.5, 4.4, 2.2), rIn - r);

                double upperMain2D = Math.Max(SdfMesher.SmoothMin(Math.Min(dRing2D, dSaddle2D), dRearLug2D, 1.4), 0.6 - y);
                double profile2D = Math.Max(Math.Min(upperMain2D, dFront2D), rIn - r);

                double dz = Math.Abs(z - 7.5) - 7.5;
                double dSideBevel = (profile2D + dz + 1.0) * 0.70710678;
                double body3D = Math.Max(Math.Max(profile2D, dz), dSideBevel);

                double cut = 1e9;
                // 100% Support-Free Half-Lap Hinge: upper jaw knuckle occupies Z = 0.0..7.35mm (flat on build plate!)
                double dKnuckleCut = Math.Max(Math.Max(x - (-rIn + 0.5), y - 5.5), 7.35 - z);
                cut = Math.Min(cut, dKnuckleCut);

                // Front hinge: M4 through-bore + Ø5.60mm heat-set pilot from Z=0.0..5.8mm (fits standard M4x5mm or M4x6mm brass insert!)
                // Bolt enters from Lower Jaw (Z=15) and pulls insert against 1.55mm solid Easy-PA!
                double rHinge = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY));
                double dHingePinBore = rHinge - 2.15;
                double dHingeInsert = Math.Max(rHinge - 2.80, z - 5.8);
                cut = Math.Min(cut, Math.Min(dHingePinBore, dHingeInsert));

                // Top saddle (2x): Ø5.60mm x 6.4mm deep heat-set pilots (M4x4, M4x5, or M4x6 inserts) + 0.6mm lead-in bevel
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    double bx = sx * 9.0;
                    double rTop = Math.Sqrt((x - bx) * (x - bx) + (z - 7.5) * (z - 7.5));
                    double dTopInsert = Math.Max(rTop - 2.80, Math.Max((ySaddleTop - 6.4) - y, y - (ySaddleTop + 1.0)));
                    double dTopLead = Math.Max(rTop - 3.20, (ySaddleTop - 0.6) - y);
                    cut = Math.Min(cut, Math.Min(dTopInsert, dTopLead));
                }

                // Rear pinch: Ø4.3mm through-bore + reverse-installed Ø5.60mm heat-set pilot from TOP (y = 3.0..9.5)
                // Tightening the bolt from below pulls the brass insert DOWN against 2.3mm of solid Easy-PA (zero pull-out!)
                double rPinch = Math.Sqrt((x - pinchX) * (x - pinchX) + (z - 7.5) * (z - 7.5));
                double dPinchBore = rPinch - 2.15;
                double dPinchInsert = Math.Max(rPinch - 2.80, 3.0 - y);
                cut = Math.Min(cut, Math.Min(dPinchBore, dPinchInsert));

                return Math.Max(body3D, -cut);
            };

            Func<double, double, double, double> lowerHalfSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                double dOct = SdfMesher.ChamferBox2D(x, y, rOut, rOut, rOut * 0.52);
                double dRing2D = Math.Max(rIn - r, Math.Max(r - (rOut + 0.6), dOct));

                double dHingeCyl2D = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 5.2;
                double dHingeWeb2D = Math.Max(SdfMesher.ChamferBox2D(x - (pivotX * 0.5 - 4.0), y + 2.5, 6.0, 4.5, 1.8), rIn - r);
                double dFront2D = SdfMesher.SmoothMin(dHingeCyl2D, dHingeWeb2D, 1.2);

                double dRearLug2D = Math.Max(SdfMesher.ChamferBox2D(x - (rIn + 5.2), y + 5.1, 6.5, 4.4, 2.2), rIn - r);

                double lowerMain2D = Math.Max(SdfMesher.SmoothMin(dRing2D, dRearLug2D, 1.4), y - (-0.6));
                double profile2D = Math.Max(Math.Min(lowerMain2D, dFront2D), rIn - r);

                double dz = Math.Abs(z - 7.5) - 7.5;
                double dSideBevel = (profile2D + dz + 1.0) * 0.70710678;
                double body3D = Math.Max(Math.Max(profile2D, dz), dSideBevel);

                double cut = 1e9;
                // Matching Half-Lap Hinge: assembled lower knuckle occupies Z = 7.65..15.0mm (rotated 180 deg around Y to sit flat on bed!)
                double dLowerKnuckleCut = Math.Max(Math.Max(x - (-rIn + 0.5), -5.5 - y), z - 7.65);
                cut = Math.Min(cut, dLowerKnuckleCut);

                double dHingePinBore = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 2.15;
                cut = Math.Min(cut, dHingePinBore);

                double rPinch = Math.Sqrt((x - pinchX) * (x - pinchX) + (z - 7.5) * (z - 7.5));
                double dPinchBore = rPinch - 2.15;
                double dPinchHead = Math.Max(rPinch - 4.15, y - (-5.2));
                cut = Math.Min(cut, Math.Min(dPinchBore, dPinchHead));

                return Math.Max(body3D, -cut);
            };

            var upperMesh = SdfMesher.ExtractMesh("Upper_Saddle_HeatSet", upperHalfSdf,
                new Vec3(pivotX - 6.0, -6.0, -0.5), new Vec3(pinchX + 6.5, ySaddleTop + 1.0, 15.5), step);
            var lowerMesh = SdfMesher.ExtractMesh("Lower_Swinging_Jaw_HeatSet", lowerHalfSdf,
                new Vec3(pivotX - 6.0, -(rOut + 1.5), -0.5), new Vec3(pinchX + 6.5, 6.0, 15.5), step);
            // Proper 180-deg rigid rotation around Y-axis: (x, y, z) -> (-x, y, 15.0 - z), det = +1
            // Puts the lower jaw's 7.35mm hinge knuckle 100% FLAT on the build plate (Z = 0.0) for ZERO supports!
            for (int i = 0; i < lowerMesh.Vertices.Count; i++)
            {
                var v = lowerMesh.Vertices[i];
                lowerMesh.Vertices[i] = new Vec3(-v.X, v.Y - 12.5, 15.0 - v.Z);
            }

            var single = new MeshData("Hinged_Clamshell_HeatSet_Unit");
            single.Append(upperMesh);
            single.Append(lowerMesh);

            var pair = new MeshData(meshName);
            var left = single.Clone(meshName + "_L"); left.Translate(-38.0, 0, 0);
            var right = single.Clone(meshName + "_R"); right.Translate(38.0, 0, 0);
            pair.Append(left);
            pair.Append(right);
            return pair;
        }

        public static MeshData BuildHandlebarTpuSleeves(double step = 0.30)
        {
            Func<double, double, double, double> singleSleeveSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                // Continuous CSG union of central sleeve + bottom/top retaining flanges
                double dShaft = Math.Max(r - 18.5, Math.Max(0.0 - z, z - 17.0));
                double dFlangeBot = Math.Max(r - 19.5, Math.Max(0.0 - z, z - 1.2));
                double dFlangeTop = Math.Max(r - 19.5, Math.Max(15.8 - z, z - 17.0));
                double dOuter = Math.Min(dShaft, Math.Min(dFlangeBot, dFlangeTop));
                double dRing = Math.Max(dOuter, 17.5 - r);
                double dSplit = Math.Max(Math.Abs(x) - 2.5, y);
                return Math.Max(dRing, -dSplit);
            };

            var single = SdfMesher.ExtractMesh("TPU_Sleeve_Single", singleSleeveSdf, new Vec3(-20.5, -20.5, -0.5), new Vec3(20.5, 20.5, 17.5), step);
            var pair = new MeshData("03d_Handlebar_35mm_TPU_Grip_Sleeves_Pair");
            var left = single.Clone("Sleeve_L"); left.Translate(-22.0, 0, 0);
            var right = single.Clone("Sleeve_R"); right.Translate(22.0, 0, 0);
            pair.Append(left);
            pair.Append(right);
            return pair;
        }

        // =========================================================================
        // 04. 360-DEG TPU LENS, GIMBAL, SENSOR & VIBRATION ARMOR SUITE (90A TPU)
        // =========================================================================
        // 04a: Shear/Bellows Vibration Isolator Puck + 6x Flanged M4 Bolt Grommets
        public static MeshData BuildTpuVibrationIsolatorSystem(double step = 0.28, bool includeLooseGrommetsOnPlate = true)
        {
            Func<double, double, double, double> isolatorPadSdf = (x, y, z) =>
            {
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);
                double body = SdfMesher.RoundedBox3D(x, y, z - 3.75, 24.0, 13.0, 3.75, 4.0);

                double dOuterPerim = SdfMesher.Box2D(x, y, 24.0, 13.0, 4.0);
                double dBellows = Math.Max(-dOuterPerim - 1.8, Math.Abs(z - 3.75) - 1.35);
                double dBoltBosses = 1e9;
                dBoltBosses = Math.Min(dBoltBosses, Math.Sqrt((ax - 7.5) * (ax - 7.5) + (ay - 7.5) * (ay - 7.5)) - 4.5);
                dBoltBosses = Math.Min(dBoltBosses, Math.Sqrt((ax - 15.0) * (ax - 15.0) + y * y) - 4.5);
                dBellows = Math.Max(dBellows, -dBoltBosses);

                double cut = dBellows;
                // Open through-venting damping cells (eliminates enclosed negative-volume internal voids & improves elastomer compliance!)
                double dCellCenter = Math.Sqrt(x * x + y * y) - 3.6;
                double dCellMidX = Math.Sqrt((ax - 19.5) * (ax - 19.5) + (ay - 6.8) * (ay - 6.8)) - 2.2;
                cut = Math.Min(cut, Math.Min(dCellCenter, dCellMidX));

                double dHoles = Math.Min(
                    Math.Sqrt((ax - 7.5) * (ax - 7.5) + (ay - 7.5) * (ay - 7.5)) - 2.15,
                    Math.Sqrt((ax - 15.0) * (ax - 15.0) + y * y) - 2.15);
                cut = Math.Min(cut, dHoles);

                double dTopRibs = Math.Max(Math.Abs(Math.Sin(x * 1.3)) - 0.5, Math.Min(z - 0.45, 7.05 - z));
                dTopRibs = Math.Max(dTopRibs, -dBoltBosses);
                cut = Math.Min(cut, dTopRibs);

                return Math.Max(body, -cut);
            };

            Func<double, double, double, double> grommetSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                // Continuous CSG union of flange + shank
                double dFlange = Math.Max(r - 4.5, Math.Max(0.0 - z, z - 1.6));
                double dShank = Math.Max(r - 3.35, Math.Max(0.0 - z, z - 4.2));
                return Math.Max(Math.Min(dFlange, dShank), 2.08 - r);
            };

            var mainPad = SdfMesher.ExtractMesh("TPU_Isolator_Pad", isolatorPadSdf, new Vec3(-25.5, -14.5, -0.5), new Vec3(25.5, 14.5, 8.0), step);
            var systemMesh = new MeshData("04a_TPU_Vibration_Isolator_Puck_And_Grommets");
            systemMesh.Append(mainPad);

            if (includeLooseGrommetsOnPlate)
            {
                var singleGrommet = SdfMesher.ExtractMesh("TPU_Bolt_Grommet", grommetSdf, new Vec3(-5.2, -5.2, -0.4), new Vec3(5.2, 5.2, 4.6), 0.42);
                for (int i = 0; i < 6; i++)
                {
                    var g = singleGrommet.Clone("Grommet_" + i);
                    g.Translate(-20.0 + i * 8.0, -21.0, 0.0);
                    systemMesh.Append(g);
                }
            }
            return systemMesh;
        }

        // 04b: Snap-In 90A TPU Belly Downward Vision & IR Sensor Mud Shield
        // Seals the 20.0 x 22.6mm rear belly cutout (X in [+40.5, +60.5], Y in [-11.3, +11.3]) with a non-contact recessed optical dome!
        public static MeshData BuildTpuBellySensorShield(double step = 0.26)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double flange = SdfMesher.StealthBlock3D(x, y, z - 0.8, 12.2, 13.0, 0.8, 3.0, 0.0);
                double plug = SdfMesher.RoundedBox3D(x, y, z - 3.1, 9.95, 11.25, 1.6, 1.5);
                // Continuous CSG retention bead
                double beadRing = SdfMesher.RoundedBox3D(x, y, z - 3.8, 10.50, 11.80, 0.6, 1.8);
                double body = Math.Min(flange, Math.Min(plug, beadRing));

                // Recessed non-contact optical dome (Z in [2.6, 5.5]) so nothing ever touches the downward fisheye lens or IR ToF window!
                double dOpticalDome = Math.Max(SdfMesher.Box2D(x, y, 7.6, 8.4, 3.0), 2.5 - z);
                return Math.Max(body, -dOpticalDome);
            };
            return SdfMesher.ExtractMesh("04b_TPU_Belly_Sensor_Shield", sdf, new Vec3(-13.5, -14.2, -0.5), new Vec3(13.5, 14.2, 5.5), step);
        }

        // 04c: Snap-In 90A TPU Heatsink Mud-Guard Louver (X in [-41.0, -21.5])
        public static MeshData BuildTpuHeatsinkLouver(double step = 0.26)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double flange = SdfMesher.StealthBlock3D(x, y, z - 0.8, 11.8, 13.0, 0.8, 3.0, 0.0);
                double plug = SdfMesher.RoundedBox3D(x, y, z - 3.1, 9.70, 11.25, 1.6, 1.5);
                // Continuous CSG retention bead
                double beadRing = SdfMesher.RoundedBox3D(x, y, z - 3.8, 10.25, 11.80, 0.6, 1.8);
                double body = Math.Min(flange, Math.Min(plug, beadRing));

                double cut = 1e9;
                for (int i = -2; i <= 2; i++)
                {
                    double shiftedX = x - i * 3.4 - (z - 2.3) * 0.65;
                    cut = Math.Min(cut, Math.Max(Math.Abs(shiftedX) - 0.85, Math.Abs(y) - 8.2));
                }
                return Math.Max(body, -cut);
            };
            return SdfMesher.ExtractMesh("04c_TPU_Heatsink_Mud_Louver", sdf, new Vec3(-13.0, -14.2, -0.5), new Vec3(13.0, 14.2, 5.5), step);
        }

        // 04d (NEW!): 90A TPU Nose-Vault Gimbal Cushion, Forward LiDAR Dome & Side-Skirt Mud Seal
        // Press-fits inside the Closed Nose Cup (X_cradle in [-69.5, -47.5], Y in [-17.5, +17.5], Z_cradle in [6.8, 36.5])!
        //   - Gently cradles the outer barrel of the 2-Axis Mechanical Gimbal so it cannot hammer its end-stops
        //   - Deep non-contact optical cavities over BOTH the Main Camera Lens and Forward LiDAR / Vision Window
        //   - Wrap-around TPU side-skirts seal the open left/right flanks of the cradle's nose cup against mud & grit!
        // Printed flat on its front nose face (Z_print = 0..21mm) for zero-support printing!
        public static MeshData BuildTpuNoseVaultGimbalGuard(double step = 0.26)
        {
            // In print orientation:
            //   - Z in [0.0, 19.5]: depth into the nose cup (Z=0 is front wall inside nose cup, Z=19.5 extends back into cradle)
            //   - Y in [-17.4, +17.4]: width across inside of nose cup (34.8mm wide, snug friction fit inside 35.4mm cup walls!)
            //   - X in [-14.0, +14.0]: height inside nose cup (28.0mm tall, fitting between Z_cradle = 7.2mm floor and Z_cradle = 35.2mm roof!)
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ay = Math.Abs(y);

                // Outer U-shell matching interior of Closed Nose Cup: 28.0mm (X) x 34.8mm (Y) x 19.0mm (Z)
                double outer2D = SdfMesher.ChamferBox2D(x, y, 14.0, 17.4, 5.0);
                double body3D = Math.Max(outer2D, Math.Max(0.0 - z, z - 19.0));

                // Carve interior drone-nose cavity (Z > 3.2mm):
                // Soft gimbal-hugging side cheeks at |Y| in [9.5..14.0] hold the outer gimbal frame while leaving a deep Ø16.4mm non-contact optical dome in the center!
                double dDroneNose = Math.Max(SdfMesher.ChamferBox2D(x, y, 11.2, 14.4, 3.5), 5.5 - z);

                // Deep non-contact optical dome for Main 2-Axis Gimbal Lens + Forward LiDAR (Z in [2.0, 20.0], X in [-9.5, +9.5], |Y| <= 8.4mm)
                double dLensAndLidarDome = Math.Max(SdfMesher.Box2D(x, y, 9.5, 8.4, 4.5), 2.0 - z);

                // Compliant flex ribs on gimbal side-cheeks (at |Y| in [8.4..14.4], Z in [3.5..12.0]) so cheeks compress like plush foam pillows!
                double dFlexGroove = Math.Max(Math.Abs(x) - 1.5, Math.Max(9.5 - ay, Math.Abs(z - 8.5) - 2.2));

                return Math.Max(body3D, -Math.Min(dDroneNose, Math.Min(dLensAndLidarDome, dFlexGroove)));
            };

            return SdfMesher.ExtractMesh("04d_TPU_Nose_Vault_Gimbal_And_LiDAR_Guard", sdf,
                new Vec3(-15.0, -18.5, -0.5), new Vec3(15.0, 18.5, 20.0), step);
        }

        // 04e (NEW!): Orbea-Styled Faceted 90A TPU Top-Rear Fisheye Lens Visor & Fail-Safe Retention Strap
        // Anchors onto the cradle's built-in Ø10.5mm cylindrical side bosses (at X_cradle = +60.6, Z_cradle = +19.2, Y = +-21.95)
        // Stretches over the top-rear deck of the DJI Neo 2 right ahead of the Digital Transceiver, placing a Ø16.0mm non-contact
        // stealth dome shield directly over the exposed Top-Rear Omnidirectional Fisheye Lens!
        // Printed FLAT on the bed (Z in [0, 5.2mm], Y in [-52, +58mm]) for 100% continuous perimeter tensile strength & zero supports!
        public static MeshData BuildTpuTopFisheyeLensVisorStrap(double step = 0.25)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                // Strap spans along Y from Left Pivot Eyelet (Y = -41.0) through Center Faceted Lens Shield (Y = 0.0) to Right Quick-Clip Eyelet + Pull Tab (Y = +41.0..+55.0)
                // 1) Flexible Strap Band: Y in [-41.0, +41.0], X in [-6.5, +6.5], Z in [0.0, 2.2mm]
                double dBand = Math.Max(SdfMesher.Box2D(x, y, 6.5, 41.0, 2.0), Math.Max(0.0 - z, z - 2.2));

                // 2) Center Faceted "Orbea Stealth" Fisheye Lens Armor Visor: Y in [-15.0, +15.0], X in [-13.5, +13.5], Z in [0.0, 5.4mm]
                // Offset forward in -X (X_center = -3.5mm) so when strapped across the X=+60.6 bosses, the dome sits directly over the top fisheye lens at X=+49..56!
                double dVisor = SdfMesher.StealthBlock3D(x + 3.0, y, z - 2.7, 13.0, 14.5, 2.7, 4.5, 1.4);

                // 3) Left Permanent Eyelet Ring at (X = 0, Y = -41.0): outer r = 8.5mm, inner hole r = 5.30mm (snaps over Ø10.51mm cradle side boss!)
                double rL = Math.Sqrt(x * x + (y + 41.0) * (y + 41.0));
                double dEyeL = Math.Max(rL - 8.5, Math.Max(0.0 - z, z - 2.8));

                // 4) Right Quick-Release C-Hook Eyelet + Angular Glove Pull-Tab at (X = 0, Y = +41.0..+56.0)
                double rR = Math.Sqrt(x * x + (y - 41.0) * (y - 41.0));
                double dEyeR = Math.Max(rR - 8.5, Math.Max(0.0 - z, z - 2.8));
                // Faceted Orbea-style glove pull tab extending to Y = +56.5mm with grip ridges!
                double dTab = SdfMesher.StealthBlock3D(x, y - 50.5, z - 1.8, 6.8, 6.5, 1.8, 2.5, 0.6);

                double body = Math.Min(Math.Min(dBand, dVisor), Math.Min(dEyeL, Math.Min(dEyeR, dTab)));

                // Subtract features:
                double cut = 1e9;
                // Left closed eyelet bore (r = 5.30mm)
                cut = Math.Min(cut, rL - 5.30);
                // Right quick-clip eyelet bore (r = 5.25mm) + angled side entry throat (width 7.6mm so it clicks over the Ø10.5mm boss with one finger!)
                double dQuickThroat = Math.Max(Math.Abs(y - 41.0) - 3.8, x - 0.0); // opens on +X side so tension pulls against solid -X arc!
                // Wait: tension pulls inward toward Y = 0 (-Y), so opening at +X (sideways) cannot unhook under strap tension!
                cut = Math.Min(cut, Math.Min(rR - 5.25, Math.Max(dQuickThroat, rR - 9.0)));

                // Non-Contact Recessed Optical Dome underneath the Center Visor (Z in [-0.5, 3.2], r = 8.2mm -> Ø16.4mm dome so nothing touches the fisheye glass!)
                double dFisheyeDome = Math.Max(Math.Sqrt((x + 3.0) * (x + 3.0) + y * y) - 8.2, z - 3.2);
                cut = Math.Min(cut, dFisheyeDome);

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh("04e_TPU_Top_Fisheye_Lens_Visor_Strap", sdf,
                new Vec3(-17.5, -51.0, -0.5), new Vec3(12.0, 58.0, 6.0), step);
        }

        // =========================================================================
        // 05. RIGID PETG FASTENER REINFORCEMENT INSERT KIT FOR 72D TPU PRINTS
        // =========================================================================
        public static MeshData BuildPetgReinforcementInsertKit(double step = 0.26)
        {
            Func<double, double, double, double> carrierPlateSdf = (x, y, z) =>
            {
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);
                // Widened hy to 12.30mm + Y-flat rotated corner hex pockets (7.50 + 3.59 = 11.09mm) -> solid 1.21mm perimeter walls!
                double body = SdfMesher.RoundedBox3D(x, y, z - 1.75, 21.30, 12.30, 1.75, 2.8);
                double holes = Math.Min(
                    M4HoleAndHexNutYFlatSdf(ax, ay, z, 7.50, 7.50, 1.10, 3.80),
                    M4HoleAndHexNutSdf(ax, y, z, 15.00, 0.00, 1.10, 3.80));
                return Math.Max(body, -holes);
            };

            Func<double, double, double, double> hexBushingSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                // Continuous CSG union of hex stem + round retention collar (zero SDF discontinuity!)
                double dHexStem = Math.Max(SdfMesher.Hexagon2D(x, y, 7.08), Math.Max(0.0 - z, z - 3.30));
                double dFlange = Math.Max(r - 4.75, Math.Max(3.20 - z, z - 4.60));
                return Math.Max(Math.Min(dHexStem, dFlange), 2.125 - r);
            };

            var carrierMesh = SdfMesher.ExtractMesh("05a_PETG_Cradle_6Nut_Carrier_Plate", carrierPlateSdf,
                new Vec3(-22.5, -13.5, -0.4), new Vec3(22.5, 13.5, 4.0), step);
            var hexBushMesh = SdfMesher.ExtractMesh("05b_PETG_Hex_Collar_M4_Bushing", hexBushingSdf,
                new Vec3(-5.5, -5.5, -0.4), new Vec3(5.5, 5.5, 5.0), 0.36);

            var kitMesh = new MeshData("05_PETG_Reinforcement_Inserts_For_72D_TPU");
            kitMesh.Append(carrierMesh);
            for (int i = 0; i < 8; i++)
            {
                var b = hexBushMesh.Clone("Hex_Bush_" + i);
                b.Translate(-38.5 + i * 11.0, -20.5, 0.0);
                kitMesh.Append(b);
            }
            return kitMesh;
        }
    }
}
