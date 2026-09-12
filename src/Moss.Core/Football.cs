using System;
using System.Numerics;

namespace Moss.Core;

public sealed class Football
{
	private float life;

	private float kickIn;

	private float seekIn;

	public Vector2 Position { get; private set; }

	public Vector2 Velocity { get; private set; }

	public bool Active { get; private set; }

	public float Rotation { get; private set; }

	public void Roll(Display display, bool fromLeft)
	{
		float num = 11f * display.Scale;
		Position = new Vector2(fromLeft ? (display.Work.X + num) : (display.Work.Right - num), display.Work.Bottom - num - 40f * display.Scale);
		Velocity = new Vector2((float)(fromLeft ? 180 : (-180)) * display.Scale, -60f * display.Scale);
		Active = true;
		life = 40f;
		kickIn = 0f;
	}

	public void Stop()
	{
		Active = false;
	}

	public void Step(float dt, World world, Creature pet, Vector2? hand = null, float gravityScale = 1f, float bounceScale = 1f)
	{
		if (!Active || dt <= 0f || !float.IsFinite(dt))
		{
			return;
		}
		dt = Math.Min(dt, 1f / 30f);
		float num = world.ScaleAt(Position);
		float num2 = 11f * num;
		if (hand.HasValue)
		{
			Vector2 valueOrDefault = hand.GetValueOrDefault();
			Velocity += ((valueOrDefault - Position) * 160f - Velocity * 22f) * dt;
			Position += Velocity * dt;
			return;
		}
		life -= dt;
		kickIn -= dt;
		seekIn -= dt;
		if (life <= 0f)
		{
			Stop();
			return;
		}
		Vector2 position = Position;
		Velocity = new Vector2(Velocity.X * MathF.Exp((0f - dt) * 0.4f), Velocity.Y + 1050f * num * Math.Clamp(gravityScale, 0.2f, 3f) * dt);
		Position += Velocity * dt;
		Surface surface = null;
		foreach (Surface surface2 in world.Surfaces)
		{
			if (Velocity.Y > 0f && position.Y + num2 <= surface2.Y + 0.5f && Position.Y + num2 >= surface2.Y && surface2.Supports(Position.X) && (surface == null || surface2.Y < surface.Y))
			{
				surface = surface2;
			}
		}
		if (surface != null)
		{
			Position = new Vector2(Position.X, surface.Y - num2);
			Velocity = new Vector2(Velocity.X * 0.95f, (Velocity.Y > 70f * num) ? ((0f - Velocity.Y) * 0.66f * Math.Clamp(bounceScale, 0f, 1.5f)) : 0f);
		}
		Display display = world.Nearest(Position);
		if (display == null)
		{
			Stop();
			return;
		}
		if (Position.X < display.Work.X + num2)
		{
			Position = new Vector2(display.Work.X + num2, Position.Y);
			Velocity = new Vector2(Math.Abs(Velocity.X), Velocity.Y);
		}
		if (Position.X > display.Work.Right - num2)
		{
			Position = new Vector2(display.Work.Right - num2, Position.Y);
			Velocity = new Vector2(0f - Math.Abs(Velocity.X), Velocity.Y);
		}
		if (Position.Y > display.Bounds.Bottom + 80f * num)
		{
			Stop();
			return;
		}
		Rotation += Velocity.X * dt / num2;
		if (!pet.Held && pet.Tug < 0.05f && Vector2.Distance(Position, pet.Position - new Vector2(0f, 12f * num)) < 38f * num && kickIn <= 0f)
		{
			float num3 = ((!(Position.X < pet.Position.X)) ? 1 : (-1));
			Velocity = new Vector2(num3 * 260f * num, -300f * num);
			kickIn = 0.8f;
			pet.Notify();
		}
		if (seekIn <= 0f && pet.Support.HasValue && !pet.Held && pet.Tug < 0.05f && pet.Activity != Activity.Dance)
		{
			pet.Seek(Position);
			seekIn = 0.8f;
		}
	}
}
