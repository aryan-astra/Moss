using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;

namespace Moss.Windows;

internal sealed class PetOverlay : Form
{
	private LayeredSurface? surface;

	private Native.Point lastPosition;

	private byte lastOpacity;

	private bool pressed;

	private bool interactable = true;

	private Vector2 down;

	private long downTime;

	private long lastStroke;

	private readonly bool hotkey;

	public Func<Vector2, bool>? SpecialClick { get; set; }

	protected override bool ShowWithoutActivation => true;

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams createParams = base.CreateParams;
			createParams.ExStyle |= 134742144;
			return createParams;
		}
	}

	public static Vector2 CursorPhysical
	{
		get
		{
			Native.GetCursorPos(out var pt);
			return new Vector2(pt.X, pt.Y);
		}
	}

	public event Action<Vector2>? GrabStarted;

	public event Action? Released;

	public event Action? Clicked;

	public event Action? Stroke;

	public event Action? MenuRequested;

	public event Action? QuietToggled;

	public event Action? DisplayChanged;

	public PetOverlay(bool ownHotkey = true)
	{
		hotkey = ownHotkey;
		base.FormBorderStyle = FormBorderStyle.None;
		base.ShowInTaskbar = false;
		base.TopMost = true;
		base.AutoScaleMode = AutoScaleMode.None;
		base.Size = new Size(180, 180);
		Text = "Moss";
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, value: true);
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		if (hotkey && !Native.RegisterHotKey(base.Handle, 1, 16390u, 123u))
		{
			Log.Event("input", "quiet-hotkey-unavailable");
		}
	}

	public void Configure(bool interaction, bool exclude)
	{
		interactable = interaction;
		long num = ((IntPtr)Native.GetWindowLongPtr(base.Handle, -20)).ToInt64();
		Native.SetWindowLongPtr(base.Handle, -20, new IntPtr(interaction ? (num & -33) : (num | 0x20)));
		if (!Native.SetWindowDisplayAffinity(base.Handle, exclude ? 17u : 0u))
		{
			Log.Event("overlay", "capture-exclusion-unavailable");
		}
		if (!interaction && pressed)
		{
			EndGrab();
		}
	}

	public void Draw(Bitmap image, Vector2 feet, float scale, byte opacity = byte.MaxValue, Vector2? anchor = null, bool pixelsChanged = true)
	{
		if (surface == null || !surface.Fits(image))
		{
			surface?.Dispose();
			surface = new LayeredSurface(image.Width, image.Height);
			pixelsChanged = true;
		}
		Vector2 vector = anchor ?? new Vector2(90f, 142f);
		Native.Point location = new Native.Point((int)MathF.Round(feet.X - vector.X * scale), (int)MathF.Round(feet.Y - vector.Y * scale));
		if (pixelsChanged || opacity != lastOpacity)
		{
			surface.Present(base.Handle, image, location, opacity);
		}
		else if (location.X != lastPosition.X || location.Y != lastPosition.Y)
		{
			Native.SetWindowPos(base.Handle, 0, location.X, location.Y, 0, 0, 21u);
		}
		lastPosition = location;
		lastOpacity = opacity;
	}

	protected override void WndProc(ref Message m)
	{
		switch (m.Msg)
		{
		case 132:
			if (!interactable)
			{
				m.Result = new IntPtr(-1);
				return;
			}
			break;
		case 33:
			m.Result = new IntPtr(3);
			return;
		case 513:
			if (interactable)
			{
				Func<Vector2, bool>? specialClick = SpecialClick;
				if (specialClick != null && specialClick(CursorPhysical))
				{
					return;
				}
			}
			if (interactable)
			{
				pressed = true;
				down = CursorPhysical;
				downTime = Environment.TickCount64;
				Native.SetCapture(base.Handle);
				this.GrabStarted?.Invoke(down);
			}
			return;
		case 514:
			if (pressed)
			{
				bool num = Environment.TickCount64 - downTime < 300 && Vector2.Distance(down, CursorPhysical) < 7f;
				EndGrab();
				if (num)
				{
					this.Clicked?.Invoke();
				}
			}
			return;
		case 533:
			if (pressed)
			{
				pressed = false;
				this.Released?.Invoke();
			}
			break;
		case 512:
			if (!pressed && interactable && Environment.TickCount64 - lastStroke > 450)
			{
				lastStroke = Environment.TickCount64;
				this.Stroke?.Invoke();
			}
			break;
		case 517:
			this.MenuRequested?.Invoke();
			return;
		case 786:
			if (m.WParam == 1)
			{
				this.QuietToggled?.Invoke();
			}
			return;
		case 126:
		case 536:
			this.DisplayChanged?.Invoke();
			break;
		case 736:
			this.DisplayChanged?.Invoke();
			m.Result = 0;
			return;
		}
		base.WndProc(ref m);
	}

	public void RecoverSurface()
	{
		if (pressed)
		{
			EndGrab();
		}
		RecreateHandle();
	}

	protected override void OnHandleDestroyed(EventArgs e)
	{
		if (base.IsHandleCreated)
		{
			Native.UnregisterHotKey(base.Handle, 1);
		}
		base.OnHandleDestroyed(e);
	}

	public void CheckCapture()
	{
		if (pressed && (Native.GetAsyncKeyState(1) & 0x8000) == 0)
		{
			EndGrab();
		}
	}

	private void EndGrab()
	{
		pressed = false;
		Native.ReleaseCapture();
		this.Released?.Invoke();
	}

	protected override void Dispose(bool disposing)
	{
		if (base.IsHandleCreated)
		{
			Native.UnregisterHotKey(base.Handle, 1);
		}
		if (disposing)
		{
			surface?.Dispose();
			surface = null;
		}
		base.Dispose(disposing);
	}
}
