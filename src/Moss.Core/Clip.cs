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

	/// <summary>
	/// Optional sprite-frame keys into the character's packed atlas
	/// (e.g. ["Walking#0","Walking#1","Walking#2"]). Empty for procedural
	/// (vector) characters. Frames are sequenced by the Animator's existing
	/// phase clocks, so timing/speed changes need no new artwork.
	/// </summary>
	public string[] Frames { get; set; } = Array.Empty<string>();
}
