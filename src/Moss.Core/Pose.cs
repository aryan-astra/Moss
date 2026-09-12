namespace Moss.Core;

public readonly record struct Pose(float Bob, float Lean, float Crouch, float Eyes, float Ears, float Arms)
{
	public static Pose Blend(Pose a, Pose b, float t)
	{
		return new Pose(L(a.Bob, b.Bob, t), L(a.Lean, b.Lean, t), L(a.Crouch, b.Crouch, t), L(a.Eyes, b.Eyes, t), L(a.Ears, b.Ears, t), L(a.Arms, b.Arms, t));
	}

	private static float L(float a, float b, float t)
	{
		return a + (b - a) * t;
	}
}
