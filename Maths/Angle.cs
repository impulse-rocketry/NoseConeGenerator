namespace ImpulseRocketry.Maths;

public static class Angle {
    public static double DegToRad(double degrees) {
        return degrees * Math.PI / 180.0;
    }

    public static double RadToDeg(double radians) {
        return radians / Math.PI * 180.0;
    }
}