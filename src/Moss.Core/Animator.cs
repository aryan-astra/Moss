using System;

namespace Moss.Core;

public sealed class Animator(Character character)
{
	private float lastVelocity;

	private float accelerationLean;

	private float swayPhase;

	public Motion State { get; private set; }

	public Pose Pose { get; private set; } = new Pose(0f, 0f, 0f, 1f, 0f, 0f);

	public float Phase { get; private set; }

	public float Time { get; private set; }

	public float GaitCycle { get; private set; }

	public int FrameIndex { get; private set; }

	public int FrameCount { get; private set; }

	public float BlendRate { get; set; } = 12f;

	public bool Finished
	{
		get
		{
			if (!character.Animations[State.ToString()].Loop)
			{
				return Time >= character.Animations[State.ToString()].Duration;
			}
			return false;
		}
	}

	public event Action<Motion>? Marker;

	public void Set(Motion state)
	{		if (State != state)
		{
			bool flag = (((uint)(state - 13) <= 2u || state == Motion.Impact) ? true : false);
			bool flag2 = flag;
			float num = State switch
			{
				Motion.Thrown => 0.18f, 
				Motion.Grabbed => 0.16f, 
				Motion.Impact => 0.18f, 
				Motion.Waking => 0.4f, 
				Motion.Reacting => 0.25f, 
				_ => 0f, 
			};
			if (flag2 || !(Time < num))
			{
				State = state;
				Time = 0f;
				Phase = 0f;
				FrameIndex = 0;
			}
		}
	}

	public void Preview(Motion state)
	{
		State = state;
		Time = 0f;
		Phase = 0f;
		FrameIndex = 0;
	}

	public void Restart()
	{
		Time = 0f;
		Phase = 0f;
		FrameIndex = 0;
	}

	public void Step(float dt, float beatPulse, float velocity)
	{
		Clip clip = character.Animations[State.ToString()];
		float num = (velocity - lastVelocity) / Math.Max(0.001f, dt);
		lastVelocity = velocity;
		accelerationLean += (Math.Clamp(num * 0.015f, -9f, 9f) - accelerationLean) * (1f - MathF.Exp((0f - dt) * 7f));
		Motion state = State;
		if (((uint)(state - 1) <= 1u || state == Motion.Investigating || state == Motion.Climbing) ? true : false)
		{
			GaitCycle = (GaitCycle + Math.Abs(velocity) * dt / 38f) % 1f;
		}
		float time = Time;
		Time += dt;
		float duration = clip.Duration;
		// Sprite-frame sequencing rides the existing phase clocks: gait-driven
		// for locomotion (speed-scaled, no new artwork), time-driven otherwise.
		int frames = clip.Frames.Length;
		FrameCount = frames;
		if (frames > 0)
		{
			bool gaited = ((uint)(state - 1) <= 1u || state == Motion.Investigating || state == Motion.Climbing);
			if (gaited)
			{
				FrameIndex = ((int)(GaitCycle * frames)) % frames;
			}
			else if (clip.Loop)
			{
				FrameIndex = duration > 0f ? ((int)(Time / duration * frames)) % frames : 0;
			}
			else
			{
				FrameIndex = duration > 0f ? Math.Min((int)(Time / duration * frames), frames - 1) : 0;
			}
		}
		else
		{
			FrameIndex = 0;
		}
		float[] markers = clip.Markers;
		for (int i = 0; i < markers.Length; i++)
		{
			float num2 = markers[i] * duration;
			bool num3;
			if (!clip.Loop)
			{
				if (!(time < num2))
				{
					continue;
				}
				num3 = Time >= num2;
			}
			else
			{
				num3 = MathF.Floor((time - num2) / duration) != MathF.Floor((Time - num2) / duration);
			}
			if (num3)
			{
				this.Marker?.Invoke(State);
			}
		}
		Phase = (clip.Loop ? ((Phase + dt * clip.Rate * ((float)Math.PI * 2f)) % ((float)Math.PI * 2f)) : (Math.Min(Time, clip.Duration) * clip.Rate * ((float)Math.PI * 2f)));
		swayPhase = (swayPhase + dt * clip.Rate * (float)Math.PI) % ((float)Math.PI * 2f);
		state = State;
		bool flag = (((uint)(state - 1) <= 1u || state == Motion.Investigating || state == Motion.Climbing) ? true : false);
		float num4 = MathF.Sin(flag ? (GaitCycle * ((float)Math.PI * 2f) * 2f) : Phase);
		Pose pose = new Pose(clip.Bob * num4 + ((State == Motion.Dancing) ? (beatPulse * 6f) : 0f), clip.Lean + Math.Clamp(velocity / 35f, -5f, 5f) + accelerationLean, clip.Crouch, clip.Eyes, clip.Ears + num4 * 0.08f, clip.Arms);
		if (State == Motion.Dancing)
		{
			pose = pose with
			{
				Lean = pose.Lean + MathF.Sin(swayPhase) * 8f,
				Arms = clip.Arms + MathF.Cos(Phase) * 2f,
				Crouch = 0.025f + MathF.Sin(Phase) * 0.018f,
				Bob = MathF.Sin(Phase) * 2.5f + beatPulse * 3f
			};
		}
		Pose = Pose.Blend(Pose, pose, 1f - MathF.Exp((0f - dt) * BlendRate));
	}
}
