namespace Moss.Core;

public static class DesktopVisibility
{
	public static ShellContext FromShellResult(int hresult, int rawState)
	{
		if (hresult != 0)
		{
			return new ShellContext(Presenting: false, ExclusiveFullscreen: false);
		}
		return new ShellContext(rawState == 4, rawState == 3);
	}

	public static VisibilityDecision Decide(Settings settings, bool quiet, bool fullscreen, bool presenting, bool renderFailed, bool manualReveal)
	{
		if (!settings.Visible)
		{
			return new VisibilityDecision(QuietPolicy.Hide, "Hidden by visibility setting");
		}
		if (quiet)
		{
			return new VisibilityDecision(QuietPolicy.Hide, "Hidden by quiet mode");
		}
		if (renderFailed)
		{
			return new VisibilityDecision(QuietPolicy.Hide, "Rendering recovery needed");
		}
		if (manualReveal)
		{
			return new VisibilityDecision(QuietPolicy.Stay, "Visible — temporary manual reveal");
		}
		if (presenting)
		{
			return new VisibilityDecision(settings.Presentation, "Windows presentation policy");
		}
		if (fullscreen)
		{
			return new VisibilityDecision(settings.Fullscreen, "Fullscreen application policy");
		}
		return new VisibilityDecision(QuietPolicy.Stay, "Visible on desktop");
	}
}
