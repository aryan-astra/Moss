using System;

namespace Moss.Core;

public sealed class AudioActivity
{
	private float audible;

	private float silent;

	public bool Active { get; private set; }

	public void Reset()
	{
		audible = (silent = 0f);
		Active = false;
	}

	public bool Step(float peak, float dt)
	{
		if (!float.IsFinite(peak) || !float.IsFinite(dt) || dt <= 0f)
		{
			return Active;
		}
		dt = Math.Min(dt, 0.25f);
		if (peak > 0.003f)
		{
			audible = Math.Min(1f, audible + dt);
			silent = 0f;
		}
		else
		{
			silent += dt;
			audible = Math.Max(0f, audible - dt * 0.5f);
		}
		if (audible >= 0.65f)
		{
			Active = true;
		}
		if (silent >= 3f)
		{
			Active = false;
		}
		return Active;
	}
}
