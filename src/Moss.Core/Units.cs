namespace Moss.Core;

public static class Units
{
	public static float Pixels(float dip, float dpi)
	{
		return dip * dpi / 96f;
	}

	public static float Dips(float px, float dpi)
	{
		return px * 96f / dpi;
	}
}
