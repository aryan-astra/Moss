using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Moss.Windows;

internal sealed class NotificationObserver : IDisposable
{
	private UserNotificationListener? listener;

	private bool enabled;

	private bool disposed;

	private bool busy;

	private long lastPoll;

	private HashSet<uint> known = new HashSet<uint>();

	public string Status { get; private set; } = "Notification access off";

	public static bool HasPackageIdentity
	{
		get
		{
			int length = 0;
			return Native.GetCurrentPackageFullName(ref length, null) != 15700;
		}
	}

	public event Action? Arrived;

	public async Task<bool> EnableAsync()
	{
		if (!HasPackageIdentity)
		{
			Status = "Requires the signed MSIX installation";
			return false;
		}
		try
		{
			listener = UserNotificationListener.Current;
			UserNotificationListenerAccessStatus userNotificationListenerAccessStatus = await listener.RequestAccessAsync();
			if (disposed)
			{
				return false;
			}
			enabled = userNotificationListenerAccessStatus == UserNotificationListenerAccessStatus.Allowed;
			Status = (enabled ? "Allowed — notification IDs only" : "Denied in Windows notification-access settings");
			if (enabled)
			{
				known = (await listener.GetNotificationsAsync(NotificationKinds.Toast)).Select((UserNotification n) => n.Id).ToHashSet();
			}
			return enabled;
		}
		catch (Exception error)
		{
			enabled = false;
			Log.Error("notifications", error);
			Status = "Notification API unavailable";
			return false;
		}
	}

	public void Disable()
	{
		enabled = false;
		listener = null;
		known.Clear();
		Status = "Notification access off";
	}

	public async void Poll()
	{
		if (!enabled || listener == null || busy || disposed || Environment.TickCount64 - lastPoll < 2500)
		{
			return;
		}
		busy = true;
		lastPoll = Environment.TickCount64;
		try
		{
			if (listener.GetAccessStatus() != UserNotificationListenerAccessStatus.Allowed)
			{
				Disable();
				Status = "Permission revoked in Windows";
				return;
			}
			IReadOnlyList<UserNotification> source = await listener.GetNotificationsAsync(NotificationKinds.Toast);
			if (enabled && !disposed)
			{
				HashSet<uint> first = source.Select((UserNotification n) => n.Id).ToHashSet();
				bool num = first.Except(known).Any();
				known = first;
				if (num)
				{
					this.Arrived?.Invoke();
				}
			}
		}
		catch (Exception error)
		{
			Log.Error("notifications", error);
			Disable();
			Status = "Notification listener unavailable";
		}
		finally
		{
			busy = false;
		}
	}

	public void Dispose()
	{
		disposed = true;
		Disable();
	}
}
