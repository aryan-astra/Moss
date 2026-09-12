using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Moss.Core;

public sealed class Note
{
	public int Version { get; set; } = 1;

	public Guid Id { get; set; } = Guid.NewGuid();

	public string Title { get; set; } = "Untitled";

	public string Markdown { get; set; } = "";

	public string? RichText { get; set; }

	public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

	public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;

	public string Preview
	{
		get
		{
			string text = Regex.Replace(Markdown, "\\s+", " ").Trim();
			return text.Substring(0, Math.Min(text.Length, 110));
		}
	}

	public void Validate()
	{
		if (Version == 1 && !(Id == Guid.Empty) && Title != null && Markdown != null && Title.Length <= 200 && Markdown.Length <= 1000000)
		{
			string? richText = RichText;
			if (richText == null || richText.Length <= 4000000)
			{
				return;
			}
		}
		throw new InvalidDataException("Invalid note.");
	}
}
