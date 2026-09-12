using System;

namespace Moss.Core;

public sealed class RhythmDetector
{
	private float envelope;

	private float baseline;

	private long lastOnset = -1000L;

	public float Sensitivity { get; set; } = 1f;

	public float PushRms(float rms, long milliseconds)
	{
		if (!float.IsFinite(rms) || rms < 0f)
		{
			return 0f;
		}
		float floor = 0.012f / Math.Max(0.3f, Sensitivity);
		envelope = envelope * 0.65f + rms * 0.35f;
		baseline = baseline * 0.98f + envelope * 0.02f;
		if (envelope > Math.Max(floor, baseline * 1.45f) && milliseconds - lastOnset > 230)
		{
			lastOnset = milliseconds;
			return Math.Clamp(envelope / Math.Max(0.02f, baseline) * 0.4f, 0f, 1f);
		}
		return 0f;
	}
}
