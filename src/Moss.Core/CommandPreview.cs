using System;

namespace Moss.Core;

public sealed record CommandPreview(ReminderKind Kind, DateTimeOffset Due, string Message, string Explanation);
