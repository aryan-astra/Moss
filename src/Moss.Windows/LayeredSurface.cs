using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

namespace Moss.Windows;

internal sealed class LayeredSurface : IDisposable
{
	private struct BitmapInfo
	{
		public uint HeaderSize;

		public int Width;

		public int Height;

		public ushort Planes;

		public ushort BitCount;

		public uint Compression;

		public uint ImageSize;

		public int XPels;

		public int YPels;

		public uint Used;

		public uint Important;

		public uint Color;
	}

	private nint dc;

	private nint bitmap;

	private nint old;

	private nint bits;

	private readonly int width;

	private readonly int height;

	private static int active;

	public static int Active => Volatile.Read(in active);

	public LayeredSurface(int w, int h)
	{
		width = w;
		height = h;
		dc = Native.CreateCompatibleDC(0);
		if (dc == 0)
		{
			throw new Win32Exception();
		}
		BitmapInfo info = new BitmapInfo
		{
			HeaderSize = 40u,
			Width = w,
			Height = -h,
			Planes = 1,
			BitCount = 32
		};
		bitmap = CreateDIBSection(dc, ref info, 0u, out bits, 0, 0u);
		if (bitmap == 0)
		{
			Native.DeleteDC(dc);
			dc = 0;
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}
		old = Native.SelectObject(dc, bitmap);
		if (old == 0 || old == -1)
		{
			int lastWin32Error = Marshal.GetLastWin32Error();
			Native.DeleteObject(bitmap);
			Native.DeleteDC(dc);
			dc = (bitmap = 0);
			throw new Win32Exception(lastWin32Error);
		}
		Interlocked.Increment(ref active);
	}

	public bool Fits(Bitmap image)
	{
		if (image.Width == width)
		{
			return image.Height == height;
		}
		return false;
	}

	public unsafe void Present(nint hwnd, Bitmap image, Native.Point location, byte opacity)
	{
		GdiFlush();
		BitmapData bitmapData = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
		try
		{
			if (bitmapData.Stride == width * 4)
			{
				Buffer.MemoryCopy((void*)bitmapData.Scan0, (void*)bits, (long)width * (long)height * 4, (long)width * (long)height * 4);
			}
			else
			{
				for (int i = 0; i < height; i++)
				{
					Buffer.MemoryCopy((void*)(bitmapData.Scan0 + i * bitmapData.Stride), (void*)(bits + i * width * 4), width * 4, width * 4);
				}
			}
		}
		finally
		{
			image.UnlockBits(bitmapData);
		}
		Native.Size size = new Native.Size(width, height);
		Native.Point origin = new Native.Point(0, 0);
		Native.Blend blend = new Native.Blend
		{
			Alpha = opacity,
			Format = 1
		};
		if (!Native.UpdateLayeredWindow(hwnd, 0, ref location, ref size, dc, ref origin, 0u, ref blend, 2u))
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}
	}

	public void Dispose()
	{
		if (dc != 0)
		{
			Native.SelectObject(dc, old);
			Native.DeleteObject(bitmap);
			Native.DeleteDC(dc);
			dc = (bitmap = (old = (bits = 0)));
			Interlocked.Decrement(ref active);
		}
	}

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);

	[DllImport("gdi32.dll")]
	private static extern bool GdiFlush();
}
