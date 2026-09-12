using System.Collections.Generic;

namespace Moss.Core;

public static class MediaSelection
{
	public static int Choose(IReadOnlyList<PlaybackCandidate> candidates)
	{
		int result = -1;
		int num = -1;
		for (int i = 0; i < candidates.Count; i++)
		{
			PlaybackCandidate playbackCandidate = candidates[i];
			int num2 = (playbackCandidate.Playing ? 100 : 0) + (playbackCandidate.Current ? 10 : 0) + (playbackCandidate.Previous ? 1 : 0);
			if (num2 > num)
			{
				num = num2;
				result = i;
			}
		}
		return result;
	}
}
