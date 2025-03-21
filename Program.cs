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
using System.Text;
using System.Text.Json;
using ImpulseRocketry.GCode;
using ImpulseRocketry.Maths;
using ImpulseRocketry.NoseConeGenerator.Config;
using ImpulseRocketry.Units.Json;

namespace ImpulseRocketry.NoseConeGenerator;

public class Program {

    public static int Main(string[] args) {
        var parametersOption = new Option<string>("-p", "Parameters file") {
            IsRequired = true
        };

        var outputFileOption = new Option<string>("-o", "Output file") {
            IsRequired = false
        };

        var cmd = new RootCommand();
        cmd.SetHandler((parametersFileName, outputFile) => Run(parametersFileName, outputFile), parametersOption, outputFileOption);
        cmd.AddOption(parametersOption);
        cmd.AddOption(outputFileOption);

        return cmd.Invoke(args);
    }

    private static int Run(string parametersFileName, string? outputFile) {
        if (!File.Exists(parametersFileName)) {
            Console.Error.WriteLine("Parameters file not found");
            return 1;
        }

        using var parametersFileStream = File.OpenRead(parametersFileName);

        var options = new JsonSerializerOptions {
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };
        options.Converters.Add(new UnitJsonConverterFactory());
        var parameters = JsonSerializer.Deserialize<Parameters>(parametersFileStream, options);

        if (parameters is null) {
            Console.Error.WriteLine("Parameters file is empty");
            return 1;
        }

        var outputStream = Console.Out;
        if (outputFile is not null) {
            var file = File.Open(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
            outputStream = new StreamWriter(file, Encoding.UTF8);
        }

        var writer = new GCodeWriter(outputStream);

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

        writer.Comment("FLAVOR:Marlin");
        writer.Comment($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.Comment($"Layer height: {layerHeight}");
        writer.Comment($"Generated with Impulse Rocketry Nose Cone Generator");
        writer.Comment($"Diameter: {d}");
        writer.Comment($"Height Ratio: {heightRatio}");
        writer.Comment($"Shape: {shape}");
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

        var filamentArea = Circle.Area(filamentDiameter / 2);
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

        var extruder = new Extruder(writer, resolution);
        double x;
        double y;
        if (parameters.Brim.To.Mm >= 0) {
            // Brim Circles
            writer.Comment("TYPE:BRIM");
            writer.LinearMoveAndExtrude(f:1500, e:0);

            var t = wallThickness * .7;
            var brimRadius = (parameters.Brim.To.Mm / 2) + (wallThickness / 2);
            var numRings = (int)(brimRadius / t);

            // Move to the first circle
            (x, y) = Circle.Point(cx, cy, r + (numRings * t), 0);
            writer.LinearMove(f:7500, x:x, y:y, z:layerHeight + 0.05);

            for (var ringNumber = numRings; ringNumber > 0; ringNumber--) {
                extruder.CircleLayer(extrusionRatio, cx, cy, r + (ringNumber * t), layerHeight);
            }
        } else {
            // Skirt Circles
            writer.Comment($"TYPE:SKIRT");
            writer.LinearMoveAndExtrude(f:1500, e:0);
            
            (x, y) = Circle.Point(cx, cy, r+8, 0);
            writer.LinearMove(f:7500, x:x, y:y, z:layerHeight + 0.05);

            extruder.CircleLayer(extrusionRatio, cx, cy, r + 8, layerHeight);
            extruder.CircleLayer(extrusionRatio, cx, cy, r + 6, layerHeight);
        }

        (x, y) = Circle.Point(cx, cy, r, 0);
        extruder.MoveTo(x, y, layerHeight);

        // Base tube and nosecone is printed in a continious spiral to avoid steps between layers and provide
        // the smoothest possible printed surface.
        for (var layerNumber = 0; layerNumber < cylinderLayerCount; layerNumber++) {
            writer.Comment($"LAYER: {layerNumber + 1}")
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
            var numSteps = (int)(Circle.Circumference(r) / resolution);

            for (var stepNumber = 0; stepNumber < numSteps; stepNumber ++) {
                // How many degrees around the circumference we are on this layer
                var a = 360.0 / numSteps * stepNumber;

                // Determine how far along the z-axis we are
                var z = (layerNumber + 1) * layerHeight + (layerHeight / 360.0 * a);

                // Determine the point on the circle
                (x, y) = Circle.Point(cx, cy, r, a);

                // Draw the line segment
                extruder.LineTo(x, y, z, extrusionRatio);
            }
        }

        var spiralLayerCount = 0;
        double spiralAngle = 0;
        double coneZ = 0;

        var zBase = (cylinderLayerCount + 1) * layerHeight;
        
        writer.Comment($"LAYER: {spiralLayerCount + cylinderLayerCount + 1}")
              .Comment("TYPE:WALL-OUTER");

        while (coneHeight - coneZ > 0) {

            var spiralRadius = shape switch
            {
                "PowerSeries" => PowerSeries(shapeParameter, coneHeight, r, coneHeight - coneZ),
                "Conic" => Conic(coneHeight, r, coneHeight - coneZ),
                "Haack" => Haack(shapeParameter, coneHeight, r, coneHeight - coneZ),
                "TangentOgive" => TangentOgive(coneHeight, r, coneHeight - coneZ),
                "Elliptical" => Elliptical(coneHeight, r, coneZ),
                "Parabolic" => Parabolic(shapeParameter, coneHeight, r, coneHeight - coneZ),
                _ => throw new ArgumentException("Unknown shape parameter")
            };

            // Determine the point on the circle
            (x, y) = Circle.Point(cx, cy, spiralRadius, spiralAngle);

            // When we get close to the top we need to extrude less because the thickness of the line
            // is larger than the radius
            if (coneZ + 8 > coneHeight) {
                // Extrude between 0% and 50% less as we get closer to the top of the cone
                var hx = coneHeight - coneZ;
                var ratio = (4 / hx) + .5;
                fillArea = layerHeight * wallThickness / ratio;
            } else {
                fillArea = layerHeight * wallThickness;
            }

            extrusionRatio = fillArea / filamentArea;

            // Draw the line segment
            extruder.LineTo(x, y, coneZ + zBase, extrusionRatio);

            // As the radius gets smaller the number of steps required gets fewer so
            // calculate the angle around the circumference to move on the next line segment
            spiralAngle += 360.0 / (Circle.Circumference(spiralRadius) / resolution);

            // Every full circle generates a new "layer" comment so that slicers can display
            // the result nicely.
            if (spiralAngle > 360) {
                spiralAngle -= 360;
                spiralLayerCount ++;
                
                writer.Comment($"LAYER: {spiralLayerCount + cylinderLayerCount + 1}")
                      .Comment("TYPE:WALL-OUTER");
            }

            // Increase the fan speed over the first few layers
            if (cylinderLayerCount + spiralLayerCount == 0) {
                writer.SetFanSpeed(s:85);
            } else if (cylinderLayerCount + spiralLayerCount == 1) {
                writer.SetFanSpeed(s:170);
            } else if (cylinderLayerCount + spiralLayerCount == 2) {
                writer.SetFanSpeed(s:255);
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

        writer.Comment($"Filament used: {extruder.E / 1000:0.#####}m");

        if (outputFile is not null) {
            outputStream.Close();
        }

        return 0;
    }

    private static double Conic(double L, double R, double x) {
        return x * R / L;
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

    private static double Parabolic(double K, double L, double R, double x) {
        return R * ((2 * (x / L) - K * Math.Pow (x / L, 2)  ) / (2 - K));
    }

    private static double PowerSeries(double n, double L, double R, double x) {
        return R * Math.Pow(x / L, n);
    }
}
