using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Moss.Core;

namespace Moss.Windows;

internal sealed class WorldObserver : IDisposable
{
	private readonly Native.EventProc callback;

	private readonly List<nint> hooks = new List<nint>();

	private readonly uint ownPid = (uint)Environment.ProcessId;

	private int dirty = 1;

	private long lastScan;

	public World World { get; } = new World();

	public double LastScanMs { get; private set; }

	public WorldObserver()
	{
		callback = OnEvent;
		(uint, uint)[] array = new(uint, uint)[4]
		{
			(3u, 3u),
			(32768u, 32771u),
			(32779u, 32779u),
			(22u, 23u)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(uint, uint) tuple = array[i];
			nint num = Native.SetWinEventHook(tuple.Item1, tuple.Item2, 0, callback, 0u, 0u, 2u);
			if (num != 0)
			{
				hooks.Add(num);
			}
		}
		if (hooks.Count == 0)
		{
			Log.Event("world", "hooks-unavailable");
		}
	}

	private void OnEvent(nint hook, uint evt, nint hwnd, int obj, int child, uint thread, uint time)
	{
		if (obj == 0 && hwnd != 0)
		{
			Interlocked.Exchange(ref dirty, 1);
		}
	}

	public void Invalidate()
	{
		Interlocked.Exchange(ref dirty, 1);
	}

	public void Update(Settings settings, bool force = false)
	{
		if (settings.CursorAwareness && Native.GetCursorPos(out var pt))
		{
			World.Cursor = new Vector2(pt.X, pt.Y);
		}
		else if (!settings.CursorAwareness)
		{
			World.Cursor = new Vector2(-100000f, -100000f);
		}
		long tickCount = Environment.TickCount64;
		if ((force || tickCount - lastScan >= 100) && (force || Volatile.Read(in dirty) != 0 || tickCount - lastScan >= 2000))
		{
			Interlocked.Exchange(ref dirty, 0);
			lastScan = tickCount;
			Stopwatch stopwatch = Stopwatch.StartNew();
			try
			{
				Scan(settings);
			}
			catch (Exception error)
			{
				Log.Error("world-scan", error);
			}
			LastScanMs = stopwatch.Elapsed.TotalMilliseconds;
		}
	}

	private void Scan(Settings settings)
	{
		List<Display> displays = new List<Display>();
		Native.EnumDisplayMonitors(0, 0, delegate(nint m, nint dc, ref Native.Rect rect, nint data)
		{
			Native.MonitorInfo info = new Native.MonitorInfo
			{
				Size = Marshal.SizeOf<Native.MonitorInfo>(),
				Device = ""
			};
			if (Native.GetMonitorInfo(m, ref info))
			{
				float value = 1f;
				if (Native.GetDpiForMonitor(m, 0, out var x, out var _) == 0)
				{
					value = (float)x / 96f;
				}
				Native.DisplayMode data2 = new Native.DisplayMode
				{
					Size = 220
				};
				float refreshRate = 60f;
				if (Native.EnumDisplaySettings(info.Device, -1, ref data2) && data2.Frequency > 1)
				{
					refreshRate = Math.Clamp(data2.Frequency, 24u, 500u);
				}
				displays.Add(new Display(info.Device, info.Monitor.Box, info.Work.Box, Math.Clamp(value, 0.75f, 5f), refreshRate));
			}
			return true;
		}, 0);
		if (displays.Count == 0)
		{
			return;
		}
		List<WindowInfo> windows = new List<WindowInfo>();
		List<Box> occluders = new List<Box>();
		List<Surface> surfaces = new List<Surface>();
		for (int num = 0; num < displays.Count; num++)
		{
			Display display = displays[num];
			surfaces.Add(new Surface(StableDisplayId(display.Id), display.Work.X, display.Work.Right, display.Work.Bottom, Floor: true, display.Work.X));
		}
		nint foregroundWindow = Native.GetForegroundWindow();
		bool flag = false;
		if (settings.ForegroundAwareness && Native.GetWindowRect(foregroundWindow, out var fr))
		{
			StringBuilder stringBuilder = new StringBuilder(128);
			Native.GetClassName(foregroundWindow, stringBuilder, 128);
			switch (stringBuilder.ToString())
			{
			default:
				if (!Native.IsZoomed(foregroundWindow) || (((IntPtr)Native.GetWindowLongPtr(foregroundWindow, -16)).ToInt64() & 0xC00000) == 0L)
				{
					flag = displays.Any((Display d) => (float)fr.Left <= d.Bounds.X + 1f && (float)fr.Top <= d.Bounds.Y + 1f && (float)fr.Right >= d.Bounds.Right - 1f && (float)fr.Bottom >= d.Bounds.Bottom - 1f);
				}
				break;
			case "Progman":
			case "WorkerW":
			case "Shell_TrayWnd":
			case "Shell_SecondaryTrayWnd":
				break;
			}
		}
		if (settings.WindowGeometry)
		{
			Native.EnumWindows(delegate(nint hwnd, nint _)
			{
				if (!Native.IsWindowVisible(hwnd) || Native.IsIconic(hwnd))
				{
					return true;
				}
				Native.GetWindowThreadProcessId(hwnd, out var pid);
				if (pid == ownPid)
				{
					return true;
				}
				Native.DwmGetWindowAttribute(hwnd, 14, out int value, 4);
				if (value != 0)
				{
					return true;
				}
				StringBuilder stringBuilder2 = new StringBuilder(128);
				Native.GetClassName(hwnd, stringBuilder2, 128);
				bool flag2;
				switch (stringBuilder2.ToString())
				{
				case "Progman":
				case "WorkerW":
				case "Shell_TrayWnd":
				case "Shell_SecondaryTrayWnd":
					flag2 = true;
					break;
				default:
					flag2 = false;
					break;
				}
				if (flag2)
				{
					return true;
				}
				long num2 = ((IntPtr)Native.GetWindowLongPtr(hwnd, -20)).ToInt64();
				if ((num2 & 0x80) != 0L || (num2 & 0x20) != 0L)
				{
					return true;
				}
				if (Native.DwmGetWindowAttribute(hwnd, 9, out Native.Rect rect, 16) != 0 && !Native.GetWindowRect(hwnd, out rect))
				{
					return true;
				}
				Box b = rect.Box;
				if (b.W < 80f || b.H < 50f || !displays.Any((Display display2) => b.Right > display2.Bounds.X && b.X < display2.Bounds.Right && b.Bottom > display2.Bounds.Y && b.Y < display2.Bounds.Bottom))
				{
					return true;
				}
				string identity = null;
				string title = null;
				if (settings.ApplicationIdentity)
				{
					try
					{
						using Process process = Process.GetProcessById((int)pid);
						identity = process.ProcessName;
					}
					catch
					{
					}
					StringBuilder stringBuilder3 = new StringBuilder(512);
					Native.GetWindowText(hwnd, stringBuilder3, 512);
					title = stringBuilder3.ToString();
				}
				windows.Add(new WindowInfo(((IntPtr)hwnd).ToInt64(), b, Native.IsZoomed(hwnd), identity, title));
				foreach (Display d in displays)
				{
					if (!(b.Y < d.Work.Y + 85f * d.Scale) && !(b.Y > d.Work.Bottom))
					{
						float num3 = Math.Max(b.X, d.Work.X);
						float num4 = Math.Min(b.Right, d.Work.Right);
						if (!(num4 - num3 < 45f * d.Scale))
						{
							IEnumerable<(float, float)> blockers = from o in occluders
								where o.Y < b.Y + 2f && o.Bottom > b.Y - 75f * d.Scale
								select (X: o.X, Right: o.Right);
							foreach (var item in Moss.Core.World.Subtract(num3, num4, blockers))
							{
								if (item.Right - item.Left > 45f * d.Scale)
								{
									surfaces.Add(new Surface(((IntPtr)hwnd).ToInt64(), item.Left, item.Right, b.Y, Floor: false, b.X));
								}
							}
						}
					}
				}
				occluders.Add(b);
				return true;
			}, 0);
		}
		World.Displays = displays;
		World.Windows = windows;
		World.Surfaces = surfaces;
		int state;
		ShellContext shellContext = DesktopVisibility.FromShellResult(Native.SHQueryUserNotificationState(out state), state);
		World.Fullscreen = flag || (settings.ForegroundAwareness && shellContext.ExclusiveFullscreen);
		World.Foreground = (settings.ForegroundAwareness ? ((IntPtr)foregroundWindow).ToInt64() : 0);
		World.Presenting = shellContext.Presenting;
		World.Revision++;
	}

	private static long StableDisplayId(string name)
	{
		ulong num = 14695981039346656037uL;
		foreach (char c in name)
		{
			num ^= c;
			num *= 1099511628211L;
		}
		return -1L - (long)(num & 0x3FFFFFFFFFFFFFFFL);
	}

	public void Dispose()
	{
		foreach (nint hook in hooks)
		{
			Native.UnhookWinEvent(hook);
		}
		hooks.Clear();
	}
}
