using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;

//credits https://gist.github.com/keenanwoodall/c37ce12e0b7c08bd59f7235ec9614562
public struct NativeCurve : IDisposable
{
	public bool IsCreated => values.IsCreated;

	[NativeDisableParallelForRestriction]
	private NativeArray<float> values;
	private float startTime, timeSpan;
	private WrapMode preWrapMode;
	private WrapMode postWrapMode;

	private void InitializeValues(int count)
	{
		if (values.IsCreated)
			values.Dispose();

		values = new NativeArray<float>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
	}

	public void Update(AnimationCurve curve, int resolution)
	{
		if (curve == null)
			throw new NullReferenceException("Animation curve is null.");

        if (curve.keys.Length > 0)
		{
			startTime = curve.keys[0].time;
			timeSpan = curve.keys[curve.keys.Length - 1].time-startTime;
        }
        else
        {
			startTime = 0;
			timeSpan = 1;
        }

		preWrapMode = curve.preWrapMode;
		postWrapMode = curve.postWrapMode;

		if (!values.IsCreated || values.Length != resolution)
			InitializeValues(resolution);

		for (int i = 0; i < resolution; i++)
		{
			values[i] = curve.Evaluate(i / (float)resolution * timeSpan + startTime);
		}
	}

	public float Evaluate(float t)
	{
		t = (t - startTime) / timeSpan;

		var count = values.Length;

		if (count == 1)
			return values[0];

		if (t < 0f)
		{
			switch (preWrapMode)
			{
				default:
					return values[0];
				case WrapMode.Loop:
					t = 1f - (Mathf.Abs(t) % 1f);
					break;
				case WrapMode.PingPong:
					t = pingpong(t, 1f);
					break;
			}
		}
		else if (t > 1f)
		{
			switch (postWrapMode)
			{
				default:
					return values[count - 1];
				case WrapMode.Loop:
					t %= 1f;
					break;
				case WrapMode.PingPong:
					t = pingpong(t, 1f);
					break;
			}
		}

		var it = t * (count - 1);

		var lower = (int)it;
		var upper = lower + 1;
		if (upper >= count)
			upper = count - 1;

		return Mathf.Lerp(values[lower], values[upper], it - lower);
	}

	public void Dispose()
	{
		if (values.IsCreated)
			values.Dispose();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private float repeat(float t, float length)
	{
		return Mathf.Clamp(t - Mathf.Floor(t / length) * length, 0, length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private float pingpong(float t, float length)
	{
		t = repeat(t, length * 2f);
		return length - Mathf.Abs(t - length);
	}
}