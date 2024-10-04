using Unity.Mathematics;
using UnityEngine;

// Import utils from Resources.cs
using Resources;
using UnityEngine.Rendering;
using System;
public static class ComputeHelper
{

// --- KERNEL DISPATCH ---

    static public void DispatchKernel (ComputeShader cs, string kernelName, int threadsNum, int threadSize)
    {
        int threadGroupsNum = Utils.GetThreadGroupsNum(threadsNum, threadSize);
        cs.Dispatch(cs.FindKernel(kernelName), threadGroupsNum, 1, 1);
    }
    static public void DispatchKernel (ComputeShader cs, string kernelName, int2 threadsNum, int threadSize)
    {
        int2 threadGroupsNums = Utils.GetThreadGroupsNum(threadsNum, threadSize);
        cs.Dispatch(cs.FindKernel(kernelName), threadGroupsNums.x, threadGroupsNums.y, 1);
    }
    static public void DispatchKernel (ComputeShader cs, string kernelName, int3 threadsNum, int threadSize)
    {
        int3 threadGroupNums = Utils.GetThreadGroupsNum(threadsNum, threadSize);
        cs.Dispatch(cs.FindKernel(kernelName), threadGroupNums.x, threadGroupNums.y, threadGroupNums.z);
    }
    static public void DispatchKernel (ComputeShader cs, string kernelName, int threadGroupsNum)
    {
        cs.Dispatch(cs.FindKernel(kernelName), threadGroupsNum, 1, 1);
    }
    static public void DispatchKernel (ComputeShader cs, string kernelName, int2 threadGroupsNums)
    {
        cs.Dispatch(cs.FindKernel(kernelName), threadGroupsNums.x, threadGroupsNums.y, 1);
    }
    static public void DispatchKernel (ComputeShader cs, string kernelName, int3 threadGroupsNums)
    {
        cs.Dispatch(cs.FindKernel(kernelName), threadGroupsNums.x, threadGroupsNums.y, threadGroupsNums.z);
    }

// --- CREATE BUFFERS ---

    // Create append buffer without ref
	public static ComputeBuffer CreateAppendBuffer<T>(int capacity) // T is the buffer struct
	{
		int stride = GetStride<T>();
		ComputeBuffer buffer = new ComputeBuffer(capacity, stride, ComputeBufferType.Append);
		buffer.SetCounterValue(0);
		return buffer;
	}
    // Create append buffer with ref
	public static void CreateAppendBuffer<T>(ref ComputeBuffer buffer, int capacity) // T is the buffer struct
	{
		int stride = GetStride<T>();
        buffer ??= new ComputeBuffer(capacity, stride, ComputeBufferType.Append);
		buffer.SetCounterValue(0);
	}
    // Create structured buffer without ref, from data
	public static ComputeBuffer CreateStructuredBuffer<T>(T[] data) // T is the buffer struct
	{
		var buffer = new ComputeBuffer(data.Length, GetStride<T>());
		buffer.SetData(data);
		return buffer;
	}
    // Create structured buffer with ref, from data
	public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, T[] data) // T is the buffer struct
	{
		buffer ??= new ComputeBuffer(data.Length, GetStride<T>());
		buffer.SetData(data);
	}
    // Create structured buffer without ref
	public static ComputeBuffer CreateStructuredBuffer<T>(int count) // T is the buffer struct
	{
		var buffer = new ComputeBuffer(count, GetStride<T>());
		return buffer;
	}
    // Create structured buffer with ref
	public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, int count) // T is the buffer struct
	{
		buffer = new ComputeBuffer(count, GetStride<T>());
	}
    // Create count buffer without ref
    public static ComputeBuffer CreateCountBuffer()
    {
        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        return countBuffer;
    }
    // Create count buffer with ref
    public static void CreateCountBuffer(ref ComputeBuffer countBuffer)
    {
        countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
    }

// --- GET APPEND BUFFER COUNT ---

    // Get append buffer count with count buffer
	public static int GetAppendBufferCount(ComputeBuffer buffer, ComputeBuffer countBuffer)
	{
        ComputeBuffer.CopyCount(buffer, countBuffer, 0);
        int[] countArr = new int[1];
        countBuffer.GetData(countArr);
        int count = countArr[0];
        return count;
	}
    // Get append buffer count without count buffer
	public static int GetAppendBufferCount(ComputeBuffer buffer)
	{
        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        ComputeBuffer.CopyCount(buffer, countBuffer, 0);
        int[] countArr = new int[1];
        countBuffer.GetData(countArr);
        int count = countArr[0];
        return count;
	}
    /// <summary>Get append buffer count asyncronously (invokes an action).
    /// MAY NOT WORK PROPERLY IF CALLED MULTIPLE TIMES WITH THE SAME COUNTBUFFER</summary>
    /// <remarks>Uses a countBuffer</remarks>
    public static void GetAppendBufferCountAsync(ComputeBuffer buffer, ComputeBuffer countBuffer, Action<int> onComplete)
    {
        ComputeBuffer.CopyCount(buffer, countBuffer, 0);

        AsyncGPUReadback.Request(countBuffer, (request) =>
        {
            if (request.hasError)
            {
                Debug.Log("request.hasError - GetAppendBufferCountAsync");
                onComplete?.Invoke(-1); // -1 indicates an error
            }
            else
            {
                int count = request.GetData<int>()[0];
                onComplete?.Invoke(count);
            }

            // Release the count buffer to free GPU memory.
            countBuffer.Release();
        });
    }
    /// <summary>Get append buffer count asyncronously (invokes an action)</summary>
    /// <remarks>Does not use a countBuffer</remarks>
    public static void GetAppendBufferCountAsync(ComputeBuffer buffer, Action<int> onComplete)
    {
        ComputeBuffer countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        ComputeBuffer.CopyCount(buffer, countBuffer, 0);

        AsyncGPUReadback.Request(countBuffer, (request) =>
        {
            if (request.hasError)
            {
                Debug.Log("request.hasError - GetAppendBufferCountAsync");
                onComplete?.Invoke(-1); // -1 indicates an error
            }
            else
            {
                int count = request.GetData<int>()[0];
                onComplete?.Invoke(count);
            }

            // Release the count buffer to free GPU memory.
            countBuffer.Release();
        });
    }

    /// <summary>Get append buffer count asyncronously (invokes an action)</summary>
    /// <remarks>Does not use a countBuffer</remarks>
    public static void GetBufferContents<T>(ComputeBuffer buffer, int count, Action<T[]> onComplete) where T : struct
    {
        // Request an async readback to read the buffer
        AsyncGPUReadback.Request(buffer, (request) =>
        {
            if (request.hasError)
            {
                Debug.LogError("request.hasError - GetBufferContents");
            }
            else
            {
                // Retrieve the data from the buffer
                T[] contents = request.GetData<T>(0).ToArray();
                onComplete?.Invoke(contents);
            }
        });
    }

// --- RELEASE BUFFERS / TEXTURES ---

    // Release single buffer
	public static void Release(ComputeBuffer buffer)
	{
		buffer?.Release(); // ComputeBuffer class passed by reference automatically
	}
    // Release multiple buffers
    public static void Release(params ComputeBuffer[] buffers)
	{
        for (int i = 0; i < buffers.Length; i++)
        {
            Release(buffers[i]);
        }
	}
    // Release single texture
	public static void Release(RenderTexture texture)
	{
		if (texture != null)
		{
			texture.Release(); // RenderTexture class passed by reference automatically
		}
	}

// --- CLASS ---

    public static int GetStride<T>() => System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
}