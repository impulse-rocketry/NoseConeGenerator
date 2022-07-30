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

using ImpulseRocketry.GCode;
using ImpulseRocketry.Maths;

namespace ImpulseRocketry.NoseConeGenerator;

internal class Extruder {
    private readonly GCodeWriter _writer;
    private double _x;
    private double _y;
    private double _z;
    private double _e;

    public Extruder(GCodeWriter writer, double resolution) {
        _writer = writer;
        Resolution = resolution;
    }

    public double X => _x;
    public double Y => _y;
    public double Z => _z;
    public double E => _e;
    public double Resolution { get; set; }

    public void MoveTo(double x, double y, double z) {
        _writer.LinearMoveAndExtrude(f:600, x:x, y:y, z:z, e:_e);
        _x = x;
        _y = y;
        _z = z;
    }

    public void LineTo(double x, double y, double z, double extrusionRatio) {
        // Add filament
        _e += extrusionRatio * Line.DistanceBetweenPoints(x, y, z, _x, _y, _z);

        MoveTo(x, y, z);
    }

    public void ArcLayer(double startAngle, double endAngle, double cx, double cy, double r, double z, double extrusionRatio) {
        // How many degrees are we moving
        var angle = Math.Abs(endAngle - startAngle);
        var c = 2 * Math.PI * r / 360.0 * angle;

        // Each layer has an integer number of steps so we do not accumulate rounding errors
        var numSteps = (int)(c / Resolution);

        // Move to the new location if neccessary
        var (x, y) = Circle.Point(cx, cy, r, startAngle);
        if (_x != x || _y != y || _z != z) {
            _writer.LinearMoveAndExtrude(f:600, x:x, y:y, z:z, e:_e);
            _x = x;
            _y = y;
            _z = z;
        }

        for (var stepNumber = 1; stepNumber <= numSteps; stepNumber ++) {
            // How many degrees around the circumference we are on this layer
            var a = angle / numSteps * stepNumber;
            a += startAngle;

            // Determine the point on the circle
            (x, y) = Circle.Point(cx, cy, r, a);

            // Draw the line segment
            LineTo(x, y, z, extrusionRatio);
        }
    }

    public void CircleLayer(double extrusionRatio, double cx, double cy, double r, double z) {                   
        ArcLayer(0, 360, cx, cy, r, z, extrusionRatio);
    }
}