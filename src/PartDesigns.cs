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

        // Same M4 nut pocket with crisp 6.90mm flats & Ø4.04mm bore for zero-spin Nyloc/hex nuts in 72D TPU (measured 6.83mm nut!)
        private static double M4HoleAndCrispHexNutSdf(double x, double y, double z, double hx, double hy, double zNutMin, double zNutMax)
        {
            double dx = x - hx;
            double dy = y - hy;
            double dHole = Math.Sqrt(dx * dx + dy * dy) - 2.02;
            double dHex2D = SdfMesher.Hexagon2D(dx, dy, 6.90);
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

        // True Watertight CSG Boolean Union of Reference Cradle Shell + Flat Mounting Insert (01a, 01c, 01e TPU Flex-Clip, or 01f Easy-PA Reinforced)
        public static MeshData BuildCradleBooleanUnion(
            string name,
            MeshData refCradleShell,
            Func<double, double, double, double> insertSdf,
            double step = 0.30,
            bool applyRearFlexClipTrim = false,
            bool applyFlatEasyPaV2 = false,
            bool applyFlatEasyPaReinforced = false)
        {
            Vec3 minB, maxB;
            refCradleShell.GetBounds(out minB, out maxB);
            if (applyFlatEasyPaReinforced)
            {
                if (-29.0 < minB.Y) minB.Y = -29.0;
                if (29.0 > maxB.Y) maxB.Y = 29.0;
            }
            else if (applyFlatEasyPaV2)
            {
                if (-27.2 < minB.Y) minB.Y = -27.2;
                if (27.2 > maxB.Y) maxB.Y = 27.2;
            }
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
                for (int iy = 0; iy <= ny; iy++)
                {
                    double y = oy + iy * step;
                    double ay = Math.Abs(y);
                    for (int iz = 0; iz <= nz; iz++)
                    {
                        double z = oz + iz * step;
                        if (applyFlatEasyPaReinforced)
                        {
                            // 0) Plug the leftover circular side-lanyard hollow at the base of the rear arms (X=53, Z=6, r=5.5mm) before outer trim!
                            if (x >= 46.5 && x <= 59.5 && ay >= 16.2 && z >= 0.0 && z <= 12.5)
                            {
                                float dPlug = (float)RearArmBaseHolePlugSdf(x, y, z);
                                if (dPlug < grid[ix, iy, iz]) grid[ix, iy, iz] = dPlug;
                            }
                            // 1) Tapered reinforced lump-free rear flank shave for Easy-PA / 72D TPU:
                            //    4.5mm thick root (|Y|=21.1 at Z<=12) tapering smoothly to 3.2mm upper hook (|Y|=19.8 at Z=38) -> +125% stronger at bend zone!
                            if (x >= 43.0 && ay >= 18.0)
                            {
                                float dReinfTrim = -(float)RearFlexClipReinforcedTrimSdf(x, y, z);
                                if (dReinfTrim > grid[ix, iy, iz]) grid[ix, iy, iz] = dReinfTrim;
                            }
                            // 2) Self-supporting 29.4-deg Stealth Front Lower Nose Chin (zero intrusion into inner Neo 2 nose pocket!)
                            if (x <= -46.0 && z >= -0.5 && z <= 17.5)
                            {
                                float dChin = (float)NoseChin30DegFillSdf(x, y, z);
                                if (dChin < grid[ix, iy, iz]) grid[ix, iy, iz] = dChin;
                            }
                            // 3) Flush Z=0.0..6.0mm MID-BASE 14.0 x 3.5mm strap loops centered at X=+6.5mm (right in the open waist between front & rear prop guards!)
                            if (x >= -6.0 && x <= 19.0 && ay >= 12.5 && z >= -0.5 && z <= 7.5)
                            {
                                float dLoopBody = (float)MidBaseStrapLoopBodySdf(x, y, z);
                                if (dLoopBody < grid[ix, iy, iz]) grid[ix, iy, iz] = dLoopBody;
                                float dSlotCut = -(float)MidBaseStrapSlotCutSdf(x, y, z);
                                if (dSlotCut > grid[ix, iy, iz]) grid[ix, iy, iz] = dSlotCut;
                            }
                        }
                        else if (applyFlatEasyPaV2)
                        {
                            // 0) Plug the leftover circular side-lanyard hollow at the base of the rear arms before outer trim!
                            if (x >= 46.5 && x <= 59.5 && ay >= 16.2 && z >= 0.0 && z <= 12.5)
                            {
                                float dPlug = (float)RearArmBaseHolePlugSdf(x, y, z);
                                if (dPlug < grid[ix, iy, iz]) grid[ix, iy, iz] = dPlug;
                            }
                            // 1) Smooth, lump-free rear flank shave (removes ALL Ø10.5mm bosses + lower shoulders down to Z=0 -> uniform 2.8mm flex arms!)
                            if (x >= 43.0 && ay >= 17.5)
                            {
                                float dCleanTrim = -(float)RearFlexClipCleanTrimSdf(x, y, z);
                                if (dCleanTrim > grid[ix, iy, iz]) grid[ix, iy, iz] = dCleanTrim;
                            }
                            // 2) Self-supporting 29-deg Stealth Front Lower Nose Chin (eliminates rough 45-55 deg Easy-PA overhang without touching inner pocket!)
                            if (x <= -46.0 && z >= -0.5 && z <= 17.5)
                            {
                                float dChin = (float)NoseChin30DegFillSdf(x, y, z);
                                if (dChin < grid[ix, iy, iz]) grid[ix, iy, iz] = dChin;
                            }
                            // 3) Flush Z=0.0..6.0mm MID-BASE 14.0 x 3.5mm strap loops centered at X=+6.5mm (between front & rear prop guards!)
                            if (x >= -6.0 && x <= 19.0 && ay >= 12.5 && z >= -0.5 && z <= 7.5)
                            {
                                float dLoopBody = (float)MidBaseStrapLoopBodySdf(x, y, z);
                                if (dLoopBody < grid[ix, iy, iz]) grid[ix, iy, iz] = dLoopBody;
                                float dSlotCut = -(float)MidBaseStrapSlotCutSdf(x, y, z);
                                if (dSlotCut > grid[ix, iy, iz]) grid[ix, iy, iz] = dSlotCut;
                            }
                        }
                        else if (applyRearFlexClipTrim && x >= 48.0 && ay >= 17.5 && z >= 2.0)
                        {
                            float dTrim = -(float)RearFlexClipTrimSdf(x, y, z);
                            if (dTrim > grid[ix, iy, iz]) grid[ix, iy, iz] = dTrim;
                        }
                        if (x >= -17.5 && x <= 36.5 && y >= -15.5 && y <= 15.5 && z >= -1.0 && z <= 6.8)
                        {
                            float dIns = (float)insertSdf(x, y, z);
                            if (dIns < grid[ix, iy, iz]) grid[ix, iy, iz] = dIns;
                        }
                    }
                }
            }

            var result = SdfMesher.ExtractFromGrid(name, grid, ox, oy, oz, step, null);
            result.SnapBottomToZero();
            return result;
        }

        // Flex-Clip outer shave for rigid filaments (Sunlu Easy-PA / PETG) preserving Ø10.5mm bosses (01d):
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

        // Clean, lump-free Flex-Clip outer shave for 01e (Flat Easy-PA V2):
        // Shaves the entire outer flank of the rear clip section (X > 45.5, all Z >= 0) smoothly to |Y| = 19.4 mm
        // (~2.8 mm uniform flex arm, 0 leftover strap bosses, 0 bulbous lower shoulders) while preserving inner |Y| = 16.6 mm drone grip!
        public static double RearFlexClipCleanTrimSdf(double x, double y, double z)
        {
            double ay = Math.Abs(y);
            return SdfMesher.SmoothMax(45.5 - x, 19.4 - ay, 1.4);
        }

        // Self-supporting 29-deg Stealth Front Lower Nose Chin for 01e (Flat Easy-PA V2):
        // Extends the Z=0 front chin footprint forward to X=-64.0 and sweeps smoothly at 29.4 deg from vertical up to X=-73.0 at Z=16.0.
        // Strictly bounded outside the inner Neo 2 nose pocket via xSafeRear so interior gimbal/nose clearance is 100% untouched!
        public static double NoseChin30DegFillSdf(double x, double y, double z)
        {
            double ay = Math.Abs(y);
            double zClamped = Math.Max(0.0, Math.Min(16.0, z));
            double xFront = -64.0 - 0.5625 * zClamped; // -64.0 at Z=0 -> -73.0 at Z=16.0 (29.4 deg from vertical!)
            double xBack = -48.0;
            double dOuter = SdfMesher.Box2D(x - 0.5 * (xFront + xBack), y, 0.5 * (xBack - xFront), 21.60, 5.5);
            double xSafeRear = -57.5 - 0.90 * zClamped + Math.Max(0.0, ay - 16.0) * 0.95;
            double dRearClip = x - xSafeRear;
            double dZ = Math.Max(0.0 - z, z - 16.2);
            return Math.Max(Math.Max(dOuter, dRearClip), dZ);
        }

        // Flush Z=0.0..6.0mm side Velcro/FPV LiPo strap loop tabs at X in [48.0, 70.0] (centered at X=59.0, right beside the rear flex arms!)
        public static double RearVelcroStrapLoopBodySdf(double x, double y, double z)
        {
            double ay = Math.Abs(y);
            // Solid flat-on-bed ear from |Y|=14.0 to |Y|=26.2, X in [48.0, 70.0], Z in [0.0, 6.0] with 3.0mm XY chamfers & 0.8mm top bevel
            return SdfMesher.StealthBlock3D(x - 59.0, ay - 20.1, z - 3.0, 11.0, 6.1, 3.0, 3.0, 0.8);
        }

        // Vertical 14.0 x 3.5 mm pass-through strap slot at X in [52.0, 66.0], |Y| in [19.7, 23.2] (clears |Y|=19.4 rear arm; 3.0mm outer tie bar!)
        public static double RearVelcroStrapSlotCutSdf(double x, double y, double z)
        {
            double ay = Math.Abs(y);
            double dSlot2D = SdfMesher.Box2D(x - 59.0, ay - 21.45, 7.0, 1.75, 0.9);
            double dSlotZ = Math.Max(-1.0 - z, z - 7.5);
            return Math.Max(dSlot2D, dSlotZ);
        }

        // Tapered reinforced lump-free rear flank shave for 01f (Flat Easy-PA Reinforced):
        // Keeps inner |Y|=16.6mm drone grip intact while tapering outer wall smoothly from |Y|=21.1mm (4.5mm thick root at Z<=12)
        // down to |Y|=19.8mm (3.2mm upper hook at Z=38) -> +125% bending strength right at the Z=18mm bend zone, with 0 boss lumps!
        public static double RearFlexClipReinforcedTrimSdf(double x, double y, double z)
        {
            double ay = Math.Abs(y);
            double u = Math.Max(0.0, Math.Min(1.0, (z - 12.0) / 26.0));
            double yOuter = 21.1 - 1.3 * u;
            return SdfMesher.SmoothMax(45.5 - x, yOuter - ay, 1.4);
        }

        // Flush Z=0.0..6.0mm side Velcro/FPV LiPo strap loops for 01f (shifted +1.7mm outward to clear the 4.5mm arm root at |Y|=21.1)
        public static double RearVelcroStrapLoopBodyReinforcedSdf(double x, double y, double z)
        {
            double ay = Math.Abs(y);
            // Solid flat-on-bed ear from |Y|=14.0 to |Y|=27.9, X in [48.0, 70.0], Z in [0.0, 6.0] with 3.0mm XY chamfers & 0.8mm top bevel
            return SdfMesher.StealthBlock3D(x - 59.0, ay - 20.95, z - 3.0, 11.0, 6.95, 3.0, 3.0, 0.8);
        }

        // Vertical 14.0 x 3.5 mm pass-through strap slot for 01f at X in [52.0, 66.0], |Y| in [21.4, 24.9] (clears |Y|=21.1 arm root; 3.0mm outer tie bar!)
        public static double RearVelcroStrapSlotCutReinforcedSdf(double x, double y, double z)
        {
            double ay = Math.Abs(y);
            double dSlot2D = SdfMesher.Box2D(x - 59.0, ay - 23.15, 7.0, 1.75, 0.9);
            double dSlotZ = Math.Max(-1.0 - z, z - 7.5);
            return Math.Max(dSlot2D, dSlotZ);
        }

        // Flush Z=0.0..6.0mm MID-BASE strap loop tabs centered at X = +6.5 mm (X in [-4.5, +17.5], |Y| in [13.5, 24.8])
        // Aligned directly with the open center waist between the DJI Neo 2's front and rear propeller guards!
        // Inner edge |Y|=13.5mm merges solidly into the cradle rail while clearing the ±11.65mm M4 hex nuts & ±13.0mm 04a TPU isolator puck!
        public static double MidBaseStrapLoopBodySdf(double x, double y, double z)
        {
            double ay = Math.Abs(y);
            return SdfMesher.StealthBlock3D(x - 6.5, ay - 19.15, z - 3.0, 11.0, 5.65, 3.0, 3.0, 0.8);
        }

        // Vertical 14.0 x 3.5 mm pass-through mid-base strap slot at X in [-0.5, +13.5], |Y| in [18.3, 21.8] (clears |Y|=17.7 rail; 3.0x6.0mm outer tie bar!)
        public static double MidBaseStrapSlotCutSdf(double x, double y, double z)
        {
            double ay = Math.Abs(y);
            double dSlot2D = SdfMesher.Box2D(x - 6.5, ay - 20.05, 7.0, 1.75, 0.9);
            double dSlotZ = Math.Max(-1.0 - z, z - 7.5);
            return Math.Max(dSlot2D, dSlotZ);
        }

        // Solid cylinder plug filling the leftover circular side-lanyard hollow at the base of the rear arms (X=53.0, Z=6.0, r=5.5mm, |Y| in [16.8..21.3])
        // Stays strictly outside the |Y|=16.6mm inner drone pocket and gets shaved 100% flush by the outer rear flank trim!
        public static double RearArmBaseHolePlugSdf(double x, double y, double z)
        {
            double ay = Math.Abs(y);
            double rHole = Math.Sqrt((x - 53.0) * (x - 53.0) + (z - 6.0) * (z - 6.0));
            double dY = Math.Max(16.8 - ay, ay - 21.3);
            return Math.Max(rHole - 5.5, dY);
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
                // 1) Top Saddle Mounting Pad: X in [-28.5, +28.5], Y in [0.0, 9.5] (matches carbon-safe outboard ±22.5mm clamp flanges!)
                double dTopPad2D = SdfMesher.ChamferBox2D(x, y - 4.75, 28.5, 4.75, 2.2);

                // 2) Front Raked Plate Flange: centered at (px0, py0), length +-18.5mm along (ux, uy), thickness 9.0mm behind plate backplane
                double dxp = x - px0, dyp = y - py0;
                double sAlong = dxp * ux + dyp * uy;      // coordinate along plate [-18.5, +18.5]
                double sNormal = dxp * nx + dyp * ny;     // coordinate along plate normal [-9.0, 0.0] (0.0 mates flush against Z_plate = 0!)
                double dFlange2D = SdfMesher.ChamferBox2D(sAlong, sNormal + 4.5, 18.5, 4.5, 2.0);

                // 3) Swept Dual-Chord Truss Strut connecting Top Pad to Front Raked Flange
                // Upper tension chord: from (20.0, 5.2) to (u1x, u1y), radius 4.2mm (8.4mm thick chord!)
                double u1x = px0 + 11.5 * ux - 4.2 * nx, u1y = py0 + 11.5 * uy - 4.2 * ny;
                double dChordTop = Capsule2D(x, y, 20.0, 5.2, u1x, u1y, 4.2);

                // Lower compression arch chord: from (16.0, 4.2) -> mid-arch (32.0, -6.5) -> lower flange (l1x, l1y), radius 4.2mm (clears outboard front clamp flange!)
                double l1x = px0 - 11.5 * ux - 4.2 * nx, l1y = py0 - 11.5 * uy - 4.2 * ny;
                double dChordBotA = Capsule2D(x, y, 16.0, 4.2, 32.0, -6.5, 4.2);
                double dChordBotB = Capsule2D(x, y, 32.0, -6.5, l1x, l1y, 4.2);

                // Web fill between chords
                double dWeb = Triangle2D(x, y, 18.0, 4.8, u1x, u1y, l1x, l1y);
                double profile2D = SdfMesher.SmoothMin(Math.Min(dTopPad2D, dFlange2D), Math.Min(dChordTop, Math.Min(dChordBotA, dChordBotB)), 2.2);
                profile2D = Math.Min(profile2D, dWeb);

                // Continuous CSG trim flush against top of C-clip saddle (Y >= 0 for X < 27.5) and flush against back of Number Plate (sNormal <= 0.0)!
                double dSaddleClear = Math.Max(x - 27.5, y - 0.0);
                profile2D = Math.Max(profile2D, -dSaddleClear);
                profile2D = Math.Max(profile2D, sNormal);

                // Extrude Z in [0.0, 15.0mm] (matches exact 15.0mm width of 03b handlebar clamps!) with 1.0mm Orbea stealth bevels
                double dz = Math.Abs(z - 7.5) - 7.5;
                double dBevel = (profile2D + dz + 1.0) * 0.70710678;
                double body3D = Math.Max(Math.Max(profile2D, dz), dBevel);

                // Subtract features:
                double cut = 1e9;

                // (A) Centered Stealth Truss Slot along the arm's neutral axis from (28.5, -2.5) to (39.0, -9.5), radius 2.6mm
                double dTrussWindow = Capsule2D(x, y, 28.5, -2.5, 39.0, -9.5, 2.6);
                dTrussWindow = Math.Max(dTrussWindow, profile2D + 4.5);
                cut = Math.Min(cut, dTrussWindow);

                // (B) Carbon-Safe Outboard Top Saddle M4 Bolt Slots (at X = -22.5 and X = +22.5, slotted +-1.2mm in Z)
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    double bx = sx * 22.5;
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
                new Vec3(-30.0, -32.0, -0.6), new Vec3(62.0, 11.0, 15.6), step);

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
        public static MeshData BuildOrbeaStealthOverStemBridge(double step = 0.38, bool includeFrontProw = true, string meshName = "03a_Orbea_Stealth_OverStem_Bridge")
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);

                // Octagonal stealth wing footprint: X in [-28.5, +28.5] across full wing span Y in [-41.5, +41.5] to support carbon-safe outboard ±22.5mm clamp bolts!
                double dMain2D = SdfMesher.ChamferBox2D(x, y, 28.5, 41.5, 6.0);

                // Top profile with sharp angular step from center isolator bezel (Z = 14.0) down to faceted clamp wings (Z = 8.5)
                double zTop = ay <= 20.5 ? 14.0 : (ay >= 24.5 ? 8.5 : 14.0 - (ay - 20.5) * (5.5 / 4.0));
                // Bottom profile: 46mm wide central arch (Z = 6.0) clearing the OC MC11 stem faceplate
                double zBot = ay >= 23.0 ? 0.0 : (ay <= 19.5 ? 6.0 : 6.0 * (23.0 - ay) / 3.5);

                double dMainZ = Math.Max(zBot - z, z - zTop);
                double dMainBevel = (dMain2D + (z - zTop) + 1.6) * 0.70710678;
                double mainBody = Math.Max(Math.Max(dMain2D, dMainZ), dMainBevel);

                double body = mainBody;
                if (includeFrontProw)
                {
                    // Integrated Front Stealth Roost Deflector Prow (X in [+22.0, +42.0], Y in [-21.0, +21.0], sweeping up to Z = 16.5mm!)
                    double prowU = Math.Max(0.0, Math.Min(1.0, (x - 22.0) / 20.0));
                    double prowHalfY = 21.0 - 7.5 * prowU;
                    double dProw2D = Math.Max(Math.Max(22.0 - x, x - 42.0), ay - prowHalfY);
                    double zProwBot = 6.0 + 6.5 * prowU;
                    double zProwTop = 14.0 + 2.5 * prowU - 0.18 * ay; // V-creased stealth top ridge!
                    double prow3D = Math.Max(dProw2D, Math.Max(zProwBot - z, z - zProwTop));
                    body = Math.Min(mainBody, prow3D);
                }

                double cut = 1e9;

                // (A) Recessed 3.5mm Deep Stadium Pocket on Top Deck (Z in [10.5, 15.0]) for the 04a/04g 90A TPU Vibration Isolator Puck!
                double dPuckPocket = Math.Max(SdfMesher.Box2D(x, y, 24.3, 13.3, 4.1), 10.5 - z);
                cut = Math.Min(cut, dPuckPocket);

                // (B) Carbon-Safe Outboard Slotted Lateral M4 Mounting Channels on Left & Right Wings (X = +-22.5, Y in [25.0..36.0] -> 50 to 72mm span!)
                double dWingSlot = SdfMesher.SlotY2D(ax, ay, 22.5, 25.0, 36.0, 2.15);
                double dWingCbore = Math.Max(SdfMesher.SlotY2D(ax, ay, 22.5, 25.0, 36.0, 4.15), 4.5 - z);
                cut = Math.Min(cut, Math.Min(dWingSlot, dWingCbore));

                // (C) Center Cradle + TPU Isolator M4 Mounting Holes + Crisp Zero-Spin 6.90mm Underside Captive M4 Hex/Nyloc Nut Pockets
                cut = Math.Min(cut, M4HoleAndCrispHexNutSdf(ax, ay, z, 7.50, 7.50, 5.5, 8.6));
                cut = Math.Min(cut, M4HoleAndCrispHexNutSdf(ax, y, z, 15.00, 0.00, 5.5, 8.6));

                return Math.Max(body, -cut);
            };

            double maxX = includeFrontProw ? 43.5 : 30.0;
            double maxZ = includeFrontProw ? 17.5 : 15.0;
            return SdfMesher.ExtractMesh(meshName, sdf, new Vec3(-30.0, -43.0, -0.6), new Vec3(maxX, 43.0, maxZ), step);
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

                // Carbon-Safe Outboard Top Saddle Flange: X in [-28.5, +28.5], Y in [ySaddleTop - 7.5, ySaddleTop]
                // Bolt centers at X = +-22.5mm sit completely outboard of the Ø35mm carbon bar (r=17.5mm) so over-long screws exit into empty air!
                double dSaddle2D = SdfMesher.ChamferBox2D(x, y - (ySaddleTop - 3.75), 28.5, 3.75, 2.2);
                dSaddle2D = Math.Max(dSaddle2D, rIn - r);

                double dHingeCyl2D = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 5.2;
                double dHingeWeb2D = Math.Max(SdfMesher.ChamferBox2D(x - (pivotX * 0.5 - 4.0), y - 2.5, 6.0, 4.5, 1.8), rIn - r);
                double dFront2D = SdfMesher.SmoothMin(dHingeCyl2D, dHingeWeb2D, 1.2);

                double dRearLug2D = Math.Max(SdfMesher.ChamferBox2D(x - (rIn + 5.2), y - 5.1, 6.5, 4.4, 2.2), rIn - r);

                double upperMain2D = Math.Max(Math.Max(SdfMesher.SmoothMin(SdfMesher.SmoothMin(dRing2D, dSaddle2D, 2.0), dRearLug2D, 1.4), 0.6 - y), y - ySaddleTop);
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

                // Carbon-Safe Outboard Top Saddle Holes (X = +-22.5mm): open bottom through-bores exit into empty air (impossible to hit carbon bar!)
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    double bx = sx * 22.5;
                    double dTopBore = Math.Max(Math.Sqrt((x - bx) * (x - bx) + (z - 7.5) * (z - 7.5)) - 2.15, Math.Max((ySaddleTop - 8.5) - y, y - (ySaddleTop + 1.0)));
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
                new Vec3(Math.Min(pivotX - 6.0, -30.0), -6.0, -0.5), new Vec3(Math.Max(pinchX + 6.5, 30.0), ySaddleTop + 1.0, 15.5), step);
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

                // Carbon-Safe Outboard Top Saddle Flange: X in [-28.5, +28.5], Y in [ySaddleTop - 7.5, ySaddleTop]
                // Bolt centers at X = +-22.5mm sit completely outboard of the Ø35mm carbon bar (r=17.5mm) so over-long screws exit into empty air!
                double dSaddle2D = SdfMesher.ChamferBox2D(x, y - (ySaddleTop - 3.75), 28.5, 3.75, 2.2);
                dSaddle2D = Math.Max(dSaddle2D, rIn - r);

                double dHingeCyl2D = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 5.2;
                double dHingeWeb2D = Math.Max(SdfMesher.ChamferBox2D(x - (pivotX * 0.5 - 4.0), y - 2.5, 6.0, 4.5, 1.8), rIn - r);
                double dFront2D = SdfMesher.SmoothMin(dHingeCyl2D, dHingeWeb2D, 1.2);

                double dRearLug2D = Math.Max(SdfMesher.ChamferBox2D(x - (rIn + 5.2), y - 5.1, 6.5, 4.4, 2.2), rIn - r);

                double upperMain2D = Math.Max(Math.Max(SdfMesher.SmoothMin(SdfMesher.SmoothMin(dRing2D, dSaddle2D, 2.0), dRearLug2D, 1.4), 0.6 - y), y - ySaddleTop);
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

                // Carbon-Safe Outboard Top Saddle (2x at X = +-22.5mm):
                // Top-entry Ø5.60mm x 6.0mm deep heat-set pilots + Ø4.3mm open exit bore out bottom into empty air (impossible to hit carbon bar!)
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    double bx = sx * 22.5;
                    double rTop = Math.Sqrt((x - bx) * (x - bx) + (z - 7.5) * (z - 7.5));
                    double dTopThrough = Math.Max(rTop - 2.15, Math.Max((ySaddleTop - 8.5) - y, y - (ySaddleTop + 1.0)));
                    double dTopInsert = Math.Max(rTop - 2.80, Math.Max((ySaddleTop - 6.0) - y, y - (ySaddleTop + 1.0)));
                    double dTopLead = Math.Max(rTop - 3.20, (ySaddleTop - 0.6) - y);
                    cut = Math.Min(cut, Math.Min(dTopThrough, Math.Min(dTopInsert, dTopLead)));
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
                new Vec3(Math.Min(pivotX - 6.0, -30.0), -6.0, -0.5), new Vec3(Math.Max(pinchX + 6.5, 30.0), ySaddleTop + 1.0, 15.5), step);
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

        // 03g & 03h: Pause-Print Embedded M4 Nyloc (DIN 985) Variant of Carbon-Safe Outboard-Flange Handlebar Clamps!
        //   - ONE SINGLE PRINT PAUSE at Z = 11.40 mm (Layer 57 @ 0.20mm / Layer 71 @ 0.16mm):
        //     Drop in 3x M4 DIN 985 Nyloc locknuts per clamp (6x per pair) standing on their hex flats:
        //       * 2x Outboard Top Saddle pockets (X = +-22.5mm, Y = ySaddleTop-7.5..ySaddleTop-2.2, 5.30mm wide, nylon ring facing DOWN/-Y)
        //       * 1x Rear Pinch Lug pocket (X = pinchX, Y = 2.8..8.1, 5.30mm wide, nylon ring facing UP/+Y)
        //   - Bottom half of each pocket is a 60-deg self-supporting hex V-cradle (anti-rotation); upper half is a straight vertical chute for effortless drop-in!
        //   - Front Half-Lap Hinge uses a bottom-entry captive M4 Nyloc hex pocket (Z = 0.0..5.30mm, 2.05mm solid bearing wall) — no second pause needed!
        //   - 100% Support-Free & Carbon-Safe (outboard +-22.5mm open exit bores clear the Ø35mm carbon bar by 2.85mm!).
        public static MeshData BuildHandlebarHingedClamshellPairPauseNyloc(double boreDiameter, string meshName, double step = 0.70)
        {
            double rIn = boreDiameter * 0.5;
            double rOut = rIn + 5.6;
            double ySaddleTop = rIn + 6.0;
            double pivotX = -(rIn + 5.2);
            double pivotY = 0.0;
            double pinchX = +(rIn + 6.5);

            Func<double, double, double, double, double, double, double> dropInNylocPocketSdf = (x, y, z, cx, yMin, yMax) =>
            {
                // ZERO-SPIN Vertical-Flat Drop-In Hex Pocket for measured 4.29mm x 6.83mm M4 Nyloc nuts:
                // Hex rotated 30 deg (point-down) so TWO PARALLEL VERTICAL FLATS (6.90mm apart -> +0.07mm snug grip!) run all the way up to Z=11.65mm!
                // Impossible for a 7.89mm across-corners nut to spin inside a 6.90mm wide vertical channel! Pause @ Z = 11.80mm!
                double dHexXZ = SdfMesher.Hexagon2D(x - cx, z - 7.5, 6.90);
                double dDropChuteXZ = Math.Max(Math.Abs(x - cx) - 3.45, Math.Max(7.5 - z, z - 11.65));
                double dNylocXZ = Math.Min(dHexXZ, dDropChuteXZ);
                return Math.Max(dNylocXZ, Math.Max(yMin - y, y - yMax));
            };

            Func<double, double, double, double> upperHalfSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                double dOct = SdfMesher.ChamferBox2D(x, y, rOut, rOut, rOut * 0.52);
                double dRing2D = Math.Max(rIn - r, Math.Max(r - (rOut + 0.6), dOct));

                // Carbon-Safe Outboard Top Saddle Flange (9.5mm thick, X in [-29.5, +29.5]): 2.60mm top bearing wall + 4.50mm Nyloc pocket + 2.40mm bottom wall!
                double dSaddle2D = SdfMesher.ChamferBox2D(x, y - (ySaddleTop - 4.75), 29.5, 4.75, 2.2);
                dSaddle2D = Math.Max(dSaddle2D, rIn - r);

                double dHingeCyl2D = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 5.2;
                double dHingeWeb2D = Math.Max(SdfMesher.ChamferBox2D(x - (pivotX * 0.5 - 4.0), y - 2.5, 6.0, 4.5, 1.8), rIn - r);
                double dFront2D = SdfMesher.SmoothMin(dHingeCyl2D, dHingeWeb2D, 1.2);

                // Rear Pinch Lug (Y in [0.6..10.0]): 2.40mm solid floor + 4.50mm Nyloc pocket + 2.50mm solid roof
                double dRearLug2D = Math.Max(SdfMesher.ChamferBox2D(x - (rIn + 5.2), y - 5.35, 6.5, 4.65, 2.2), rIn - r);

                double upperMain2D = Math.Max(Math.Max(SdfMesher.SmoothMin(SdfMesher.SmoothMin(dRing2D, dSaddle2D, 2.0), dRearLug2D, 1.4), 0.6 - y), y - ySaddleTop);
                double profile2D = Math.Max(Math.Min(upperMain2D, dFront2D), rIn - r);

                double dz = Math.Abs(z - 7.5) - 7.5;
                double dSideBevel = (profile2D + dz + 1.0) * 0.70710678;
                double body3D = Math.Max(Math.Max(profile2D, dz), dSideBevel);

                double cut = 1e9;
                // 100% Support-Free Half-Lap Hinge: upper jaw knuckle occupies Z = 0.0..7.35mm (flat on build plate!)
                double dKnuckleCut = Math.Max(Math.Max(x - (-rIn + 0.5), y - 5.5), 7.35 - z);
                cut = Math.Min(cut, dKnuckleCut);

                // Front hinge: snug Ø4.04mm M4 through-bore + flush bottom-entry captive M4 Nyloc hex pocket (6.90mm crisp flats, Z=0.0..4.10mm -> 3.25mm solid bearing wall!)
                double dHingePinBore = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 2.02;
                double dHingeNyloc = Math.Max(SdfMesher.Hexagon2D(x - pivotX, y - pivotY, 6.90), z - 4.10);
                cut = Math.Min(cut, Math.Min(dHingePinBore, dHingeNyloc));

                // Outboard Top Saddle (2x at X = +-22.5mm): Pause-Embed 4.50mm-wide M4 Nyloc pockets (Z <= 11.15mm) + snug Ø4.04mm open carbon-safe through-bores!
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    double bx = sx * 22.5;
                    double dTopBore = Math.Max(Math.Sqrt((x - bx) * (x - bx) + (z - 7.5) * (z - 7.5)) - 2.02, Math.Max((ySaddleTop - 10.5) - y, y - (ySaddleTop + 1.0)));
                    double dTopNyloc = dropInNylocPocketSdf(x, y, z, bx, ySaddleTop - 7.10, ySaddleTop - 2.60);
                    cut = Math.Min(cut, Math.Min(dTopBore, dTopNyloc));
                }

                // Rear Pinch Lug (X = pinchX): Pause-Embed 4.50mm-wide M4 Nyloc pocket (Z <= 11.15mm) + snug Ø4.04mm M4 through-bore!
                double dPinchBore = Math.Sqrt((x - pinchX) * (x - pinchX) + (z - 7.5) * (z - 7.5)) - 2.02;
                double dPinchNyloc = dropInNylocPocketSdf(x, y, z, pinchX, 3.00, 7.50);
                cut = Math.Min(cut, Math.Min(dPinchBore, dPinchNyloc));

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
                double dLowerKnuckleCut = Math.Max(Math.Max(x - (-rIn + 0.5), -5.5 - y), z - 7.65);
                cut = Math.Min(cut, dLowerKnuckleCut);

                double rHingeLower = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY));
                double dHingePinBore = rHingeLower - 2.02;
                // Flush Ø8.0mm x 2.50mm deep M4 button-head counterbore (z in [12.50..15.00] -> sits flat on Z=0.0..2.50 when rotated for printing, 4.85mm solid wall!)
                double dHingeHeadCbore = Math.Max(rHingeLower - 4.00, 12.50 - z);
                cut = Math.Min(cut, Math.Min(dHingePinBore, dHingeHeadCbore));

                double rPinch = Math.Sqrt((x - pinchX) * (x - pinchX) + (z - 7.5) * (z - 7.5));
                double dPinchBore = rPinch - 2.02;
                double dPinchHead = Math.Max(rPinch - 4.05, y - (-5.2));
                cut = Math.Min(cut, Math.Min(dPinchBore, dPinchHead));

                return Math.Max(body3D, -cut);
            };

            var upperMesh = SdfMesher.ExtractMesh("Upper_Saddle_PauseNyloc", upperHalfSdf,
                new Vec3(Math.Min(pivotX - 6.0, -31.0), -6.0, -0.5), new Vec3(Math.Max(pinchX + 6.5, 31.0), ySaddleTop + 1.0, 15.5), step);
            var lowerMesh = SdfMesher.ExtractMesh("Lower_Swinging_Jaw_PauseNyloc", lowerHalfSdf,
                new Vec3(pivotX - 6.0, -(rOut + 1.5), -0.5), new Vec3(pinchX + 6.5, 6.0, 15.5), step);
            for (int i = 0; i < lowerMesh.Vertices.Count; i++)
            {
                var v = lowerMesh.Vertices[i];
                lowerMesh.Vertices[i] = new Vec3(-v.X, v.Y - 12.5, 15.0 - v.Z);
            }

            var single = new MeshData("Hinged_Clamshell_PauseNyloc_Unit");
            single.Append(upperMesh);
            single.Append(lowerMesh);

            var pair = new MeshData(meshName);
            var left = single.Clone(meshName + "_L"); left.Translate(-39.0, 0, 0);
            var right = single.Clone(meshName + "_R"); right.Translate(39.0, 0, 0);
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

        // 04b: Positive-Fit Snap-In 90A TPU Belly Downward Vision & IR Sensor Mud Shield
        // Measured cradle opening: 19.60 x 22.40 mm, floor thickness 2.70 mm (Z=0..2.7mm).
        // Plug shank: 19.20 x 22.00 mm (0.20mm clearance/side -> zero bulging!) + crisp +0.35mm snap-lip above the 2.70mm floor!
        public static MeshData BuildTpuBellySensorShield(double step = 0.26)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                // Underside mud flange (Z = 0.0..1.4mm)
                double flange = SdfMesher.StealthBlock3D(x, y, z - 0.7, 11.8, 13.0, 0.7, 3.0, 0.0);
                // Positive-fit shank (Z = 1.4..4.2mm, 19.20 x 22.00 mm -> 0.20mm clean clearance per side!)
                double plug = SdfMesher.RoundedBox3D(x, y, z - 2.8, 9.60, 11.00, 1.4, 2.0);
                // Low-profile retention snap-lip (Z = 4.1..4.9mm, 19.90 x 22.70 mm -> clicks cleanly over 2.70mm cradle floor w/ 45-deg top lead-in!)
                double snapLip = SdfMesher.StealthBlock3D(x, y, z - 4.5, 9.95, 11.35, 0.4, 2.0, 0.35);
                double body = Math.Min(flange, Math.Min(plug, snapLip));

                // Recessed non-contact optical dome (Z in [2.2, 5.5]) so nothing ever touches the downward fisheye lens or IR ToF window!
                double dOpticalDome = Math.Max(SdfMesher.Box2D(x, y, 7.2, 8.2, 3.0), 2.2 - z);
                return Math.Max(body, -dOpticalDome);
            };
            return SdfMesher.ExtractMesh("04b_TPU_Belly_Sensor_Shield", sdf, new Vec3(-13.0, -14.2, -0.5), new Vec3(13.0, 14.2, 5.5), step);
        }

        // 04c: Positive-Fit Snap-In 90A TPU Heatsink Mud-Guard Louver
        // Measured cradle opening: 19.20 x 22.40 mm, floor thickness 2.70 mm.
        // Plug shank: 18.80 x 22.00 mm (0.20mm clearance/side -> zero bulging!) + crisp +0.35mm snap-lip!
        public static MeshData BuildTpuHeatsinkLouver(double step = 0.26)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double flange = SdfMesher.StealthBlock3D(x, y, z - 0.7, 11.6, 13.0, 0.7, 3.0, 0.0);
                double plug = SdfMesher.RoundedBox3D(x, y, z - 2.8, 9.40, 11.00, 1.4, 2.0);
                double snapLip = SdfMesher.StealthBlock3D(x, y, z - 4.5, 9.75, 11.35, 0.4, 2.0, 0.35);
                double body = Math.Min(flange, Math.Min(plug, snapLip));

                double cut = 1e9;
                for (int i = -2; i <= 2; i++)
                {
                    double shiftedX = x - i * 3.3 - (z - 2.3) * 0.65;
                    cut = Math.Min(cut, Math.Max(Math.Abs(shiftedX) - 0.80, Math.Abs(y) - 8.0));
                }
                return Math.Max(body, -cut);
            };
            return SdfMesher.ExtractMesh("04c_TPU_Heatsink_Mud_Louver", sdf, new Vec3(-12.8, -14.2, -0.5), new Vec3(12.8, 14.2, 5.5), step);
        }

        // 04d: Positive-Fit 90A TPU Nose-Vault Gimbal Cushion, Forward LiDAR Dome & Side-Skirt Mud Seal
        // Measured interior nose cup width: 34.40 mm -> outer shell tuned to 33.60 mm (0.40mm clearance/side, zero bulging!)
        public static MeshData BuildTpuNoseVaultGimbalGuard(double step = 0.26)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ay = Math.Abs(y);

                // Outer U-shell matching interior of Closed Nose Cup: 27.4mm (X) x 33.6mm (Y) x 18.5mm (Z)
                double outer2D = SdfMesher.ChamferBox2D(x, y, 13.7, 16.8, 5.0);
                double body3D = Math.Max(outer2D, Math.Max(0.0 - z, z - 18.5));

                // Carve interior drone-nose cavity (Z > 5.0mm)
                double dDroneNose = Math.Max(SdfMesher.ChamferBox2D(x, y, 11.0, 14.0, 3.5), 5.0 - z);

                // Deep non-contact optical dome for Main 2-Axis Gimbal Lens + Forward LiDAR
                double dLensAndLidarDome = Math.Max(SdfMesher.Box2D(x, y, 9.2, 8.2, 4.5), 2.0 - z);

                // Compliant flex ribs on gimbal side-cheeks
                double dFlexGroove = Math.Max(Math.Abs(x) - 1.5, Math.Max(9.2 - ay, Math.Abs(z - 8.5) - 2.2));

                return Math.Max(body3D, -Math.Min(dDroneNose, Math.Min(dLensAndLidarDome, dFlexGroove)));
            };

            return SdfMesher.ExtractMesh("04d_TPU_Nose_Vault_Gimbal_And_LiDAR_Guard", sdf,
                new Vec3(-14.8, -18.0, -0.5), new Vec3(14.8, 18.0, 19.5), step);
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

        // 04f (V3 - Supple 1.6mm Band, Generous 38mm Short Loop, Effortless Ø7.8mm Boss + 11.2x6.4mm Eyelets):
        // Mid-Base (X=+6.5mm) 14.0 x 3.5 mm strap loops between the DJI Neo 2's front & rear propeller guards!
        // Prints 100% FLAT on Z = 0.0 (248 mm long, fits 256 mm P2S bed) with ZERO supports!
        public static MeshData BuildTpu90ASelfLockingCradleStrap(double step = 0.36)
        {
            Func<double, double, double, double, double> mushroomBossSdf = (xLocal, y, z, cx) =>
            {
                double dxS = xLocal - cx;
                double rStud = Math.Sqrt(dxS * dxS + y * y);
                double radStud;
                if (z <= 4.4)
                {
                    radStud = 2.80; // Ø5.6mm solid neck
                }
                else if (z <= 5.9)
                {
                    radStud = 2.80 + 0.733 * (z - 4.4); // 40-deg self-supporting conical flare to Ø7.8mm cap
                }
                else
                {
                    radStud = 3.90 - 1.10 * Math.Min(1.0, (z - 5.9) / 1.1); // Smooth domed top bevel
                }
                return Math.Max(rStud - radStud, Math.Max(1.4 - z, z - 7.0));
            };

            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double xLocal = x + 124.0; // [0.0 .. 248.0]

                // 1) Supple 1.6mm-thick continuous strap band (3.4x more flexible than 2.4mm!)
                double dEndA = SdfMesher.StealthBlock3D(xLocal - 13.0, y, z - 0.8, 13.0, 6.6, 0.8, 4.5, 0.25);
                double dBand = SdfMesher.StealthBlock3D(xLocal - 104.0, y, z - 0.8, 86.0, 6.0, 0.8, 2.0, 0.25);
                double dLadder = SdfMesher.StealthBlock3D(xLocal - 216.0, y, z - 0.8, 32.0, 6.6, 0.8, 4.5, 0.25);

                // 2) Generous 38mm short-loop anchor (xLocal=10 eyelet -> xLocal=48 boss) + Top-Center Main Boss (xLocal=102)
                double dPad1 = SdfMesher.StealthBlock3D(xLocal - 48.0, y, z - 1.0, 7.5, 6.6, 1.0, 3.0, 0.25);
                double dPad2 = SdfMesher.StealthBlock3D(xLocal - 102.0, y, z - 1.0, 7.5, 6.6, 1.0, 3.0, 0.25);
                double dBoss1 = mushroomBossSdf(xLocal, y, z, 48.0);
                double dBoss2 = mushroomBossSdf(xLocal, y, z, 102.0);

                // 3) Raised tactile thumb grip ribs on pull-tab
                double dRib1 = SdfMesher.RoundedBox3D(xLocal - 243.0, y, z - 2.0, 1.0, 4.2, 0.5, 0.3);
                double dRib2 = SdfMesher.RoundedBox3D(xLocal - 246.0, y, z - 2.0, 1.0, 3.6, 0.5, 0.3);

                double baseBand = Math.Min(Math.Min(dEndA, dBand), dLadder);
                double pads = Math.Min(dPad1, dPad2);
                double bosses = Math.Min(dBoss1, dBoss2);
                double body = SdfMesher.SmoothMin(Math.Min(baseBand, pads), bosses, 0.5);
                body = Math.Min(body, Math.Min(dRib1, dRib2));

                // 4) Generous 11.2 x 6.4 mm elongated oval eyelets (effortless 1-thumb pop over Ø7.8mm cap, locks cleanly on Ø5.6mm stem!)
                double holes = 1e9;
                double[] eyeXs = new double[] { 10.0, 194.0, 208.0, 222.0, 236.0 };
                for (int i = 0; i < eyeXs.Length; i++)
                {
                    double dEye = SdfMesher.Box2D(xLocal - eyeXs[i], y, 5.6, 3.2, 3.15);
                    holes = Math.Min(holes, dEye);
                }

                return Math.Max(body, -holes);
            };

            return SdfMesher.ExtractMesh("04f_TPU_90A_SelfLocking_Cradle_Strap", sdf,
                new Vec3(-125.5, -8.0, -0.5), new Vec3(125.5, 8.0, 7.8), step);
        }

        // 04g (V7): Wide-Loop 4.6mm-Gap Full-Travel 4.5mm Outboard C-Bow Hairpin 90A TPU Suspension Puck (Side-Printed, Zero Supports!)
        //   - Bumped +2.0mm taller (20.0mm total) to open up a massive 4.6mm clear air gap INSIDE the banana loops (R_c=3.9mm) so they NEVER pinch shut!
        //   - Outer Bridge Feet (|x| in [12.2..24.0], z in [0.0..6.0]): drop right into 03a/03i's 48.6 x 26.6 x 3.5mm pocket!
        //   - Floating Center Cradle Platform (|x| <= 11.2, z in [4.5..20.0]): flat underside with FULL unobstructed 4.5mm vertical travel above bridge floor!
        //   - Twin Outboard C-Bow Hairpin Leaf Springs (t=3.2mm, R_c=3.9mm, top z=14.8..15.0mm): 5.0-5.2mm BELOW cradle plane + 4.6mm clear inside loop!
        public static MeshData BuildTpuDecoupledShearIsolatorPuck(double step = 0.30)
        {
            Func<double, double, double, double> sdf = (px, py, pz) =>
            {
                // Map print coords (px, py, pz) -> installed coords (x, y, z):
                //   px = x [-38.0..+38.0], py = z [0..20.0], pz = y + 13.0 [0..26.0]
                double x = px;
                double z = py;
                double y = pz - 13.0;
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);

                // 1) 2x Outer Bridge Feet: |x| in [12.2..24.0], z in [0.0..6.0] (drops cleanly into 03a/03i's 48.6 x 26.6 x 3.5mm pocket)
                double dFoot2D = SdfMesher.Box2D(ax - 18.1, z - 3.0, 5.9, 3.0, 1.2);
                double dFootXY = SdfMesher.Box2D(x, y, 24.0, 13.0, 4.0);
                double dFoot3D = Math.Max(dFoot2D, dFootXY);

                // 2) Floating Center Cradle Platform: |x| <= 11.2, z in [4.5..20.0] (clean flat underside -> FULL 4.5mm unobstructed vertical travel!)
                double dCenter2D = SdfMesher.Box2D(x, z - 12.25, 11.2, 7.75, 1.6);

                // 3) Twin Wide-Open Outboard C-Bow Hairpin Leaf Springs (t = 3.2mm -> r = 1.60mm):
                //    - Lower leg: (22.0, 5.4) -> (32.5, 5.4) (bottom edge 3.8mm -> clears 3.5mm pocket rim!)
                //    - Smooth semicircular C-bend centered at (32.5, 9.3) with centerline radius R_c = 3.90mm (z=5.4 to z=13.2 -> 4.6mm internal air gap!)
                //    - Upper leg: (32.5, 13.2) -> (10.5, 13.4) (top edge 14.8..15.0mm -> 5.0-5.2mm BELOW z=20.0 cradle!)
                double dLowerLeg = Capsule2D(ax, z, 22.0, 5.4, 32.5, 5.4, 1.60);
                double dUpperLeg = Capsule2D(ax, z, 32.5, 13.2, 10.5, 13.4, 1.60);

                // Smooth right-half semicircular arc at (32.5, 9.3), R_c = 3.90mm, tube radius = 1.60mm
                double arcDx = Math.Max(0.0, ax - 32.5);
                double arcDz = z - 9.3;
                double dArc2D = Math.Abs(Math.Sqrt(arcDx * arcDx + arcDz * arcDz) - 3.90) - 1.60;
                double dArcClipped = Math.Max(dArc2D, 32.5 - ax);

                double dHairpin2D = Math.Min(Math.Min(dLowerLeg, dUpperLeg), dArcClipped);
                double dUpperBody3D = Math.Max(SdfMesher.SmoothMin(dCenter2D, dHairpin2D, 1.0), Math.Abs(y) - 13.0);

                double body = SdfMesher.SmoothMin(dFoot3D, dUpperBody3D, 0.9);

                double cut = 1e9;

                // 4x Square Cradle Bolt Holes (+-7.5, +-7.5): M4 bore + bottom Ø9.6mm counterbore (z in [4.5..14.0], 6.0mm solid TPU roof for M4x10-12mm bolts!)
                double dxSq = ax - 7.5;
                double dySq = ay - 7.5;
                double rSq = Math.Sqrt(dxSq * dxSq + dySq * dySq);
                double dSqBore = Math.Max(rSq - 2.05, 4.0 - z);
                double dSqCbore = Math.Max(rSq - 4.80, Math.Max(4.0 - z, z - 14.00));
                cut = Math.Min(cut, Math.Min(dSqBore, dSqCbore));

                // 2x Centerline Bridge Bolt Holes (+-15.0, 0.0):
                //   - M4 bore through outer foot (z <= 6.2)
                //   - Recessed hex/button pocket in outer foot (z in [3.8..6.4], 3.8mm solid bearing floor!)
                //   - Straight-down Ø8.6mm access bore through upper hairpin leg (z in [10.5..16.5]) for effortless bolt & Allen-key installation!
                double dxCtr = ax - 15.0;
                double rCtr = Math.Sqrt(dxCtr * dxCtr + y * y);
                double dCtrBore = Math.Max(rCtr - 2.05, z - 6.4);
                double dCtrPocket = Math.Max(SdfMesher.Hexagon2D(dxCtr, y, 7.05), Math.Max(3.80 - z, z - 6.6));
                double dUpperAccess = Math.Max(rCtr - 4.30, Math.Max(10.5 - z, z - 16.5));
                cut = Math.Min(cut, Math.Min(Math.Min(dCtrBore, dCtrPocket), dUpperAccess));

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh("04g_TPU_Decoupled_Shear_Isolator_Puck", sdf,
                new Vec3(-39.0, -0.6, -0.5), new Vec3(39.0, 20.8, 26.6), step);
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

        // =========================================================================
        // 01h / 01i: 90A TPU "EXO-CRADLE" (INTEGRATED GIMBAL/BELLY/HEATSINK ARMOR
        //            + FLUSH NOSE-TO-TAIL STRAP TIE-BARS, ZERO SUPPORTS!)
        //   - Option 1 (01h, hybridWithSpineRecess=true): Flush underside 114 x 24.4 x 3.65mm
        //     recess for snap-in 05c Rigid 72D/PETG Backbone Spine (zero droop, zero bolt pull-through!)
        //   - Option 2 (01i, hybridWithSpineRecess=false): Pure All-90A One-Piece Thick-Keel
        //     Cradle w/ twin longitudinal side-keels + 4x Ø9.6mm top washer/Nyloc counterbores!
        // =========================================================================
        public static MeshData BuildCradle90ATpuExo(
            string name,
            MeshData refCradleShell,
            bool hybridWithSpineRecess,
            double step = 0.36)
        {
            Vec3 minB, maxB;
            refCradleShell.GetBounds(out minB, out maxB);
            if (-82.5 < minB.X) minB.X = -82.5;
            if (84.0 > maxB.X) maxB.X = 84.0;
            if (-23.0 < minB.Y) minB.Y = -23.0;
            if (23.0 > maxB.Y) maxB.Y = 23.0;

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
                for (int iy = 0; iy <= ny; iy++)
                {
                    double y = oy + iy * step;
                    double ay = Math.Abs(y);
                    for (int iz = 0; iz <= nz; iz++)
                    {
                        double z = oz + iz * step;
                        float d = grid[ix, iy, iz];

                        // 1) Plug leftover circular rear-arm side holes (X=53, Z=6) and smoothly trim rear arms (4.2 -> 3.0mm soft cheeks)
                        if (x >= 46.5 && x <= 59.5 && ay >= 16.2 && z >= 0.0 && z <= 12.5)
                        {
                            float dPlug = (float)RearArmBaseHolePlugSdf(x, y, z);
                            if (dPlug < d) d = dPlug;
                        }
                        if (x >= 43.0 && ay >= 18.0)
                        {
                            float dTrim = -(float)RearFlexClipReinforcedTrimSdf(x, y, z);
                            if (dTrim > d) d = dTrim;
                        }

                        // 2) Self-supporting 29.4-deg front lower nose chin + integrated +2.4mm plush 90A front gimbal bumper pad!
                        if (x <= -46.0 && z >= -0.5 && z <= 17.5)
                        {
                            float dChin = (float)NoseChin30DegFillSdf(x, y, z);
                            if (dChin < d) d = dChin;
                        }
                        if (x >= -77.0 && x <= -66.0 && ay <= 16.5 && z >= 4.0 && z <= 37.5)
                        {
                            // Plush external nose bumper pad hugging the front of the closed nose cup (zero intrusion into inner gimbal vault!)
                            double dBumper = SdfMesher.RoundedBox3D(x + 71.5, y, z - 21.0, 4.0, 14.2, 14.5, 2.2);
                            double dGuardClip = Math.Max(dBumper, x - (-69.2)); // stays strictly on outer front face X <= -69.2
                            if ((float)dGuardClip < d) d = (float)dGuardClip;
                        }

                        // 3) Integrated Belly Sensor Seal (solidly closes rear belly cutout X in [40.5..60.5], |Y|<=11.4, Z in [0..2.7])
                        if (x >= 39.5 && x <= 61.5 && ay <= 12.0 && z >= -0.5 && z <= 3.5)
                        {
                            double dBellySeal = SdfMesher.RoundedBox3D(x - 50.5, y, z - 1.35, 10.5, 11.6, 1.35, 0.5);
                            if ((float)dBellySeal < d) d = (float)dBellySeal;
                        }

                        // 4) Integrated Front Heatsink Mud-Louver Bars (3 angled slats across X in [-41..-21.5], |Y|<=11.5, Z in [0..2.7])
                        if (x >= -42.0 && x <= -20.5 && ay <= 12.0 && z >= -0.5 && z <= 3.5)
                        {
                            double dLouvers = 1e9;
                            double[] slatXs = new double[] { -37.2, -31.2, -25.2 };
                            for (int s = 0; s < slatXs.Length; s++)
                            {
                                double dSlat = SdfMesher.RoundedBox3D(x - slatXs[s], y, z - 1.35, 1.45, 11.6, 1.35, 0.4);
                                if (dSlat < dLouvers) dLouvers = dSlat;
                            }
                            if ((float)dLouvers < d) d = (float)dLouvers;
                        }

                        // 5) Solid Center Mounting Deck (X in [-16..+35], |Y|<=13.5, Z in [0..5.55])
                        if (x >= -17.5 && x <= 36.5 && ay <= 15.0 && z >= -0.5 && z <= 6.2)
                        {
                            double w1 = SdfMesher.RoundedBox3D(x - (-4.125), y, z - 1.425, 10.075, 11.50, 1.425, 0.8);
                            double w2 = SdfMesher.RoundedBox3D(x - 23.00, y, z - 1.425, 9.95, 11.50, 1.425, 0.8);
                            double deck = SdfMesher.StealthBlock3D(x - 9.50, y, z - 4.125, 25.00, 13.40, 1.425, 3.0, 0.6);
                            double dBase = Math.Min(Math.Min(w1, w2), deck);
                            if ((float)dBase < d) d = (float)dBase;
                        }

                        // 6) Flush Nose-to-Tail Strap Loops (Front X in [-81.0..-69.5], Rear X in [+71.0..+82.5], |Y|<=11.5, Z in [0..6.5])
                        //    Each has a clean 16.0 x 3.8 mm vertical pass-through slot + 3.2 x 6.5 mm outer tie bar (ZERO supports!)
                        if (x <= -68.0 && ay <= 13.0 && z >= -0.5 && z <= 7.5)
                        {
                            double dFrontLoop = SdfMesher.StealthBlock3D(x + 75.25, y, z - 3.25, 5.75, 11.5, 3.25, 2.5, 0.8);
                            if ((float)dFrontLoop < d) d = (float)dFrontLoop;
                            double dFrontSlot = Math.Max(SdfMesher.Box2D(x + 75.9, y, 1.90, 8.0, 0.9), Math.Max(-1.0 - z, z - 8.0));
                            if ((float)(-dFrontSlot) > d) d = (float)(-dFrontSlot);
                        }
                        if (x >= 69.5 && ay <= 13.0 && z >= -0.5 && z <= 7.5)
                        {
                            double dRearLoop = SdfMesher.StealthBlock3D(x - 76.75, y, z - 3.25, 5.75, 11.5, 3.25, 2.5, 0.8);
                            if ((float)dRearLoop < d) d = (float)dRearLoop;
                            double dRearSlot = Math.Max(SdfMesher.Box2D(x - 77.4, y, 1.90, 8.0, 0.9), Math.Max(-1.0 - z, z - 8.0));
                            if ((float)(-dRearSlot) > d) d = (float)(-dRearSlot);
                        }

                        // 7) Hybrid vs All-90A One-Piece Base Structure & 4x M4 Mount Holes
                        if (hybridWithSpineRecess)
                        {
                            // Continuous solid floor bridge across X in [-49..+67] at Z in [3.65..5.55] above the spine pocket
                            if (x >= -50.0 && x <= 68.0 && ay <= 13.5 && z >= 2.8 && z <= 6.0)
                            {
                                double dRoofBridge = SdfMesher.RoundedBox3D(x - 9.0, y, z - 4.60, 58.0, 12.8, 0.95, 0.6);
                                if ((float)dRoofBridge < d) d = (float)dRoofBridge;
                            }
                            // Flush underside Backbone Spine Recess: 114.0 x 24.4 x 3.65mm (X in [-48.0..+66.0], |Y|<=12.2, Z in [-0.5..3.65])
                            if (x >= -49.5 && x <= 67.5 && ay <= 13.5 && z <= 6.0)
                            {
                                double dSpineCut = Math.Max(SdfMesher.Box2D(x - 9.0, y, 57.0, 12.2, 3.0), z - 3.65);
                                // 4x M4 bores + upper nut-clearance domes (Z in [3.65..5.05]) so 4.29mm Nylocs never bulge the 90A floor!
                                double dxM = Math.Abs(x - 9.50) - 7.50;
                                double dyM = ay - 7.50;
                                double rM = Math.Sqrt(dxM * dxM + dyM * dyM);
                                double dBoltCut = Math.Min(rM - 2.15, Math.Max(rM - 4.20, z - 5.05));
                                double dCut = Math.Min(dSpineCut, dBoltCut);
                                if ((float)(-dCut) > d) d = (float)(-dCut);
                            }
                        }
                        else
                        {
                            // All-90A One-Piece: Twin deep longitudinal stiffening side-keels (X in [-50..+66], |Y| in [12.0..16.8], Z in [0..6.2])
                            if (x >= -51.0 && x <= 67.0 && ay >= 11.2 && ay <= 17.5 && z >= -0.5 && z <= 6.8)
                            {
                                double dKeel = SdfMesher.RoundedBox3D(x - 8.0, ay - 14.4, z - 3.1, 58.0, 2.4, 3.1, 1.0);
                                if ((float)dKeel < d) d = (float)dKeel;
                            }
                            // 4x Top-Access Ø9.6mm Fender-Washer / M4 Nyloc Counterbores (Z in [2.20..6.2], 2.20mm solid bearing floor!)
                            if (x >= -2.0 && x <= 21.0 && ay <= 14.0 && z <= 6.5)
                            {
                                double dxM = Math.Abs(x - 9.50) - 7.50;
                                double dyM = ay - 7.50;
                                double rM = Math.Sqrt(dxM * dxM + dyM * dyM);
                                double dBoltCut = Math.Min(rM - 2.15, Math.Max(rM - 4.80, 2.20 - z));
                                if ((float)(-dBoltCut) > d) d = (float)(-dBoltCut);
                            }
                        }

                        grid[ix, iy, iz] = d;
                    }
                }
            }

            var result = SdfMesher.ExtractFromGrid(name, grid, ox, oy, oz, step, null);
            result.SnapBottomToZero();
            return result;
        }

        // =========================================================================
        // 05c: RIGID BACKBONE SPINE PLATE FOR 01h HYBRID 90A EXO-CRADLE
        //   - Print in Elegoo 72D TPU or PETG (flat on bed, ~10 min, ZERO supports!)
        //   - 113.2 x 23.6 x 3.50 mm (snaps flush into 01h's underside recess!)
        //   - Carries 4x crisp 6.90mm zero-spin M4 Nyloc pockets (Z in [1.25..3.80])
        //     + lightweight front/rear truss cutouts while keeping 4.5mm outer rails!
        // =========================================================================
        public static MeshData BuildRigidBackboneSpinePlate(double step = 0.28)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ay = Math.Abs(y);
                // Center at X = +9.0 (matching 01h recess X in [-48..+66], shifted to origin for clean bed centering!)
                // Local x in [-56.6..+56.6], y in [-11.8..+11.8], z in [0.0..3.50]
                // 4x M4 holes at x_cradle = 2.0, 17.0 -> x_local = -7.0, +8.0 (centered at x_local = +0.50!)
                double body = SdfMesher.RoundedBox3D(x, y, z - 1.75, 56.6, 11.8, 1.75, 2.6);

                double cut = 1e9;
                // 4x Crisp 6.90mm Zero-Spin M4 Nyloc Pockets at x_local = +0.50 +- 7.50, y = +-7.50 (1.25mm solid floor!)
                double dxM = Math.Abs(x - 0.50) - 7.50;
                double dyM = ay - 7.50;
                double rM = Math.Sqrt(dxM * dxM + dyM * dyM);
                double dHex = Math.Max(SdfMesher.Hexagon2D(dyM, dxM, 6.90), 1.25 - z);
                cut = Math.Min(cut, Math.Min(rM - 2.10, dHex));

                // Weight-saving stealth truss windows in front (x in [-48..-16]) and rear (x in [+18..+48])
                // Leaves rigid 4.8mm outer longitudinal rails (|y| in [7.0..11.8]) for maximum bending stiffness!
                double dFrontWin = SdfMesher.Box2D(x + 32.0, y, 16.0, 6.8, 3.0);
                double dRearWin = SdfMesher.Box2D(x - 33.0, y, 15.0, 6.8, 3.0);
                cut = Math.Min(cut, Math.Min(dFrontWin, dRearWin));

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh("05c_Rigid_Backbone_Spine_72D_Or_PETG", sdf,
                new Vec3(-57.6, -12.8, -0.4), new Vec3(57.6, 12.8, 4.0), step);
        }

        // =========================================================================
        // 04h (V6): ULTRA-SUPPLE 1.2mm QUARTER-TURN OVAL-LOCK NOSE-TO-TAIL STRAP
        //   - Ultra-thin 1.2 mm (6 layers @ 0.20mm) continuous 15.2mm strap band (zero lens shield bulk!)
        //   - Low-Profile Quarter-Turn Oval Buttons (5.0mm tall — 2.2mm lower than V5!):
        //       * Round Ø6.0mm neck (z=1.2..2.5mm -> 1.3mm snug clearance for 1.2mm strap)
        //       * Perpendicular Oval Cap: 8.8mm along strap x 10.3mm across strap (+1.5mm wider sideways!)
        //       * Twist strap slightly to slip 11.2mm eyelet over 10.3mm oval; straightens to lock crosswise across 6.4mm width!
        //   - Rear Locking Button @ 248mm + Single Outermost Eyelet @ 304mm; pre-rotated 45-deg for 256x256 P2S bed!
        // =========================================================================
        public static MeshData BuildTpu90ANoseToTailStrap(double step = 0.36)
        {
            Func<double, double, double, double, double> mushroomBossSdf = (xLocal, y, z, cx) =>
            {
                double dxS = xLocal - cx;
                double rx, ry;
                if (z <= 2.5)
                {
                    rx = 3.00; // Ø6.0mm round neck (rotates smoothly inside 6.4mm eyelet width!)
                    ry = 3.00;
                }
                else if (z <= 4.2)
                {
                    double u = (z - 2.5) / 1.7;
                    rx = 3.00 + 1.40 * u; // flares to 8.8mm along strap (xLocal)
                    ry = 3.00 + 2.15 * u; // flares to 10.3mm perpendicular to strap (y) (+1.5mm wider sideways!)
                }
                else
                {
                    double u = Math.Min(1.0, (z - 4.2) / 0.8);
                    rx = Math.Max(0.4, 4.40 - 0.90 * u);
                    ry = Math.Max(0.4, 5.15 - 0.90 * u);
                }
                double dOval2D = SdfMesher.Box2D(dxS, y, rx, ry, Math.Min(rx, ry));
                return Math.Max(dOval2D, Math.Max(1.0 - z, z - 5.0));
            };

            Func<double, double, double, double> sdf = (px, py, pz) =>
            {
                // Rotate -45 deg into strap-local coords (xLocal in [0..318], yLocal in [-10..+10], z in [0..5.0])
                const double invSqrt2 = 0.7071067811865475;
                double xLocal = (px + py) * invSqrt2 + 159.0;
                double y = (-px + py) * invSqrt2;
                double z = pz;

                // 1) Ultra-supple 15.2mm wide x 1.2mm thick continuous 90A strap band (6 layers @ 0.20mm!)
                double dBand2D = SdfMesher.Box2D(xLocal - 159.0, y, 159.0, 7.6, 3.8);
                double dBand = Math.Max(dBand2D, Math.Max(0.0 - z, z - 1.2));

                // 2) Dual Low-Profile Perpendicular Oval Quarter-Turn Buttons (8.8 x 10.3 mm cap, 5.0mm tall):
                //    - Front Anchor Boss (xLocal=48) gives generous 38mm short loop around the Front Nose Tie Bar
                //    - Rear Locking Boss (xLocal=248) sits at the exact old innermost-eyelet spot right above the Rear Tie Bar!
                double dFrontBoss = mushroomBossSdf(xLocal, y, z, 48.0);
                double dRearBoss = mushroomBossSdf(xLocal, y, z, 248.0);

                // 3) Ribbed 2.2mm-tall Glove Pull-Tab at End B (xLocal in [311..318])
                double dPullTab = Math.Max(SdfMesher.Box2D(xLocal - 314.5, y, 3.5, 7.6, 2.2), Math.Max(0.0 - z, z - 2.2));

                double body = Math.Min(dBand, Math.Min(Math.Min(dFrontBoss, dRearBoss), dPullTab));

                // Subtract 1x Captive Front-Anchor Oval Eyelet (xLocal=10) + 1x Single Outermost Rear-Locking Eyelet (xLocal=304)
                double cut = 1e9;
                double[] eyeXs = new double[] { 10.0, 304.0 };
                for (int i = 0; i < eyeXs.Length; i++)
                {
                    double ex = xLocal - eyeXs[i];
                    double dOval = SdfMesher.Box2D(ex, y, 5.6, 3.2, 3.15);
                    cut = Math.Min(cut, dOval);
                }

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh("04h_TPU_90A_NoseToTail_OverTop_Strap", sdf,
                new Vec3(-124.0, -124.0, -0.4), new Vec3(124.0, 124.0, 5.6), step);
        }

        // =========================================================================
        // 04i: LOW-PROFILE 90A TPU SNAP-IN GIMBAL LOCK CLIP (FITS INSIDE CRADLE!)
        //   - Slides into the DJI Neo 2 front camera bay & fits completely inside 01h/01i/01g nose vault!
        //   - Soft 90A TPU side cheek-fingers + top/bottom flex-ribs gently hug the camera barrel's outer housing
        //     to immobilize BOTH pitch & roll against MTB rock-garden rattle!
        //   - Deep Ø14.5mm recessed non-contact optical dome ensures NOTHING ever touches the camera lens glass!
        //   - Low-profile textured top pull-tab pops out in 1 second before flight; prints FLAT on Z=0 with ZERO supports (~12 min)!
        // =========================================================================
        public static MeshData BuildTpu90AGimbalLockClip(double step = 0.24)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                // Print coordinates:
                //   x: width across Neo 2 nose bay [-15.2..+15.2] (30.4mm outer flex-wings tapering to 24.4mm front)
                //   y: height of camera bay [-11.5..+11.5] (23.0mm tall, +Y = top pull-tab side)
                //   z: depth from front protective face (Z=0.0 on bed) back into camera bay (Z=15.5mm)
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);

                // 1) Tapered Outer Protective Shell (matches 01h/01i nose vault taper so it fits cleanly inside cradle!)
                //    Half-width tapers from 12.2mm at front (z=0) to 15.0mm at back (z=15.5)
                double zU = Math.Max(0.0, Math.Min(1.0, z / 15.5));
                double halfW = 12.2 + 2.8 * zU;
                double halfH = 10.6 + 0.9 * zU;
                double dOuter2D = SdfMesher.Box2D(x, y, halfW, halfH, 3.6);
                double dOuter = Math.Max(dOuter2D, Math.Max(0.0 - z, z - 15.5));

                // 2) Compliant Outer Side-Grip Wings (gentle 0.8mm ribbed friction swell at z in [7..14] to grip inside Neo 2 nose bay)
                double dGripRib = Math.Max(
                    Math.Abs(ax - (halfW - 0.1)) - 0.9,
                    Math.Max(ay - 6.0, Math.Abs(z - 10.5) - 3.5));

                // 3) Low-Profile Textured Top Pull-Tab (y in [+10.0..+17.0], x in [-5.5..+5.5], z in [0.0..2.8mm] — flush on bed, zero supports!)
                double dTab = SdfMesher.StealthBlock3D(x, y - 13.5, z - 1.4, 5.5, 4.0, 1.4, 2.2, 0.5);

                double body = SdfMesher.SmoothMin(Math.Min(dOuter, dGripRib), dTab, 0.6);

                // 4) Carve Interior Gimbal Bay + Compliant Camera-Barrel Lock Fingers:
                //    - Outer hollow bay (z >= 4.2mm) leaves 2.4mm outer walls
                double dBayInner = Math.Max(SdfMesher.Box2D(x, y, halfW - 2.3, halfH - 2.2, 2.2), 4.2 - z);

                //    - Soft Gimbal-Barrel Cradle Saddle (z in [3.5..14.0]): hugs camera barrel sides (17.2mm) & top/bottom (16.6mm)
                //      Leave 2 compliant curved side cheeks + top/bottom pads inside the bay by subtracting a central barrel cavity!
                double dSaddleBlock = Math.Max(SdfMesher.Box2D(x, y, 11.4, 10.2, 2.4), Math.Max(3.2 - z, z - 13.8));
                //    - Camera Barrel Cavity: 17.6mm wide (|x|<=8.8) x 16.8mm tall (|y|<=8.4), tapering slightly to 17.0 x 16.2mm at z=4.5 for gentle snug grip!
                double barrelW = 8.4 + 0.5 * zU;
                double barrelH = 8.0 + 0.5 * zU;
                double dBarrelCavity = Math.Max(SdfMesher.Box2D(x, y, barrelW, barrelH, 3.2), 4.0 - z);

                //    - Flex-relief slots separating the inner camera-cradle cheeks from the outer shell so they flex softly like rubber cushions!
                double dFlexSlotX = Math.Max(Math.Abs(ax - 10.6) - 0.85, Math.Max(ay - 7.5, 5.2 - z));
                double dFlexSlotY = Math.Max(Math.Abs(ay - 9.2) - 0.85, Math.Max(ax - 6.0, 5.2 - z));

                //    - Deep Non-Contact Lens Optical Dome (r=7.25mm -> Ø14.5mm, z in [1.8..5.0]) so NOTHING ever touches the lens glass!
                double dLensDome = Math.Max(Math.Sqrt(x * x + y * y) - 7.25, 1.8 - z);

                //    - Small finger-grip hole in top pull-tab (r=1.8mm at y=14.2) for optional lanyard / easy pull
                double dTabHole = Math.Sqrt(x * x + (y - 14.2) * (y - 14.2)) - 1.8;

                // Hollow out the bay EXCEPT keep the soft saddle block, then carve the camera barrel cavity + flex slots + lens dome!
                double hollowed = Math.Max(body, -dBayInner);
                double withSaddle = Math.Min(hollowed, Math.Max(dSaddleBlock, dOuter));
                double innerCuts = Math.Min(Math.Min(dBarrelCavity, dLensDome), Math.Min(Math.Min(dFlexSlotX, dFlexSlotY), dTabHole));

                return Math.Max(withSaddle, -innerCuts);
            };

            return SdfMesher.ExtractMesh("04i_TPU_90A_Gimbal_Lock_Clip", sdf,
                new Vec3(-16.5, -12.5, -0.4), new Vec3(16.5, 18.5, 16.2), step);
        }

        // =========================================================================
        // 04j (V2): COMPACT PORTRAIT TAPERED-BASE 3-STRAND SPIRAL GIMBAL CUSHION (90A TPU!)
        //   - Portrait orientation (23.6mm wide x 26.0mm tall front spiral face), tapering cleanly
        //     inward at 28.6-deg (zero supports!) to a compact 18.8 x 21.2 mm FLAT GLUING BASE
        //     at Z=6.2mm that seats 100% dead-flat inside 01h/01i/01g's nose cavity!
        //   - Preserves the exact same 4.4mm open spring travel & Ø13.6mm open lens hole (zero glass contact)!
        // =========================================================================
        public static MeshData BuildTpu90ASpiralGimbalCushion(double step = 0.20)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                // Portrait orientation: X = width (narrower), Y = height (taller), Z = 0.0 (spiral face) -> 6.2 (compact gluing base)
                double r = Math.Sqrt(x * x + y * y);

                // 1) Tapered Outer Standoff Rim:
                //    - Front spiral face (z <= 1.8): 23.6 x 26.0 mm (hx=11.8, hy=13.0, R=6.8)
                //    - Tapers cleanly inward across z in [1.8..6.2] (28.6 deg, 100% support-free!) to 18.8 x 21.2 mm flat gluing base (hx=9.4, hy=10.6, R=5.0)
                double zT = Math.Max(0.0, Math.Min(1.0, (z - 1.8) / 4.4));
                double hxOut = 11.8 - 2.4 * zT;
                double hyOut = 13.0 - 2.4 * zT;
                double rOutCorner = 6.8 - 1.8 * zT;
                double wallW = 1.6 + 0.5 * zT; // 1.6mm front rim widening to 2.1mm flat gluing flange at Z=6.2mm!

                double dOuterBox = SdfMesher.Box2D(x, y, hxOut, hyOut, rOutCorner);
                double dInnerCav = SdfMesher.Box2D(x, y, hxOut - wallW, hyOut - wallW, Math.Max(2.0, rOutCorner - wallW));
                double dRim = Math.Max(Math.Max(dOuterBox, -dInnerCav), Math.Max(0.0 - z, z - 6.2));

                // 2) Center Hollow Gimbal-Bezel Cushion Ring (r in [6.8..8.0mm] -> Ø13.6mm open center clears lens glass!)
                double dRing2D = Math.Max(6.8 - r, r - 8.0);
                double dRing = Math.Max(dRing2D, Math.Max(0.0 - z, z - 1.8));

                // 3) 3-Strand Archimedean Spiral Flexure Arms (1.15mm wide x 1.6mm tall, sweeping 82 deg from r=7.5 to r=12.6mm)
                //    Clipped cleanly inside dOuterBox so all 3 strands anchor 100% solidly into the outer rim!
                double dSpirals2D = 1e9;
                const int nSeg = 14;
                const double rStart = 7.4;
                const double rEnd = 12.8;
                const double sweepRad = 1.43; // ~82 deg
                for (int k = 0; k < 3; k++)
                {
                    double baseAng = k * (2.0 * Math.PI / 3.0) + 0.35;
                    double prevX = rStart * Math.Cos(baseAng);
                    double prevY = rStart * Math.Sin(baseAng);
                    for (int s = 1; s <= nSeg; s++)
                    {
                        double u = (double)s / nSeg;
                        double rk = rStart + (rEnd - rStart) * u;
                        double ak = baseAng + sweepRad * u;
                        double curX = rk * Math.Cos(ak);
                        double curY = rk * Math.Sin(ak);
                        double dSeg = Capsule2D(x, y, prevX, prevY, curX, curY, 0.58); // 1.16mm strand width
                        if (dSeg < dSpirals2D) dSpirals2D = dSeg;
                        prevX = curX;
                        prevY = curY;
                    }
                }
                double dOuterAtFront = SdfMesher.Box2D(x, y, 11.8, 13.0, 6.8);
                double dSpirals = Math.Max(Math.Max(dSpirals2D, Math.Max(dOuterAtFront, 6.8 - r)), Math.Max(0.0 - z, z - 1.6));

                return Math.Min(dRim, Math.Min(dRing, dSpirals));
            };

            return SdfMesher.ExtractMesh("04j_TPU_90A_Spiral_Gimbal_Cushion", sdf,
                new Vec3(-12.6, -13.8, -0.3), new Vec3(12.6, 13.8, 6.8), step);
        }

        // =========================================================================
        // 05d (V2): COMPACT SHALLOW-SOCKET T-HANDLE M4 NYLOC (6.83mm) KEY — PRINT IN PLA (100% INFILL!)
        //   - Prints 100% FLAT on Z=0 (ZERO supports!) with continuous perimeter fibers from T-handle to socket!
        //   - Compact 39mm-long x 46mm-wide T-handle driver (11.0 x 10.0 mm flat-sided barrel, >=1.50mm solid walls!)
        //   - Shallow 5.0mm crisp 6.92mm 6-point hex socket at tip (holds 4.29mm Nyloc flush!) + Ø4.6mm thread clearance bore!
        // =========================================================================
        public static MeshData BuildM4NylocHoldingKey(double step = 0.22)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                // Laid flat on bed:
                //   X: along shaft from T-handle (X = -19..-8) to shallow socket tip (X = +20) [total length 39mm]
                //   Y: across T-handle (Y in [-23..+23] -> 46mm wide)
                //   Z: thickness [0.0..10.0mm] centered at Z = 5.0mm (flat bottom & top for 100% support-free printing!)
                double zc = z - 5.0;

                // 1) Compact Socket Shaft: X in [-10..+20], 11.2mm wide x 10.0mm tall, clipped flat on Z=0 & Z=10
                double dShaftYZ = Math.Max(Math.Sqrt(y * y + zc * zc) - 5.6, Math.Abs(zc) - 5.0);
                double dShaft = Math.Max(dShaftYZ, Math.Max(-10.0 - x, x - 20.0));

                // 2) Ergonomic 46mm-Wide T-Handle Bar: X in [-19..-8], Y in [-23..+23], Z in [0..10]
                double dTBar2D = SdfMesher.Box2D(x + 13.5, y, 5.5, 23.0, 4.5);
                double dTBarZ = Math.Abs(zc) - 5.0;
                double dTBarBevel = (dTBar2D + dTBarZ + 1.0) * 0.70710678;
                double dTBar = Math.Max(Math.Max(dTBar2D, dTBarZ), dTBarBevel);

                // Smoothly blend shaft into T-handle with generous 3.5mm torque fillets!
                double body = Math.Max(SdfMesher.SmoothMin(dShaft, dTBar, 3.5), Math.Max(0.0 - z, z - 10.0));

                double cut = 1e9;
                // 3) Shallow 5.0mm Crisp 6.92mm 6-Point Hex Socket at tip (X in [+15.0..+21.0], holds 4.29mm Nyloc flush!)
                double dHex = Math.Max(SdfMesher.Hexagon2D(zc, y, 6.92), 15.0 - x);
                cut = Math.Min(cut, dHex);

                // 4) Ø4.6mm Bolt-Thread Clearance Bore behind socket (X in [0.0..+21.0] so M4 screw tip passes cleanly past nut!)
                double dBore = Math.Max(Math.Sqrt(y * y + zc * zc) - 2.30, 0.0 - x);
                cut = Math.Min(cut, dBore);

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh("05d_PLA_M4_Nyloc_Holding_Key", sdf,
                new Vec3(-20.0, -24.0, -0.3), new Vec3(21.0, 24.0, 10.5), step);
        }

        // =========================================================================
        // 06a (V4): SLIM 2.0mm (10-LAYER) LEATT 5.5 PRO UNIVERSAL 90A TPU BASEPLATE
        //   - Slimmed from 3.2mm -> 2.0mm (10 layers @ 0.20mm) for faster printing, cleaner curve conformance,
        //     and LESS rubber squish under the rigid 72D Action 5 Pro adapter (06b V2)!
        //   - Exact 3.5mm shell geometry (z = 2.0..5.5mm):
        //       * 2x Upper 45-deg Ramped J-Hooks @ X=+-31.0mm (inner throat z=5.50mm = exact 3.50mm grip!)
        //       * 4x Lower Chunky +1.15mm Barbed Triangular Plugs locking right behind z=5.5mm shell!
        //       * 4x M4 universal mount holes (+-7.5, +-7.5mm centered at Y=+8.0mm) w/ back counterbores!
        // =========================================================================
        public static MeshData BuildLeatt55UniversalChestBaseplate(double step = 0.28)
        {
            Func<double, double, double, double, double, double> barbedTriPlugSdf = (x, y, z, cx, cy) =>
            {
                // Upward-pointing (+Y) equilateral triangle centered at (cx, cy): base ~10.8mm, height ~9.6mm
                // Tuned for exact 3.5mm chestplate shell (z = 2.0..5.5mm):
                //   straight stem (Z=1.8..5.5) + chunky 41-deg +1.15mm radial snap-barb (Z=5.5..6.8) + smooth 45-deg lead-in pyramid nose (Z=6.8..10.2)!
                double scale = 1.0;
                if (z >= 5.5 && z <= 6.8)
                    scale = 1.0 + 0.22 * ((z - 5.5) / 1.3); // chunky +1.15mm radial snap-ridge grabbing cleanly behind 3.5mm shell!
                else if (z > 6.8)
                    scale = Math.Max(0.28, 1.22 - 0.94 * ((z - 6.8) / 3.4)); // smooth 45-deg lead-in nose for effortless push-in!

                double hw = 5.3 * scale;
                double hBot = -3.4 * scale;
                double hTop = +6.1 * scale;
                double dTri2D = Triangle2D(x - cx, y - cy, -hw, hBot, +hw, hBot, 0.0, hTop);
                double dTriRound = dTri2D - 0.6; // smooth 0.6mm corner fillets so it never chews the Leatt plastic
                return Math.Max(dTriRound, Math.Max(1.6 - z, z - 10.2));
            };

            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                // Print coords: front face flat on Z=0.0, chest-facing hooks & triangular plugs stick UP in +Z!
                double ax = Math.Abs(x);

                // 1) Slim 2.0mm-thick (10 layers @ 0.20mm) PURE-TENSION X-TRUSS Universal Chest Baseplate:
                //    - Upper Y-arms: diagonal 16mm-wide tension arms from center 4x M4 mount (+-9, +11) up to 2x J-hooks (+-31, +41.5)
                //    - Lower Y-arms: matching diagonal arms from center 4x M4 mount (+-6.5, +2) through Row 1 (+-7.1, -24.4) to Row 2 (+-14.5, -37.2)
                //    - Compact center 4x M4 mount hub (30 x 28 mm @ Y=+8.0) -> ultra-light, breathable, conforms cleanly to 3D chest curve!
                double dUpperY = Capsule2D(ax, y, 8.5, 11.0, 31.0, 41.5, 7.8);
                double dLowerY = Capsule2D(ax, y, 6.0, 0.0, 14.5, -37.5, 9.2);
                double dRow1Bridge = SdfMesher.Box2D(x, y + 23.0, 13.0, 7.0, 3.5); // 100% full flat support under Row 1 triangle bases!
                double dCenterHub = SdfMesher.Box2D(x, y - 8.0, 14.8, 14.0, 5.5);
                double dPlate2D = SdfMesher.SmoothMin(Math.Min(dUpperY, Math.Min(dLowerY, dRow1Bridge)), dCenterHub, 4.5);
                double dPlate = Math.Max(dPlate2D, Math.Max(0.0 - z, z - 2.0));

                // 2) 2x Upper 45-Deg Self-Supporting Ramped J-Hooks at X = +-31.0mm (12.0mm wide), sized for EXACT 3.5mm shell (z=2.0..5.5mm)!
                //    Stem rises cleanly through vent slot from z=2.0 to z=7.2mm, then hooks DOWNWARD (-Y) at 45-deg from (43.5, 7.2) to (36.5, 11.4)
                //    (r=1.70 -> inner throat at z=5.50mm = exact 3.50mm snug grip on the 3.5mm Leatt chestplate with 7.0mm downward catch!)
                double dHookStem = SdfMesher.RoundedBox3D(ax - 31.0, y - 43.5, z - 4.6, 6.0, 1.9, 3.0, 0.7);
                double dHookRamp = Math.Max(
                    Math.Abs(ax - 31.0) - 6.0,
                    Capsule2D(y, z, 43.5, 7.2, 36.5, 11.4, 1.70));
                double dHooks = SdfMesher.SmoothMin(dHookStem, dHookRamp, 0.8);

                // 3) 4x Lower Barbed Triangular Interference Plugs (Row 1 @ +-7.1, -24.4; Row 2 @ +-14.5, -37.2)
                double dT1 = barbedTriPlugSdf(ax, y, z, 7.1, -24.4);
                double dT2 = barbedTriPlugSdf(ax, y, z, 14.5, -37.2);
                double dTris = Math.Min(dT1, dT2);

                double body = SdfMesher.SmoothMin(dPlate, Math.Min(dHooks, dTris), 0.7);

                // 4) Standard 4x M4 Universal Mount Holes (+-7.5, +-7.5 centered at Y=+8.0mm) w/ Back Counterbores (1.0mm solid clamping gasket + 1.0mm recess)
                double dxM = ax - 7.5;
                double dyM = Math.Abs(y - 8.0) - 7.5;
                double rM = Math.Sqrt(dxM * dxM + dyM * dyM);
                double dM4Cut = Math.Min(rM - 2.15, Math.Max(rM - 4.30, 1.0 - z));

                return Math.Max(body, -dM4Cut);
            };

            return SdfMesher.ExtractMesh("06a_Leatt55_Universal_Chest_Baseplate_90A", sdf,
                new Vec3(-42.5, -48.0, -0.3), new Vec3(42.5, 50.0, 13.8), step);
        }

        // =========================================================================
        // 06b (V2): LEATT 5.5 PRO RIGID 72D TPU DJI ACTION 5 PRO / GOPRO 3-PRONG ADAPTER
        //           WITH 19.0mm LENS-CENTERING OFFSET!
        //   - DJI Osmo Action 5 Pro (70.5 x 44.2 x 32.8mm) has its lens offset ~19.0mm from the mount center.
        //   - Shifts the 3-prong ears 19.0mm off-center from the 4x M4 bolt square (+-7.5, +-7.5mm) so the lens
        //     sits DEAD-CENTER on your chest! (Symmetric 4x M4 bolt square lets you flip 180-deg for upside-down or upright!)
        //   - Extra-stiff 6.0mm-thick baseplate in Elegoo 72D TPU (zero cantilever flex, zero shatter risk, ZERO supports!)
        // =========================================================================
        public static MeshData BuildLeatt55GoPro3ProngAdapter(double step = 0.24)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);

                // 1) Stiff 6.0mm-thick Offset Base Plate: X in [-15.0..+32.0] (47mm wide), Y in [-15.0..+15.0] (30mm tall), Z in [0.0..6.0]
                double dBase = SdfMesher.StealthBlock3D(x - 8.5, y, z - 3.0, 23.5, 15.0, 3.0, 4.5, 0.8);

                // 2) Standard GoPro / DJI Action 5 Pro 3-Prong Mount Ears shifted +19.0mm along X (X = 12.8, 19.0, 25.2mm)
                //    Each ear 3.0mm thick -> 3.2mm clear slots; Ø15.0mm rounded ears centered at (X=+19.0, Y=0, Z=17.5mm)
                double dEars = 1e9;
                double[] earXs = new double[] { 12.8, 19.0, 25.2 };
                for (int i = 0; i < earXs.Length; i++)
                {
                    double ex = Math.Abs(x - earXs[i]) - 1.50;
                    double rPivot = Math.Sqrt(y * y + (z - 17.5) * (z - 17.5)) - 7.5;
                    double dStem = Math.Max(ay - 7.5, Math.Max(5.0 - z, z - 17.5));
                    double dEar = Math.Max(ex, Math.Min(rPivot, dStem));
                    if (dEar < dEars) dEars = dEar;
                }

                double body = SdfMesher.SmoothMin(dBase, dEars, 0.8);

                double cut = 1e9;
                // A) Standard M5 Thumbscrew Bore (Ø5.2mm -> r=2.60mm) through all 3 ears at (X=19.0, Y=0, Z=17.5)
                double rGoPro = Math.Sqrt(y * y + (z - 17.5) * (z - 17.5));
                cut = Math.Min(cut, Math.Max(rGoPro - 2.60, Math.Abs(x - 19.0) - 10.0));

                // B) 4x M4 Mounting Bores + Top Crisp 6.90mm Zero-Spin M4 Nyloc Pockets at (+-7.5, +-7.5) (2.0mm solid floor!)
                double dxM = ax - 7.5;
                double dyM = ay - 7.5;
                double rM = Math.Sqrt(dxM * dxM + dyM * dyM);
                double dHexM4 = Math.Max(SdfMesher.Hexagon2D(dxM, dyM, 6.90), 2.0 - z);
                cut = Math.Min(cut, Math.Min(rM - 2.10, dHexM4));

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh("06b_Leatt55_GoPro_3Prong_Adapter_72D", sdf,
                new Vec3(-16.0, -16.0, -0.3), new Vec3(33.0, 16.0, 26.0), step);
        }
    }
}
