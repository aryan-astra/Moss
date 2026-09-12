namespace Moss.Core;

public sealed record Display(string Id, Box Bounds, Box Work, float Scale, float RefreshRate = 60f);
