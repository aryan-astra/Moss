using System;
using System.Numerics;

namespace Moss.Core;

public readonly record struct Box(float X, float Y, float W, float H)
{
	public float Right => X + W;

	public float Bottom => Y + H;

	public bool Contains(Vector2 p)
	{
		if (p.X >= X && p.X < Right && p.Y >= Y)
		{
			return p.Y < Bottom;
		}
		return false;
	}

	public Vector2 Clamp(Vector2 p, float inset = 0f)
	{
		return new Vector2(Math.Clamp(p.X, X + inset, Math.Max(X + inset, Right - inset)), Math.Clamp(p.Y, Y + inset, Math.Max(Y + inset, Bottom - inset)));
	}

	public float DistanceSquared(Vector2 p)
	{
		return Vector2.DistanceSquared(p, Clamp(p));
	}
}
