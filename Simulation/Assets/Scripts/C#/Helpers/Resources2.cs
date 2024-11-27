using UnityEngine;
using Unity.Mathematics;
using System;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using System.Globalization;

namespace Resources2
{
#region Constants
    public static class Const
    {
        public static readonly Vector2 Vector2Half = new(0.5f, 0.5f);
        public static readonly float PI = 3.14159265f;
        public static readonly float LARGE_FLOAT = 1e6f;
    }
#endregion

#region General Utilities
    public static class Utils
    {
        public static bool2 GetMousePressed()
        {
            bool LMousePressed = Input.GetMouseButton(0);
            bool RMousePressed = Input.GetMouseButton(1);

            bool2 MousePressed = new(LMousePressed, RMousePressed);

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

        public static float KelvinToCelcius(float kelvinTemp)
        {
            return kelvinTemp - 273.15f;
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

        public static Vector2 Int2ToVector2(int2 a)
        {
            return new(a.x, a.y);
        }

        public static Vector2 Float2ToVector2(float2 a)
        {
            return new(a.x, a.y);
        }

        public static Vector3 Int3ToVector3(int3 a)
        {
            return new(a.x, a.y, a.z);
        }

        public static Vector3 Float3ToVector3(float3 a)
        {
            return new(a.x, a.y, a.z);
        }
    }
#endregion

#region Math Functions
    public class Func
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

        public static float Magnitude(float2 a)
        {
            return Mathf.Sqrt(a.x*a.x + a.y*a.y);
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

        public static Vector2 LerpVector2(Vector2 a, Vector2 b, float t)
        {
            return new(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.y, b.y, t));
        }

        public static int FloatAsInt(float a, float precision)
        {
            return (int)(a * precision);
        }

        public static int2 Float2AsInt2(float2 a, float precision)
        {
            return new int2(FloatAsInt(a.x, precision), FloatAsInt(a.y, precision));
        }

        public static float IntToFloat(int a, float precision)
        {
            return a / precision;
        }

        public static float2 Int2ToFloat2(int2 a, float precision)
        {
            return new float2(IntToFloat(a.x, precision), IntToFloat(a.y, precision));
        }

        public static float3 ColorToFloat3(Color color)
        {
            return new float3(color.r, color.g, color.b);
        }
        public static Vector3 ColorToVector3(Color color)
        {
            return new Vector3(color.r, color.g, color.b);
        }

        public static Color Float3ToColor(float3 a)
        {
            return new Color(a.x, a.y, a.z);
        }
        
        public static Vector3 Float2ToVector3(float2 a)
        {
            return new Vector3(a.x, a.y, 1);
        }

        public static float MsToSeconds(float ms) => ms / 1000.0f;

        public static float SecondsToMs(float s) => s * 1000.0f;

        public static int RandInt(int min, int max)
        {
            return UnityEngine.Random.Range(min, max+1);
        }

        // Function to rotate a 2D point by an angle in radians
        public static Vector2 RotatePoint(Vector2 point, float angle)
        {
            float cosTheta = Mathf.Cos(angle);
            float sinTheta = Mathf.Sin(angle);

            float x = point.x * cosTheta - point.y * sinTheta;
            float y = point.x * sinTheta + point.y * cosTheta;

            return new Vector2(x, y);
        }
    }
#endregion

#region Transform Utilities
    public static class TransformUtils
    {
        public static Vector2 SimSpaceToWorldSpace(Vector2 simCoords)
        {
            (Vector2 viewMin, Vector2 viewDims) = ProgramManager.Instance.GetUIBoundaries();

            Vector2 boundaryDims = ProgramManager.Instance.main != null
                ? new Vector2(ProgramManager.Instance.main.BoundaryDims.x, ProgramManager.Instance.main.BoundaryDims.y)
                : new Vector2(float.MaxValue, float.MaxValue);

            Vector2 normalizedCoords = simCoords / boundaryDims;
            return normalizedCoords * viewDims + viewMin;
        }
    }
#endregion

#region Spring Utilities
    public static class StringUtils
    {
        public static void LogIfInEditor(string message)
        {
            if (Application.isEditor) Debug.Log(message);
        }

        public static string FloatToString(float value, int numDecimals)
        {
            float factor = Mathf.Pow(10, numDecimals);
            return (Mathf.Round(value * factor) / factor).ToString();
        }

        public static string RemoveCharsFromEnd(string input, int charsToRemove) => input[..^charsToRemove];

        public static string FloatToStr(float value, int numDecimals) => value.ToString($"F{numDecimals}", CultureInfo.InvariantCulture);
        public static string FloatToStr(float2 value, int numDecimals) => "X: " + value.x.ToString($"F{numDecimals}", CultureInfo.InvariantCulture) + "Y: " + value.y.ToString($"F{numDecimals}", CultureInfo.InvariantCulture);
    }
#endregion

#region Array Utilities
    public class ArrayUtils
    {
        public static T[] RemoveElementAtIndex<T>(ref T[] array, int index)
        {
            if (index < 0 || index >= array.Length)
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of bounds.");

            // Create a new array with one less element
            T[] newArray = new T[array.Length - 1];
            int newIndex = 0;

            // Copy elements except the one to remove
            for (int i = 0; i < array.Length; i++)
            {
                if (i != index)
                {
                    newArray[newIndex] = array[i];
                    newIndex++;
                }
            }

            return newArray;
        }

        public static Vector2[] RemoveAdjacentDuplicates(Vector2[] array)
        {
            if (array == null || array.Length == 0) return array;

            for (int i = 0; i < array.Length - 1; i++)
            {
                Vector2 a = array[i];
                Vector2 b = array[i + 1];
                if (a.x == b.x && a.y == b.y)
                {
                    RemoveElementAtIndex(ref array, i + 1);
                    i--;
                }
            }

            return array;
        }
    }
#endregion
}