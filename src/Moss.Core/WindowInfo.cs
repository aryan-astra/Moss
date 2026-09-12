namespace Moss.Core;

public sealed record WindowInfo(long Id, Box Bounds, bool Maximized, string? Identity, string? Title);
