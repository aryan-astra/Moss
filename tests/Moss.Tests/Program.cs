// Moss.Tests: deterministic regression checks over Moss.Core.
// Returns 0 when every check passes, 1 otherwise.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Moss.Core;

internal static class Program
{
	internal static int failures;

	internal static void Pass(string name)
	{
		Console.WriteLine("PASS  " + name);
	}

	internal static void Check(bool condition, string name)
	{
		if (condition)
		{
			Pass(name);
		}
		else
		{
			Console.WriteLine("FAIL  " + name);
			failures++;
		}
	}

	internal static void Expect<TException>(Action action, string name) where TException : Exception
	{
		try
		{
			action();
			Console.WriteLine("FAIL  " + name + " (no exception)");
			failures++;
		}
		catch (TException)
		{
			Pass(name);
		}
		catch (Exception error)
		{
			Console.WriteLine("FAIL  " + name + " (wrong exception: " + error.GetType().Name + ")");
			failures++;
		}
	}

	internal static string RepoRoot { get; private set; } = "";

	private static string FindRepoRoot()
	{
		string? directory = AppContext.BaseDirectory;
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory, "Moss.sln")) && Directory.Exists(Path.Combine(directory, "characters")))
			{
				return directory;
			}
			directory = Directory.GetParent(directory)?.FullName;
		}
		throw new InvalidOperationException("Repository root (Moss.sln + characters/) not found.");
	}

	private static int Main()
	{
		string root = FindRepoRoot();
		RepoRoot = root;

		string[] species = { "moss", "miso", "pip", "lark", "inky", "clover", "puck" };
		int motions = Enum.GetNames<Motion>().Length;
		Check(motions == 28, "motion set has 28 states");
		foreach (string name in species)
		{
			Character character = Character.Load(Path.Combine(root, "characters", name, "character.json"));
			Check(character.Name.Length > 0 && character.Animations.Count == motions, "character " + name + " loads with full motion set");
		}

		string empty = Path.GetTempFileName();
		File.WriteAllText(empty, string.Empty);
		Expect<JsonException>(() => Character.Load(empty), "empty character rejected");

		string dragon = Path.GetTempFileName();
		File.WriteAllText(dragon, "{\"species\":\"dragon\"}");
		Expect<InvalidDataException>(() => Character.Load(dragon), "unknown species rejected");

		string huge = Path.GetTempFileName();
		File.WriteAllText(huge, new string('x', 300000));
		Expect<InvalidDataException>(() => Character.Load(huge), "oversized character rejected");

		AudioActivity audio = new AudioActivity();
		for (int i = 0; i < 40; i++)
		{
			audio.Step(0f, 0.1f);
		}
		Check(!audio.Active, "silence never activates audio fallback");
		for (int i = 0; i < 10; i++)
		{
			audio.Step(0.5f, 0.1f);
		}
		Check(audio.Active, "sustained sound activates audio fallback");
		audio.Reset();
		Check(!audio.Active, "audio reset releases activity");
		audio.Step(float.NaN, 0.1f);
		Check(!audio.Active, "invalid levels never activate");

		DateTimeOffset now = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
		CommandPreview timer = NoteCommands.Parse("@timer 25m", now, TimeZoneInfo.Utc);
		Check(timer.Due - now == TimeSpan.FromMinutes(25), "compact timer parses to exact deadline");
		CommandPreview quick = NoteCommands.Parse("@timer 5s", now, TimeZoneInfo.Utc);
		Check(quick.Due - now == TimeSpan.FromSeconds(5), "second-granularity timer parses");
		Expect<FormatException>(() => NoteCommands.Parse("hello world", now, TimeZoneInfo.Utc), "plain text is not a command");

		string? informational = typeof(Character).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
		Check(informational != null && (informational == "1.2.0" || informational.StartsWith("1.2.0+", StringComparison.Ordinal)), "core version stamp is 1.2.0 (got " + informational + ")");

		FeatureChecks.Run();

		Console.WriteLine(failures == 0 ? "RESULT: all checks passed" : "RESULT: " + failures + " check(s) failed");
		return failures == 0 ? 0 : 1;
	}
}
