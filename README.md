# Nose Cone Generator
Utility to generate 3D printer G-code files for common nose cone shapes


# Usage

This console mode utility accepts a JSON formatted parameters file and produces the resulting G-code as output which can be optionally written to a file or produced as output to the console.

``
NoseConeGenerator -p=paramtersFile -o=outputFile
``

| Argument | Description |
| --- | --- |
| `parametersFile` | JSON formatted parameters file |
| `outputFile` | Optional file to write G-code output to instead of writing to the console. |

# Parameters File

The JSON formatted parameters file accepts the following parameters:

| Parameter | Description | Default Value |
| --- | --- | --- |
| Shape | The name of the nose cone shape to generate as detailed below | Haack |
| ShapeParameter | The additional shape specific parameter where applicable | 0 |
| Diameter | The external diameter of the base of the nose cone | 26.6 mm |
| Ratio | The ratio of the height of the nose code to the base diameter | 5.5 |
| WallThickness | The thickness of the nose cone wall | 1 mm |
| BaseHeight | The height of the base cylinder | 5 mm |
| LayerHeight | The height of each layer of the print | 0.2 mm |
| Resolution | The length of each straight line segment that makes up the circumference of the nose cone | 0.6 mm |
| BedTemperature | Temperature of the 3D printer bed | 60 C |
| ExtruderTemperature | Temperature of the 3D printer extruder | 230 C |
| FilamentDiameter | The diameter of the filament | 1.75 mm |
| BuildPlateWidth | The width (max x coordinate) of the build plate | 220 mm |
| BuildPlateDepth | The depth (max y coordinate) of the build plate | 220 mm |

## Example

```
{
    "shape": "PowerSeries",
    "diameter": {
        "value": 26.6,
        "unit": "mm"
    },
    "ratio": 5,
    "shapeParameter": 0.5
}
```


# Available Shapes


## Conic

A simple cone shape defined by the formula:

$$ y = { x R \over L} $$

Where $L$ is the length of the nose cone and $R$ is the radius of the base.  The `shapeParameter` is ignored for this shape.

## Haack

Defined by the formula:

$$ \theta = {\arccos \left( 1 - {2x \over L} \right)}$$

$$ y = { R \over \sqrt \pi} \sqrt { \theta - {\sin \left(2\theta \right) \over 2} + C \sin^3\left(\theta \right)} $$

Where $L$ is the length of the nose cone, $R$ is the radius of the base, and $C$ is the `shapeParameter` value.

| Haack Series Type | $C$ Value |
| --- | --- |
| LD-Haack (Von Kármán) | $0$ |
| LV-Haack | $1/3$ |
| Tangent | $2/3$ |


## Tangent Ogive

This is the segment of a circle aligned so that the rocket body is tangent to the curve of the nose cone at its base, and the base is on the radius of the circle.

$$ \rho = {{R^2 + L^2} \over 2R } $$

$$ y = { \sqrt { \rho^2 - \left(L - x \right)^2 } + R - \rho} $$

Where $L$ is the length of the nose cone and $R$ is the radius of the base.  The `shapeParameter` is ignored for this shape.

## Parabolic

Similar to the tangent ogive, except that the shape is defined by a parabola rather than a circle.

$$ y = {R \left( {{ 2 \left({x \over L}\right) - K \left({x \over L} \right)^2 } \over 2-K } \right)} $$

Where $L$ is the length of the nose cone, $R$ is the radius of the base, and $K$ is the `shapeParameter` in the range $ 0 \leq K \leq 1 $

## Elliptical

This shape is formed from one-half of an ellipse, with the major axis as the center line and the minor axis aligned to the base of the nose cone.  The shape is defined by this formula:

$$ y = { R \sqrt { 1 - { x^2 \over L^2 } } } $$

Where $L$ is the length of the nose cone, and $R$ is the radius of the base, The `shapeParameter` is ignored.

## Power Series

Defined by the following formula:

$$ y = R { \left( { x \over L } \right)^n } $$

Where $L$ is the length of the nose cone, $R$ is the radius of the base, and $n$ is the `shapeParameter` in the range $ 0 \leq n \leq 1 $ which determines how blunt the tip is.

| Power Series Type | $x$ Value |
| --- | --- |
| Cylinder | $0$ |
| Parabola | $1/2$ |
| Three Quarter | $1/3$ |
| Cone | $1$ |