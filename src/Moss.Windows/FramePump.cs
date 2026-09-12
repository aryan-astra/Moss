using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Moss.Windows;

internal sealed class FramePump : IDisposable
{
	private readonly Control dispatcher;

	private readonly nint timer;

	private Thread? thread;

	private volatile bool running;

	private int pending;

	private int disposed;

	private readonly object gate = new object();

	private double interval = 16.6667;

	public double Interval
	{
		get
		{
			return Volatile.Read(in interval);
		}
		set
		{
			Volatile.Write(ref interval, Math.Clamp(value, 2.0, 2000.0));
		}
	}

	public event EventHandler? Tick;

	public FramePump(Control target)
	{
		dispatcher = target;
		timer = CreateWaitableTimerEx(0, null, 2u, 2031619u);
		if (timer == 0)
		{
			timer = CreateWaitableTimerEx(0, null, 0u, 2031619u);
		}
		if (timer == 0)
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}
	}

	public void Start()
	{
		if (!running)
		{
			running = true;
			thread = new Thread(Loop)
			{
				IsBackground = true,
				Name = "Moss frame pacing"
			};
			thread.Start();
		}
	}

	public void Stop()
	{
		lock (gate)
		{
			running = false;
			long due = -1L;
			SetWaitableTimer(timer, ref due, 0, 0, 0, resume: false);
		}
	}

	private void Loop()
	{
		while (running)
		{
			long due = -(long)(Interval * 10000.0);
			lock (gate)
			{
				if (!running || !SetWaitableTimer(timer, ref due, 0, 0, 0, resume: false))
				{
					break;
				}
			}
			if (WaitForSingleObject(timer, 2500u) != 0)
			{
				continue;
			}
			if (!running)
			{
				break;
			}
			if (Interlocked.Exchange(ref pending, 1) != 0)
			{
				continue;
			}
			try
			{
				dispatcher.BeginInvoke(delegate
				{
					try
					{
						if (running)
						{
							this.Tick?.Invoke(this, EventArgs.Empty);
						}
					}
					finally
					{
						Interlocked.Exchange(ref pending, 0);
					}
				});
			}
			catch (InvalidOperationException)
			{
				Interlocked.Exchange(ref pending, 0);
				break;
			}
		}
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref disposed, 1) == 0)
		{
			Stop();
			thread?.Join(600);
			CloseHandle(timer);
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern nint CreateWaitableTimerEx(nint attributes, string? name, uint flags, uint access);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool SetWaitableTimer(nint handle, ref long due, int period, nint completion, nint arg, bool resume);

	[DllImport("kernel32.dll")]
	private static extern uint WaitForSingleObject(nint handle, uint timeout);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(nint handle);
}
