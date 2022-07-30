namespace ImpulseRocketry.Maths;

public static class Circle {
    public static (double x, double y) Point(double cx, double cy, double r, double a) {
        var aRad = Angle.DegToRad(a);

        var x = cx + r * Math.Cos(aRad);
        var y = cy + r * Math.Sin(aRad);

        return new (x, y);
    }

    public static double Area(double r) {
        return Math.PI * r * r;
    }

    public static double Circumference(double r) {
        return 2 * Math.PI * r;
    }
}