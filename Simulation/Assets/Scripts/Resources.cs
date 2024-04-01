using UnityEngine;
using Unity.Mathematics;
using System;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Resources
{
    public struct SpringStruct
    {
        public int PLinkedA;
        public int PLinkedB;
        public float RestLength;
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
        public int FluidSpringsGroup;

        public float SpringPlasticity;
        public float SpringTolDeformation;
        public float SpringStiffness;

        public float ThermalConductivity;
        public float SpecificHeatCapacity;
        public float FreezeThreshold;
        public float VaporizeThreshold;

        public float Pressure;
        public float NearPressure;

        public float Mass;
        public float TargetDensity;
        public float Damping;
        public float PassiveDamping;
        public float Viscosity;
        public float Stickyness;
        public float Gravity;

        public float InfluenceRadius;
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

        public static int GetThreadGroupsNums(int threadsNum, int threadSize) // TO BE REMOVED
        {
            int threadGroupsNum = (int)Math.Ceiling((float)threadsNum / threadSize);
            return threadGroupsNum;
        }
        public static int GetThreadGroupsNum(int threadsNum, int threadSize)
        {
            int threadGroupsNum = (int)Math.Ceiling((float)threadsNum / threadSize);
            return threadGroupsNum;
        }
        public static int2 GetThreadGroupsNum(int2 threadsNum, int threadSize)
        {
            int threadGroupsNumX = GetThreadGroupsNum(threadsNum.x, threadSize);
            int threadGroupsNumY = GetThreadGroupsNum(threadsNum.y, threadSize);
            return new(threadGroupsNumX, threadGroupsNumY);
        }
        public static int3 GetThreadGroupsNum(int3 threadsNum, int threadSize)
        {
            int threadGroupsNumX = GetThreadGroupsNum(threadsNum.x, threadSize);
            int threadGroupsNumY = GetThreadGroupsNum(threadsNum.y, threadSize);
            int threadGroupsNumZ = GetThreadGroupsNum(threadsNum.z, threadSize);
            return new(threadGroupsNumX, threadGroupsNumY, threadGroupsNumZ);
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
        public static void Log2(ref int a, bool doCeil = false)
        {
            double logValue = Math.Log(a, 2);
            a = doCeil ? (int)Math.Ceiling(logValue) : (int)logValue;
        }
        public static int Log2(int a, bool doCeil = false)
        {
            double logValue = Math.Log(a, 2);
            return doCeil ? (int)Math.Ceiling(logValue) : (int)logValue;
        }
        public static int Pow2(int a)
        {
            double powValue = Mathf.Pow(2, a);
            return (int)powValue;
        }
        public static int RandInt(int min, int max)
        {
            return UnityEngine.Random.Range(min, max+1);
        }
        public static int NextPow2(int a)
        {
            int nextPow2 = 1;
            while (nextPow2 < a)
            {
                nextPow2 *= 2;
            }
            return nextPow2;
        }
        public static void NextPow2(ref int a)
        {
            int nextPow2 = 1;
            while (nextPow2 < a)
            {
                nextPow2 *= 2;
            }
            a = nextPow2;
        }
        public static int NextLog2(int a)
        {
            return Log2(NextPow2(a));
        }
        public static void NextLog2(ref int a)
        {
            a = Log2(NextPow2(a));
        }
    }
}