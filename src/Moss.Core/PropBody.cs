using System;
using System.Numerics;

namespace Moss.Core;

public sealed class PropBody
{
	private float strain;

	private float retrieveIn;

	private Vector2 lastTarget;

	private Vector2 lastHandVelocity;

	private bool handSample;

	public Vector2 Position { get; set; }

	public Vector2 Velocity { get; set; }

	public PropState State { get; private set; }

	public bool Contested { get; private set; }

	public bool Grounded { get; private set; }

	public float Angle { get; private set; }

	public event Action? Lost;

	public event Action? Recovered;

	public void Grab()
	{
		Contested = State == PropState.Carried;
		State = PropState.HeldByUser;
		Grounded = false;
		strain = 0f;
		handSample = true;
		lastTarget = Position;
		lastHandVelocity = Vector2.Zero;
	}

	public void Release()
	{
		State = ((!Contested) ? PropState.Free : PropState.Carried);
		Contested = false;
		Velocity = Vector2.Clamp(Velocity, new Vector2(-1600f), new Vector2(1600f));
	}

	public void Step(float dt, Creature pet, World w, Vector2? cursor, float gravityScale = 1f, float gripStrength = 1f)
	{
		float scale = pet.Scale;
		pet.HandTarget = ((State == PropState.Carried || Contested) ? new Vector2?(Position) : ((Vector2?)null));
		pet.Tug = (Contested ? (0.25f + Math.Clamp(strain / 0.2f, 0f, 0.75f)) : 0f);
		Vector2 vector = pet.Position + new Vector2(pet.Facing * 27, -25f) * scale;
		if (State == PropState.Carried)
		{
			Velocity = (vector - Position) * 12f;
			Position += Velocity * dt;
			Angle = MathF.Sin((float)pet.Time * 2f) * 0.12f;
			return;
		}
		if (State == PropState.HeldByUser && cursor.HasValue)
		{
			Vector2 valueOrDefault = cursor.GetValueOrDefault();
			Vector2 value = (handSample ? ((valueOrDefault - lastTarget) / Math.Max(0.001f, dt)) : Vector2.Zero);
			Vector2 vector2 = Vector2.Lerp(lastHandVelocity, value, 1f - MathF.Exp((0f - dt) * 14f));
			float num = (handSample ? ((vector2 - lastHandVelocity).Length() / Math.Max(0.001f, dt) / scale) : 0f);
			lastTarget = valueOrDefault;
			lastHandVelocity = vector2;
			handSample = true;
			Velocity += ((valueOrDefault - Position) * 160f - Velocity * 22f) * dt;
			if (Contested)
			{
				Vector2 vector3 = Position - vector;
				float val = Math.Max(0f, vector2.Length() / scale - 800f) / 1000f + Math.Max(0f, num - 14000f) / 40000f;
				strain = Math.Clamp(strain + Math.Min(4f, val) * dt - dt * 0.45f, 0f, 1f);
				if (strain > 0.2f * Math.Clamp(gripStrength, 0.3f, 3f))
				{
					Contested = false;
					pet.LoseToy();
					this.Lost?.Invoke();
				}
				else
				{
					if (vector3.Y < -38f * scale && pet.Support.HasValue)
					{
						pet.LiftForGrip();
					}
					pet.Velocity += Vector2.Clamp((vector3 * 90f - pet.Velocity * 18f) * dt, new Vector2(-180f * scale), new Vector2(180f * scale));
					Velocity -= vector3 * 16f * dt;
				}
			}
			Position += Velocity * dt;
			Angle += Velocity.X * dt * 0.006f;
			return;
		}
		if (State == PropState.HeldByUser)
		{
			Release();
		}
		Vector2 position = Position;
		Velocity = new Vector2(Velocity.X * MathF.Exp((0f - dt) * (Grounded ? 8f : 0.2f)), Velocity.Y + 1300f * scale * Math.Clamp(gravityScale, 0.2f, 3f) * dt);
		Position += Velocity * dt;
		Angle += Velocity.X * dt * 0.009f;
		Grounded = false;
		Surface surface = null;
		foreach (Surface surface2 in w.Surfaces)
		{
			if (Velocity.Y >= 0f && position.Y + 5f * scale <= surface2.Y && Position.Y + 5f * scale >= surface2.Y && surface2.Supports(Position.X) && (surface == null || surface2.Y < surface.Y))
			{
				surface = surface2;
			}
		}
		if (surface != null)
		{
			Position = new Vector2(Position.X, surface.Y - 5f * scale);
			Velocity = new Vector2(Velocity.X * 0.7f, (Velocity.Y > 200f * scale) ? ((0f - Velocity.Y) * 0.3f) : 0f);
			Grounded = Velocity.Y == 0f;
		}
		Display display = w.Nearest(Position);
		if ((object)display != null && (double)display.Bounds.DistanceSquared(Position) > Math.Pow(300f * scale, 2.0))
		{
			Position = display.Work.Clamp(Position, 15f * scale);
			Velocity = Vector2.Zero;
		}
		if (Grounded && !pet.Held)
		{
			retrieveIn -= dt;
			if (retrieveIn <= 0f && pet.Activity != Activity.Dance)
			{
				pet.Seek(Position);
				retrieveIn = 0.6f;
			}
			if (Vector2.Distance(Position, pet.Position) < 40f * scale)
			{
				State = PropState.Carried;
				pet.RecoverToy();
				this.Recovered?.Invoke();
			}
		}
	}
}
