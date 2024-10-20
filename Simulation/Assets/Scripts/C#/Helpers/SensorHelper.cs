using System;

public static class SensorHelper
{
    public static bool CheckIfSensorRequiresDataOfType(SensorType sensorType, string type)
    {
        return sensorType switch
        {
            SensorType.RigidBodyVelocity or SensorType.RigidBodyPosition => type.Equals("RigidBody", StringComparison.OrdinalIgnoreCase),
            SensorType.FluidPressure => type.Equals("Fluid", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}