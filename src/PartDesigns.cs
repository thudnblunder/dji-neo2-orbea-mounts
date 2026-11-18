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

        // Slotted M4 hole + captive hex nut along Z (for vertical tower fine height adjustment!)
        // Bore axis is Y (from yMin to yMax), slot along Z centered at (hx, hz) with +-slotHalfZ
        private static double M4SlotYAndHexNutSdf(double x, double y, double z, double hx, double hz, double slotHalfZ, double yNutMin, double yNutMax)
        {
            double dx = x - hx;
            double cz = Math.Max(hz - slotHalfZ, Math.Min(hz + slotHalfZ, z));
            double dz = z - cz;
            double dBore = Math.Sqrt(dx * dx + dz * dz) - 2.15;
            // Pointed-top hex nut channel along Z so it prints horizontally with zero overhang!
            double dHex2D = SdfMesher.Hexagon2D(dz, dx, 7.20);
            double dHexY = Math.Max(yNutMin - y, y - yNutMax);
            double dHex3D = Math.Max(dHex2D, dHexY);
            return Math.Min(dBore, dHex3D);
        }

        // 1. Structural Mounting Insert for the DJI Neo 2 Cradle
        public static MeshData BuildCradleMountingInsert(double step = 0.30)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double w1 = SdfMesher.RoundedBox3D(x - (-4.125), y, z - 1.425, 10.075, 11.50, 1.425, 0.8);
                double w2 = SdfMesher.RoundedBox3D(x - 23.00, y, z - 1.425, 9.95, 11.50, 1.425, 0.8);
                double deck = SdfMesher.RoundedBox3D(x - 9.50, y, z - 4.125, 25.00, 13.20, 1.425, 2.0);
                double body = Math.Min(Math.Min(w1, w2), deck);

                double holes = 1e9;
                holes = Math.Min(holes, M4HoleAndHexNutSdf(x, y, z, 2.00, -7.50, 2.05, 6.00));
                holes = Math.Min(holes, M4HoleAndHexNutSdf(x, y, z, 2.00,  7.50, 2.05, 6.00));
                holes = Math.Min(holes, M4HoleAndHexNutSdf(x, y, z, 17.00, -7.50, 2.05, 6.00));
                holes = Math.Min(holes, M4HoleAndHexNutSdf(x, y, z, 17.00,  7.50, 2.05, 6.00));
                holes = Math.Min(holes, M4HoleAndHexNutSdf(x, y, z, -5.50, 0.00, 2.05, 6.00));
                holes = Math.Min(holes, M4HoleAndHexNutSdf(x, y, z, 24.50, 0.00, 2.05, 6.00));

                return Math.Max(body, -holes);
            };

            return SdfMesher.ExtractMesh(
                "Cradle_Mounting_Insert",
                sdf,
                new Vec3(-17.0, -15.0, -0.5),
                new Vec3(36.0, 15.0, 6.2),
                step);
        }

        // 2a. NEW SLEEK DIRECT-MOUNT L-TOWER: Sculpted Twin-Spar A-Frame & Open-Truss Parabolic Gusset Design (No Fidlock Bulk!)
        // - 100% flat Z=0.00mm bottom face (zero underside supports!)
        // - Slim 6.8mm base plate with scalloped hourglass waist & open center window
        // - Lowers drone by 7.0mm (Z_center = 93.0mm instead of 100.0mm) for extra top-tube clearance and higher stiffness
        // - Twin-spar swept legs with large central cathedral arch cutout + hollow open-truss parabolic gussets
        // - Contoured 53 x 31mm stadium upper head that tightly hugs the 48 x 26mm TPU Vibration Isolator Puck at Y = -28.0mm
        //   so [Sleek Tower + 7.5mm TPU Puck + Cradle + Drone] is 100% CENTERED at Y = 0.0mm!
        public static MeshData BuildDowntubeSleekSideMountLTower(double step = 0.42)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);

                // 1) Sleek Scalloped Base Plate (Z in [0.0, 6.8mm]):
                // Central 64mm M5 mounting spine: X in [-46.5, +46.5], width Y tapers smoothly to a narrow waist at X=0
                double spineHalfY = 10.5 - 2.2 * Math.Exp(-(x * x) / (18.0 * 18.0));
                double dSpine2D = SdfMesher.Box2D(x, y, 46.5, spineHalfY, 8.0);
                double baseSpine3D = Math.Max(dSpine2D, Math.Max(0.0 - z, z - 6.8));

                // 2) Streamlined Outrigger Pods for Lower Prop-Guard Saddles at ax = 38.0, Y = +6.8, Z in [0.0, 14.2]
                // Blended smoothly into the central spine at ax in [28..46]
                double dPod2D = SdfMesher.Box2D(ax - 38.0, y - 4.0, 8.8, 12.2, 5.5);
                double pod3D = Math.Max(dPod2D, Math.Max(0.0 - z, z - 14.2));

                // 3) Twin-Spar Swept A-Frame Struts connecting Base (Z = 0..6.8, Y_center = -13.5) to Stadium Head (Z = 75..93, Y_center = -29.25)
                double tAngle = Math.Max(0.0, Math.Min(1.0, (z - 6.8) / 68.0));
                double tSmooth = tAngle * tAngle * (3.0 - 2.0 * tAngle);
                double towerCenterY = -13.5 - 15.75 * tSmooth; // sweeps from Y = -13.5 at base out to Y = -29.25 at Z >= 74.8!

                // Each of the two twin-spar legs is centered at ax_leg(z): from ax = 20.0 at base to ax = 18.0 at head, width hx = 5.2mm, hy = 4.6mm
                double legCenterX = 20.5 - 2.0 * tSmooth;
                double dLeg2D = SdfMesher.Box2D(ax - legCenterX, y - towerCenterY, 5.4, 4.6, 2.2);
                double legs3D = Math.Max(dLeg2D, Math.Max(0.0 - z, z - 86.0));

                // Base lateral tie bar connecting the bottom roots of the two twin-spar legs to the central M5 spine at ax in [14..26], Y in [-18..0], Z in [0..6.8]
                double dRootTie2D = SdfMesher.Box2D(ax - 20.5, y + 6.5, 6.5, 11.5, 3.0);
                double rootTie3D = Math.Max(dRootTie2D, Math.Max(0.0 - z, z - 6.8));

                // 4) Contoured Stadium Upper Head (centered at X = 0, Z = 93.0, Y = -29.25 -> Y in [-34.0, -24.5], 9.5mm thick)
                // Hugs the 48 x 26mm TPU Vibration Isolator Puck with a sleek 53.0 x 31.0mm rounded stadium capsule (hx = 26.5, hz = 15.5, r = 8.0)!
                double dHeadXZ = SdfMesher.Box2D(x, z - 93.0, 26.5, 15.5, 8.0);
                double dHeadY = Math.Abs(y - (-29.25)) - 4.75;
                double head3D = Math.Max(dHeadXZ, dHeadY);

                // 5) Parabolic Open-Truss Gusset Ribs at ax = 20.5 (thickness 5.2mm in X: hx = 2.6mm)
                // Braces from (Y = +6.5, Z = 6.8) up to (Y = -25.0, Z = 78.0) with a concave parabolic front profile!
                double dGussetX = Math.Abs(ax - legCenterX) - 2.6;
                double uG = Math.Max(0.0, Math.Min(1.0, (z - 6.8) / 71.2));
                // Parabolic front chord of gusset: curves inward gracefully!
                double yFrontChord = 6.5 * (1.0 - uG) + (-25.0) * uG - 6.5 * uG * (1.0 - uG);
                double dGussetFront = y - yFrontChord;
                double dGussetBack = (towerCenterY - 3.0) - y;
                double gussetSolid3D = Math.Max(Math.Max(dGussetX, Math.Max(dGussetFront, dGussetBack)), Math.Max(0.0 - z, z - 78.0));

                // Carve an open triangular truss window out of the interior of each gusset (leaving sturdy 4.8mm outer tension/compression chords!)
                double dTrussFront = (yFrontChord - 4.8) - y;
                double dTrussBack = y - (towerCenterY + 4.2);
                double dTrussBot = 12.5 - z;
                double dTrussTop = z - 58.0;
                double gussetWindow = Math.Max(Math.Max(dTrussFront, dTrussBack), Math.Max(dTrussBot, dTrussTop));
                double gusset3D = Math.Max(gussetSolid3D, -gussetWindow);

                // Smoothly blend the skeletal structure together
                double body = SdfMesher.SmoothMin(baseSpine3D, pod3D, 2.5);
                body = SdfMesher.SmoothMin(body, rootTie3D, 2.5);
                body = SdfMesher.SmoothMin(body, legs3D, 2.5);
                body = SdfMesher.SmoothMin(body, head3D, 3.0);
                body = SdfMesher.SmoothMin(body, gusset3D, 1.8);
                body = Math.Max(body, 0.0 - z);

                // Subtract features:
                double cut = 1e9;

                // (A) Concave U-Trough in Lower Prop-Guard Saddles (centered at ax = 38.0, Y = +6.8, Z = 15.0)
                // Drops the prop-guard resting height by 7.0mm to match Z_center = 93.0mm!
                double dySaddle = y - 6.8;
                double dzSaddle = z - 15.0;
                double dTrough = Math.Max(Math.Sqrt(dySaddle * dySaddle + dzSaddle * dzSaddle * 1.4) - 8.2, Math.Abs(ax - 38.0) - 7.6);
                double dSaddlePin = Math.Max(Math.Sqrt((ax - 38.0) * (ax - 38.0) + (y - 6.8) * (y - 6.8)) - 2.15, Math.Max(3.5 - z, z - 11.0));
                cut = Math.Min(cut, Math.Min(dTrough, dSaddlePin));

                // (B) Recessed 3.5mm Deep Anti-Rotation Pocket on Stadium Head (Y in [-28.0, -24.0], centered at X = 0, Z = 93.0)
                // Captures the 48.0 x 26.0 x 7.5mm TPU Vibration Isolator Puck (04a)
                double dPuckPocket2D = SdfMesher.Box2D(x, z - 93.0, 24.3, 13.3, 4.1);
                double dPuckPocket3D = Math.Max(dPuckPocket2D, -28.0 - y);
                cut = Math.Min(cut, dPuckPocket3D);

                // (C) Standard 64.0mm M5 Bottle Cage Boss Slots (X in [-39, -25] and [+25, +39], Y = 0)
                // Through-slot width = 5.40mm (Z in [0, 6.8]); Top counterbore channel (Z in [3.8, 7.5]) width = 10.20mm (accepts M5 washers or 05b PETG slot plates!)
                double dSlotM5 = SdfMesher.SlotX2D(ax, y, 25.0, 39.0, 0.0, 2.70);
                double dCboreM5 = Math.Max(SdfMesher.SlotX2D(ax, y, 25.0, 39.0, 0.0, 5.10), 3.8 - z);
                cut = Math.Min(cut, Math.Min(dSlotM5, dCboreM5));

                // (D) Oval Lightening Window in Center of Base Spine between the two M5 slots (X in [-10.5, +10.5], Y in [-3.8, +3.8])
                double dBaseWin = SdfMesher.Box2D(x, y, 10.5, 3.8, 3.8);
                cut = Math.Min(cut, dBaseWin);

                // (E) Sideways Cradle + TPU Isolator Mounting Pattern on Stadium Head (Y in [-34.0, -28.0], centered at X = 0.0, Z = 93.0mm)
                // 4-bolt 15.0 x 15.0 mm square (X = +-7.50, Z = 93.0 +- 7.50 = {85.5, 100.5}) + 2-bolt 30.0mm centerline (X = +-15.0, Z = 93.0)
                // Captive M4 hex nut channels on back face (Y in [-34.5, -30.5], leaving a 2.5mm solid clamping wall at Y in [-30.5, -28.0]!)
                cut = Math.Min(cut, M4SlotYAndHexNutSdf(ax, y, z, 7.50, 85.50, 1.0, -34.5, -30.5));
                cut = Math.Min(cut, M4SlotYAndHexNutSdf(ax, y, z, 7.50, 100.50, 1.0, -34.5, -30.5));
                cut = Math.Min(cut, M4SlotYAndHexNutSdf(ax, y, z, 15.00, 93.00, 1.0, -34.5, -30.5));

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh(
                "Orbea_Wild_Downtube_Sleek_SideMount_LTower",
                sdf,
                new Vec3(-48.5, -35.5, -0.6),
                new Vec3(48.5, 18.0, 110.0),
                step);
        }

        // 2f. Archived Fidlock-Compatible Side-Mount Downtube L-Tower (kept as optional 02f variant)
        public static MeshData BuildDowntubeFidlockSideMountLTower(double step = 0.45)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);

                // 1) Horizontal Downtube Base: X in [-56, +56], Y in [-24, +23], Z in [0, 14]
                double basePlate = SdfMesher.RoundedBox3D(x, y + 0.5, z - 7.0, 56.0, 23.5, 7.0, 5.0);

                // 2) Angled-Outward L-Tower Wall:
                // At base (Z <= 14): centered at Y = -19.0 (Y in [-24.0, -14.0]).
                // From Z = 14 to Z = 68: angles outward smoothly to Y_center = -29.5 (Y in [-34.5, -24.5], 10.0mm thick structural head!).
                // Above Z = 68 (the cradle + TPU isolator mounting zone Z in [68, 120]): vertical at Y in [-34.5, -24.5]!
                double tAngle = Math.Max(0.0, Math.Min(1.0, (z - 14.0) / 54.0));
                // Smooth cubic hermite blend for extra-clean print transitions
                double tSmooth = tAngle * tAngle * (3.0 - 2.0 * tAngle);
                double towerCenterY = -19.0 - 10.5 * tSmooth; // goes from -19.0 at Z=14 out to -29.5 at Z>=68!

                double zClamped = Math.Max(14.0, Math.Min(68.0, z));
                double towerHalfX = 42.0 - (zClamped - 14.0) * (16.5 / 54.0); // tapers from 42mm to 25.5mm at Z>=68
                double tower2D = SdfMesher.Box2D(x, y - towerCenterY, towerHalfX, 5.0, 2.5);
                double tower3D = Math.Max(tower2D, Math.Max(0.0 - z, z - 121.0));
                if (z > 113.0 && ax > 17.5)
                {
                    double rx = ax - 17.5;
                    double rz = z - 113.0;
                    double dCorner = Math.Sqrt(rx * rx + rz * rz) - 8.0;
                    tower3D = Math.Max(tower3D, dCorner);
                }

                // 3) Twin Structural Gusset Ribs at ax in [17.5, 23.5] (bracing the angled tower to the base, completely clear of the Y=0 M5 bottle slots!)
                double dGussetX = Math.Abs(ax - 20.5) - 3.0;
                // Inner edge of gusset sweeps from (Y = +16, Z = 14) to (Y = -24.5, Z = 76)
                double dGussetDiag = (62.0 * (y + 24.5) + 40.5 * (z - 76.0)) / Math.Sqrt(62.0 * 62.0 + 40.5 * 40.5);
                double dGussetBack = (towerCenterY - 4.5) - y;
                double gusset3D = Math.Max(Math.Max(dGussetX, Math.Max(dGussetDiag, dGussetBack)), Math.Max(2.0 - z, z - 80.0));

                // 4) Dual Lower Prop-Guard Saddle Rests at ax in [28.0, 48.0], centered at Y = +6.8mm (exact prop-guard centerline when 7.5mm TPU puck is used!)
                double saddleBlock = SdfMesher.RoundedBox3D(ax - 38.0, y - 6.8, z - 15.0, 9.5, 13.0, 6.0, 3.0);

                double body = SdfMesher.SmoothMin(basePlate, tower3D, 2.5);
                body = SdfMesher.SmoothMin(body, gusset3D, 1.8);
                body = SdfMesher.SmoothMin(body, saddleBlock, 2.0);
                body = Math.Max(body, 0.0 - z);

                // Subtract features:
                double cut = 1e9;

                // (A) Concave U-Trough in the Lower Prop-Guard Saddles (centered at Y = +6.8, Z = 22.0)
                double dySaddle = y - 6.8;
                double dzSaddle = z - 22.0;
                double dTrough = Math.Max(Math.Sqrt(dySaddle * dySaddle + dzSaddle * dzSaddle * 1.4) - 8.2, Math.Abs(ax - 38.0) - 7.6);
                double dSaddlePin = Math.Max(Math.Sqrt((ax - 38.0) * (ax - 38.0) + (y - 6.8) * (y - 6.8)) - 2.15, Math.Max(10.5 - z, z - 18.0));
                cut = Math.Min(cut, Math.Min(dTrough, dSaddlePin));

                // (B) Recessed 3.5mm Deep Pocket on the Upper Tower Face (Y in [-28.0, -24.0], X in [-24.3, +24.3], Z in [86.7, 113.3])
                // Captures the 48 x 26 x 7.5mm TPU Vibration Isolator Puck (04a) so 3.5mm nests inside the tower and 4.0mm floats the cradle at Y = -20.5mm!
                double dPuckPocket2D = SdfMesher.Box2D(x, z - 100.0, 24.3, 13.3, 4.1);
                double dPuckPocket3D = Math.Max(dPuckPocket2D, -28.0 - y);
                cut = Math.Min(cut, dPuckPocket3D);

                // (C) Underside Fidlock TWIST Bike Base Saddle Channel: X in [-54, +54], Y in [-12.9, +12.9], Z in [-1, 3.2]
                double saddle2D = SdfMesher.Box2D(x, y, 54.0, 12.9, 2.0);
                double snapRib = Math.Max(Math.Max(ax - 12.0, 12.35 - ay), Math.Abs(z - 1.4) - 0.8);
                double saddleCut = Math.Max(saddle2D, z - 3.2);
                saddleCut = Math.Max(saddleCut, -snapRib);
                cut = Math.Min(cut, saddleCut);

                // (D) Twin Fidlock TWIST Undercut Keyhole Sockets at X = -32.0 and X = +32.0
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    double sx0 = sign * 32.0, sy0 = 0.0;
                    double sx1 = sign * 30.7, sy1 = -sign * 8.8;
                    double ux = sx1 - sx0, uy = sy1 - sy0;
                    double ut = Math.Max(0.0, Math.Min(1.0, ((x - sx0) * ux + (y - sy0) * uy) / (ux * ux + uy * uy)));
                    double kx = sx0 + ut * ux;
                    double ky = sy0 + ut * uy;
                    double distArc = Math.Sqrt((x - kx) * (x - kx) + (y - ky) * (y - ky));

                    double dEntry2D = Math.Sqrt((x - sx1) * (x - sx1) + (y - sy1) * (y - sy1)) - 7.1;
                    double dEntry3D = Math.Max(dEntry2D, Math.Max(2.8 - z, z - 7.6));
                    double dUpperKey = Math.Max(distArc - 7.1, Math.Max(5.2 - z, z - 7.6));
                    double dLowerNeck = Math.Max(distArc - 5.0, Math.Max(2.8 - z, z - 5.3));

                    cut = Math.Min(cut, Math.Min(dEntry3D, Math.Min(dUpperKey, dLowerNeck)));
                }

                // (E) Neodymium Magnet Press-Fit Pockets (d = 10.25mm, Z in [3.0, 6.2]) at X = +-19.5mm, Y = 0
                double dMag2D = Math.Sqrt((ax - 19.5) * (ax - 19.5) + y * y) - 5.125;
                double dMag3D = Math.Max(dMag2D, Math.Max(3.0 - z, z - 6.2));
                cut = Math.Min(cut, dMag3D);

                // (F) Standard 64.0mm M5 Bottle Cage Boss Slots (X in [-40, -24] and [+24, +40], Y = 0)
                double dSlotM5 = SdfMesher.SlotX2D(ax, y, 24.0, 40.0, 0.0, 2.70);
                double dCboreM5 = Math.Max(SdfMesher.SlotX2D(ax, y, 24.0, 40.0, 0.0, 5.10), 8.5 - z);
                cut = Math.Min(cut, Math.Min(dSlotM5, dCboreM5));

                // (G) Sideways Cradle + TPU Isolator Mounting Pattern on Upper Vertical Tower Head (Y in [-34.5, -28.0], centered at X = 0.0, Z = 100.0mm)
                // Captive M4 hex nut channels on back face (Y in [-35.5, -30.8], leaving a 2.8mm solid clamping wall at Y in [-30.8, -28.0]!)
                cut = Math.Min(cut, M4SlotYAndHexNutSdf(ax, y, z, 7.50, 92.50, 1.2, -35.5, -30.8));
                cut = Math.Min(cut, M4SlotYAndHexNutSdf(ax, y, z, 7.50, 107.50, 1.2, -35.5, -30.8));
                cut = Math.Min(cut, M4SlotYAndHexNutSdf(ax, y, z, 15.00, 100.00, 1.2, -35.5, -30.8));
                cut = Math.Min(cut, M4SlotYAndHexNutSdf(x, y, z, 0.00, 85.00, 1.0, -35.5, -30.8));
                cut = Math.Min(cut, M4SlotYAndHexNutSdf(x, y, z, 0.00, 115.00, 1.0, -35.5, -30.8));

                // (H) Weight-saving rounded window through the angled lower-mid tower wall (X in [-10.5, +10.5], Z in [28, 66])
                double dTowerWin = Math.Max(SdfMesher.Box2D(x, z - 47.0, 10.5, 17.5, 5.0), Math.Abs(y - towerCenterY) - 8.0);
                cut = Math.Min(cut, dTowerWin);

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh(
                "Orbea_Wild_Downtube_SideMount_LTower",
                sdf,
                new Vec3(-58.0, -36.0, -0.8),
                new Vec3(58.0, 24.5, 122.5),
                step);
        }

        // 2b. TPU Lower Prop-Guard Saddle Cushions (Pair) for the Side-Mount Downtube L-Tower
        public static MeshData BuildDowntubeTpuSaddlePads(double step = 0.25)
        {
            Func<double, double, double, double> singleSaddlePadSdf = (x, y, z) =>
            {
                // Fits into the U-trough at (ax=38, y=6): 14.8mm wide in X, 15.8mm curved width in Y, Z in [2.5, 9.5]
                double outerCyl = Math.Max(Math.Sqrt(y * y + (z - 9.5) * (z - 9.5) * 1.4) - 8.0, Math.Abs(x) - 7.4);
                outerCyl = Math.Max(outerCyl, z - 9.5);
                // Inner U-groove where the DJI Neo 2 prop guard ring sits (r = 5.2mm)
                double innerGroove = Math.Sqrt(y * y + (z - 9.8) * (z - 9.8)) - 5.2;
                double padBody = Math.Max(outerCyl, -innerGroove);
                // Bottom locator pin (r = 2.05mm, Z in [0.0, 4.0])
                double pin = Math.Max(Math.Sqrt(x * x + y * y) - 2.05, Math.Max(0.0 - z, z - 4.0));
                return Math.Min(padBody, pin);
            };

            var single = SdfMesher.ExtractMesh("Single_Saddle_TPU_Pad", singleSaddlePadSdf, new Vec3(-9, -9, -0.5), new Vec3(9, 9, 10.2), step);
            var pairMesh = new MeshData("Downtube_SideMount_TPU_PropGuard_Cushions_Pair");
            var p1 = single.Clone("Pad_Front"); p1.Translate(-12.0, 0, 0);
            var p2 = single.Clone("Pad_Rear");  p2.Translate(12.0, 0, 0);
            pairMesh.Append(p1);
            pairMesh.Append(p2);
            return pairMesh;
        }

        // 2d. Original Flat Downtube Mount (kept as optional extra)
        public static MeshData BuildDowntubeFlatFidlockMount(double step = 0.45)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);
                double spine = SdfMesher.RoundedBox3D(x, y, z - 7.0, 56.0, 20.0, 7.0, 6.0);
                double p0x = 18.0, p0y = 12.0, p1x = 38.0, p1y = 44.0;
                double vx = p1x - p0x, vy = p1y - p0y;
                double t = Math.Max(0.0, Math.Min(1.0, ((ax - p0x) * vx + (ay - p0y) * vy) / (vx * vx + vy * vy)));
                double cx = p0x + t * vx, cy = p0y + t * vy;
                double dWing2D = Math.Sqrt((ax - cx) * (ax - cx) + (ay - cy) * (ay - cy)) - (8.5 + 2.5 * t);
                double wing3D = Math.Max(dWing2D, Math.Max(0.0 - z, z - 14.0));
                double body = Math.Max(SdfMesher.SmoothMin(spine, wing3D, 3.0), Math.Max(0.0 - z, z - 14.0));

                double cut = 1e9;
                double saddle2D = SdfMesher.Box2D(x, y, 54.0, 12.9, 2.0);
                double snapRib = Math.Max(Math.Max(ax - 12.0, 12.35 - ay), Math.Abs(z - 1.4) - 0.8);
                cut = Math.Min(cut, Math.Max(Math.Max(saddle2D, z - 3.2), -snapRib));

                for (int sign = -1; sign <= 1; sign += 2)
                {
                    double sx0 = sign * 32.0, sy0 = 0.0, sx1 = sign * 30.7, sy1 = -sign * 8.8;
                    double ux = sx1 - sx0, uy = sy1 - sy0;
                    double ut = Math.Max(0.0, Math.Min(1.0, ((x - sx0) * ux + (y - sy0) * uy) / (ux * ux + uy * uy)));
                    double kx = sx0 + ut * ux, ky = sy0 + ut * uy;
                    double distArc = Math.Sqrt((x - kx) * (x - kx) + (y - ky) * (y - ky));
                    double dEntry3D = Math.Max(Math.Sqrt((x - sx1) * (x - sx1) + (y - sy1) * (y - sy1)) - 7.1, Math.Max(2.8 - z, z - 7.6));
                    double dUpperKey = Math.Max(distArc - 7.1, Math.Max(5.2 - z, z - 7.6));
                    double dLowerNeck = Math.Max(distArc - 5.0, Math.Max(2.8 - z, z - 5.3));
                    cut = Math.Min(cut, Math.Min(dEntry3D, Math.Min(dUpperKey, dLowerNeck)));
                }

                cut = Math.Min(cut, Math.Max(Math.Sqrt((ax - 19.5) * (ax - 19.5) + y * y) - 5.125, Math.Max(3.0 - z, z - 6.2)));
                cut = Math.Min(cut, Math.Min(SdfMesher.SlotX2D(ax, y, 24.0, 40.0, 0.0, 2.70), Math.Max(SdfMesher.SlotX2D(ax, y, 24.0, 40.0, 0.0, 5.10), 8.5 - z)));
                cut = Math.Min(cut, Math.Min(M4HoleAndHexNutSdf(ax, ay, z, 7.50, 7.50, 3.0, 8.5), Math.Max(Math.Sqrt((ax - 7.50) * (ax - 7.50) + (ay - 7.50) * (ay - 7.50)) - 4.15, z - 3.1)));
                cut = Math.Min(cut, M4HoleAndHexNutSdf(ax, y, z, 15.00, 0.00, 3.0, 8.5));
                cut = Math.Min(cut, M4HoleAndHexNutSdf(x, ay, z, 0.00, 15.00, 0.0, 5.5));
                cut = Math.Min(cut, Math.Sqrt((ax - 28.0) * (ax - 28.0) + (ay - 27.5) * (ay - 27.5)) - 4.2);

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh("Orbea_Wild_Downtube_Flat_Fidlock_Mount", sdf, new Vec3(-58, -56, -0.8), new Vec3(58, 56, 14.8), step);
        }

        // 3a. Over-Stem Bridge Plate for 2027 Orbea Wild LT M-Team (OC MC11 40mm Stem)
        public static MeshData BuildStemBridgePlate(double step = 0.40)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);

                double halfX = ay <= 21.0 ? 27.0 : Math.Max(16.0, 27.0 - (ay - 21.0) * 0.55);
                double d2D = SdfMesher.Box2D(x, y, halfX, 41.0, 5.0);

                double zTop = ay <= 20.0 ? 14.0 : (ay >= 24.0 ? 8.5 : 14.0 - (ay - 20.0) * (5.5 / 4.0));
                double zBot = ay >= 23.0 ? 0.0 : (ay <= 19.0 ? 6.0 : 6.0 * (23.0 - ay) / 4.0);

                double body = Math.Max(d2D, Math.Max(zBot - z, z - zTop));

                double cut = 1e9;
                double dWingSlot = SdfMesher.SlotY2D(ax, ay, 9.0, 25.0, 36.0, 2.15);
                double dWingCbore = Math.Max(SdfMesher.SlotY2D(ax, ay, 9.0, 25.0, 36.0, 4.15), 4.5 - z);
                cut = Math.Min(cut, Math.Min(dWingSlot, dWingCbore));

                cut = Math.Min(cut, M4HoleAndHexNutSdf(ax, ay, z, 7.50, 7.50, 5.5, 9.6));
                cut = Math.Min(cut, M4HoleAndHexNutSdf(ax, y, z, 15.00, 0.00, 5.5, 9.6));
                cut = Math.Min(cut, M4HoleAndHexNutSdf(x, ay, z, 0.00, 15.00, 5.5, 9.6));

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh(
                "Orbea_MC11_Stem_Bridge_Plate",
                sdf,
                new Vec3(-29.0, -43.0, -0.6),
                new Vec3(29.0, 43.0, 14.6),
                step);
        }

        // 3b. NEW: Swinging M4 Pin-Hinged Clamshell Handlebar Clamps (ZERO Plastic Bending to Clear 35mm Bar!)
        // Consists of:
        //   - Upper Saddle Half (180 deg top arch + top bridge deck with side-entry captive M4 nuts + Front Double-Knuckle Hinge Fork + Rear Upper Pinch Lug)
        //   - Lower Swinging Jaw Half (180 deg bottom arch + Front Center Hinge Tongue + Rear Lower Pinch Lug)
        // Joined at the front by a permanent M4x20mm Hinge Bolt + Captive M4 Hex Nut so the lower jaw swings open 180 deg with ZERO flexing!
        // Closed at the rear by a single M4x20mm Pinch Bolt + Captive M4 Hex Nut!
        public static MeshData BuildHandlebarHingedClamshellSingle(double boreDiameter, bool assembledClosed, double step = 0.34)
        {
            double rIn = boreDiameter * 0.5;
            double rOut = rIn + 5.5;
            double ySaddleTop = rIn + 6.0;
            double pivotX = -(rIn + 5.2); // Front hinge barrel center X
            double pivotY = 0.0;          // Front hinge barrel center Y
            double pinchX = +(rIn + 6.5); // Rear pinch bolt center X

            // Upper Half SDF
            Func<double, double, double, double> upperHalfSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                double dRing2D = Math.Max(rIn - r, r - rOut);

                // Top flat bridge saddle: X in [-14.5, +14.5], Y in [0, ySaddleTop]
                double dSaddle2D = SdfMesher.Box2D(x, y - (ySaddleTop * 0.5 + 3.0), 14.5, ySaddleTop * 0.5 - 3.0, 2.5);
                dSaddle2D = Math.Max(dSaddle2D, rIn - r);

                // Front hinge boss around (pivotX, 0) with radius 5.2mm, connected to upper arch
                double dHingeCyl2D = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 5.2;
                double dHingeWeb2D = Math.Max(SdfMesher.Box2D(x - (pivotX * 0.5 - 4.0), y - 2.5, 6.0, 4.5, 1.5), rIn - r);
                double dFront2D = SdfMesher.SmoothMin(dHingeCyl2D, dHingeWeb2D, 1.5);

                // Rear upper pinch lug: X in [rIn, pinchX + 5.5], Y in [0.7, 9.5]
                double dRearLug2D = Math.Max(SdfMesher.Box2D(x - (rIn + 5.2), y - 5.1, 6.5, 4.4, 1.8), rIn - r);

                // Upper arch body (Y >= 0.6mm everywhere except front hinge barrel which goes down to Y = -5.2mm)
                double upperMain2D = Math.Max(SdfMesher.SmoothMin(Math.Min(dRing2D, dSaddle2D), dRearLug2D, 1.8), 0.6 - y);
                double profile2D = Math.Min(upperMain2D, dFront2D);
                profile2D = Math.Max(profile2D, rIn - r);

                double body3D = Math.Max(profile2D, Math.Max(0.0 - z, z - 15.0));

                double cut = 1e9;
                // (1) Cut central knuckle slot out of front hinge barrel (Z in [4.75, 10.25], X < -rIn + 0.5, Y < 5.5) so Lower Jaw's center tongue fits inside!
                double dKnuckleSlot = Math.Max(Math.Max(x - (-rIn + 0.5), y - 5.5), Math.Abs(z - 7.5) - 2.75);
                cut = Math.Min(cut, dKnuckleSlot);

                // (2) Front Hinge Pin M4 Bore (at pivotX, pivotY, through Z in [-1, 16]) + Captive M4 Hex Nut Pocket in outer knuckle (Z in [12.0, 15.5])
                double dHingePinBore = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 2.15;
                double dHingeNut = Math.Max(SdfMesher.Hexagon2D(x - pivotX, y - pivotY, 7.18), 12.0 - z);
                cut = Math.Min(cut, Math.Min(dHingePinBore, dHingeNut));

                // (3) Top Saddle Bridge Mounting Holes (X = +-9.0, Z = 7.5) + Side-Entry Captive M4 Hex Nut Slots
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    double bx = sx * 9.0;
                    double dTopBore = Math.Max(Math.Sqrt((x - bx) * (x - bx) + (z - 7.5) * (z - 7.5)) - 2.15, Math.Max((rIn - 1.0) - y, y - (ySaddleTop + 1.0)));
                    double dNutSlot = Math.Max(Math.Max(Math.Abs(x - bx) - 3.60, Math.Abs(y - (ySaddleTop - 4.45)) - 1.75), z - 11.1);
                    cut = Math.Min(cut, Math.Min(dTopBore, dNutSlot));
                }

                // (4) Rear Pinch Bolt Bore (at X = pinchX, Z = 7.5, along Y) + Captive M4 Hex Nut Pocket on top of rear lug (Y in [5.2, 10.5])
                double dPinchBore = Math.Sqrt((x - pinchX) * (x - pinchX) + (z - 7.5) * (z - 7.5)) - 2.15;
                double dPinchHex = Math.Max(SdfMesher.Hexagon2D(x - pinchX, z - 7.5, 7.18), 5.2 - y);
                cut = Math.Min(cut, Math.Min(dPinchBore, dPinchHex));

                return Math.Max(body3D, -cut);
            };

            // Lower Swinging Jaw Half SDF
            Func<double, double, double, double> lowerHalfSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                double dRing2D = Math.Max(rIn - r, r - rOut);

                // Front hinge barrel around (pivotX, 0) with radius 5.2mm
                double dHingeCyl2D = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 5.2;
                double dHingeWeb2D = Math.Max(SdfMesher.Box2D(x - (pivotX * 0.5 - 4.0), y + 2.5, 6.0, 4.5, 1.5), rIn - r);
                double dFront2D = SdfMesher.SmoothMin(dHingeCyl2D, dHingeWeb2D, 1.5);

                // Rear lower pinch lug: X in [rIn, pinchX + 5.5], Y in [-9.5, -0.7]
                double dRearLug2D = Math.Max(SdfMesher.Box2D(x - (rIn + 5.2), y + 5.1, 6.5, 4.4, 1.8), rIn - r);

                // Lower arch body (Y <= -0.6mm everywhere except front center hinge tongue)
                double lowerMain2D = Math.Max(SdfMesher.SmoothMin(dRing2D, dRearLug2D, 1.8), y - (-0.6));
                double lowerMain3D = Math.Max(lowerMain2D, Math.Max(0.0 - z, z - 15.0));

                // Front center hinge tongue only exists for Z in [5.05, 9.95] (4.9mm wide, fits inside the 5.5mm slot of Upper Half!)
                double dTongue3D = Math.Max(Math.Max(dFront2D, rIn - r), Math.Abs(z - 7.5) - 2.45);
                // Outside Z in [5.05, 9.95], trim the front of the lower jaw at X >= pivotX + 5.5 so it clears the upper outer knuckles when swinging!
                lowerMain3D = Math.Max(lowerMain3D, (pivotX + 5.5) - x);

                double body3D = Math.Min(lowerMain3D, dTongue3D);

                double cut = 1e9;
                // (1) Front Hinge Pin M4 Bore at (pivotX, pivotY)
                double dHingePinBore = Math.Sqrt((x - pivotX) * (x - pivotX) + (y - pivotY) * (y - pivotY)) - 2.15;
                cut = Math.Min(cut, dHingePinBore);

                // (2) Rear Pinch Bolt Bore at (pinchX, Z = 7.5) + Socket Head Seat on bottom of rear lug (Y <= -5.2)
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
                // Offset the lower jaw downward by 12.5mm on the print bed so the center hinge tongue is completely clear of the upper knuckles!
                lowerMesh.Translate(0.0, -12.5, 0.0);
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
            var left = single.Clone(meshName + "_L");
            left.Translate(-38.0, 0, 0);
            var right = single.Clone(meshName + "_R");
            right.Translate(38.0, 0, 0);
            pair.Append(left);
            pair.Append(right);
            return pair;
        }

        // 3c. NEW: Single-Bolt Hook-In Clamshell Handlebar Clamps (Front Interlocking Hook + 1 Rear M4 Pinch Bolt, Zero Flexing!)
        public static MeshData BuildHandlebarHookHingeClampsPair(double boreDiameter, string meshName, double step = 0.34)
        {
            double rIn = boreDiameter * 0.5;
            double rOut = rIn + 5.5;
            double ySaddleTop = rIn + 6.0;
            double hookX = -(rIn + 4.8);
            double hookY = 1.5;
            double pinchX = +(rIn + 6.5);

            // Upper Half with Front Downward-Facing C-Hook Receiver
            Func<double, double, double, double> upperHookSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                double dRing2D = Math.Max(rIn - r, r - rOut);
                double dSaddle2D = Math.Max(SdfMesher.Box2D(x, y - (ySaddleTop * 0.5 + 3.0), 14.5, ySaddleTop * 0.5 - 3.0, 2.5), rIn - r);
                double dRearLug2D = Math.Max(SdfMesher.Box2D(x - (rIn + 5.2), y - 5.1, 6.5, 4.4, 1.8), rIn - r);

                // Front C-hook receiver around (hookX, hookY): outer r = 6.2mm, inner socket r = 3.3mm, open throat at bottom-inner so lower hook drops in at 45 deg
                double dHookOuter = Math.Sqrt((x - hookX) * (x - hookX) + (y - hookY) * (y - hookY)) - 6.2;
                double dHookSocket = Math.Sqrt((x - hookX) * (x - hookX) + (y - hookY) * (y - hookY)) - 3.35;
                // Trim bottom of hook outer at y < hookY - 2.2 so lower jaw hooks in from underneath
                dHookOuter = Math.Max(dHookOuter, (hookY - 2.2) - y);

                double upperMain2D = Math.Max(SdfMesher.SmoothMin(Math.Min(dRing2D, dSaddle2D), dRearLug2D, 1.8), 0.6 - y);
                double profile2D = Math.Max(SdfMesher.SmoothMin(upperMain2D, dHookOuter, 1.5), -dHookSocket);
                profile2D = Math.Max(profile2D, rIn - r);

                double body3D = Math.Max(profile2D, Math.Max(0.0 - z, z - 15.0));

                double cut = 1e9;
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

            // Lower Jaw with Front Cylindrical Hook Pin (r = 3.0mm at hookX, hookY)
            Func<double, double, double, double> lowerHookSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                double dRing2D = Math.Max(rIn - r, r - rOut);
                double dRearLug2D = Math.Max(SdfMesher.Box2D(x - (rIn + 5.2), y + 5.1, 6.5, 4.4, 1.8), rIn - r);
                double lowerMain2D = Math.Max(SdfMesher.SmoothMin(dRing2D, dRearLug2D, 1.8), y - (-0.6));

                // Front hook neck + cylindrical pin (r = 3.0mm at hookX, hookY)
                double dPin = Math.Sqrt((x - hookX) * (x - hookX) + (y - hookY) * (y - hookY)) - 3.0;
                double dNeck = SdfMesher.Box2D(x - (hookX + 1.8), y - (hookY - 3.5), 2.4, 3.5, 0.8);
                double profile2D = Math.Max(Math.Min(lowerMain2D, Math.Min(dPin, dNeck)), rIn - r);

                double body3D = Math.Max(profile2D, Math.Max(0.0 - z, z - 15.0));

                double dPinchBore = Math.Sqrt((x - pinchX) * (x - pinchX) + (z - 7.5) * (z - 7.5)) - 2.15;
                double dPinchHead = Math.Max(Math.Sqrt((x - pinchX) * (x - pinchX) + (z - 7.5) * (z - 7.5)) - 4.15, y - (-5.2));
                return Math.Max(body3D, -Math.Min(dPinchBore, dPinchHead));
            };

            var upper = SdfMesher.ExtractMesh("Upper_Hook_Saddle", upperHookSdf,
                new Vec3(hookX - 7.0, -3.5, -0.5), new Vec3(pinchX + 6.5, ySaddleTop + 1.0, 15.5), step);
            var lower = SdfMesher.ExtractMesh("Lower_Hook_Jaw", lowerHookSdf,
                new Vec3(hookX - 4.0, -(rOut + 1.5), -0.5), new Vec3(pinchX + 6.5, hookY + 4.0, 15.5), step);
            lower.Translate(0.0, -8.0, 0.0); // separate on print bed

            var single = new MeshData("HookHinge_Unit");
            single.Append(upper);
            single.Append(lower);

            var pair = new MeshData(meshName);
            var left = single.Clone(meshName + "_L"); left.Translate(-33.0, 0, 0);
            var right = single.Clone(meshName + "_R"); right.Translate(33.0, 0, 0);
            pair.Append(left);
            pair.Append(right);
            return pair;
        }

        // 3d. TPU Split Grip Sleeves Pair for OC MC10 35.0mm Carbon Handlebar
        public static MeshData BuildHandlebarTpuSleeves(double step = 0.28)
        {
            Func<double, double, double, double> singleSleeveSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                bool isFlange = (z <= 1.0 || z >= 16.0);
                double rOuter = isFlange ? 19.5 : 18.5;
                double dRing = Math.Max(17.5 - r, r - rOuter);
                double dSplit = Math.Max(Math.Abs(x) - 2.5, y);
                double d2D = Math.Max(dRing, -dSplit);
                return Math.Max(d2D, Math.Max(0.0 - z, z - 17.0));
            };

            var single = SdfMesher.ExtractMesh(
                "TPU_Sleeve_Single",
                singleSleeveSdf,
                new Vec3(-20.5, -20.5, -0.5),
                new Vec3(20.5, 20.5, 17.5),
                step);

            var pair = new MeshData("Handlebar_35mm_TPU_Grip_Sleeves_Pair");
            var left = single.Clone("Sleeve_L");
            left.Translate(-22.0, 0, 0);
            var right = single.Clone("Sleeve_R");
            right.Translate(22.0, 0, 0);
            pair.Append(left);
            pair.Append(right);
            return pair;
        }

        // 4a. TPU Vibration-Isolator Sandwich Block + 6x Flanged M4 Bolt Isolating Grommets
        // Fits between 01_DJI_Neo2_Universal_Cradle and either 02a (Downtube L-Tower) or 03a (Stem Bridge)
        // Decouples high-frequency trail shock (15-100Hz) with cellular shear ribs + elastomer bolt grommets
        public static MeshData BuildTpuVibrationIsolatorSystem(double step = 0.26, bool includeLooseGrommetsOnPlate = true)
        {
            Func<double, double, double, double> isolatorPadSdf = (x, y, z) =>
            {
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);

                // Main pad footprint: 48.0 x 26.0 x 7.5 mm (Z in [0.0, 7.5]), corner radius 4.0mm
                double body = SdfMesher.RoundedBox3D(x, y, z - 3.75, 24.0, 13.0, 3.75, 4.0);

                // Peripheral compliant bellows groove around mid-height (Z = 3.75mm, depth = 2.0mm) so the pad shears & compresses smoothly!
                double dOuterPerim = SdfMesher.Box2D(x, y, 24.0, 13.0, 4.0);
                double dBellows = Math.Max(-dOuterPerim - 1.8, Math.Abs(z - 3.75) - 1.35);
                // Keep solid bosses around the 6 M4 bolt locations so bolt preload is controlled
                double dBoltBosses = 1e9;
                dBoltBosses = Math.Min(dBoltBosses, Math.Sqrt((ax - 7.5) * (ax - 7.5) + (ay - 7.5) * (ay - 7.5)) - 4.5);
                dBoltBosses = Math.Min(dBoltBosses, Math.Sqrt((ax - 15.0) * (ax - 15.0) + y * y) - 4.5);
                dBoltBosses = Math.Min(dBoltBosses, Math.Sqrt(x * x + (ay - 15.0) * (ay - 15.0)) - 4.5);
                dBellows = Math.Max(dBellows, -dBoltBosses);

                double cut = dBellows;

                // Internal vibration-damping honeycomb/shear cells at center & corners
                double dCellCenter = Math.Max(Math.Sqrt(x * x + y * y) - 3.8, Math.Abs(z - 3.75) - 2.6);
                double dCellMidX = Math.Max(Math.Sqrt((ax - 19.5) * (ax - 19.5) + (ay - 6.8) * (ay - 6.8)) - 2.6, Math.Abs(z - 3.75) - 2.6);
                cut = Math.Min(cut, Math.Min(dCellCenter, dCellMidX));

                // 6 x M4 Bolt Through-Holes (r = 2.15mm)
                double dHoles = 1e9;
                dHoles = Math.Min(dHoles, Math.Sqrt((ax - 7.5) * (ax - 7.5) + (ay - 7.5) * (ay - 7.5)) - 2.15);
                dHoles = Math.Min(dHoles, Math.Sqrt((ax - 15.0) * (ax - 15.0) + y * y) - 2.15);
                cut = Math.Min(cut, dHoles);

                // Top & bottom micro-ribbed contact faces (0.5mm deep concentric/linear compliance ribs)
                double dTopRibs = Math.Max(Math.Abs(Math.Sin(x * 1.3)) - 0.5, Math.Min(z - 0.45, 7.05 - z));
                dTopRibs = Math.Max(dTopRibs, -dBoltBosses);
                cut = Math.Min(cut, dTopRibs);

                return Math.Max(body, -cut);
            };

            // Flanged TPU Bolt-Head Isolator Grommet (isolates metal M4 bolt head from rigid plastic!)
            // ID = 4.2mm (r = 2.1mm), sleeve OD = 6.8mm (r = 3.4mm, fits inside 7.18mm hex pocket!), flange OD = 9.2mm (r = 4.6mm, h = 1.6mm), total height = 4.2mm
            Func<double, double, double, double> grommetSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                double rOut = z <= 1.6 ? 4.5 : 3.35;
                double dRing = Math.Max(2.08 - r, r - rOut);
                return Math.Max(dRing, Math.Max(0.0 - z, z - 4.2));
            };

            var mainPad = SdfMesher.ExtractMesh("TPU_Isolator_Pad", isolatorPadSdf, new Vec3(-25.5, -14.5, -0.5), new Vec3(25.5, 14.5, 8.0), step);
            var singleGrommet = SdfMesher.ExtractMesh("TPU_Bolt_Grommet", grommetSdf, new Vec3(-5.2, -5.2, -0.4), new Vec3(5.2, 5.2, 4.6), 0.22);

            var systemMesh = new MeshData("04a_TPU_Vibration_Isolator_Puck_And_Grommets");
            systemMesh.Append(mainPad);
            if (includeLooseGrommetsOnPlate)
            {
                for (int i = 0; i < 6; i++)
                {
                    var g = singleGrommet.Clone("Grommet_" + i);
                    g.Translate(-20.0 + i * 8.0, -21.0, 0.0);
                    systemMesh.Append(g);
                }
            }
            return systemMesh;
        }

        // 4b. TPU Gimbal Lock Cushion & Downward Sensor Shield
        // Snaps directly into the 20.0 x 22.6 mm front window of 01_DJI_Neo2_Universal_Cradle (X in [+40.5, +60.5], Y in [-11.3, +11.3])
        // and extends forward (X in [+60.5, +70.0]) with soft compliant gimbal side-cheeks and a recessed non-contact lens dome!
        public static MeshData BuildTpuGimbalAndSensorShield(double step = 0.24)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ay = Math.Abs(y);

                // 1) Lower outer sealing flange (Z in [0.0, 1.4], X in [-12.0, +19.0], Y in [-12.8, +12.8])
                double flange = SdfMesher.RoundedBox3D(x - 3.5, y, z - 0.7, 15.5, 12.8, 0.7, 2.5);

                // 2) Snap-in window plug (Z in [1.4, 4.6], X in [-10.0, +10.0] -> 20.0mm long, Y in [-11.3, +11.3] -> 22.6mm wide)
                // With a 0.6mm peripheral retention bead at Z = 4.2mm so it clicks into the 2.78mm thick cradle window and stays captive!
                double bead = (z >= 3.2 && z <= 4.4) ? 0.55 : 0.0;
                double plug = SdfMesher.RoundedBox3D(x, y, z - 3.0, 9.95 + bead, 11.25 + bead, 1.6, 1.5);

                // 3) Upper Gimbal Stabilizer Cradle (X in [+2.0, +18.5], Y in [-11.0, +11.0], Z in [2.8, 12.5])
                // Soft TPU side cheeks that gently hug the outer barrel of the 2-axis gimbal to prevent end-stop hammering!
                double gimbalPillow = SdfMesher.RoundedBox3D(x - 10.2, y, z - 7.6, 8.2, 10.8, 4.9, 3.0);

                double body = Math.Min(Math.Min(flange, plug), gimbalPillow);

                // Subtract features:
                double cut = 1e9;

                // (A) Non-Contact Recessed Camera Lens Dome at (X = +10.2, Y = 0.0, Z in [5.0, 13.5], r = 7.2mm)
                // Guarantees nothing ever touches the glass lens while the soft TPU rim (r = 7.2..10.8mm) cushions the gimbal housing!
                double dLensDome = Math.Max(Math.Sqrt((x - 10.2) * (x - 10.2) + y * y) - 7.2, 5.2 - z);
                cut = Math.Min(cut, dLensDome);

                // (B) Recessed Non-Contact Pocket over the Downward Optical/IR Sensors at (X = -3.8, Y in [-6.5, +6.5], Z in [3.0, 6.0])
                double dSensorPocket = Math.Max(SdfMesher.Box2D(x - (-3.8), y, 4.2, 6.8, 1.5), 3.0 - z);
                cut = Math.Min(cut, dSensorPocket);

                // (C) Compliant flex slots in the gimbal side cheeks so they compress effortlessly when clipping the drone in
                double dFlexSlot = Math.Max(Math.Abs(x - 10.2) - 1.2, Math.Max(8.2 - ay, 6.5 - z));
                cut = Math.Min(cut, dFlexSlot);

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh(
                "04b_TPU_Gimbal_Lock_And_Sensor_Shield",
                sdf,
                new Vec3(-13.5, -14.0, -0.5),
                new Vec3(20.0, 14.0, 13.5),
                step);
        }

        // 4c. TPU Heatsink Window Mud-Guard Louver
        // Snaps into the 19.5 x 22.6 mm rear window (X in [-41.0, -21.5], Y in [-11.3, +11.3]) to block pebbles/mud while venting heat
        public static MeshData BuildTpuHeatsinkLouver(double step = 0.24)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                // Outer lower flange (23.0 x 25.6 x 1.4mm, Z in [0, 1.4])
                double flange = SdfMesher.RoundedBox3D(x, y, z - 0.7, 11.5, 12.8, 0.7, 2.5);
                // Snap-in window plug (19.4 x 22.5 x 3.2mm, Z in [1.4, 4.6]) with peripheral snap bead
                double bead = (z >= 3.2 && z <= 4.4) ? 0.55 : 0.0;
                double plug = SdfMesher.RoundedBox3D(x, y, z - 3.0, 9.70 + bead, 11.25 + bead, 1.6, 1.5);
                double body = Math.Min(flange, plug);

                // Angled ventilation slots (blocks direct mud/rock spray along Z while allowing airflow at 45 deg)
                double cut = 1e9;
                for (int i = -2; i <= 2; i++)
                {
                    double sx = i * 3.4;
                    double shiftedX = x - sx - (z - 2.3) * 0.65; // 33-deg angled louver slot!
                    double dSlot = Math.Max(Math.Abs(shiftedX) - 0.85, Math.Abs(y) - 8.2);
                    cut = Math.Min(cut, dSlot);
                }
                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh(
                "04c_TPU_Heatsink_Mud_Louver",
                sdf,
                new Vec3(-13.0, -14.0, -0.5),
                new Vec3(13.0, 14.0, 5.2),
                step);
        }

        // 01b. Rotate Universal Cradle to its native 47.08-deg self-supporting angle (cos=0.680983722, sin=0.732298553)
        // where the built-in 19.6 x 31.0mm flat pad on the closed cup sits dead-flat at Z = 0.00mm!
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
            foreach (var v in tempVerts)
            {
                rotated.Vertices.Add(new Vec3(v.X, v.Y, v.Z - minZ));
            }
            rotated.Triangles.AddRange(flatCradle.Triangles);
            return rotated;
        }

        // Build the Snap-Off A-Frame Bed-Anchor Stabilization Foot for 45/47-deg Sunlu Easy-PA Printing
        // Connects to the rising outer spine rails (Y = +-15.5mm) via 0.48mm perforated snap-off score tabs!
        public static MeshData BuildCradle45DegSnapOffAnchorFin(double step = 0.38)
        {
            // In rotated coordinates (with minZ shifted to 0):
            // The flat back spine plane is z_spine(x) = 1.075354 * (x + 23.410) for x >= -23.41
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double ay = Math.Abs(y);
                double zSpine = 1.075354 * (x + 23.410);

                // 1) Wide Bed Anchor Base Pad at Z in [0.0, 1.4], X in [-19.5, +28.0], Y in [-24.0, +24.0]
                // Leaves X < -19.5 completely clear of the cradle's own flat heel pad at X in [-43.0, -23.4]!
                double basePad = SdfMesher.RoundedBox3D(x - 4.25, y, z - 0.7, 23.75, 24.0, 0.7, 4.0);
                // Cut central weight-saving window out of base pad (Y in [-11.5, +11.5], X in [-12.0, +18.0])
                double baseCut = SdfMesher.Box2D(x - 3.0, y, 14.0, 11.5, 2.5);
                basePad = Math.Max(basePad, -baseCut);

                // 2) Twin A-Frame Triangular Support Fins under the solid side rails (ay in [14.2, 16.8] -> 2.6mm thick walls, tapering to 0.50mm score tabs at zSpine!)
                double distToSpine = zSpine - z;
                // Above zSpine + 0.05 (inside the cradle spine), do not exist!
                // Within 1.8mm of zSpine, thin the wall from 1.3mm half-thickness down to 0.32mm half-thickness (0.64mm breakaway tab!)
                double halfThickY = distToSpine < 2.2 ? Math.Max(0.32, 0.32 + (distToSpine / 2.2) * 0.98) : 1.30;
                double dFinY = Math.Abs(ay - 15.5) - halfThickY;
                double dFinX = Math.Max(-18.5 - x, x - 25.0);
                // Slanted back edge of fin from (x = -18.5, z = 0) to (x = +25.0, z = zSpine(25))
                // And keep distToSpine >= -0.05 so it just kisses the flat spine at Z = zSpine!
                double dFinZ = Math.Max(0.0 - z, -0.05 - distToSpine);
                double fins = Math.Max(Math.Max(dFinY, dFinX), dFinZ);

                // 3) Diagonal Cross-Brace Strut connecting Left and Right fins at X in [+14.0, +24.0], Z in [0.0, 22.0] for lateral torsional rigidity!
                double dCrossX = Math.Abs(x - 19.0) - 2.0;
                double dCrossY = ay - 16.0;
                double dCrossZ = Math.Max(0.0 - z, z - 20.0);
                double crossBrace = Math.Max(Math.Max(dCrossX, dCrossY), dCrossZ);

                double body = Math.Min(Math.Min(basePad, fins), crossBrace);

                // 4) Perforation windows along the snap-off seam (every 8.5mm along X, right below zSpine: distToSpine in [0.0, 2.4])
                // Creates 4 discrete 4mm-long breakaway tabs per side so the anchor snaps off effortlessly by hand!
                double cut = 1e9;
                for (int i = -1; i <= 2; i++)
                {
                    double cx = -7.0 + i * 9.5;
                    double dPerfX = Math.Abs(x - cx) - 2.4;
                    double dPerfSpine = Math.Abs(distToSpine - 1.1) - 1.3;
                    cut = Math.Min(cut, Math.Max(dPerfX, dPerfSpine));
                }

                // Also add triangular lightening hole in the middle of each side fin
                double dTriHole = Math.Max(Math.Abs(x - 9.0) - 6.5, Math.Max(5.0 - z, (zSpine - 6.0) - z));
                cut = Math.Min(cut, dTriHole);

                return Math.Max(body, -cut);
            };

            return SdfMesher.ExtractMesh(
                "SnapOff_45Deg_Anchor_Fin",
                sdf,
                new Vec3(-21.0, -25.5, -0.5),
                new Vec3(29.5, 25.5, 56.0),
                step);
        }

        // 01c. Mounting Insert for 72D TPU Cradle with Recessed Snap-In Pocket for the Rigid PETG Multi-Nut Carrier Plate (05a)
        public static MeshData BuildCradle72DMountingInsertForPetgCarrier(double step = 0.28)
        {
            Func<double, double, double, double> sdf = (x, y, z) =>
            {
                double w1 = SdfMesher.RoundedBox3D(x - (-4.125), y, z - 1.425, 10.075, 11.50, 1.425, 0.8);
                double w2 = SdfMesher.RoundedBox3D(x - 23.00, y, z - 1.425, 9.95, 11.50, 1.425, 0.8);
                double deck = SdfMesher.RoundedBox3D(x - 9.50, y, z - 4.125, 25.00, 13.20, 1.425, 2.0);
                double body = Math.Min(Math.Min(w1, w2), deck);

                // Subtract:
                // (1) Recessed pocket in upper deck (Z in [1.95, 6.00], X in [-12.0, +31.0] -> hx=21.5 centered at 9.5, Y in [-10.8, +10.8] -> hy=10.8)
                // With a subtle 0.35mm top snap-retention lip at Z in [5.0, 5.55] so the 3.5mm thick PETG carrier plate snaps in and stays captive!
                double snapLip = (z >= 4.95 && z <= 5.55) ? -0.35 : 0.0;
                double dCarrierPocket2D = SdfMesher.Box2D(x - 9.50, y, 21.50 + snapLip, 10.80 + snapLip, 3.0);
                double dCarrierPocket3D = Math.Max(dCarrierPocket2D, 1.95 - z);

                // (2) 6 x M4 Bolt Through-Bores (r = 2.15mm) through the bottom 1.95mm floor
                double holes = 1e9;
                holes = Math.Min(holes, Math.Sqrt((x - 2.00) * (x - 2.00) + (y + 7.50) * (y + 7.50)) - 2.15);
                holes = Math.Min(holes, Math.Sqrt((x - 2.00) * (x - 2.00) + (y - 7.50) * (y - 7.50)) - 2.15);
                holes = Math.Min(holes, Math.Sqrt((x - 17.00) * (x - 17.00) + (y + 7.50) * (y + 7.50)) - 2.15);
                holes = Math.Min(holes, Math.Sqrt((x - 17.00) * (x - 17.00) + (y - 7.50) * (y - 7.50)) - 2.15);
                holes = Math.Min(holes, Math.Sqrt((x + 5.50) * (x + 5.50) + y * y) - 2.15);
                holes = Math.Min(holes, Math.Sqrt((x - 24.50) * (x - 24.50) + y * y) - 2.15);

                return Math.Max(body, -Math.Min(dCarrierPocket3D, holes));
            };

            return SdfMesher.ExtractMesh(
                "Cradle_72D_Pocket_Insert",
                sdf,
                new Vec3(-17.0, -15.0, -0.5),
                new Vec3(36.0, 15.0, 6.2),
                step);
        }

        // 05. Complete PETG Rigid Fastener Reinforcement Insert Kit for 72D TPU Prints:
        //   (A) 05a: Unified 6-Nut Rigid PETG Carrier Plate (42.6 x 21.2 x 3.5 mm) that snaps into 01c (72D TPU Cradle)
        //   (B) 05b: Pair of Rigid PETG M5 Bottle-Boss Slot Load-Spreader Plates (25.6 x 9.8 x 2.4 mm) that drop into 02a's M5 counterbore slots
        //   (C) 05c: Set of 8 Press-In Flanged PETG Hex-Collar M4 Bushings (7.10mm AF hex outer collar + 9.2mm top washer flange + 4.25mm M4 bore)
        //            that press directly into the standard M4 hex pockets of 01 (72D Cradle) or 02a (72D L-Tower)!
        public static MeshData BuildPetgReinforcementInsertKit(double step = 0.24)
        {
            // (A) Unified 6-Nut PETG Cradle Carrier Plate (centered at X = 0, Y = 0, Z in [0.0, 3.5])
            Func<double, double, double, double> carrierPlateSdf = (x, y, z) =>
            {
                double ax = Math.Abs(x);
                double ay = Math.Abs(y);
                double body = SdfMesher.RoundedBox3D(x, y, z - 1.75, 21.30, 10.60, 1.75, 2.8);
                // Subtract 6 x M4 holes + captive M4 hex nut pockets (Z in [1.10, 3.60] -> 2.4mm deep hex trap + 1.1mm solid PETG floor!)
                double holes = 1e9;
                holes = Math.Min(holes, M4HoleAndHexNutSdf(ax, ay, z, 7.50, 7.50, 1.10, 3.80));
                holes = Math.Min(holes, M4HoleAndHexNutSdf(ax, y, z, 15.00, 0.00, 1.10, 3.80));
                return Math.Max(body, -holes);
            };

            // (B) Rigid PETG M5 Bottle-Cage Slot Load-Spreader Insert (fits 02a's 10.2mm wide x 26.2mm long M5 counterbore track!)
            // Length = 25.6mm (capsule from x=-7.9 to +7.9 with r=4.9mm -> 25.6 x 9.8mm), thickness Z in [0.0, 2.4mm], inner M5 slot width = 5.30mm (r=2.65mm)
            Func<double, double, double, double> m5SlotInsertSdf = (x, y, z) =>
            {
                double dOuter = SdfMesher.SlotX2D(x, y, -7.9, 7.9, 0.0, 4.90);
                double dInner = SdfMesher.SlotX2D(x, y, -7.9, 7.9, 0.0, 2.65);
                double d2D = Math.Max(dOuter, -dInner);
                return Math.Max(d2D, Math.Max(0.0 - z, z - 2.40));
            };

            // (C) Press-In Flanged PETG Hex-Collar M4 Bolt Bushing (presses into any standard 7.18mm AF hex pocket in 01 or 02a!)
            // Lower Hex Collar: 7.08mm across flats, Z in [0.0, 3.2]
            // Upper Load-Spreading Flange: d = 9.6mm (r = 4.8mm), Z in [3.2, 4.6]
            // Through-Bore: d = 4.25mm (r = 2.125mm) for M4 bolt
            Func<double, double, double, double> hexBushingSdf = (x, y, z) =>
            {
                double r = Math.Sqrt(x * x + y * y);
                double dOuter = z <= 3.2 ? SdfMesher.Hexagon2D(x, y, 7.08) : (r - 4.75);
                double d2D = Math.Max(dOuter, 2.125 - r);
                return Math.Max(d2D, Math.Max(0.0 - z, z - 4.60));
            };

            var carrierMesh = SdfMesher.ExtractMesh("05a_PETG_Cradle_6Nut_Carrier_Plate", carrierPlateSdf,
                new Vec3(-22.5, -12.0, -0.4), new Vec3(22.5, 12.0, 4.0), step);
            var m5SlotMesh = SdfMesher.ExtractMesh("05b_PETG_M5_Slot_Washer", m5SlotInsertSdf,
                new Vec3(-14.0, -6.0, -0.4), new Vec3(14.0, 6.0, 2.9), 0.22);
            var hexBushMesh = SdfMesher.ExtractMesh("05c_PETG_Hex_Collar_M4_Bushing", hexBushingSdf,
                new Vec3(-5.5, -5.5, -0.4), new Vec3(5.5, 5.5, 5.0), 0.20);

            var kitMesh = new MeshData("05_PETG_Reinforcement_Inserts_For_72D_TPU");
            kitMesh.Append(carrierMesh);

            // Add 2 x M5 Bottle-Slot Load-Spreader Plates above the carrier plate
            var m5L = m5SlotMesh.Clone("M5_Slot_1"); m5L.Translate(-15.0, 19.0, 0.0);
            var m5R = m5SlotMesh.Clone("M5_Slot_2"); m5R.Translate(15.0, 19.0, 0.0);
            kitMesh.Append(m5L);
            kitMesh.Append(m5R);

            // Add 8 x Press-In Flanged Hex-Collar M4 Bushings below the carrier plate
            for (int i = 0; i < 8; i++)
            {
                var b = hexBushMesh.Clone("Hex_Bush_" + i);
                b.Translate(-38.5 + i * 11.0, -19.0, 0.0);
                kitMesh.Append(b);
            }

            return kitMesh;
        }
    }
}
