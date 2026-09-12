namespace Moss.Core;

public sealed class PetProfile
{
	public string Name { get; set; } = "";

	public float Size { get; set; } = 1f;

	public bool Hat { get; set; }

	public bool Glasses { get; set; }

	public PetMemory? Memory { get; set; }
}
