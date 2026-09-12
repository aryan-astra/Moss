using System;
using System.Collections.Generic;
using System.Numerics;

namespace Moss.Core;

// Screen-edge climbing. The display boundary is treated as a physical
// surface: the creature approaches a real vertical screen edge, grips it,
// hangs, pulls up, climbs toward the corner and transitions over the top.
// All positions stay in physical pixels inside the world model; gravity is
// suspended only while hanging from a grip (the same way held dragging
// suspends it) and resumes for every fall and landing.
public enum ClimbPhase
{
	None,
	ApproachEdge,
	PrepareClimb,
	Reach,
	Grip,
	Hang,
	PullUp,
	Climb,
	CornerTransition,
	TopTransition,
	Balance,
	Recover,
	Fall
}

public enum ClimbCommand
{
	Hang,
	PullUp,
	Peek,
	Slip,
	Recover,
	JumpFromEdge,
	ReachCorner,
	ClimbToTop
}

public sealed class ClimbEdge
{
	public int DisplayIndex;

	public bool LeftSide;

	public float X;

	public float Top;

	public float Bottom;

	public float Inward = 1f;

	public Vector2 Corner => new Vector2(X, Top);
}

public sealed class ClimbSession
{
	private float phaseTime;

	private float phaseLimit;

	private float gripY;

	private double hangUntil;

	private float nextGripIn;

	private int gripSide = 1;

	private int retries;

	private int hopCount;

	private bool descending;

	private float descendFloor;

	public ClimbPhase Phase { get; private set; }

	public ClimbEdge? Edge { get; private set; }

	public Vector2 GripPoint { get; private set; }

	public bool Succeeded { get; private set; }

	public bool Manual { get; private set; }

	public string Style { get; private set; } = "normal";

	public List<Vector2> Route { get; } = new List<Vector2>();

	private float speed = 1f;

	private bool pauseLook;

	private bool oneHand;

	private float slipChance = 0.06f;

	private bool slipPlanned;

	private float airTime;

	private float agility = 1f;

	private const float EdgeInset = 44f;

	private const float GripLead = 12f;

	private const float TopClearance = 150f;

	public bool SuppressLocomotion
	{
		get
		{
			switch (Phase)
			{
			case ClimbPhase.ApproachEdge:
			case ClimbPhase.None:
			case ClimbPhase.Fall:
				return false;
			default:
				return true;
			}
		}
	}

	public bool SuspendLanding
	{
		get
		{
			switch (Phase)
			{
			case ClimbPhase.Grip:
			case ClimbPhase.Hang:
			case ClimbPhase.PullUp:
			case ClimbPhase.Climb:
			case ClimbPhase.CornerTransition:
			case ClimbPhase.TopTransition:
			case ClimbPhase.Balance:
			case ClimbPhase.Recover:
				return true;
			default:
				return false;
			}
		}
	}

	public static bool TryFindEdge(World w, Vector2 pos, float scale, out ClimbEdge edge, bool? preferLeft = null, float maxDistance = 600f)
	{
		edge = null!;
		if (w.Displays.Count == 0)
		{
			return false;
		}
		Display? nearest = w.Nearest(pos);
		if (nearest == null)
		{
			return false;
		}
		int index = w.Displays.IndexOf(nearest);
		float best = float.MaxValue;
		bool left = true;
		if (!preferLeft.HasValue || preferLeft.Value)
		{
			float d = Math.Abs(pos.X - nearest.Bounds.X);
			if (d < best)
			{
				best = d;
				left = true;
			}
		}
		if (!preferLeft.HasValue || !preferLeft.Value)
		{
			float d2 = Math.Abs(pos.X - nearest.Bounds.Right);
			if (d2 < best)
			{
				best = d2;
				left = false;
			}
		}
		if (best > maxDistance * scale && preferLeft.HasValue)
		{
			best = Math.Min(Math.Abs(pos.X - nearest.Bounds.X), Math.Abs(pos.X - nearest.Bounds.Right));
			left = Math.Abs(pos.X - nearest.Bounds.X) < Math.Abs(pos.X - nearest.Bounds.Right);
		}
		float limit = preferLeft.HasValue ? 2400f : maxDistance;
		if (best > limit * scale)
		{
			return false;
		}
		edge = new ClimbEdge
		{
			DisplayIndex = index,
			LeftSide = left,
			X = (left ? nearest.Bounds.X : nearest.Bounds.Right),
			Top = nearest.Bounds.Y,
			Bottom = nearest.Bounds.Bottom,
			Inward = (left ? 1f : -1f)
		};
		return true;
	}

	private static (float Speed, float Slip, bool OneHand) AgilityFor(string species)
	{
		switch (species)
		{
		case "cat":
			return (1.25f, 0.02f, true);
		case "bird":
			return (1.1f, 0.05f, false);
		case "octopus":
			return (0.9f, 0.03f, true);
		case "dog":
			return (0.9f, 0.1f, false);
		case "rabbit":
			return (1f, 0.08f, false);
		case "penguin":
			return (0.7f, 0.08f, false);
		default:
			return (1f, 0.06f, true);
		}
	}

	public bool Start(World w, Creature c, Settings s, string species, bool manual, int seed = 0, bool? preferLeft = null)
	{
		if (Phase != ClimbPhase.None)
		{
			return false;
		}
		c.Construction.ClearShelter();
		if (!TryFindEdge(w, c.Position, c.Scale, out ClimbEdge found, preferLeft, manual ? 2400f : 500f))
		{
			return false;
		}
		Random random = ((seed == 0) ? new Random() : new Random(seed));
		var agilityValues = AgilityFor(species);
		agility = agilityValues.Speed;
		slipChance = agilityValues.Slip;
		slipPlanned = random.NextDouble() < slipChance * 3f;
		oneHand = agilityValues.OneHand && random.NextDouble() < 0.4;
		speed = (0.85f + (float)random.NextDouble() * 0.45f) * s.Advanced.ClimbSpeed;
		pauseLook = random.NextDouble() < 0.35;
		Style = (oneHand ? "one-handed" : ((speed > 1.15f) ? "fast" : ((speed < 0.95f) ? "slow" : "normal")));
		if (species == "rabbit")
		{
			Style = "hoppy";
		}
		if (species == "bird")
		{
			Style = "flutter-assisted";
		}
		Edge = found;
		Manual = manual;
		Route.Clear();
		retries = 0;
		hopCount = 0;
		airTime = 0f;
		descending = false;
		Succeeded = false;
		float approachBudget = Math.Clamp(6f + Math.Abs(c.Position.X - found.X) / 55f, 10f, 30f);
		SetPhase(ClimbPhase.ApproachEdge, approachBudget);
		return true;
	}

	public void Abort()
	{
		Phase = ClimbPhase.None;
		Edge = null;
	}

	public bool Command(ClimbCommand command, Creature c, Settings s)
	{
		switch (command)
		{
		case ClimbCommand.Hang:
			if (Phase == ClimbPhase.Grip || Phase == ClimbPhase.Climb)
			{
				SetPhase(ClimbPhase.Hang, 6f);
				hangUntil = c.Time + 2f;
				return true;
			}
			return false;
		case ClimbCommand.PullUp:
			if (Phase == ClimbPhase.Hang || Phase == ClimbPhase.Grip)
			{
				SetPhase(ClimbPhase.PullUp, 3f);
				return true;
			}
			return false;
		case ClimbCommand.Peek:
			if (Phase == ClimbPhase.Climb || Phase == ClimbPhase.Hang || Phase == ClimbPhase.Balance)
			{
				pauseLook = true;
				phaseTime = 0f;
				phaseLimit = 1.6f;
				return true;
			}
			return false;
		case ClimbCommand.Slip:
			if (Phase == ClimbPhase.Hang || Phase == ClimbPhase.Climb)
			{
				BeginRecover(c, true);
				return true;
			}
			return false;
		case ClimbCommand.Recover:
			if (Phase == ClimbPhase.Recover || Phase == ClimbPhase.Hang)
			{
				SetPhase(ClimbPhase.Grip, 2f);
				return true;
			}
			return false;
		case ClimbCommand.JumpFromEdge:
			if (Phase != ClimbPhase.None && Phase != ClimbPhase.ApproachEdge && Phase != ClimbPhase.Fall)
			{
				BeginFall(c, pushOut: true);
				return true;
			}
			return false;
		case ClimbCommand.ReachCorner:
		case ClimbCommand.ClimbToTop:
			if (Phase == ClimbPhase.Climb || Phase == ClimbPhase.Hang || Phase == ClimbPhase.PullUp)
			{
				speed *= 3f;
				phaseLimit = Math.Min(phaseLimit, phaseTime + 4f);
				return true;
			}
			return false;
		default:
			return false;
		}
	}

	private void SetPhase(ClimbPhase phase, float limit)
	{
		Phase = phase;
		phaseTime = 0f;
		phaseLimit = limit;
	}

	private void BeginRecover(Creature c, bool slipped)
	{
		retries++;
		if (retries > 2)
		{
			BeginFall(c, pushOut: false);
			return;
		}
		if (slipped)
		{
			c.SetVelocity(new Vector2(c.Velocity.X, Math.Max(c.Velocity.Y, 160f * c.Scale)));
		}
		SetPhase(ClimbPhase.Recover, 3f);
	}

	private void BeginFall(Creature c, bool pushOut)
	{
		if (Edge != null && pushOut)
		{
			c.SetVelocity(new Vector2(Edge.Inward * 160f * c.Scale, Math.Min(c.Velocity.Y, -140f * c.Scale)));
		}
		SetPhase(ClimbPhase.Fall, 4f);
	}

	private void Finish(Creature c, Settings s, bool completed)
	{
		c.SetHandTarget(null);
		c.SetMotionOverride(null);
		if (completed)
		{
			Succeeded = true;
			c.SetActivity(Activity.Sit, 2f);
		}
		else
		{
			c.SetActivity(Activity.Wander, 1f);
		}
		if (!Manual)
		{
			c.AddCooldown(Activity.ClimbEdge, s.Advanced.ClimbCooldownSec);
		}
		c.NextClimbAt = c.Time + s.Advanced.ClimbCooldownSec + (s.Advanced.ClimbIntervalMinSec + s.Advanced.ClimbIntervalMaxSec) / 2.0;
		Phase = ClimbPhase.None;
		Edge = null;
	}

	public void Step(float dt, Creature c, World w, Settings s)
	{
		if (Phase == ClimbPhase.None || Edge == null)
		{
			return;
		}
		if (Edge.DisplayIndex < 0 || Edge.DisplayIndex >= w.Displays.Count)
		{
			Finish(c, s, completed: false);
			return;
		}
		Display display = w.Displays[Edge.DisplayIndex];
		Edge.Top = display.Bounds.Y;
		Edge.Bottom = display.Bounds.Bottom;
		phaseTime += dt;
		float scale = c.Scale;
		float climb = 70f * scale * speed * agility;
		if (pauseLook && (Phase == ClimbPhase.Hang || Phase == ClimbPhase.Climb || Phase == ClimbPhase.Balance) && phaseTime > phaseLimit * 0.5f)
		{
			c.SetMotionOverride(Motion.Peeking);
		}
		switch (Phase)
		{
		case ClimbPhase.ApproachEdge:
		{
			float holdX = Edge.X + Edge.Inward * EdgeInset * scale;
			c.SetTargetX(holdX);
			c.SetMotionOverride(null);
			if (c.Support.HasValue)
			{
				airTime = 0f;
			}
			else
			{
				airTime += dt;
			}
			if (Math.Abs(c.Position.X - holdX) < 24f * scale && c.Support.HasValue)
			{
				c.SetFacing(Edge.Inward > 0f ? -1 : 1);
				SetPhase(ClimbPhase.PrepareClimb, 3f);
			}
			else if (phaseTime > phaseLimit || airTime > 2.5f)
			{
				Finish(c, s, completed: false);
			}
			break;
		}
		case ClimbPhase.PrepareClimb:
		{
			float wait = pauseLook ? 1.6f : 0.8f;
			c.SetMotionOverride(Motion.Peeking);
			c.SetVelocity(new Vector2(c.Velocity.X * MathF.Exp(0f - dt * 8f), c.Velocity.Y));
			if (phaseTime >= wait)
			{
				SetPhase(ClimbPhase.Reach, 3f);
			}
			break;
		}
		case ClimbPhase.Reach:
		{
			GripPoint = new Vector2(Edge.X + Edge.Inward * GripLead * scale, c.Position.Y - 55f * scale);
			RouteAdd(GripPoint);
			c.SetHandTarget(GripPoint);
			c.SetMotionOverride(null);
			if (phaseTime >= 0.45f)
			{
				c.SetSupport(null);
				SetPhase(ClimbPhase.Grip, 2f);
			}
			break;
		}
		case ClimbPhase.Grip:
		{
			c.SetHandTarget(GripPoint);
			c.SetMotionOverride(Motion.Hanging);
			c.SetVelocity(new Vector2((Edge.X + Edge.Inward * EdgeInset * scale - c.Position.X) * 8f, c.Velocity.Y * 0.5f));
			if (phaseTime >= 0.35f)
			{
				gripY = GripPoint.Y;
				hangUntil = c.Time + (oneHand ? 2.4f : 1.2f);
				SetPhase(ClimbPhase.Hang, 6f);
			}
			break;
		}
		case ClimbPhase.Hang:
		{
			float bodyLen = 62f * scale;
			Vector2 target = new Vector2(Edge.X + Edge.Inward * EdgeInset * scale + MathF.Sin((float)c.Time * 2f) * 3f * scale, gripY + bodyLen);
			c.SetVelocity((target - c.Position) * 6f);
			c.SetHandTarget(new Vector2(Edge.X, gripY));
			c.SetMotionOverride(pauseLook && phaseTime > phaseLimit * 0.5f ? Motion.Peeking : Motion.Hanging);
			if (slipPlanned && phaseTime > 0.5f && phaseTime < 0.6f)
			{
				slipPlanned = false;
				BeginRecover(c, slipped: true);
			}
			else if (c.Time >= hangUntil || phaseTime >= phaseLimit)
			{
				SetPhase(ClimbPhase.PullUp, 3f);
			}
			break;
		}
		case ClimbPhase.PullUp:
		{
			Vector2 over = new Vector2(Edge.X + Edge.Inward * EdgeInset * scale, gripY - 26f * scale);
			c.SetVelocity((over - c.Position) * 5f);
			c.SetHandTarget(new Vector2(Edge.X + Edge.Inward * GripLead * scale, gripY - 10f * scale));
			c.SetMotionOverride(Motion.Climbing);
			if (phaseTime >= 0.7f || Vector2.Distance(c.Position, over) < 12f * scale)
			{
				gripY -= 30f * scale;
				nextGripIn = 0f;
				SetPhase(ClimbPhase.Climb, 25f);
			}
			break;
		}
		case ClimbPhase.Climb:
		{
			nextGripIn -= dt;
			float stepLen = (c.Species == "rabbit" ? 68f : 46f) * scale;
			if (nextGripIn <= 0f)
			{
				gripSide = -gripSide;
				hopCount++;
				float lift = stepLen * (c.Species == "bird" && hopCount % 3 == 0 ? 1.5f : 1f);
				gripY += descending ? lift : (0f - lift);
				float wobble = (c.Species == "octopus" ? (hopCount % 4 - 1.5f) * 6f * scale : gripSide * 7f * scale);
				GripPoint = new Vector2(Edge.X + wobble, gripY);
				RouteAdd(GripPoint);
				nextGripIn = 0.45f / Math.Max(0.3f, speed * agility);
				if (c.Species == "bird" && hopCount % 3 == 0)
				{
					c.SetVelocity(new Vector2(c.Velocity.X, -120f * scale));
				}
			}
			Vector2 bodyTarget = new Vector2(Edge.X + Edge.Inward * EdgeInset * scale, gripY + 40f * scale);
			c.SetVelocity((bodyTarget - c.Position) * 5f);
			c.SetHandTarget(GripPoint);
			c.SetMotionOverride(Motion.Climbing);
			if (!descending && c.Position.Y <= Edge.Top + TopClearance * scale)
			{
				Succeeded = true;
				SetPhase(ClimbPhase.CornerTransition, 5f);
			}
			else if (descending && c.Position.Y >= descendFloor)
			{
				BeginFall(c, pushOut: false);
			}
			else if (phaseTime >= phaseLimit)
			{
				BeginFall(c, pushOut: false);
			}
			break;
		}
		case ClimbPhase.CornerTransition:
		{
			c.SetHandTarget(new Vector2(Edge.X + Edge.Inward * GripLead * scale, Edge.Top + TopClearance * scale));
			c.SetMotionOverride(Motion.Peeking);
			c.SetVelocity((new Vector2(Edge.X + Edge.Inward * EdgeInset * scale, Edge.Top + TopClearance * scale) - c.Position) * 4f);
			if (phaseTime >= 1f)
			{
				float roll = (float)(GripPoint.X * 0.37 + c.Position.Y * 0.73 + c.Time) % 1f;
				if (roll < 0f)
				{
					roll += 1f;
				}
				if (roll < 0.6f)
				{
					SetPhase(ClimbPhase.TopTransition, 3f);
				}
				else if (roll < 0.8f)
				{
					descending = true;
					descendFloor = w.Nearest(c.Position)?.Work.Bottom - 40f * scale ?? (c.Position.Y + 400f * scale);
					SetPhase(ClimbPhase.Climb, 25f);
				}
				else
				{
					BeginFall(c, pushOut: true);
				}
			}
			break;
		}
		case ClimbPhase.TopTransition:
		{
			Vector2 top = new Vector2(Edge.X + Edge.Inward * EdgeInset * scale, Edge.Top + TopClearance * scale);
			c.SetVelocity((top - c.Position) * 5f);
			c.SetHandTarget(null);
			c.SetMotionOverride(Motion.Climbing);
			if (phaseTime >= 0.8f || Vector2.Distance(c.Position, top) < 10f * scale)
			{
				c.SetFacing(Edge.Inward > 0f ? 1 : -1);
				SetPhase(ClimbPhase.Balance, 8f);
			}
			break;
		}
		case ClimbPhase.Balance:
		{
			Display? near = w.Nearest(c.Position);
			float minX = (near != null ? near.Bounds.X : Edge.X) + EdgeInset * scale;
			float maxX = (near != null ? near.Bounds.Right : Edge.X + Edge.Inward * 200f * scale) - EdgeInset * scale;
			if (minX > maxX)
			{
				float swap = minX;
				minX = maxX;
				maxX = swap;
			}
			float nx = Math.Clamp(c.Position.X + c.Facing * 20f * scale * dt, minX, maxX);
			if (nx <= minX + 1f || nx >= maxX - 1f)
			{
				c.SetFacing(-c.Facing);
			}
			Vector2 hold = new Vector2(nx, Edge.Top + TopClearance * scale);
			c.SetVelocity((hold - c.Position) * 6f);
			c.SetBalance(MathF.Sin((float)c.Time * 3f) * 4f);
			c.SetMotionOverride(pauseLook && phaseTime > phaseLimit * 0.5f ? Motion.Peeking : Motion.Balancing);
			if (phaseTime >= 2f + (pauseLook ? 2f : 0f))
			{
				float roll2 = (float)(c.Position.X * 0.61 + c.Time * 0.37) % 1f;
				if (roll2 < 0f)
				{
					roll2 += 1f;
				}
				if (roll2 < 0.5f)
				{
					descending = true;
					descendFloor = w.Nearest(c.Position)?.Work.Bottom - 40f * scale ?? (c.Position.Y + 400f * scale);
					gripY = c.Position.Y - 40f * scale;
					nextGripIn = 0f;
					c.SetBalance(0f);
					SetPhase(ClimbPhase.Climb, 25f);
				}
				else
				{
					c.SetBalance(0f);
					BeginFall(c, pushOut: true);
				}
			}
			break;
		}
		case ClimbPhase.Recover:
		{
			c.SetHandTarget(GripPoint);
			c.SetMotionOverride(Motion.Hanging);
			c.SetVelocity((new Vector2(Edge.X + Edge.Inward * EdgeInset * scale, GripPoint.Y + 62f * scale) - c.Position) * 5f);
			if (phaseTime >= 0.6f)
			{
				SetPhase(ClimbPhase.Grip, 2f);
			}
			break;
		}
		case ClimbPhase.Fall:
		{
			c.SetHandTarget(null);
			c.SetMotionOverride(null);
			if (c.Support.HasValue || phaseTime >= phaseLimit)
			{
				Finish(c, s, completed: Succeeded);
			}
			break;
		}
		}
		if (phaseTime > phaseLimit + 2f && Phase != ClimbPhase.None)
		{
			BeginFall(c, pushOut: false);
		}
	}

	private void RouteAdd(Vector2 point)
	{
		if (Route.Count == 0 || Vector2.Distance(Route[Route.Count - 1], point) > 4f)
		{
			Route.Add(point);
			if (Route.Count > 64)
			{
				Route.RemoveAt(0);
			}
		}
	}
}
