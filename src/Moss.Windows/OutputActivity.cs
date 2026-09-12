using System;
using System.Runtime.InteropServices;
using Moss.Core;

namespace Moss.Windows;

internal sealed class OutputActivity : IDisposable
{
	[ComImport]
	[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface Enumerator
	{
		void EnumEndpoints(int flow, uint state, out nint result);

		void GetDefault(int flow, int role, out Device device);
	}

	[ComImport]
	[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface Device
	{
		void Activate(ref Guid iid, uint context, nint parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result);

		void OpenStore(int access, out nint store);

		void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
	}

	[ComImport]
	[Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface Meter
	{
		void GetPeak(out float peak);
	}

	private Enumerator? enumerator;

	private Device? device;

	private Meter? meter;

	private string? id;

	private long nextSample;

	private long nextDevice;

	private readonly AudioActivity activity = new AudioActivity();

	public bool Active => activity.Active;

	public string Status { get; private set; } = "Output-level fallback off";

	public void Update(bool enabled)
	{
		if (!enabled)
		{
			Dispose();
			Status = "Output-level fallback off";
			return;
		}
		long tickCount = Environment.TickCount64;
		if (tickCount < nextSample)
		{
			return;
		}
		nextSample = tickCount + 125;
		try
		{
			if (tickCount >= nextDevice)
			{
				nextDevice = tickCount + 5000;
				if (enumerator == null)
				{
					enumerator = (Enumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
				}
				enumerator.GetDefault(0, 1, out Device device);
				try
				{
					device.GetId(out string text);
					if (text != id)
					{
						Release(meter);
						Release(this.device);
						meter = null;
						this.device = null;
						id = null;
						Guid iid = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
						device.Activate(ref iid, 23u, 0, out object result);
						meter = (Meter)result;
						this.device = device;
						device = null;
						id = text;
						activity.Reset();
					}
				}
				finally
				{
					Release(device);
				}
			}
			if (meter != null)
			{
				meter.GetPeak(out var peak);
				activity.Step(peak, 0.125f);
				Status = "Output levels only · no audio recording";
			}
		}
		catch
		{
			Release(meter);
			Release(this.device);
			Release(enumerator);
			enumerator = null;
			meter = null;
			this.device = null;
			id = null;
			activity.Reset();
			Status = "Output meter unavailable";
			nextSample = tickCount + 5000;
		}
	}

	private static void Release(object? value)
	{
		if (value != null && Marshal.IsComObject(value))
		{
			Marshal.ReleaseComObject(value);
		}
	}

	public void Dispose()
	{
		Release(meter);
		Release(device);
		Release(enumerator);
		meter = null;
		device = null;
		enumerator = null;
		id = null;
		nextDevice = (nextSample = 0L);
		activity.Reset();
	}
}
