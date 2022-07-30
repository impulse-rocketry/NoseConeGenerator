namespace ImpulseRocketry.Maths;

public static class Line {
    public static double DistanceBetweenPoints(double x1, double y1, double z1, double x2, double y2, double z2) {
        return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2) +  Math.Pow(z2 - z1, 2));
    }
}