namespace Moss.Core;

public sealed record Surface(long Id, float Left, float Right, float Y, bool Floor = false, float AnchorX = float.NaN)
{
	public float OriginX
	{
		get
		{
			if (!float.IsNaN(AnchorX))
			{
				return AnchorX;
			}
			return Left;
		}
	}

	public bool Supports(float x, float margin = 0f)
	{
		if (x >= Left + margin)
		{
			return x <= Right - margin;
		}
		return false;
	}
}
