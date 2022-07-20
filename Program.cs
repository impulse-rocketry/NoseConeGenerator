// Copyright 2022 Ben Voß
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files
// (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.CommandLine;
using System.Text.Json;
using ImpulseRocketry.GCode;
using ImpulseRocketry.NoseConeGenerator.Config;
using ImpulseRocketry.Units.Json;

namespace ImpulseRocketry.NoseConeGenerator;

public class Program {

    public static int Main(string[] args) {
        var parametersOption = new Option<string>("-p", "Parameters file") {
            IsRequired = true
        };

        var cmd = new RootCommand();
        cmd.SetHandler((file) => Run(file), parametersOption);
        cmd.AddOption(parametersOption);

        return cmd.Invoke(args);
    }

    private static int Run(string fileName) {
        if (!File.Exists(fileName)) {
            Console.Error.WriteLine("Parameters file not found");
            return 1;
        }

        using var file = File.OpenRead(fileName);

        var options = new JsonSerializerOptions {
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new UnitJsonConverterFactory());
        var parameters = JsonSerializer.Deserialize<Parameters>(file, options);

        if (parameters is null) {
            Console.Error.WriteLine("Parameters file is empty");
            return 1;
        }

        // 3D Printers work in millimeters
        var d = parameters.Diameter.To.Mm;
        var heightRatio = parameters.Ratio;
        var cx = parameters.BuildPlateWidth.To.Mm / 2;
        var cy = parameters.BuildPlateWidth.To.Mm / 2;
        var shapeParameter = parameters.ShapeParameter;
        var resolution = parameters.Resolution.To.Mm;
        var layerHeight = parameters.LayerHeight.To.Mm;
        var filamentDiameter = parameters.FilamentDiameter.To.Mm;
        var wallThickness = parameters.WallThickness.To.Mm;
        var bedTemperature = parameters.BedTemperature.To.C;
        var extruderTemperature = parameters.ExtruderTemperature.To.C;
        var baseHeight = parameters.BaseHeight.To.Mm;
        var shape = parameters.Shape;

        var writer = new GCodeWriter(Console.Out);

        writer.Comment("FLAVOR:Marlin");
        writer.Comment($"Layer height: {layerHeight}");
        writer.Comment($"Generated with Impulse Rocketry Nose Cone Generator");
        writer.Comment($"Diameter: {d}");
        writer.Comment($"Height Ratio: {heightRatio}");
        writer.Comment($"Shape Parameter: {shapeParameter}");
        writer.Comment($"Wall Thickness: {wallThickness}");
        writer.Comment($"Base Height: {baseHeight}");
        writer.Comment($"Resolution: {resolution}");
        writer.Comment($"Filament Diameter: {filamentDiameter}");
        writer.Comment($"Bed Temperature: {bedTemperature}");
        writer.Comment($"Extruder Temperature: {extruderTemperature}");

        writer.MillimeterUnits();
        writer.SetTemperatureUnits(c:true);

        // Heat the bed
        writer.SetBedTemperature(s:bedTemperature);
        writer.ReportTemperatures();
        writer.WaitForBedTemperature(s:bedTemperature);

        // Heat the extruder
        writer.SetHotendTemperature(s:extruderTemperature);
        writer.ReportTemperatures();
        writer.WaitForHotendTemperature(s:extruderTemperature);

        // Reset the extruder
        writer.AbsoluteExtrusionMode("Absolute extrusion mode");
        writer.SetPosition(e:0, comment:"Reset extruder");

        // Level the bed
        writer.AutoHome(comment:"Auto home all axes");
        writer.BedLeveling(comment:"Auto bed level");

        // Wipe the extruder nozzle
        writer.LinearMoveAndExtrude(z: 2, f:3000, comment:"Move Z Axis up little to prevent scratching of Heat Bed");
        writer.LinearMoveAndExtrude(x: 0.1, y: 20, z:0.3, f:5000, comment:"Move to start position");
        writer.LinearMoveAndExtrude(x: 0.1, y:200, z:0.3, f:1500, e:15, comment:"Draw the first line");
        writer.LinearMoveAndExtrude(x: 0.4, y:200, z:0.3, f:5000, comment:"Move to side a little");
        writer.LinearMoveAndExtrude(x: 0.4, y:20, z:0.3, f:1500, e:30, comment:"Draw the second line");
        writer.SetPosition(e:0, comment:"Reset extruder");
        writer.LinearMoveAndExtrude(z:2, f:3000, comment:"Move Z Axis up little to prevent scratching the Heat Bed");
        writer.LinearMoveAndExtrude(x:5, y:20, z:0.3, f:5000, comment:"Move over to prevent blob squish");
        writer.SetPosition(e:0, comment:"Reset extruder");
        writer.LinearMoveAndExtrude(f:1500, e:-6.5);

        var filamentArea = AreaOfCircle(filamentDiameter / 2);
        var fillArea = layerHeight * wallThickness;
        var extrusionRatio = fillArea / filamentArea;

        // Take account of the thickness of the walls when determinging the radius.  The
        // diameter is the external diameter and we need to measure to the middle of the wall
        var r = (d - wallThickness) / 2;
        var coneHeight = heightRatio * r * 2;
        var coneLayerCount = (int)Math.Round(coneHeight / layerHeight);
        var cylinderLayerCount = (int)Math.Round(baseHeight / layerHeight);

        writer.Comment($"LAYER_COUNT: {cylinderLayerCount + coneLayerCount}");
        
        // Move printer head to the starting point
        writer.FanOff();
        writer.LinearMoveAndExtrude(f:1500, e:0);
        
        var (x, y) = PointOnCircle(cx, cy, r+8, 0);
        writer.LinearMove(f:7500, x:x, y:y, z:layerHeight + 0.05);
        var prevX = x;
        var prevY = y;
        var prevZ = layerHeight;

        var e = 0.0;
        var zBase = layerHeight;

        // Skirt Circles
        (prevX, prevY, prevZ, e) = PrintCircleLayer(e, extrusionRatio, prevX, prevY, prevZ, cx, cy, r + 8, zBase, resolution, writer);
        (prevX, prevY, prevZ, e) = PrintCircleLayer(e, extrusionRatio, prevX, prevY, prevZ, cx, cy, r + 6, zBase, resolution, writer);

        // Print cylindrical base tube
        for (var layerNumber = 0; layerNumber < cylinderLayerCount; layerNumber++) {
            writer.Comment($"LAYER: {layerNumber}")
                .Comment("TYPE:WALL-OUTER");

            // Increase the fan speed over the first few layers
            if (layerNumber == 0) {
                writer.SetFanSpeed(s:85);
            } else if (layerNumber == 1) {
                writer.SetFanSpeed(s:170);
            } else if (layerNumber == 2) {
                writer.SetFanSpeed(s:255);
            }

            // Each layer has an integer number of steps so we do not accumulate rounding errors
            var c = 2 * Math.PI * r;
            var numSteps = (int)(c / resolution);

            for (var stepNumber = 0; stepNumber < numSteps; stepNumber ++) {
                // How many degrees around the circumference we are on this layer
                var a = 360.0 / numSteps * stepNumber;

                // Determine how far along the z-axis we are
                var z = layerNumber * layerHeight + (layerHeight / 360.0 * a);
                z += zBase;

                // Determine the point on the circle
                (x, y) = PointOnCircle(cx, cy, r, a);

                // Add filament
                var dist = DistanceBetweenPoints(x, y, z, prevX, prevY, prevZ);
                e += extrusionRatio * dist;

                writer.LinearMoveAndExtrude(f:600, x:x, y:y, z:z, e:e);
                prevX = x;
                prevY = y;
                prevZ = z;
            }
        }

        zBase = 5 + layerHeight;

        // Nosecode is printed in a continious spiral to avoid steps between layers and provide
        // the smoothest possible printed surface.
        var spiralLayerCount = 0;
        double spiralAngle = 0;
        double coneZ = 0;
        
        while (coneHeight - coneZ > 0) {

            var spiralRadius = shape switch
            {
                "Haack" => Haack(shapeParameter, coneHeight, r, coneHeight - coneZ),
                "TangentOgive" => TangentOgive(coneHeight, r, coneHeight - coneZ),
                "Elliptical" => Elliptical(coneHeight, r, coneZ),
                _ => throw new ArgumentException("Unknown shape parameter")
            };

            // Determine the point on the circle
            (x, y) = PointOnCircle(cx, cy, spiralRadius, spiralAngle);

            // Calculate the amount of filament needed for the distance being moved
            e += extrusionRatio * DistanceBetweenPoints(x, y, coneZ + zBase, prevX, prevY, prevZ);

            // Generate the GCode to move and extrude
            writer.LinearMoveAndExtrude(x:x, y:y, z:coneZ + zBase, e:e);
            prevX = x;
            prevY = y;
            prevZ = coneZ + zBase;

            // As the radius gets smaller the number of steps required gets fewer so
            // calculate the angle around the circumference to move on the next line segment
            spiralAngle += 360.0 / (CircumferenceOfCircle(spiralRadius) / resolution);

            // Every full circle generates a new "layer" comment so that slicers can display
            // the result nicely.
            if (spiralAngle > 360) {
                spiralAngle -= 360;
                spiralLayerCount ++;
                
                writer.Comment($"LAYER: {spiralLayerCount + cylinderLayerCount}")
                    .Comment("TYPE:WALL-OUTER");
            }

            coneZ = layerHeight * spiralLayerCount + (layerHeight / 360.0 * spiralAngle);
        }

        // End of print lift head
        writer.RelativePositioning();
        writer.LinearMoveAndExtrude(e:-2, f:2700, comment: "Retract filament a bit");
        writer.LinearMoveAndExtrude(e:-2, z:10, f:2400, comment: "Retract and raise z");
        writer.LinearMove(x:5, y:5, f:3000);
        writer.AbsolutePositioning();

        writer.LinearMove(x:0, y:220, comment:"Present print");

        writer.FanOff(comment:"Turn off fan");
        writer.SetHotendTemperature(s:0, comment:"Turn off hot end");
        writer.SetBedTemperature(s:0, comment:"Turn off bed");
        writer.DisableSteppers(e:true, x:true, y:true, z:false, comment:"Disable all steppers except Z");

        writer.Comment($"Filament used: {e / 1000:0.#####}m");

        return 0;
    }

    private static (double, double, double, double) PrintCircleLayer(double e, double extrusionRatio, double prevX, double prevY, double prevZ, double cx, double cy, double r, double z, double resolution, GCodeWriter writer) {
        // Each layer has an integer number of steps so we do not accumulate rounding errors
        var c = 2 * Math.PI * r;
        var numSteps = (int)(c / resolution);

        for (var stepNumber = 0; stepNumber < numSteps; stepNumber ++) {
            // How many degrees around the circumference we are on this layer
            var a = 360.0 / numSteps * stepNumber;

            // Determine the point on the circle
            var (x, y) = PointOnCircle(cx, cy, r, a);

            // Add filament
            var dist = DistanceBetweenPoints(x, y, z, prevX, prevY, prevZ);
            e += extrusionRatio * dist;

            writer.LinearMoveAndExtrude(f:600, x:x, y:y, z:z, e:e);
            prevX = x;
            prevY = y;
            prevZ = z;
        }

        return (prevX, prevY, prevZ, e);
    }

    private static (double x, double y) PointOnCircle(double cx, double cy, double r, double a) {
        var aRad = a * Math.PI / 180.0;

        var x = cx + r * Math.Cos(aRad);
        var y = cy + r * Math.Sin(aRad);

        return new (x, y);
    }

    private static double AreaOfCircle(double r) {
        return Math.PI * r * r;
    }

    private static double CircumferenceOfCircle(double r) {
        return 2 * Math.PI * r;
    }

    private static double DistanceBetweenPoints(double x1, double y1, double z1, double x2, double y2, double z2) {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2) +  Math.Pow(z2 - z1, 2));
    }

    private static double Haack(double C, double L, double R, double x) {
        var theta = Math.Acos(1 - 2 * x / L);
        return R / Math.Sqrt(Math.PI) * Math.Sqrt(theta - (Math.Sin(2 * theta) / 2) + C * Math.Pow(Math.Sin(theta), 3));
    }

    private static double TangentOgive(double L, double R, double x) {
        var p = (Math.Pow(R, 2) + Math.Pow(L, 2)) / (2 * R);
        return Math.Sqrt(Math.Pow(p, 2) - Math.Pow(L - x, 2)) + R - p;
    }

    private static double Elliptical(double L, double R, double x) {
        return R * Math.Sqrt(1 - (Math.Pow(x, 2) / Math.Pow(L, 2)));
    }
}
