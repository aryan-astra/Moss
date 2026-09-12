using System;
using System.Numerics;

namespace Moss.Core;

public static class PoseDynamics
{
	public static (Vector2 Elbow, Vector2 Hand) Arm(Vector2 shoulder, Vector2 target, float upper, float lower, int bend)
	{
		Vector2 vector = target - shoulder;
		float num = vector.Length();
		Vector2 vector2 = ((num > 0.001f) ? (vector / num) : Vector2.UnitY);
		float num2 = Math.Clamp(num, Math.Abs(upper - lower) + 0.001f, upper + lower - 0.001f);
		float num3 = (upper * upper - lower * lower + num2 * num2) / (2f * num2);
		float num4 = MathF.Sqrt(Math.Max(0f, upper * upper - num3 * num3));
		return (Elbow: shoulder + vector2 * num3 + new Vector2(0f - vector2.Y, vector2.X) * num4 * bend, Hand: shoulder + vector2 * num2);
	}

	public static Vector2 Foot(float cycle, float stride, float lift)
	{
		cycle -= MathF.Floor(cycle);
		if (cycle < 0.6f)
		{
			return new Vector2(stride * (0.5f - cycle / 0.6f), 0f);
		}
		float num = (cycle - 0.6f) / 0.4f;
		return new Vector2(stride * (-0.5f + num), (0f - MathF.Sin(num * (float)Math.PI)) * lift);
	}
}
