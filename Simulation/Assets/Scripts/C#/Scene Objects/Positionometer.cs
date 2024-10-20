// using System.Globalization;
// using Resources;
// using UnityEngine;

// public class Positionometer : Sensor
// {
//     public void UpdateSensor()
//     {
//         if (sensorText != null)
//         {
//             // Get rigid body data
//             if (linkedRBIndex == -1) Debug.LogWarning("Sensor not linked to any rigid body. Sensor UI will not be updated");
//             else
//             {
//                 RBData rbData = sensorManager.retrievedRBData[linkedRBIndex];
//                 targetPosition = rbData.pos + offset;

//                 // Init sensor UI position
//                 if (!firstDataRecieved) sensorUIRect.localPosition = SimSpaceToCanvasSpace(targetPosition);
//                 firstDataRecieved = true;

//                 switch (sensorType)
//                 {
//                     case SensorType.RigidBodyVelocity:
//                         Vector2 xy = Func.Int2ToFloat2(rbData.vel_AsInt2);
//                         sensorText.text = FloatToStr(xy.magnitude, numDecimals) + " h.e";
//                         return;

//                     case SensorType.RigidBodyPosition:
//                         sensorText.text = "X: " + rbData.pos.x.ToString($"F{numDecimals}", CultureInfo.InvariantCulture) + ", Y: " + rbData.pos.y.ToString($"F{numDecimals}", CultureInfo.InvariantCulture);
//                         return;
                        
//                     default:
//                         Debug.LogWarning("SensorType not recognised. Sensor UI text will not be updated");
//                         return;
//                 }
//             }
//         }
//     }
// }