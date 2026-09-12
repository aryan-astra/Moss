using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Moss.Windows;

internal sealed class Metrics : IDisposable
{
	private readonly Process process = Process.GetCurrentProcess();

	private TimeSpan lastCpu;

	private long lastTime = Environment.TickCount64;

	private int frames;

	public double Cpu { get; private set; }

	public double RamMiB { get; private set; }

	public double Fps { get; private set; }

	public double FrameMs { get; set; }

	public double SimulationMs { get; set; }

	public double RenderMs { get; set; }

	public int Steps { get; set; }

	public int GdiHandles { get; private set; }

	public void Dispose()
	{
		process.Dispose();
	}

	[DllImport("user32.dll")]
	private static extern uint GetGuiResources(nint process, uint flags);

	public void Frame(bool drawn)
	{
		if (drawn)
		{
			frames++;
		}
		long tickCount = Environment.TickCount64;
		if (tickCount - lastTime >= 1000)
		{
			process.Refresh();
			GdiHandles = (int)GetGuiResources(process.Handle, 0u);
			TimeSpan totalProcessorTime = process.TotalProcessorTime;
			Cpu = (totalProcessorTime - lastCpu).TotalMilliseconds / (double)(tickCount - lastTime) / (double)Environment.ProcessorCount * 100.0;
			RamMiB = (double)process.WorkingSet64 / 1048576.0;
			Fps = (double)frames * 1000.0 / (double)(tickCount - lastTime);
			frames = 0;
			lastCpu = totalProcessorTime;
			lastTime = tickCount;
		}
	}
}
