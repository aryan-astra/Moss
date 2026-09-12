using System;
using System.IO;

namespace Moss.Core;

public sealed class PetMemory
{
	public float Mood { get; set; } = 0.7f;

	public float Energy { get; set; } = 0.8f;

	public float Attention { get; set; } = 0.5f;

	public DateTimeOffset Saved { get; set; } = DateTimeOffset.UtcNow;

	public void Validate()
	{
		bool flag = !float.IsFinite(Mood) || !float.IsFinite(Energy) || !float.IsFinite(Attention);
		if (!flag)
		{
			float mood = Mood;
			bool flag2 = ((mood < 0f || mood > 1f) ? true : false);
			flag = flag2;
		}
		bool flag3 = flag;
		if (!flag3)
		{
			float mood = Energy;
			bool flag2 = ((mood < 0f || mood > 1f) ? true : false);
			flag3 = flag2;
		}
		bool flag4 = flag3;
		if (!flag4)
		{
			float mood = Attention;
			bool flag2 = ((mood < 0f || mood > 1f) ? true : false);
			flag4 = flag2;
		}
		if (flag4)
		{
			throw new InvalidDataException("Invalid pet memory.");
		}
	}
}
