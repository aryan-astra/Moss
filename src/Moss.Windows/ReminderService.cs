using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Moss.Core;

namespace Moss.Windows;

internal sealed class ReminderService
{
	private readonly DocumentStore store;

	private readonly Settings config;

	private readonly HashSet<Guid> announced = new HashSet<Guid>();

	public List<Reminder> Items { get; }

	public string SchedulerStatus { get; private set; } = "Closed-app delivery is off";

	public event Action<Reminder>? Due;

	public ReminderService(DocumentStore storage, Settings settings)
	{
		store = storage;
		config = settings;
		Items = store.LoadAll("reminders", delegate(Reminder r)
		{
			r.Validate();
		});
	}

	public Reminder Create(CommandPreview preview, Guid? note)
	{
		Reminder reminder = new Reminder
		{
			Kind = preview.Kind,
			Due = preview.Due,
			Message = preview.Message,
			NoteId = note
		};
		reminder.Validate();
		store.Save("reminders", reminder.Id, reminder);
		Items.Add(reminder);
		if (config.ClosedAppReminders)
		{
			Schedule(reminder);
		}
		return reminder;
	}

	public void Tick()
	{
		Reminder[] array = Items.Where(delegate(Reminder r)
		{
			ReminderStatus status = r.Status;
			return (uint)status <= 1u;
		}).ToArray();
		foreach (Reminder reminder in array)
		{
			if (reminder.IsDue(DateTimeOffset.UtcNow))
			{
				Commit(reminder, ReminderStatus.Due, reminder.Due);
				RemoveTask(reminder);
			}
			if (reminder.Status == ReminderStatus.Due && announced.Add(reminder.Id))
			{
				this.Due?.Invoke(reminder);
			}
		}
	}

	public void Dismiss(Reminder r)
	{
		Commit(r, ReminderStatus.Dismissed, r.Due);
		RemoveTask(r);
	}

	public void Cancel(Reminder r)
	{
		Commit(r, ReminderStatus.Cancelled, r.Due);
		RemoveTask(r);
	}

	public void Snooze(Reminder r)
	{
		Commit(r, ReminderStatus.Pending, DateTimeOffset.UtcNow.AddMinutes(5.0));
		announced.Remove(r.Id);
		if (config.ClosedAppReminders)
		{
			Schedule(r);
		}
	}

	private void Commit(Reminder r, ReminderStatus status, DateTimeOffset due)
	{
		Reminder reminder = new Reminder
		{
			Version = r.Version,
			Id = r.Id,
			NoteId = r.NoteId,
			Kind = r.Kind,
			Message = r.Message,
			Created = r.Created,
			Due = due,
			Status = status
		};
		reminder.Validate();
		store.Save("reminders", reminder.Id, reminder);
		r.Due = due;
		r.Status = status;
	}

	public void Reconcile()
	{
		foreach (Reminder item in Items)
		{
			if (config.ClosedAppReminders && item.Status == ReminderStatus.Pending)
			{
				Schedule(item);
			}
			else
			{
				RemoveTask(item);
			}
		}
		if (!config.ClosedAppReminders)
		{
			SchedulerStatus = "Closed-app delivery is off";
		}
	}

	private static string TaskName(Reminder r)
	{
		using WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
		return "Moss-" + windowsIdentity.User.Value + "-" + r.Id.ToString("N");
	}

	private void Schedule(Reminder r)
	{
		List<object> com = new List<object>();
		try
		{
			dynamic val = Keep<object>(Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")));
			val.Connect();
			dynamic val2 = Keep<object>((object)val.GetFolder("\\"));
			dynamic val3 = Keep<object>((object)val.NewTask(0));
			dynamic val4 = Keep<object>((object)val3.Principal);
			using WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
			val4.UserId = windowsIdentity.User.Value;
			val4.LogonType = 3;
			val4.RunLevel = 0;
			dynamic val5 = Keep<object>((object)val3.Settings);
			val5.StartWhenAvailable = true;
			val5.DisallowStartIfOnBatteries = false;
			val5.StopIfGoingOnBatteries = false;
			val5.ExecutionTimeLimit = "PT0S";
			val5.MultipleInstances = 2;
			dynamic val6 = Keep<object>((object)val3.Triggers);
			dynamic val7 = Keep<object>((object)val6.Create(1));
			val7.StartBoundary = r.Due.ToLocalTime().ToString("yyyy-MM-dd'T'HH:mm:sszzz");
			val7.EndBoundary = r.Due.AddDays(30.0).ToLocalTime().ToString("yyyy-MM-dd'T'HH:mm:sszzz");
			dynamic val8 = Keep<object>((object)val6.Create(9));
			val8.UserId = val4.UserId;
			dynamic val9 = Keep<object>((object)val3.Actions);
			dynamic val10 = Keep<object>((object)val9.Create(0));
			val10.Path = Environment.ProcessPath;
			val10.Arguments = "--reminder";
			val10.WorkingDirectory = AppContext.BaseDirectory;
			Keep<object>((object)val2.RegisterTaskDefinition(TaskName(r), val3, 6, val4.UserId, null, 3, null));
			SchedulerStatus = "Windows scheduled delivery registered (while signed in; missed delivery catches up)";
		}
		catch (Exception error)
		{
			Log.Error("reminder-schedule", error);
			SchedulerStatus = "Windows blocked scheduling. Reminders are saved; keep Moss running for delivery.";
		}
		finally
		{
			foreach (object item in com.AsEnumerable().Reverse())
			{
				if (Marshal.IsComObject(item))
				{
					Marshal.FinalReleaseComObject(item);
				}
			}
		}
		T Keep<T>(T value)
		{
			com.Add(value);
			return value;
		}
	}

	private void RemoveTask(Reminder r)
	{
		object obj = null;
		object obj2 = null;
		try
		{
			dynamic val = (obj = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")));
			val.Connect();
			dynamic val2 = (obj2 = val.GetFolder("\\"));
			val2.DeleteTask(TaskName(r), 0);
		}
		catch (COMException)
		{
		}
		catch (Exception error)
		{
			Log.Error("reminder-unschedule", error);
		}
		finally
		{
			if (obj2 != null && Marshal.IsComObject(obj2))
			{
				Marshal.FinalReleaseComObject(obj2);
			}
			if (obj != null && Marshal.IsComObject(obj))
			{
				Marshal.FinalReleaseComObject(obj);
			}
		}
	}
}
