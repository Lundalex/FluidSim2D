using System;

public static class SensorHelper
{
    public static bool CheckIfSensorRequiresDataOfType(SensorType sensorType, string type)
    {
        switch (sensorType)
        {
            case SensorType.RigidBodyVelocity:
            case SensorType.RigidBodyPosition:
                return type.Equals("RigidBody", StringComparison.OrdinalIgnoreCase);
                
            case SensorType.FluidPressure:
                return type.Equals("Fluid", StringComparison.OrdinalIgnoreCase);
                
            default:
                return false;
        }
    }
}