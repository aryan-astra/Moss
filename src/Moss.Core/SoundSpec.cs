using System.IO;

namespace Moss.Core;

public sealed class SoundSpec
{
	public float Frequency { get; set; } = 430f;

	public float Decay { get; set; } = 65f;

	public float Volume { get; set; } = 0.06f;

	public void Validate()
	{
		bool flag = !float.IsFinite(Frequency + Decay + Volume);
		if (!flag)
		{
			float frequency = Frequency;
			bool flag2 = ((frequency < 100f || frequency > 2000f) ? true : false);
			flag = flag2;
		}
		bool flag3 = flag;
		if (!flag3)
		{
			float frequency = Decay;
			bool flag2 = ((frequency < 20f || frequency > 150f) ? true : false);
			flag3 = flag2;
		}
		bool flag4 = flag3;
		if (!flag4)
		{
			float frequency = Volume;
			bool flag2 = ((frequency < 0f || frequency > 0.15f) ? true : false);
			flag4 = flag2;
		}
		if (flag4)
		{
			throw new InvalidDataException("Invalid sound parameters.");
		}
	}
}
