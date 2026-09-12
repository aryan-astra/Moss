using System;
using System.Collections.Generic;
using System.IO;

namespace Moss.Core;

// Advanced timing/frequency/physics/animation controls. Normal users only
// ever see categorical levels (Frequency / AnimationStyle / MischiefLevel);
// every numeric here is editable in Feature Lab with min/max validation and
// persists through Settings. Defaults reproduce the original tuning exactly.
public sealed class AdvancedSettings
{
	public Frequency ClimbFrequency { get; set; } = Frequency.Occasional;

	public Frequency BuildFrequency { get; set; } = Frequency.Rare;

	public AnimationStyle AnimationStyle { get; set; } = AnimationStyle.Normal;

	public MischiefLevel MischiefLevel { get; set; } = MischiefLevel.Normal;

	public TimingPreset Preset { get; set; } = TimingPreset.Normal;

	public float ClimbCooldownSec { get; set; } = 180f;

	public float ClimbIntervalMinSec { get; set; } = 300f;

	public float ClimbIntervalMaxSec { get; set; } = 1200f;

	public float BuildCooldownSec { get; set; } = 7200f;

	public float BuildIntervalMinSec { get; set; } = 3600f;

	public float BuildIntervalMaxSec { get; set; } = 10800f;

	public float StructureLifetimeMin { get; set; } = 30f;

	public float RareEventProbability { get; set; } = 0.05f;

	public float WanderDecisionMinSec { get; set; } = 3f;

	public float WanderDecisionMaxSec { get; set; } = 9f;

	public float PettingSensitivity { get; set; } = 1f;

	public float GrabSensitivity { get; set; } = 1f;

	public float ThrowPower { get; set; } = 1f;

	public float ObjectGripStrength { get; set; } = 1f;

	public float GravityScale { get; set; } = 1f;

	public float FrictionScale { get; set; } = 1f;

	public float BounceScale { get; set; } = 1f;

	public float JumpVelocity { get; set; } = 550f;

	public float ClimbSpeed { get; set; } = 1f;

	public float AnimationSpeed { get; set; } = 1f;

	public float BlendRate { get; set; } = 12f;

	public float MusicEnergyThreshold { get; set; } = 0.18f;

	public float RhythmSensitivity { get; set; } = 1f;

	public float ContextReactionCooldownSec { get; set; } = 2f;

	public float CursorAttractRadius { get; set; } = 240f;

	public float WindowInvestigateProbability { get; set; } = 1f;

	public float SleepThreshold { get; set; } = 0.35f;

	public float EnergyRecoveryRate { get; set; } = 1f;

	public float MoodRecoveryRate { get; set; } = 1f;

	public float PhysicsStepSec { get; set; } = 1f / 120f;

	public float SurpriseIntervalMinSec { get; set; } = 120f;

	public float SurpriseIntervalMaxSec { get; set; } = 240f;

	public float PlayScoreBoost { get; set; } = 1f;

	public long LastBuildUnix { get; set; }

	public static readonly IReadOnlyDictionary<string, string> Help = new Dictionary<string, string>
	{
		["ClimbFrequency"] = "How often Moss may independently decide to climb a screen edge.",
		["BuildFrequency"] = "How often Moss may independently decide to build something.",
		["AnimationStyle"] = "Overall animation energy; maps to playback speed.",
		["MischiefLevel"] = "How often playful surprises (football, play) happen.",
		["Preset"] = "Named bundle of the numeric values below. Editing any value switches to Custom.",
		["ClimbCooldownSec"] = "Quiet time after a climb before the next one may start.",
		["ClimbIntervalMinSec"] = "Shortest wait between independent climb decisions.",
		["ClimbIntervalMaxSec"] = "Longest wait between independent climb decisions.",
		["BuildCooldownSec"] = "Quiet time after a construction before the next one may start.",
		["BuildIntervalMinSec"] = "Shortest wait between independent build decisions.",
		["BuildIntervalMaxSec"] = "Longest wait between independent build decisions.",
		["StructureLifetimeMin"] = "How long a finished house or platform stays in the world.",
		["RareEventProbability"] = "Chance per decision for rare flourishes (peek, pause-and-look).",
		["WanderDecisionMinSec"] = "Shortest idle behavior duration.",
		["WanderDecisionMaxSec"] = "Longest idle behavior duration.",
		["PettingSensitivity"] = "How easily rubbing counts as a pet. Higher is easier.",
		["GrabSensitivity"] = "How strongly the grab spring follows your hand.",
		["ThrowPower"] = "Scales release velocity when you throw Moss.",
		["ObjectGripStrength"] = "How hard the pet holds toys before a hard pull wins.",
		["GravityScale"] = "Multiplies gravity for the pet, twig and football.",
		["FrictionScale"] = "Multiplies ground friction and air drag.",
		["BounceScale"] = "Multiplies landing and ball rebound. Zero disables bounce.",
		["JumpVelocity"] = "Takeoff speed of an autonomous jump.",
		["ClimbSpeed"] = "Multiplies vertical climbing speed.",
		["AnimationSpeed"] = "Multiplies animation playback speed.",
		["BlendRate"] = "How fast poses blend into each other. Higher is snappier.",
		["MusicEnergyThreshold"] = "Minimum energy for dancing to start.",
		["RhythmSensitivity"] = "How easily speaker-output onsets register.",
		["ContextReactionCooldownSec"] = "Quiet time between window/media/notification reactions.",
		["CursorAttractRadius"] = "How close the cursor must be to attract attention.",
		["WindowInvestigateProbability"] = "Chance an investigation picks a window versus wandering.",
		["SleepThreshold"] = "Energy level below which sleep becomes likely.",
		["EnergyRecoveryRate"] = "Multiplies energy gain and drain.",
		["MoodRecoveryRate"] = "Multiplies mood drift toward neutral.",
		["PhysicsStepSec"] = "Fixed simulation step. Smaller is smoother and costlier.",
		["SurpriseIntervalMinSec"] = "Shortest wait between surprise play.",
		["SurpriseIntervalMaxSec"] = "Longest wait between surprise play.",
		["PlayScoreBoost"] = "Multiplies the play behavior score.",
		["LastBuildUnix"] = "When the last construction finished. Used for cooldowns."
	};

	public void SetClimbing(Frequency level)
	{
		ClimbFrequency = level;
		switch (level)
		{
		case Frequency.Off:
			break;
		case Frequency.Rare:
			ClimbIntervalMinSec = 1200f;
			ClimbIntervalMaxSec = 3600f;
			ClimbCooldownSec = 600f;
			break;
		case Frequency.Occasional:
			ClimbIntervalMinSec = 300f;
			ClimbIntervalMaxSec = 1200f;
			ClimbCooldownSec = 180f;
			break;
		default:
			ClimbIntervalMinSec = 60f;
			ClimbIntervalMaxSec = 420f;
			ClimbCooldownSec = 30f;
			break;
		}
	}

	public void SetConstruction(Frequency level)
	{
		BuildFrequency = level;
		switch (level)
		{
		case Frequency.Off:
			break;
		case Frequency.Rare:
			BuildIntervalMinSec = 3600f;
			BuildIntervalMaxSec = 10800f;
			BuildCooldownSec = 7200f;
			break;
		case Frequency.Occasional:
			BuildIntervalMinSec = 1200f;
			BuildIntervalMaxSec = 3600f;
			BuildCooldownSec = 1800f;
			break;
		default:
			BuildIntervalMinSec = 300f;
			BuildIntervalMaxSec = 1200f;
			BuildCooldownSec = 300f;
			break;
		}
	}

	public void SetAnimationStyle(AnimationStyle style)
	{
		AnimationStyle = style;
		AnimationSpeed = style switch
		{
			AnimationStyle.Subtle => 0.8f, 
			AnimationStyle.Lively => 1.3f, 
			_ => 1f, 
		};
	}

	public void SetMischief(MischiefLevel level)
	{
		MischiefLevel = level;
		switch (level)
		{
		case MischiefLevel.Low:
			SurpriseIntervalMinSec = 240f;
			SurpriseIntervalMaxSec = 480f;
			PlayScoreBoost = 0.7f;
			break;
		case MischiefLevel.High:
			SurpriseIntervalMinSec = 45f;
			SurpriseIntervalMaxSec = 120f;
			PlayScoreBoost = 1.4f;
			break;
		default:
			SurpriseIntervalMinSec = 120f;
			SurpriseIntervalMaxSec = 240f;
			PlayScoreBoost = 1f;
			break;
		}
	}

	public void ApplyPreset(TimingPreset preset)
	{
		Preset = preset;
		switch (preset)
		{
		case TimingPreset.Relaxed:
			ClimbIntervalMinSec = 900f;
			ClimbIntervalMaxSec = 2700f;
			BuildIntervalMinSec = 7200f;
			BuildIntervalMaxSec = 14400f;
			RareEventProbability = 0.02f;
			AnimationSpeed = 0.85f;
			ClimbSpeed = 0.8f;
			EnergyRecoveryRate = 1.2f;
			break;
		case TimingPreset.Lively:
			ClimbIntervalMinSec = 120f;
			ClimbIntervalMaxSec = 600f;
			BuildIntervalMinSec = 1800f;
			BuildIntervalMaxSec = 5400f;
			RareEventProbability = 0.09f;
			AnimationSpeed = 1.2f;
			ClimbSpeed = 1.25f;
			PlayScoreBoost = 1.3f;
			break;
		case TimingPreset.Experimental:
			ClimbIntervalMinSec = 30f;
			ClimbIntervalMaxSec = 180f;
			BuildIntervalMinSec = 600f;
			BuildIntervalMaxSec = 1800f;
			BuildCooldownSec = 300f;
			ClimbCooldownSec = 20f;
			RareEventProbability = 0.2f;
			AnimationSpeed = 1.4f;
			ClimbSpeed = 1.6f;
			break;
		case TimingPreset.Custom:
			break;
		default:
			ClimbIntervalMinSec = 300f;
			ClimbIntervalMaxSec = 1200f;
			BuildIntervalMinSec = 3600f;
			BuildIntervalMaxSec = 10800f;
			RareEventProbability = 0.05f;
			AnimationSpeed = 1f;
			ClimbSpeed = 1f;
			EnergyRecoveryRate = 1f;
			PlayScoreBoost = 1f;
			ClimbCooldownSec = 180f;
			BuildCooldownSec = 7200f;
			break;
		}
	}

	public void Validate()
	{
		if (!Enum.IsDefined(ClimbFrequency) || !Enum.IsDefined(BuildFrequency) || !Enum.IsDefined(AnimationStyle) || !Enum.IsDefined(MischiefLevel) || !Enum.IsDefined(Preset))
		{
			throw new InvalidDataException("Unsupported advanced settings.");
		}
		float[] values = { ClimbCooldownSec, ClimbIntervalMinSec, ClimbIntervalMaxSec, BuildCooldownSec, BuildIntervalMinSec, BuildIntervalMaxSec, StructureLifetimeMin, RareEventProbability, WanderDecisionMinSec, WanderDecisionMaxSec, PettingSensitivity, GrabSensitivity, ThrowPower, ObjectGripStrength, GravityScale, FrictionScale, BounceScale, JumpVelocity, ClimbSpeed, AnimationSpeed, BlendRate, MusicEnergyThreshold, RhythmSensitivity, ContextReactionCooldownSec, CursorAttractRadius, WindowInvestigateProbability, SleepThreshold, EnergyRecoveryRate, MoodRecoveryRate, PhysicsStepSec, SurpriseIntervalMinSec, SurpriseIntervalMaxSec, PlayScoreBoost };
		foreach (float value in values)
		{
			if (!float.IsFinite(value))
			{
				throw new InvalidDataException("Invalid advanced settings.");
			}
		}
		if (LastBuildUnix < 0)
		{
			throw new InvalidDataException("Invalid advanced settings.");
		}
		ClimbCooldownSec = Math.Clamp(ClimbCooldownSec, 0f, 7200f);
		ClimbIntervalMinSec = Math.Clamp(ClimbIntervalMinSec, 30f, 3600f);
		ClimbIntervalMaxSec = Math.Clamp(ClimbIntervalMaxSec, ClimbIntervalMinSec, 7200f);
		BuildCooldownSec = Math.Clamp(BuildCooldownSec, 0f, 14400f);
		BuildIntervalMinSec = Math.Clamp(BuildIntervalMinSec, 300f, 7200f);
		BuildIntervalMaxSec = Math.Clamp(BuildIntervalMaxSec, BuildIntervalMinSec, 14400f);
		StructureLifetimeMin = Math.Clamp(StructureLifetimeMin, 5f, 180f);
		RareEventProbability = Math.Clamp(RareEventProbability, 0f, 1f);
		WanderDecisionMinSec = Math.Clamp(WanderDecisionMinSec, 1f, 30f);
		WanderDecisionMaxSec = Math.Clamp(WanderDecisionMaxSec, WanderDecisionMinSec, 60f);
		PettingSensitivity = Math.Clamp(PettingSensitivity, 0.3f, 3f);
		GrabSensitivity = Math.Clamp(GrabSensitivity, 0.3f, 3f);
		ThrowPower = Math.Clamp(ThrowPower, 0.3f, 2.5f);
		ObjectGripStrength = Math.Clamp(ObjectGripStrength, 0.3f, 3f);
		GravityScale = Math.Clamp(GravityScale, 0.2f, 3f);
		FrictionScale = Math.Clamp(FrictionScale, 0.2f, 3f);
		BounceScale = Math.Clamp(BounceScale, 0f, 1.5f);
		JumpVelocity = Math.Clamp(JumpVelocity, 200f, 900f);
		ClimbSpeed = Math.Clamp(ClimbSpeed, 0.3f, 3f);
		AnimationSpeed = Math.Clamp(AnimationSpeed, 0.3f, 2.5f);
		BlendRate = Math.Clamp(BlendRate, 2f, 30f);
		MusicEnergyThreshold = Math.Clamp(MusicEnergyThreshold, 0f, 0.8f);
		RhythmSensitivity = Math.Clamp(RhythmSensitivity, 0.3f, 3f);
		ContextReactionCooldownSec = Math.Clamp(ContextReactionCooldownSec, 0.5f, 30f);
		CursorAttractRadius = Math.Clamp(CursorAttractRadius, 50f, 600f);
		WindowInvestigateProbability = Math.Clamp(WindowInvestigateProbability, 0f, 1f);
		SleepThreshold = Math.Clamp(SleepThreshold, 0.05f, 0.8f);
		EnergyRecoveryRate = Math.Clamp(EnergyRecoveryRate, 0f, 3f);
		MoodRecoveryRate = Math.Clamp(MoodRecoveryRate, 0f, 3f);
		PhysicsStepSec = Math.Clamp(PhysicsStepSec, 1f / 240f, 1f / 30f);
		SurpriseIntervalMinSec = Math.Clamp(SurpriseIntervalMinSec, 30f, 600f);
		SurpriseIntervalMaxSec = Math.Clamp(SurpriseIntervalMaxSec, SurpriseIntervalMinSec, 1200f);
		PlayScoreBoost = Math.Clamp(PlayScoreBoost, 0.5f, 2f);
	}
}
