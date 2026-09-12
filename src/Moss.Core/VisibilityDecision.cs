namespace Moss.Core;

public readonly record struct VisibilityDecision(QuietPolicy Policy, string Reason)
{
	public bool Hidden => Policy == QuietPolicy.Hide;
}
