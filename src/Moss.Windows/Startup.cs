using System;
using System.Threading.Tasks;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Moss.Windows;

internal static class Startup
{
	private const string Key = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	public static bool Enabled
	{
		get
		{
			try
			{
				if (NotificationObserver.HasPackageIdentity)
				{
					StartupTaskState state = StartupTask.GetAsync("MossStartup").AsTask().GetAwaiter()
						.GetResult()
						.State;
					return (state == StartupTaskState.Enabled || state == StartupTaskState.EnabledByPolicy) ? true : false;
				}
				using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
				return registryKey?.GetValue("Moss") != null;
			}
			catch (Exception error)
			{
				Log.Error("startup-status", error);
				return false;
			}
		}
	}

	public static async Task SetAsync(bool enabled)
	{
		if (NotificationObserver.HasPackageIdentity)
		{
			StartupTask startupTask = await StartupTask.GetAsync("MossStartup");
			if (enabled)
			{
				StartupTaskState startupTaskState = await startupTask.RequestEnableAsync();
				if (startupTaskState != StartupTaskState.Enabled && startupTaskState != StartupTaskState.EnabledByPolicy)
				{
					throw new InvalidOperationException("Startup is disabled by Windows or policy.");
				}
			}
			else
			{
				startupTask.Disable();
			}
			return;
		}
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
		if (enabled)
		{
			registryKey.SetValue("Moss", "\"" + Environment.ProcessPath + "\" --startup");
		}
		else
		{
			registryKey.DeleteValue("Moss", throwOnMissingValue: false);
		}
	}
}
