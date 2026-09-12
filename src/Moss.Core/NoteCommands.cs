using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Moss.Core;

public static class NoteCommands
{
	private static readonly Regex duration = new Regex("\\G\\s*(\\d+)\\s*(hours?|hrs?|h|minutes?|mins?|m|seconds?|secs?|s)(?![a-z])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static CommandPreview Parse(string command, DateTimeOffset now, TimeZoneInfo zone)
	{
		if (command.Length > 2000)
		{
			throw new FormatException("This command is too long.");
		}
		Match match = Regex.Match(command.Trim(), "^@(remind|reminder|timer|alarm)\\s+(.+)$", RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			throw new FormatException("Try @timer 25m or @remind tomorrow 9am call home.");
		}
		string text = match.Groups[1].Value.ToLowerInvariant();
		string value = match.Groups[2].Value;
		ReminderKind reminderKind = ((text == "timer") ? ReminderKind.Timer : ((text == "alarm") ? ReminderKind.Alarm : ReminderKind.Reminder));
		string text2 = Regex.Replace(value, "^in\\s+", "", RegexOptions.IgnoreCase);
		long num = 0L;
		int num2 = 0;
		while (true)
		{
			Match match2 = duration.Match(text2, num2);
			if (!match2.Success)
			{
				break;
			}
			if (!long.TryParse(match2.Groups[1].Value, CultureInfo.InvariantCulture, out var result))
			{
				throw new FormatException("That duration is too large.");
			}
			long num3 = char.ToLowerInvariant(match2.Groups[2].Value[0]) switch
			{
				'h' => 3600, 
				'm' => 60, 
				_ => 1, 
			};
			try
			{
				num = checked(num + result * num3);
			}
			catch (OverflowException)
			{
				throw new FormatException("That duration is too large.");
			}
			num2 = match2.Index + match2.Length;
		}
		DateTimeOffset dateTimeOffset;
		string text4;
		if (num2 > 0)
		{
			if (num < 1 || num > 31622400)
			{
				throw new FormatException("Choose a duration between one second and one year.");
			}
			dateTimeOffset = now.AddSeconds(num);
			string text3 = text2;
			int num4 = num2;
			text4 = text3.Substring(num4, text3.Length - num4).Trim();
		}
		else
		{
			if (reminderKind == ReminderKind.Timer)
			{
				throw new FormatException("Give a timer a duration, such as 25m or 1h30m.");
			}
			DateTimeOffset dateTimeOffset2 = TimeZoneInfo.ConvertTime(now, zone);
			Match match3 = Regex.Match(value, "^(?:(tomorrow|monday|tuesday|wednesday|thursday|friday|saturday|sunday)\\s+)?(?:at\\s+)?(\\d{1,2})(?::(\\d{2}))?\\s*(am|pm)?\\b\\s*(.*)$", RegexOptions.IgnoreCase);
			if (!match3.Success)
			{
				throw new FormatException("Use a time such as tomorrow 9am, Friday 5pm or at 19:30.");
			}
			int num5 = int.Parse(match3.Groups[2].Value);
			int num6 = (match3.Groups[3].Success ? int.Parse(match3.Groups[3].Value) : 0);
			string text5 = match3.Groups[4].Value.ToLowerInvariant();
			if (num6 > 59 || num5 > 23 || (text5 != "" && (num5 < 1 || num5 > 12)))
			{
				throw new FormatException("That clock time is not valid.");
			}
			if (text5 != "")
			{
				num5 = num5 % 12 + ((text5 == "pm") ? 12 : 0);
			}
			DateTime dateTime = dateTimeOffset2.Date;
			string text6 = match3.Groups[1].Value.ToLowerInvariant();
			if (text6 == "tomorrow")
			{
				dateTime = dateTime.AddDays(1.0);
			}
			else if (text6 != "")
			{
				int num7 = (Enum.Parse<DayOfWeek>(text6, ignoreCase: true) - dateTime.DayOfWeek + 7) % 7;
				dateTime = dateTime.AddDays(num7);
			}
			DateTime dateTime2 = DateTime.SpecifyKind(dateTime.AddHours(num5).AddMinutes(num6), DateTimeKind.Unspecified);
			if (dateTime2 <= dateTimeOffset2.DateTime)
			{
				dateTime2 = dateTime2.AddDays((!(text6 != "") || !(text6 != "tomorrow")) ? 1 : 7);
			}
			if (zone.IsInvalidTime(dateTime2) || zone.IsAmbiguousTime(dateTime2))
			{
				throw new FormatException("This time crosses an ambiguous daylight-saving change. Use a duration instead.");
			}
			dateTimeOffset = new DateTimeOffset(dateTime2, zone.GetUtcOffset(dateTime2));
			text4 = match3.Groups[5].Value.Trim();
		}
		if (text4.Length == 0)
		{
			text4 = reminderKind switch
			{
				ReminderKind.Alarm => "Alarm", 
				ReminderKind.Timer => "Time is up", 
				_ => "Reminder", 
			};
		}
		if (text4.Length > 1000)
		{
			throw new FormatException("Keep the reminder under 1,000 characters.");
		}
		return new CommandPreview(reminderKind, dateTimeOffset, text4, $"{TimeZoneInfo.ConvertTime(dateTimeOffset, zone):dddd, dd MMM yyyy 'at' HH:mm zzz}\n{text4}\nAll commands require confirmation; unsuffixed hours use the 24-hour clock.");
	}
}
