using System;
using System.IO;
using System.Text.Json;

namespace Moss.Windows;

internal static class Log
{
	private static readonly object gate = new object();

	public static void Event(string system, string message)
	{
		lock (gate)
		{
			try
			{
				Directory.CreateDirectory(Paths.Root);
				string text = Path.Combine(Paths.Root, "moss.jsonl");
				if (File.Exists(text) && new FileInfo(text).Length > 1000000)
				{
					File.Move(text, text + ".1", overwrite: true);
				}
				File.AppendAllText(text, JsonSerializer.Serialize(new
				{
					at = DateTimeOffset.UtcNow,
					system = system,
					message = message
				}) + Environment.NewLine);
			}
			catch
			{
			}
		}
	}

	public static void Error(string system, Exception error)
	{
		Event(system, $"{error.GetType().Name}; HRESULT={error.HResult:X8}");
	}
}
