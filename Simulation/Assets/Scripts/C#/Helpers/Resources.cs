using UnityEngine;
using Unity.Mathematics;
using System;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Resources
{
    public static class Utils
    {
        public static Vector2 GetMouseWorldPos(int2 dims)
        {
            Vector3 MousePos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x , Input.mousePosition.y , -Camera.main.transform.position.z));
            Vector2 MouseWorldPos = new(((MousePos.x - dims.x/2) * 0.55f + dims.x) / 2, ((MousePos.y - dims.y/2) * 0.55f + dims.y) / 2);

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

        public static float CelsiusToKelvin(float celciusTemp)
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

        public static string RemoveCharsFromEnd(string input, int charsToRemove) => input[..^charsToRemove];
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

        public static float MaxFloat(params float[] inputArray)
        {
            float max = float.MinValue;
            for (int i = 0; i < inputArray.Length; i++)
            {
                max = Mathf.Max(max, inputArray[i]);
            }
            return max;
        }
        public static float MinFloat(params float[] inputArray)
        {
            float min = float.MaxValue;
            for (int i = 0; i < inputArray.Length; i++)
            {
                min = Mathf.Min(min, inputArray[i]);
            }
            return min;
        }

        public static Vector2 MaxVector2(params Vector2[] inputArray)
        {
            Vector2 max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < inputArray.Length; i++)
            {
                max = new(Mathf.Max(max.x, inputArray[i].x), Mathf.Max(max.y, inputArray[i].y));
            }
            return max;
        }
        public static Vector2 MinVector2(params Vector2[] inputArray)
        {
            Vector2 min = new(float.MaxValue, float.MaxValue);
            for (int i = 0; i < inputArray.Length; i++)
            {
                min = new(Mathf.Min(min.x, inputArray[i].x), Mathf.Min(min.y, inputArray[i].y));
            }
            return min;
        }

        public static int FloatAsInt(float a)
        {
            float int_float_precision = 100000.0f;
            return (int)(a * int_float_precision);
        }

        public static int2 Float2AsInt2(float2 a)
        {
            return new int2(FloatAsInt(a.x), FloatAsInt(a.y));
        }

        public static float IntToFloat(int a)
        {
            float int_float_precision = 100000.0f;
            return a / int_float_precision;
        }

        public static float2 Int2ToFloat2(int2 a)
        {
            return new float2(IntToFloat(a.x), IntToFloat(a.y));
        }

        public static float3 ColorToFloat3(Color color)
        {
            return new float3(color.r, color.g, color.b);
        }
        public static Vector3 ColorToVector3(Color color)
        {
            return new Vector3(color.r, color.g, color.b);
        }

        public static Vector3 Float2ToVector3(float2 a)
        {
            return new Vector3(a.x, a.y, 1);
        }
    }
}