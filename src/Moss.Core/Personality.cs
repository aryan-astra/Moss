using System.IO;

namespace Moss.Core;

public sealed class Personality
{
	public float Curiosity { get; set; } = 0.75f;

	public float Playfulness { get; set; } = 0.7f;

	public float Sociability { get; set; } = 0.65f;

	public float Calmness { get; set; } = 0.5f;

	public void Validate()
	{
		float[] array = new float[4] { Curiosity, Playfulness, Sociability, Calmness };
		foreach (float num in array)
		{
			if (!float.IsFinite(num) || num < 0f || num > 1f)
			{
				throw new InvalidDataException("Personality values must be between zero and one.");
			}
		}
	}
}
