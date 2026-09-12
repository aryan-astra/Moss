using System;

namespace Moss.Core;

public sealed class Clip
{
	public float Rate { get; set; } = 1f;

	public float Bob { get; set; }

	public float Lean { get; set; }

	public float Crouch { get; set; }

	public float Eyes { get; set; } = 1f;

	public float Ears { get; set; }

	public float Arms { get; set; }

	public float Duration { get; set; } = 1f;

	public bool Loop { get; set; } = true;

	public float[] Markers { get; set; } = Array.Empty<float>();
}
