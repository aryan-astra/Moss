using System;
using System.IO;

namespace Moss.Windows;

internal static class Paths
{
	public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Moss");

	public static string Settings => Path.Combine(Root, "settings.json");

	public static string DefaultCharacter => Path.Combine(AppContext.BaseDirectory, "characters", "moss", "character.json");
}
