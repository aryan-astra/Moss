using System;
using System.IO;

namespace Moss.Core;

public sealed class Reminder
{
	public int Version { get; set; } = 1;

	public Guid Id { get; set; } = Guid.NewGuid();

	public Guid? NoteId { get; set; }

	public ReminderKind Kind { get; set; }

	public string Message { get; set; } = "";

	public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

	public DateTimeOffset Due { get; set; }

	public ReminderStatus Status { get; set; }

	public void Validate()
	{
		if (Version != 1 || Id == Guid.Empty || !Enum.IsDefined(Kind) || !Enum.IsDefined(Status) || Message == null || Message.Length > 1000 || Due == default(DateTimeOffset))
		{
			throw new InvalidDataException("Invalid reminder.");
		}
	}

	public bool IsDue(DateTimeOffset now)
	{
		if (Status == ReminderStatus.Pending)
		{
			return Due <= now;
		}
		return false;
	}
}
