using System;
using System.Numerics;

namespace Moss.Core;

public sealed class PettingGesture
{
	private Vector2 last;

	private double lastTime;

	private double lastPet;

	private int direction;

	private int reversals;

	private float distance;

	public bool Sample(Vector2 cursor, bool inHead, double time, float scale)
	{
		if (!inHead || time - lastTime > 0.35)
		{
			reversals = 0;
			distance = 0f;
			direction = 0;
			last = cursor;
			lastTime = time;
			return false;
		}
		float value = cursor.X - last.X;
		last = cursor;
		lastTime = time;
		if (Math.Abs(value) < 2f * scale)
		{
			return false;
		}
		int num = Math.Sign(value);
		if (direction != 0 && num != direction)
		{
			reversals++;
		}
		direction = num;
		distance += Math.Abs(value);
		if (reversals >= 2 && distance > 32f * scale && time - lastPet > 0.7)
		{
			lastPet = time;
			reversals = 0;
			distance = 0f;
			return true;
		}
		return false;
	}
}
