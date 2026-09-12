namespace Moss.Core;

public sealed class MediaState
{
	public bool Playing { get; private set; }

	public int Changes { get; private set; }

	public void Update(bool playing, bool trackChanged = false)
	{
		if (playing != Playing || trackChanged)
		{
			Changes++;
		}
		Playing = playing;
	}
}
