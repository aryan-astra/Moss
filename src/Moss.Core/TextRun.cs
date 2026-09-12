namespace Moss.Core;

public sealed record TextRun(string Text, bool Bold = false, bool Italic = false, bool Code = false, int Heading = 0, string? Link = null);
