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

using ImpulseRocketry.Units;

namespace ImpulseRocketry.NoseConeGenerator.Config;

/// <summary>
///
/// </summary>
public class Parameters
{

    /// <summary>
    /// The shape of the nose code.  Valid values are: TangentOgive, Elliptical, Haack.
    /// </summary>
    public string Shape { get; init; } = "Haack";

    /// <summary>
    /// The external diameter of the base of the nose cone.
    /// </summary>
    public Length Diameter { get; init; } = Length.Mm.Value(26.6);

    /// <summary>
    /// The ratio of the diameter to the height
    /// </summary>
    public double Ratio { get; init; } = 5.5;

    /// <summary>
    /// The shape parameter for the nose cone function
    /// </summary>
    public double ShapeParameter { get; init; } = 0;

    /// <summary>
    /// The thickness of the nose cone wall
    /// </summary>    
    public Length WallThickness { get; init; } = Length.Mm.Value(1);

    /// <summary>
    /// The height of the base cylinder
    /// </summary>    
    public Length BaseHeight { get; init; } = Length.Mm.Value(0);

    /// <summary>
    /// The layer height
    /// </summary>    
    public Length LayerHeight { get; init; } = Length.Mm.Value(0.2);

    /// <summary>
    /// The length of each straight line segment that makes up the circumference of the nose cone.
    /// </summary>
    public Length Resolution { get; init; } = Length.Mm.Value(0.5);

    /// <summary>
    /// Temperature of the bed
    /// </summary>
    public Temperature BedTemperature { get; init; } = Temperature.C.Value(60);

    /// <summary>
    /// Temperature of the extruder
    /// </summary>
    public Temperature ExtruderTemperature { get; init; } = Temperature.C.Value(230);

    /// <summary>
    /// The diameter of the filament
    /// </summary>    
    public Length FilamentDiameter { get; init; } = Length.Mm.Value(1.75);

    /// <summary>
    /// The width (max x coordinate) of the build plate
    /// </summary>
    public Length BuildPlateWidth { get; init; } = Length.Mm.Value(220);

    /// <summary>
    /// The depth (max y coordinate) of the build plate
    /// </summary>
    public Length BuildPlateDepth { get; init; } = Length.Mm.Value(220);

    /// <summary>
    /// The width of the brim
    /// </summary>
    public Length Brim { get; init; } = Length.Mm.Value(0);

    /// <summary>
    /// The cooling fan percentage
    /// </summary>
    public int FanSpeed { get; init; } = 100;
}