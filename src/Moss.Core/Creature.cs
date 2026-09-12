using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Moss.Core;

public sealed class Creature
{
	private readonly Random random;

	private readonly Dictionary<Activity, double> cooldowns = new Dictionary<Activity, double>();

	private float decisionIn;

	private float remaining;

	private float impactIn;

	private float reactionIn;

	private float heldTime;

	private float wakeIn;

	private float pettedIn;

	private float notificationIn;

	private Vector2 lastCursor;

	private Vector2 lastUserCursor;

	private bool userCursorInit;

	private double lastActiveTime;

	private bool justReleased;

	private bool wasPlaying;

	private float noticeMusic;

	private float arrivalPause;

	private float parked;

	private float nextHop = 14f;

	private double lastCursorReaction;

	private Vector2 awarenessCursor;

	private float cursorTravel;

	private Surface? previousSupport;

	private Surface? climbTarget;

	public ClimbSession Climb { get; } = new ClimbSession();

	public ConstructionWorld Construction { get; } = new ConstructionWorld();

	public string Species { get; set; } = "bean";

	public float CharWidth { get; set; } = 66f;

	public float CharHeight { get; set; } = 74f;

	public bool AutonomyEnabled { get; set; } = true;

	internal Motion? MotionOverride { get; private set; }

	internal void SetMotionOverride(Motion? motion)
	{
		MotionOverride = motion;
	}

	internal void SetVelocity(Vector2 velocity)
	{
		Velocity = velocity;
	}

	internal void SetHandTarget(Vector2? target)
	{
		HandTarget = target;
	}

	public double NextClimbAt { get; internal set; }

	public double NextBuildAt { get; internal set; }

	public float Affection { get; private set; }

	public float Tug { get; set; }

	public Vector2? HandTarget { get; set; }

	public float Disappointment { get; private set; }

	public float Relief { get; private set; }

	public Vector2 Position { get; set; }

	public Vector2 Velocity { get; set; }

	public long? Support { get; private set; }

	public float Scale { get; private set; } = 1f;

	public float Energy { get; private set; } = 0.8f;

	public float Mood { get; private set; } = 0.7f;

	public float Curiosity { get; private set; } = 0.5f;

	public float Attention { get; private set; } = 0.5f;

	public float Balance { get; private set; }

	public int Facing { get; private set; } = 1;

	public Activity Activity { get; private set; }

	public bool Held { get; private set; }

	public Motion Motion { get; private set; }

	public double Time { get; private set; }

	public double IdleSeconds => Time - lastActiveTime;

	public float TargetX { get; private set; }

	public event Action<float>? Landed;

	public event Action? Petted;

	public PetMemory Remember(DateTimeOffset now)
	{
		return new PetMemory
		{
			Mood = Mood,
			Energy = Energy,
			Attention = Attention,
			Saved = now
		};
	}

	public void Recall(PetMemory? memory, DateTimeOffset now)
	{
		if (memory != null)
		{
			memory.Validate();
			float num = (float)(1.0 - Math.Exp((0.0 - Math.Max(0.0, (now - memory.Saved).TotalHours)) / 4.0));
			Mood = memory.Mood + (0.7f - memory.Mood) * num;
			Energy = memory.Energy + (0.9f - memory.Energy) * num;
			Attention = memory.Attention + (0.5f - memory.Attention) * num;
		}
	}

	public void LoseToy()
	{
		Disappointment = 0.85f;
		Mood = Math.Max(0.25f, Mood - 0.15f);
		notificationIn = 0f;
	}

	public void RecoverToy()
	{
		Relief = 1f;
		Disappointment = 0f;
		Mood = Math.Min(1f, Mood + 0.2f);
	}

	public Creature(int seed = 0)
	{
		random = ((seed == 0) ? new Random() : new Random(seed));
	}

	public void Reset(World w)
	{
		Display display = w.Displays.FirstOrDefault();
		if (!(display == null))
		{
			Position = new Vector2(display.Work.X + display.Work.W * 0.65f, display.Work.Y + display.Work.H * 0.5f);
			Velocity = Vector2.Zero;
		Support = null;
		previousSupport = null;
		climbTarget = null;
		Held = false;
		Activity = Activity.Wander;
		decisionIn = 0f;
		EndClimbSession();
		Construction.Cancel();
	}
	}

	public void Pet()
	{
		Affection = 2.4f;
		Mood = Math.Min(1f, Mood + 0.12f);
		Attention = Math.Max(0f, Attention - 0.25f);
		reactionIn = 1.3f;
		pettedIn = 1.3f;
		lastActiveTime = Time;
		this.Petted?.Invoke();
	}

	public void Notify()
	{
		Curiosity = Math.Min(1f, Curiosity + 0.4f);
		reactionIn = 1.4f;
		notificationIn = 1.4f;
		decisionIn = Math.Min(decisionIn, 1.5f);
	}

	public void Grab(Vector2 cursor)
	{
		Held = true;
		heldTime = 0f;
		Support = null;
		previousSupport = null;
		climbTarget = null;
		EndClimbSession();
		lastCursor = cursor;
		lastActiveTime = Time;
		Velocity *= 0.25f;
	}

	public void Release(float power = 1f)
	{
		Held = false;
		justReleased = true;
		lastActiveTime = Time;
		float clamp = 1800f * Scale * Math.Clamp(power, 0.3f, 2.5f);
		Velocity = Vector2.Clamp(Velocity, new Vector2(-clamp, -clamp), new Vector2(clamp, clamp));
	}

	internal void SetSupport(long? id)
	{
		Support = id;
	}

	internal void SetClimbTarget(Surface? surface)
	{
		climbTarget = surface;
	}

	internal void SetTargetX(float x)
	{
		TargetX = x;
	}

	internal void SetFacing(int facing)
	{
		Facing = facing;
	}

	internal void SetBalance(float value)
	{
		Balance = value;
	}

	internal void SetActivity(Activity activity, float duration)
	{
		Activity = activity;
		remaining = duration;
		decisionIn = duration;
	}

	internal void AddCooldown(Activity activity, double seconds)
	{
		cooldowns[activity] = Time + seconds;
	}

	internal double CooldownLeft(Activity activity)
	{
		return cooldowns.GetValueOrDefault(activity) - Time;
	}

	public void EndClimbSession()
	{
		Climb.Abort();
		HandTarget = null;
		MotionOverride = null;
	}

	public void ResetCooldowns()
	{
		NextClimbAt = 0.0;
		NextBuildAt = 0.0;
	}

	public void RequestActivity(Activity activity, float duration)
	{
		SetActivity(activity, duration);
	}

	public void SetMood(float value)
	{
		Mood = Math.Clamp(value, 0f, 1f);
	}

	public void SetEnergy(float value)
	{
		Energy = Math.Clamp(value, 0f, 1f);
	}

	public void SetAttention(float value)
	{
		Attention = Math.Clamp(value, 0f, 1f);
	}

	public void SetCuriosity(float value)
	{
		Curiosity = Math.Clamp(value, 0f, 1f);
	}

	public void Halt()
	{
		Velocity = Vector2.Zero;
		RequestActivity(Activity.Sit, 2f);
	}

	public void Drop()
	{
		Support = null;
		previousSupport = null;
		climbTarget = null;
		Velocity = new Vector2(Velocity.X, Math.Max(0f, Velocity.Y));
	}

	public void MoveTo(float x)
	{
		TargetX = x;
		RequestActivity(Activity.Wander, 4f);
	}

	public void Recover()
	{
		climbTarget = null;
		decisionIn = 0f;
		remaining = 0f;
	}

	public IReadOnlyDictionary<Activity, float> Scores(World w, Settings settings, Personality p, bool music)
	{
		bool hasValue = Support.HasValue;
		bool flag = settings.CursorAwareness && Vector2.Distance(Position, w.Cursor) < settings.Advanced.CursorAttractRadius * Scale * settings.Sensitivity;
		Dictionary<Activity, float> dictionary = new Dictionary<Activity, float>
		{
			[Activity.Wander] = 0.28f + 0.3f * Energy,
			[Activity.Sit] = 0.15f + 0.45f * p.Calmness + 0.3f * (1f - Energy),
			[Activity.Sleep] = ((Energy < settings.Advanced.SleepThreshold) ? (1.3f - Energy) : (0.025f * p.Calmness)),
			[Activity.Investigate] = 0.15f + Curiosity * p.Curiosity * 0.7f,
			[Activity.Play] = Energy * p.Playfulness * 0.6f * settings.Advanced.PlayScoreBoost,
			[Activity.Dance] = ((music && Energy > settings.Advanced.MusicEnergyThreshold) ? (1.3f + p.Playfulness * 0.4f) : 0f),
			[Activity.Hide] = 0.06f + (1f - Mood) * 0.4f,
			[Activity.Attention] = (flag ? (Attention * p.Sociability * 0.95f) : 0.03f),
			[Activity.Climb] = ((hasValue && settings.WindowGeometry && Reachable(w).Any()) ? (0.25f + p.Curiosity * 0.7f) : 0f),
			[Activity.ClimbEdge] = EdgeClimbScore(w, settings, p),
			[Activity.Build] = BuildScore(settings, p)
		};
		Activity[] array = dictionary.Keys.ToArray();
		foreach (Activity activity in array)
		{
			if (activity != Activity.Dance && cooldowns.GetValueOrDefault(activity) > Time)
			{
				dictionary[activity] = 0f;
			}
		}
		return dictionary;
	}

	private IEnumerable<Surface> Reachable(World w)
	{
		float headroom = (w.Displays.Count > 0 ? w.Displays.Min((Display d) => d.Bounds.Y) : float.MinValue) + 150f * Scale;
		return w.Surfaces.Where((Surface s) => !s.Floor && s.Id != Support && s.Y > headroom && s.Y < Position.Y - 20f * Scale && s.Y > Position.Y - 320f * Scale && s.Right - s.Left > 65f * Scale && Math.Min(Math.Abs(s.Left - Position.X), Math.Abs(s.Right - Position.X)) < 350f * Scale);
	}

	private float EdgeClimbScore(World w, Settings settings, Personality p)
	{
		if (settings.Advanced.ClimbFrequency == Frequency.Off || !Support.HasValue || !settings.WindowGeometry || Time < NextClimbAt)
		{
			return 0f;
		}
		if (settings.Advanced.ClimbIdleSec > 0f && Time - lastActiveTime < settings.Advanced.ClimbIdleSec)
		{
			return 0f;
		}
		if (!ClimbSession.TryFindEdge(w, Position, Scale, out _, null, 500f))
		{
			return 0f;
		}
		return (0.2f + Curiosity * p.Curiosity * 0.6f) * (Energy > 0.25f ? 1f : 0.2f);
	}

	private float BuildScore(Settings settings, Personality p)
	{
		if (settings.Advanced.BuildFrequency == Frequency.Off || !Support.HasValue || Time < NextBuildAt)
		{
			return 0f;
		}
		if (settings.Advanced.BuildIdleSec > 0f && Time - lastActiveTime < settings.Advanced.BuildIdleSec)
		{
			return 0f;
		}
		if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - settings.Advanced.LastBuildUnix < settings.Advanced.BuildCooldownSec)
		{
			return 0f;
		}
		float score = Curiosity * p.Curiosity * (0.15f + 0.5f * Mood) * (Energy > 0.3f ? 1f : 0.1f);
		if (settings.Advanced.LastBuildUnix <= 0)
		{
			score *= 2.5f;
		}
		return score;
	}

	private void Decide(World w, Settings settings, Personality p, bool music)
	{
		if (Activity == Activity.Sleep && Energy < 0.8f && reactionIn <= 0f)
		{
			return;
		}
		Activity key = (from s in Scores(w, settings, p, music)
			where s.Value > 0f
			select s).MaxBy((KeyValuePair<Activity, float> s) => s.Value * (0.65f + (float)random.NextDouble() * 0.7f)).Key;
		if (Activity == Activity.Sleep && key != Activity.Sleep)
		{
			wakeIn = 1.2f;
		}
		Activity = key;
		remaining = key switch
		{
			Activity.Sleep => 35f, 
			Activity.Sit => 5f, 
			Activity.Dance => 7f, 
			Activity.Climb => 12f, 
			Activity.ClimbEdge => 30f, 
			Activity.Build => 60f, 
			_ => settings.Advanced.WanderDecisionMinSec + (float)random.NextDouble() * Math.Max(0.5f, settings.Advanced.WanderDecisionMaxSec - settings.Advanced.WanderDecisionMinSec), 
		};
		decisionIn = remaining;
		cooldowns[key] = Time + (double)remaining + (double)((key != Activity.Dance) ? 2 : 0);
		Display display = w.Nearest(Position);
		if (!(display == null))
		{
			TargetX = display.Work.X + 45f * Scale + (float)random.NextDouble() * Math.Max(1f, display.Work.W - 90f * Scale);
			if (key == Activity.Attention && settings.CursorAwareness)
			{
				TargetX = w.Cursor.X;
			}
			if (key == Activity.Investigate && w.Windows.Count > 0)
			{
				if (random.NextDouble() < settings.Advanced.WindowInvestigateProbability)
				{
					TargetX = w.Windows[random.Next(w.Windows.Count)].Bounds.X;
				}
				else
				{
					key = Activity.Wander;
					Activity = key;
				}
			}
			if (key == Activity.Hide)
			{
				TargetX = ((Position.X < display.Work.X + display.Work.W / 2f) ? (display.Work.X + 38f * Scale) : (display.Work.Right - 38f * Scale));
			}
			climbTarget = ((key == Activity.Climb) ? (from s in Reachable(w)
				orderby Math.Abs(s.Left - Position.X)
				select s).FirstOrDefault() : null);
			if (climbTarget != null)
			{
				TargetX = ((Math.Abs(climbTarget.Left - Position.X) < Math.Abs(climbTarget.Right - Position.X)) ? (climbTarget.Left + 20f * Scale) : (climbTarget.Right - 20f * Scale));
			}
			if (key == Activity.Play && Support.HasValue)
			{
				Jump();
			}
			if (key == Activity.ClimbEdge)
			{
				if (Climb.Start(w, this, settings, Species, manual: false))
				{
					TargetX = Climb.Edge.X + Climb.Edge.Inward * 20f * Scale;
				}
				else
				{
					key = Activity.Wander;
					Activity = key;
				}
			}
			if (key == Activity.Build)
			{
				StructureKind kind = random.NextDouble() < 0.7 ? StructureKind.House : (random.NextDouble() < 0.65 ? StructureKind.Platform : StructureKind.StickStructure);
				if (!Construction.StartBuild(w, this, settings, Species, kind, manual: false, charWidth: CharWidth, charHeight: CharHeight))
				{
					key = Activity.Wander;
					Activity = key;
				}
			}
		}
	}

	public void Seek(Vector2 goal)
	{
		TargetX = goal.X;
		Activity = Activity.Investigate;
		decisionIn = 2f;
	}

	public void LiftForGrip()
	{
		Support = null;
		previousSupport = null;
		climbTarget = null;
		Velocity = new Vector2(Velocity.X, Math.Min(0f, Velocity.Y));
	}

	public void Jump(float power = 1f)
	{
		if (Support.HasValue)
		{
			Velocity = new Vector2(Velocity.X, -550f * Scale * Math.Clamp(power, 0.36f, 1.64f));
			Support = null;
			previousSupport = null;
		}
	}

	public void Step(float dt, World w, Settings settings, Personality p, bool music, bool calm = false, Vector2? grabTarget = null)
	{
		if (dt <= 0f || !float.IsFinite(dt) || w.Displays.Count == 0)
		{
			return;
		}
		dt = Math.Min(dt, 1f / 30f);
		Time += dt;
		Scale = w.ScaleAt(Position) * settings.PetScale;
		Affection = Math.Max(0f, Affection - dt);
		Disappointment = Math.Max(0f, Disappointment - dt * 0.06f);
		Relief = Math.Max(0f, Relief - dt * 0.5f);
		bool num = music && !wasPlaying;
		wasPlaying = music;
		if (num)
		{
			noticeMusic = 0.65f;
			decisionIn = Math.Min(decisionIn, 0.65f);
			if (Activity == Activity.Sleep)
			{
				wakeIn = 1.1f;
				Activity = Activity.Sit;
			}
			cooldowns.Remove(Activity.Dance);
		}
		noticeMusic = Math.Max(0f, noticeMusic - dt);
		arrivalPause = Math.Max(0f, arrivalPause - dt);
		if (!float.IsFinite(Position.X + Position.Y + Velocity.X + Velocity.Y))
		{
			Reset(w);
		}
		float num2;
		Activity activity;
		bool flag;
		if (Activity == Activity.Sleep)
		{
			num2 = -0.035f;
		}
		else
		{
			activity = Activity;
			flag = ((activity == Activity.Sit || activity == Activity.Hide) ? true : false);
			num2 = (flag ? (-0.007f) : (0.004f * settings.Activity));
		}
		float num3 = num2;
		Energy = Math.Clamp(Energy - num3 * dt * settings.Advanced.EnergyRecoveryRate, 0f, 1f);
		Mood = Math.Clamp(Mood + (0.7f - Mood) * dt * 0.01f * settings.Advanced.MoodRecoveryRate, 0f, 1f);
		Curiosity = Math.Clamp(Curiosity + dt * 0.012f, 0f, 1f);
		Attention = Math.Clamp(Attention + dt * 0.009f, 0f, 1f);
		remaining -= dt;
		impactIn -= dt;
		reactionIn -= dt;
		wakeIn -= dt;
		pettedIn -= dt;
		notificationIn -= dt;
		if (Held && grabTarget.HasValue)
		{
			Vector2 valueOrDefault = grabTarget.GetValueOrDefault();
			heldTime += dt;
			Vector2 vector = (valueOrDefault - Position) * 180f * settings.Advanced.GrabSensitivity - Velocity * 25f * settings.Advanced.GrabSensitivity;
			Velocity += vector * dt;
			Position += Velocity * dt;
			lastCursor = valueOrDefault;
			Motion = ((heldTime < 0.22f) ? Motion.Grabbed : Motion.Held);
			return;
		}
		if (Held)
		{
			Release();
		}
		long? support = Support;
		object obj;
		if (support.HasValue)
		{
			long id = support.GetValueOrDefault();
			obj = w.Surfaces.FirstOrDefault((Surface s) => s.Id == id && (previousSupport == null || s.Supports(Position.X + (s.OriginX - previousSupport.OriginX), -3f * Scale)));
		}
		else
		{
			obj = null;
		}
		Surface surface = (Surface)obj;
		if (surface != null && previousSupport != null)
		{
			Vector2 vector2 = new Vector2(surface.OriginX - previousSupport.OriginX, surface.Y - previousSupport.Y);
			Position += vector2;
			Balance = Math.Clamp(Balance + vector2.X * 0.08f, -18f, 18f);
			if (vector2.Length() > 60f * Scale)
			{
				reactionIn = 0.6f;
			}
		}
		if (Support.HasValue && surface == null)
		{
			Support = null;
			reactionIn = 0.7f;
		}
		previousSupport = surface;
		cursorTravel = ((settings.CursorAwareness && Vector2.Distance(w.Cursor, Position) < 170f * Scale) ? Math.Min(100f * Scale, cursorTravel + Vector2.Distance(w.Cursor, awarenessCursor)) : 0f);
		if (settings.CursorAwareness && Time - lastCursorReaction > 8.0 && cursorTravel > 18f * Scale)
		{
			lastCursorReaction = Time;
			cursorTravel = 0f;
			if (Activity == Activity.Sleep)
			{
				Activity = Activity.Sit;
				wakeIn = 1.2f;
				decisionIn = 1.8f;
			}
			else if (Activity != Activity.Dance && Tug < 0.05f)
			{
				TargetX = w.Cursor.X;
				decisionIn = Math.Min(decisionIn, 1f);
			}
		}
		awarenessCursor = w.Cursor;
		if (!userCursorInit)
		{
			lastUserCursor = w.Cursor;
			userCursorInit = true;
		}
		if (Vector2.Distance(w.Cursor, lastUserCursor) > 40f * Scale)
		{
			lastUserCursor = w.Cursor;
			lastActiveTime = Time;
		}
		nextHop -= dt;
		if (music && Activity == Activity.Dance && nextHop <= 0f && surface != null && surface.Supports(Position.X, 45f * Scale) && !calm && !settings.ReducedMotion && Tug < 0.05f)
		{
			Jump();
			Velocity = new Vector2(Velocity.X, -320f * Scale);
			nextHop = 12f + (float)random.NextDouble() * 16f;
		}
		activity = Activity;
		flag = ((activity == Activity.Wander || activity == Activity.Investigate || (uint)(activity - 7) <= 1u) ? true : false);
		bool flag2 = flag;
		parked = ((Tug < 0.05f && Support.HasValue && flag2 && Math.Abs(TargetX - Position.X) > 35f * Scale && Math.Abs(Velocity.X) < 5f * Scale) ? (parked + dt) : 0f);
		if (parked > 4f)
		{
			climbTarget = null;
			Activity = Activity.Wander;
			TargetX = w.Nearest(Position).Work.X + w.Nearest(Position).Work.W * 0.5f;
			decisionIn = 0f;
			parked = 0f;
		}
		decisionIn -= dt;
		if (Activity == Activity.Dance && !music)
		{
			Activity = Activity.Sit;
			decisionIn = 1.2f;
			cooldowns.Remove(Activity.Dance);
		}
		if (music && Energy > settings.Advanced.MusicEnergyThreshold && noticeMusic <= 0f && wakeIn <= 0f && Support.HasValue && impactIn <= 0f && Tug < 0.05f && !calm && !Construction.HasSession)
		{
			if (Climb.Phase == ClimbPhase.ApproachEdge)
			{
				EndClimbSession();
			}
			if (Climb.Phase == ClimbPhase.None)
			{
				Activity = Activity.Dance;
				decisionIn = 3f;
				climbTarget = null;
			}
		}
		if (reactionIn > 0f && Activity == Activity.Sleep)
		{
			Activity = Activity.Sit;
			wakeIn = 1.2f;
			decisionIn = 2f;
		}
		if (calm)
		{
			if (Activity != Activity.Sleep)
			{
				Activity = Activity.Sit;
			}
			climbTarget = null;
			if (Climb.Phase != ClimbPhase.None)
			{
				EndClimbSession();
			}
		}
		if (!calm && AutonomyEnabled && Climb.Phase == ClimbPhase.None && !Construction.HasSession && decisionIn <= 0f && Support.HasValue && impactIn <= 0f)
		{
			Decide(w, settings, p, music);
		}
		float num4 = 0f;
		flag = !calm;
		if (flag)
		{
			bool flag3;
			switch (Activity)
			{
			case Activity.Wander:
			case Activity.Investigate:
			case Activity.Play:
			case Activity.Hide:
			case Activity.Attention:
			case Activity.Climb:
			case Activity.ClimbEdge:
			case Activity.Build:
				flag3 = true;
				break;
			default:
				flag3 = false;
				break;
			}
			flag = flag3;
		}
		if (flag)
		{
			float value = TargetX - Position.X;
			if (Math.Abs(value) > 8f * Scale && arrivalPause <= 0f)
			{
				float val = (float)((Activity == Activity.Play) ? 150 : 65) * Scale * settings.Activity;
				num4 = (float)Math.Sign(value) * Math.Min(val, MathF.Sqrt(480f * Scale * Math.Abs(value)));
			}
		}
		if (Math.Abs(TargetX - Position.X) <= 9f * Scale && Math.Abs(Velocity.X) > 10f * Scale)
		{
			arrivalPause = 0.45f;
			decisionIn = Math.Min(decisionIn, 1.4f);
		}
		if (Tug > 0.05f)
		{
			num4 = 0f;
		}
		if (num4 != 0f)
		{
			Facing = Math.Sign(num4);
		}
		if (Support.HasValue)
		{
			Velocity = new Vector2(Velocity.X + (num4 - Velocity.X) * (1f - MathF.Exp((0f - dt) * 9f * settings.Advanced.FrictionScale)), 0f);
			if (climbTarget != null && Activity == Activity.Climb && Math.Abs(Position.X - TargetX) < 28f * Scale)
			{
				Support = null;
				previousSupport = null;
			}
		}
		else
		{
			Velocity = new Vector2(Velocity.X * MathF.Exp((0f - dt) * 0.18f * settings.Advanced.FrictionScale), Velocity.Y + 1300f * Scale * settings.Advanced.GravityScale * dt);
		}
		bool flag4 = climbTarget != null && Activity == Activity.Climb && Math.Abs(Position.X - TargetX) < 30f * Scale && remaining > 0f;
		if (flag4)
		{
			Surface surface2 = w.Surfaces.FirstOrDefault((Surface s) => s.Id == climbTarget.Id);
			if (surface2 == null)
			{
				climbTarget = null;
				flag4 = false;
			}
			else
			{
				climbTarget = surface2;
				Velocity = new Vector2(Math.Clamp(TargetX - Position.X, -35f * Scale, 35f * Scale), -85f * Scale);
				if (Position.Y <= surface2.Y)
				{
					Position = new Vector2(Math.Clamp(Position.X, surface2.Left + 12f * Scale, surface2.Right - 12f * Scale), surface2.Y);
					Support = surface2.Id;
					previousSupport = surface2;
					Velocity = Vector2.Zero;
					climbTarget = null;
					flag4 = false;
					Activity = Activity.Sit;
					decisionIn = 2f;
				}
			}
		}
		Vector2 before = Position;
		Position += Velocity * dt;
		if (Support.HasValue && previousSupport != null && !previousSupport.Supports(Position.X))
		{
			Support = null;
			previousSupport = null;
		}
		if (!Support.HasValue && !flag4 && Velocity.Y >= 0f && !Climb.SuspendLanding)
		{
			Surface surface3 = (from s in w.Surfaces
				where before.Y <= s.Y + 0.5f && Position.Y >= s.Y && s.Supports(before.X + (Position.X - before.X) * Math.Clamp((s.Y - before.Y) / Math.Max(0.001f, Position.Y - before.Y), 0f, 1f))
				orderby s.Y
				select s).FirstOrDefault();
			if (surface3 != null)
			{
				float num5 = Velocity.Y / Scale;
				Position = new Vector2(Position.X, surface3.Y);
				float retention = MathF.Pow(0.72f, settings.Advanced.FrictionScale);
				if (num5 > 850f && !settings.ReducedMotion)
				{
					Velocity = new Vector2(Velocity.X * MathF.Pow(0.65f, settings.Advanced.FrictionScale), (0f - Velocity.Y) * 0.22f * settings.Advanced.BounceScale);
				}
				else
				{
					Support = surface3.Id;
					previousSupport = surface3;
					Velocity = new Vector2(Velocity.X * retention, 0f);
				}
				impactIn = ((num5 > 350f) ? 0.8f : 0.25f);
				Mood = Math.Max(0.25f, Mood - 0.025f);
				this.Landed?.Invoke(num5);
			}
		}
		Display display = w.Nearest(Position);
		if (!w.Displays.Any((Display d) => d.Bounds.Contains(new Vector2(Position.X, Math.Clamp(Position.Y, d.Bounds.Y, d.Bounds.Bottom - 1f)))))
		{
			float x = Math.Clamp(Position.X, display.Bounds.X + 15f * Scale, display.Bounds.Right - 15f * Scale);
			Position = new Vector2(x, Position.Y);
			Velocity = new Vector2((0f - Velocity.X) * 0.35f, Velocity.Y);
			TargetX = display.Work.X + display.Work.W / 2f;
		}
		if (Position.Y < w.Displays.Min((Display d) => d.Bounds.Y) - 150f * Scale)
		{
			Position = new Vector2(Position.X, w.Displays.Min((Display d) => d.Bounds.Y) - 150f * Scale);
			Velocity = new Vector2(Velocity.X, Math.Max(0f, Velocity.Y));
		}
		if (Position.Y > display.Bounds.Bottom + 100f * Scale)
		{
			Position = new Vector2(display.Work.Clamp(Position, 30f * Scale).X, display.Work.Y + 50f * Scale);
			Velocity = Vector2.Zero;
			Support = null;
			previousSupport = null;
		}
		Balance *= MathF.Exp((0f - dt) * 5f);
		if (Climb.Phase != ClimbPhase.None)
		{
			Climb.Step(dt, this, w, settings);
		}
		Construction.Step(dt, this, w, settings, Species);
		Motion = SelectMotion(flag4, calm);
		justReleased = false;
	}

	private Motion SelectMotion(bool climbing, bool calm)
	{
		if (justReleased)
		{
			return Motion.Thrown;
		}
		if (MotionOverride.HasValue)
		{
			return MotionOverride.Value;
		}
		if (climbing)
		{
			return Motion.Climbing;
		}
		if (Tug > 0.08f)
		{
			return Motion.Reacting;
		}
		if (!Support.HasValue)
		{
			if (!(Velocity.Y < -70f * Scale))
			{
				if (!(Math.Abs(Velocity.Y) < 70f * Scale))
				{
					return Motion.Falling;
				}
				return Motion.Airborne;
			}
			return Motion.Jumping;
		}
		if (impactIn > 0.5f)
		{
			return Motion.Impact;
		}
		if (impactIn > 0f)
		{
			return Motion.Recovery;
		}
		if (wakeIn > 0f)
		{
			return Motion.Waking;
		}
		if (pettedIn > 0f)
		{
			return Motion.Petted;
		}
		if (notificationIn > 0f && Activity != Activity.Dance)
		{
			return Motion.Reacting;
		}
		if (reactionIn > 0f)
		{
			return Motion.Startled;
		}
		if (Math.Abs(Velocity.X) > 8f * Scale)
		{
			if (!(Math.Abs(Velocity.X) > 105f * Scale))
			{
				if (Activity != Activity.Investigate)
				{
					return Motion.Walking;
				}
				return Motion.Investigating;
			}
			return Motion.Running;
		}
		if (calm)
		{
			return Motion.Sitting;
		}
		return Activity switch
		{
			Activity.Sleep => Motion.Sleeping, 
			Activity.Sit => Motion.Sitting, 
			Activity.Dance => Motion.Dancing, 
			Activity.Hide => Motion.Hiding, 
			Activity.Attention => Motion.Looking, 
			Activity.Play => Motion.Celebrating, 
			Activity.Investigate => Motion.Investigating, 
			_ => Motion.Idle, 
		};
	}
}
