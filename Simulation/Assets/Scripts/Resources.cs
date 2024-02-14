using UnityEngine;
using Unity.Mathematics;
using System;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Resources
{
    public struct BufferDataStruct
    {
        public string bufferName;
        public ComputeBuffer buffer;
    }
    public struct SpringStruct
    {
        public int PLinkedA;
        public int PLinkedB;
        public float RestLength;
        // public float yieldLen;
        // public float plasticity;
        // public float stiffness;
    };
    public struct StickynessRequestStruct
    {
        public int pIndex;
        public int StickyLineIndex;
        public float2 StickyLineDst;
        public float absDstToLineSqr;
        public float RBStickyness;
        public float RBStickynessRange;
    };
    public struct PDataStruct
    {
        public float2 PredPosition;
        public float2 Position;
        public float2 Velocity;
        public float2 LastVelocity;
        public float Density;
        public float NearDensity;
        public float Temperature; // kelvin
        public float TemperatureExchangeBuffer;
        public int LastChunkKey_PType_POrder; // composed 3 int structure
        // POrder; // POrder is dynamic, 
        // LastChunkKey; // 0 <= LastChunkKey <= ChunkNum
        // PType; // 0 <= PType <= PTypeNum
    }
    public struct PTypeStruct
    {
        public float TargetDensity;
        public int MaxInfluenceRadius;
        public float Pressure;
        public float NearPressure;
        public float Damping;
        public float Viscocity;
        public float Elasticity;
        public float Plasticity;
        public float Stickyness;
        public float ThermalConductivity;
        public float SpecificHeatCapacity;
        public float Gravity;
        public float colorG;
    };
    public struct RBDataStruct
    {
        public float2 Position;
        public float2 Velocity;
        // radians / second
        public float AngularImpulse;

        public float Stickyness;
        public float StickynessRange;
        public float StickynessRangeSqr;
        public float2 NextPos;
        public float2 NextVel;
        public float NextAngImpulse;
        public float Mass;
        public int2 LineIndices;
        public float MaxDstSqr;
        public int WallCollision;
        public int Stationary; // 1 -> Stationary, 0 -> Non-stationary
    };
    public struct RBVectorStruct
    {
        public float2 Position;
        public float2 LocalPosition;
        public float3 ParentImpulse;
        public int ParentRBIndex;
        public int WallCollision;
    };

    public class Utils
    {
        public static Vector2 GetMouseWorldPos(int Width, int Height)
        {
            Vector3 MousePos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x , Input.mousePosition.y , -Camera.main.transform.position.z));
            Vector2 MouseWorldPos = new(((MousePos.x - Width/2) * 0.55f + Width) / 2, ((MousePos.y - Height/2) * 0.55f + Height) / 2);

            return MouseWorldPos;
        }

        public static bool2 GetMousePressed()
        {
            bool LMousePressed = Input.GetMouseButton(0);
            bool RMousePressed = Input.GetMouseButton(1);

            bool2 MousePressed = new bool2(LMousePressed, RMousePressed);

            return MousePressed;
        }

        public static int GetThreadGroupsNums(int threadsNum, int threadSize)
        {
            int threadGroupsNum = (int)Math.Ceiling((float)threadsNum / threadSize);
            return threadGroupsNum;
        }

        public static float CelciusToKelvin(float celciusTemp)
        {
            return 273.15f + celciusTemp;
        }
        
        public static float2 GetParticleSpawnPosition(int pIndex, int maxIndex, int Width, int Height, int SpawnDims)
        {
            float x = (Width - SpawnDims) / 2 + Mathf.Floor(pIndex % Mathf.Sqrt(maxIndex)) * (SpawnDims / Mathf.Sqrt(maxIndex));
            float y = (Height - SpawnDims) / 2 + Mathf.Floor(pIndex / Mathf.Sqrt(maxIndex)) * (SpawnDims / Mathf.Sqrt(maxIndex));
            if (SpawnDims > Width || SpawnDims > Height)
            {
                throw new ArgumentException("Particle spawn dimensions larger than either border_width or border_height");
            }
            return new float2(x, y);
        }


    }

    public class Func // Math resources
    {
        public static int Log2(int value, bool doCeil = false)
        {
            double logValue = Math.Log(value, 2);
            return doCeil ? (int)Math.Ceiling(logValue) : (int)logValue;
        }
    }
}