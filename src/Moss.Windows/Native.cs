using System.Runtime.InteropServices;
using System.Text;
using Moss.Core;

namespace Moss.Windows;

internal static class Native
{
	internal struct Point
	{
		public int X;

		public int Y;

		public Point(int x, int y)
		{
			X = x;
			Y = y;
		}
	}

	internal struct Size
	{
		public int Cx;

		public int Cy;

		public Size(int x, int y)
		{
			Cx = x;
			Cy = y;
		}
	}

	internal struct Rect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;

		public readonly Box Box => new Box(Left, Top, Right - Left, Bottom - Top);
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	internal struct MonitorInfo
	{
		public int Size;

		public Rect Monitor;

		public Rect Work;

		public uint Flags;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string Device;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct Blend
	{
		public byte Op;

		public byte Flags;

		public byte Alpha;

		public byte Format;
	}

	[StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode, Size = 220)]
	internal struct DisplayMode
	{
		[FieldOffset(68)]
		public ushort Size;

		[FieldOffset(184)]
		public uint Frequency;
	}

	internal delegate bool EnumProc(nint hwnd, nint data);

	internal delegate bool MonitorProc(nint monitor, nint hdc, ref Rect rect, nint data);

	internal delegate void EventProc(nint hook, uint evt, nint hwnd, int obj, int child, uint thread, uint time);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	internal static extern bool EnumDisplaySettings(string device, int mode, ref DisplayMode data);

	[DllImport("user32.dll")]
	internal static extern bool EnumWindows(EnumProc proc, nint param);

	[DllImport("user32.dll")]
	internal static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorProc proc, nint data);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

	[DllImport("shcore.dll")]
	internal static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);

	[DllImport("user32.dll")]
	internal static extern bool IsWindowVisible(nint hwnd);

	[DllImport("user32.dll")]
	internal static extern bool IsIconic(nint hwnd);

	[DllImport("user32.dll")]
	internal static extern bool IsZoomed(nint hwnd);

	[DllImport("user32.dll")]
	internal static extern bool GetWindowRect(nint hwnd, out Rect rect);

	[DllImport("user32.dll")]
	internal static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	internal static extern int GetClassName(nint hwnd, StringBuilder text, int max);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	internal static extern int GetWindowText(nint hwnd, StringBuilder text, int max);

	[DllImport("user32.dll")]
	internal static extern nint GetWindow(nint hwnd, uint cmd);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	internal static extern nint GetWindowLongPtr(nint hwnd, int index);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
	internal static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

	[DllImport("user32.dll")]
	internal static extern bool GetCursorPos(out Point pt);

	[DllImport("user32.dll")]
	internal static extern nint SetCapture(nint hwnd);

	[DllImport("user32.dll")]
	internal static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	internal static extern short GetAsyncKeyState(int key);

	[DllImport("user32.dll")]
	internal static extern nint SetWinEventHook(uint min, uint max, nint module, EventProc callback, uint process, uint thread, uint flags);

	[DllImport("user32.dll")]
	internal static extern bool UnhookWinEvent(nint hook);

	[DllImport("dwmapi.dll")]
	internal static extern int DwmGetWindowAttribute(nint hwnd, int attr, out Rect rect, int size);

	[DllImport("dwmapi.dll")]
	internal static extern int DwmGetWindowAttribute(nint hwnd, int attr, out int value, int size);

	[DllImport("user32.dll")]
	internal static extern bool SetWindowDisplayAffinity(nint hwnd, uint affinity);

	[DllImport("user32.dll")]
	internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int cx, int cy, uint flags);

	[DllImport("user32.dll")]
	internal static extern nint GetDC(nint hwnd);

	[DllImport("user32.dll")]
	internal static extern int ReleaseDC(nint hwnd, nint dc);

	[DllImport("gdi32.dll")]
	internal static extern nint CreateCompatibleDC(nint dc);

	[DllImport("gdi32.dll")]
	internal static extern bool DeleteDC(nint dc);

	[DllImport("gdi32.dll")]
	internal static extern nint SelectObject(nint dc, nint obj);

	[DllImport("gdi32.dll")]
	internal static extern bool DeleteObject(nint obj);

	[DllImport("user32.dll", SetLastError = true)]
	internal static extern bool UpdateLayeredWindow(nint hwnd, nint screen, ref Point dest, ref Size size, nint source, ref Point origin, uint color, ref Blend blend, uint flags);

	[DllImport("shell32.dll")]
	internal static extern int SHQueryUserNotificationState(out int state);

	[DllImport("user32.dll")]
	internal static extern bool RegisterHotKey(nint hwnd, int id, uint mods, uint key);

	[DllImport("user32.dll")]
	internal static extern bool UnregisterHotKey(nint hwnd, int id);

	[DllImport("kernel32.dll")]
	internal static extern int GetCurrentPackageFullName(ref int length, StringBuilder? name);
}
