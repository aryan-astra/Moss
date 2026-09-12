using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Moss.Core;
using Activity = Moss.Core.Activity;

namespace Moss.Windows;

internal sealed class PetApplication : ApplicationContext
{
	private NotebookForm? notebook;

	private MusicCard? musicCard;

	private readonly PettingGesture petting = new PettingGesture();

	private readonly PropController props;

	private long lastReminderCheck;

	private long lastMemorySave;

	private float transition = 1f;

	private string? pendingPet;

	private bool switching;

	private readonly EventWaitHandle showSignal;

	private long revealUntil;

	private string lastVisibility = "";

	private Animator animator;

	private CreatureRenderer renderer;

	private readonly PetOverlay overlay = new PetOverlay();

	private readonly NotifyIcon tray;

	private readonly ContextMenuStrip menu = new ContextMenuStrip();

	private Sounds sounds;

	private readonly FramePump timer;

	private readonly Stopwatch clock = Stopwatch.StartNew();

	private SettingsForm? settings;

	private double lastTime;

	private double accumulator;

	private bool quiet;

	private bool quitting;

	private bool renderFailed;

	private bool resourcesDisposed;

	private Vector2 grabOffset;

	private long lastDiagnostic;

	private long lastForeground;

	private long lastContextReaction;

	private readonly HashSet<long> knownWindows = new HashSet<long>();

	private int lastMediaChange;

	private long eventRevision;

	private readonly Dictionary<long, Box> previousWindows = new Dictionary<long, Box>();

	private string previousDisplays = "";

	private bool previousFullscreen;

	private bool previousPlaying;

	public Settings Config { get; private set; }

	public Character Character { get; private set; }

	public DocumentStore Documents { get; }

	public ReminderService Reminders { get; }

	public EventHub Events { get; } = new EventHub();

	private string ActiveCharacterFile
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Config.CharacterPath))
			{
				return Config.CharacterPath;
			}
			return Path.Combine(AppContext.BaseDirectory, "characters", Config.ActivePet, "character.json");
		}
	}

	public string PetName
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Config.Profile.Name))
			{
				return Config.Profile.Name;
			}
			return Character.Name;
		}
	}

	public byte PetAlpha => (byte)(255f * Math.Clamp(transition, 0f, 1f));

	public Creature Creature { get; private set; } = new Creature();

	public WorldObserver Observer { get; } = new WorldObserver();

	public MediaObserver Media { get; } = new MediaObserver();

	public NotificationObserver Notifications { get; } = new NotificationObserver();

	public Metrics Metrics { get; } = new Metrics();

	public bool Paused { get; set; }

	public string VisibilityStatus { get; private set; } = "Starting";

	public void RollFootball()
	{
		props.RollFootball();
	}

	public void OpenMusic()
	{
		if (musicCard == null || musicCard.IsDisposed)
		{
			musicCard = new MusicCard(this);
		}
		musicCard.Show();
		musicCard.Activate();
	}

	public PetApplication(EventWaitHandle signal)
	{
		showSignal = signal;
		Directory.CreateDirectory(Paths.Root);
		File.Exists(Paths.Settings);
		try
		{
			Config = Settings.Load(Paths.Settings);
		}
		catch (Exception error)
		{
			Log.Error("settings-load", error);
			Config = new Settings();
			try
			{
				File.Copy(Paths.Settings, Paths.Settings + ".invalid", overwrite: true);
			}
			catch
			{
			}
		}
		if (!Environment.GetCommandLineArgs().Contains("--startup") && !Environment.GetCommandLineArgs().Contains("--reminder"))
		{
			Config.Visible = true;
		}
		Config.PetScale = Config.Profile.Size;
		Documents = new DocumentStore(Path.Combine(Paths.Root, "notebook"));
		Reminders = new ReminderService(Documents, Config);
		try
		{
			Character = Moss.Core.Character.Load(ActiveCharacterFile);
		}
		catch (Exception error2)
		{
			Log.Error("character-load", error2);
			Character = Moss.Core.Character.Load(Paths.DefaultCharacter);
			Config.CharacterPath = "";
			Config.ActivePet = "moss";
			Config.PetScale = Config.Profile.Size;
		}
		animator = new Animator(Character);
		renderer = new CreatureRenderer(Character);
		sounds = new Sounds(Character.Sound, Config.SoundLevel);
		WireCreature();
		overlay.SpecialClick = delegate(Vector2 point)
		{
			if (!Media.State.Playing || Creature.Activity != Activity.Dance)
			{
				return false;
			}
			Vector2 vector = (point - Creature.Position) / Creature.Scale;
			float width = Character.Width;
			float height = Character.Height;
			bool num;
			if (!(Character.MusicProp == "headphones"))
			{
				if (Math.Abs(vector.X) < 28f && vector.Y > -38f)
				{
					num = vector.Y < 3f;
					goto IL_0102;
				}
			}
			else if (Math.Abs(vector.X) < width * 0.7f && vector.Y > (0f - height) * 1.2f && vector.Y < (0f - height) * 0.55f)
			{
				if (!(Math.Abs(vector.X) > width * 0.32f))
				{
					num = vector.Y < (0f - height) * 0.95f;
					goto IL_0102;
				}
				goto IL_0106;
			}
			goto IL_0104;
			IL_0106:
			OpenMusic();
			return true;
			IL_0104:
			return false;
			IL_0102:
			if (!num)
			{
				goto IL_0104;
			}
			goto IL_0106;
		};
		overlay.GrabStarted += delegate(Vector2 cursor)
		{
			grabOffset = Creature.Position - cursor;
			Creature.Grab(cursor);
		};
		overlay.Released += delegate
		{
			Creature.Release();
			Events.Publish("pet.thrown", Creature.Time);
		};
		overlay.Clicked += delegate
		{
			Creature.Notify();
		};
		overlay.DisplayChanged += delegate
		{
			Observer.Invalidate();
		};
		overlay.QuietToggled += delegate
		{
			quiet = !quiet;
			UpdateTray();
		};
		overlay.MenuRequested += delegate
		{
			menu.Show(Cursor.Position);
		};
		Notifications.Arrived += delegate
		{
			Events.Publish("notification.received", Creature.Time);
			Creature.Notify();
		};
		Reminders.Due += delegate(Reminder r)
		{
			Events.Publish((r.Kind == ReminderKind.Timer) ? "timer.finished" : "reminder.due", Creature.Time);
			Creature.Notify();
			if (Config.SoundEffects)
			{
				sounds.Pop();
			}
			if (Config.ReminderAlerts)
			{
				tray.ShowBalloonTip(6000, PetName + " · " + ((r.Kind == ReminderKind.Timer) ? "Timer" : "Reminder"), r.Message, ToolTipIcon.Info);
			}
		};
		tray = new NotifyIcon
		{
			Text = "Moss · A small life on your desktop",
			Icon = MakeIcon(),
			Visible = true,
			ContextMenuStrip = menu
		};
		tray.DoubleClick += delegate
		{
			OpenSettings();
		};
		UpdateTray();
		Observer.Update(Config, force: true);
		Creature.Reset(Observer.World);
		Creature.Recall(Config.Profile.Memory, DateTimeOffset.UtcNow);
		overlay.Show();
		overlay.Configure(Config.Interaction, Config.ExcludeFromCapture);
		timer = new FramePump(overlay);
		props = new PropController(this);
		timer.Interval = 22.0;
		timer.Tick += delegate
		{
			Tick();
		};
		timer.Start();
		if (Config.Notifications)
		{
			_ = StartNotifications();
		}
		Log.Event("runtime", "started");
		if (!Environment.GetCommandLineArgs().Contains("--startup") && !Environment.GetCommandLineArgs().Contains("--reminder"))
		{
			OpenSettings();
		}
		if (Config.ClosedAppReminders)
		{
			Reminders.Reconcile();
		}
	}

	private async Task StartNotifications()
	{
		bool flag = await Notifications.EnableAsync();
		if (!quitting && !resourcesDisposed && !flag)
		{
			Config.Notifications = false;
			Save();
		}
	}

	private void WireCreature()
	{
		Creature.Landed += delegate(float force)
		{
			if (Config.SoundEffects && force > 400f)
			{
				sounds.Pop();
			}
		};
		Creature.Petted += delegate
		{
			if (Config.SoundEffects)
			{
				sounds.Pop();
			}
		};
		animator.Marker += delegate(Motion state)
		{
			if (Config.SoundEffects && state == Motion.Celebrating)
			{
				sounds.Pop();
			}
		};
	}

	private static Icon MakeIcon()
	{
		using Bitmap bitmap = new Bitmap(32, 32);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.Clear(Color.Transparent);
			using SolidBrush brush = new SolidBrush(Color.FromArgb(169, 187, 130));
			using SolidBrush brush2 = new SolidBrush(Color.FromArgb(38, 59, 50));
			graphics.FillEllipse(brush, 5, 7, 23, 23);
			graphics.FillEllipse(brush, 7, 1, 7, 14);
			graphics.FillEllipse(brush, 19, 3, 6, 11);
			graphics.FillEllipse(brush2, 11, 15, 3, 5);
			graphics.FillEllipse(brush2, 21, 15, 3, 5);
		}
		nint hicon = bitmap.GetHicon();
		try
		{
			return (Icon)Icon.FromHandle(hicon).Clone();
		}
		finally
		{
			DestroyIcon(hicon);
		}
	}

	[DllImport("user32.dll")]
	private static extern bool DestroyIcon(nint icon);

	private void UpdateTray()
	{
		while (menu.Items.Count > 0)
		{
			menu.Items[0].Dispose();
		}
		menu.Items.Add("Show Moss / recover position", null, delegate
		{
			Reset();
		});
		menu.Items.Add("Hide Moss", null, delegate
		{
			Config.Visible = false;
			revealUntil = 0L;
			Save();
		});
		menu.Items.Add(new ToolStripMenuItem(VisibilityStatus)
		{
			Enabled = false
		});
		menu.Items.Add(Paused ? "Resume behavior" : "Pause behavior", null, delegate
		{
			Paused = !Paused;
			UpdateTray();
		});
		menu.Items.Add(quiet ? "Leave quiet mode" : "Quiet mode  ·  Ctrl+Shift+F12", null, delegate
		{
			quiet = !quiet;
			UpdateTray();
		});
		menu.Items.Add(Config.Interaction ? "Make click-through" : "Enable interaction", null, delegate
		{
			Config.Interaction = !Config.Interaction;
			Save();
		});
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add("Open Moss…", null, delegate
		{
			OpenSettings();
		});
		menu.Items.Add("Now playing…", null, delegate
		{
			OpenMusic();
		});
		menu.Items.Add("Notebook…", null, delegate
		{
			OpenNotebook();
		});
		menu.Items.Add("Reload character", null, delegate
		{
			ReloadCharacter();
		});
		menu.Items.Add("Exit Moss", null, delegate
		{
			Quit();
		});
	}

	public void RememberPet()
	{
		Config.Profile.Memory = Creature.Remember(DateTimeOffset.UtcNow);
	}

	public void Save()
	{
		RememberPet();
		Config.Save(Paths.Settings);
		props.Configure();
		overlay.Configure(Config.Interaction, Config.ExcludeFromCapture);
		Observer.Update(Config, force: true);
		Media.Update(Config);
		UpdateTray();
	}

	public void OpenNotebook()
	{
		if (notebook == null || notebook.IsDisposed)
		{
			notebook = new NotebookForm(this);
		}
		notebook.Show();
		notebook.Activate();
	}

	public void SelectPet(string id)
	{
		if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, "characters", id)) || id.Any((char c) => !char.IsAsciiLetterOrDigit(c)))
		{
			throw new InvalidDataException("Unknown pet.");
		}
		if (notebook != null && !notebook.IsDisposed)
		{
			notebook.Shutdown();
			notebook = null;
		}
		if (Config.ReducedMotion)
		{
			ChangePet(id);
			return;
		}
		pendingPet = id;
		switching = true;
	}

	private void ChangePet(string id)
	{
		RememberPet();
		string activePet = Config.ActivePet;
		string characterPath = Config.CharacterPath;
		float petScale = Config.PetScale;
		Config.ActivePet = id;
		Config.CharacterPath = "";
		Config.PetScale = Config.Profile.Size;
		if (ReloadCharacter())
		{
			Save();
			Events.Publish("pet.changed", Creature.Time);
		}
		else
		{
			Config.ActivePet = activePet;
			Config.CharacterPath = characterPath;
			Config.PetScale = petScale;
		}
	}

	public void RefreshSound()
	{
		sounds.Dispose();
		sounds = new Sounds(Character.Sound, Config.SoundLevel);
	}

	public void OpenSettings()
	{
		if (settings == null || settings.IsDisposed)
		{
			settings = new SettingsForm(this);
		}
		settings.Show();
		settings.Activate();
	}

	public void Reset()
	{
		Observer.Update(Config, force: true);
		Creature.Reset(Observer.World);
		renderFailed = false;
		Paused = false;
		quiet = false;
		Config.Visible = true;
		revealUntil = Environment.TickCount64 + 30000;
		accumulator = 0.0;
		overlay.RecoverSurface();
		Save();
		timer.Interval = 22.0;
	}

	public bool ReloadCharacter()
	{
		try
		{
			Character character = Moss.Core.Character.Load(ActiveCharacterFile);
			CreatureRenderer creatureRenderer = new CreatureRenderer(character);
			sounds.Dispose();
			sounds = new Sounds(character.Sound, Config.SoundLevel);
			renderer.Dispose();
			renderer = creatureRenderer;
			Character = character;
			animator = new Animator(character);
			Creature = new Creature();
			WireCreature();
			Creature.Reset(Observer.World);
			Creature.Recall(Config.Profile.Memory, DateTimeOffset.UtcNow);
			renderFailed = false;
			Log.Event("content", "loaded");
			return true;
		}
		catch (Exception error)
		{
			Log.Error("content", error);
			MessageBox.Show("This character could not be loaded. Moss is keeping the previous character.", "Moss", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			return false;
		}
	}

	private void Tick()
	{
		if (quitting)
		{
			return;
		}
		if (showSignal.WaitOne(0))
		{
			Reset();
			OpenSettings();
		}
		Stopwatch stopwatch = Stopwatch.StartNew();
		double totalSeconds = clock.Elapsed.TotalSeconds;
		float num = (float)Math.Min(0.25, totalSeconds - lastTime);
		lastTime = totalSeconds;
		try
		{
			Observer.Update(Config);
			Media.Update(Config);
			Media.Decay(num);
			Notifications.Poll();
			overlay.CheckCapture();
			World world = Observer.World;
			if (switching)
			{
				transition -= num * 5f;
				if (transition <= 0f)
				{
					transition = 0f;
					switching = false;
					if (pendingPet != null)
					{
						ChangePet(pendingPet);
						pendingPet = null;
					}
				}
			}
			else
			{
				transition = Math.Min(1f, transition + num * 5f);
			}
			if (Environment.TickCount64 - lastMemorySave > 60000)
			{
				lastMemorySave = Environment.TickCount64;
				try
				{
					RememberPet();
					Config.Save(Paths.Settings);
				}
				catch (Exception error)
				{
					Log.Error("memory-save", error);
				}
			}
			if (Environment.TickCount64 - lastReminderCheck > 1000)
			{
				lastReminderCheck = Environment.TickCount64;
				try
				{
					Reminders.Tick();
				}
				catch (Exception error2)
				{
					Log.Error("reminder-update", error2);
				}
			}
			if (Config.CursorAwareness && Config.Interaction && !Creature.Held)
			{
				Vector2 value = Creature.Position - new Vector2(0f, Character.Height * 0.68f * Creature.Scale);
				if (petting.Sample(world.Cursor, Vector2.Distance(value, world.Cursor) < 28f * Creature.Scale, Creature.Time, Creature.Scale))
				{
					Creature.Pet();
					Events.Publish("pet.petted", Creature.Time);
				}
			}
			bool flag = false;
			bool flag2 = false;
			if (eventRevision != world.Revision)
			{
				eventRevision = world.Revision;
				flag = world.Windows.Any((WindowInfo window) => !knownWindows.Contains(window.Id));
				flag2 = lastForeground != 0L && lastForeground != world.Foreground;
				knownWindows.Clear();
				foreach (WindowInfo window in world.Windows)
				{
					knownWindows.Add(window.Id);
				}
				lastForeground = world.Foreground;
				foreach (WindowInfo window2 in world.Windows)
				{
					if (previousWindows.TryGetValue(window2.Id, out var value2) && value2 != window2.Bounds)
					{
						Events.Publish((value2.W != window2.Bounds.W || value2.H != window2.Bounds.H) ? "window.resized" : "window.moved", Creature.Time);
					}
				}
				HashSet<long> hashSet = world.Windows.Select((WindowInfo window) => window.Id).ToHashSet();
				foreach (long key in previousWindows.Keys)
				{
					if (!hashSet.Contains(key))
					{
						Events.Publish("window.closed", Creature.Time);
					}
				}
				previousWindows.Clear();
				foreach (WindowInfo window3 in world.Windows)
				{
					previousWindows[window3.Id] = window3.Bounds;
				}
				string text = string.Join(";", world.Displays.Select((Display d) => $"{d.Id}:{d.Bounds}:{d.Scale}:{d.RefreshRate}"));
				if (previousDisplays != "" && previousDisplays != text)
				{
					Events.Publish("monitor.changed", Creature.Time);
				}
				previousDisplays = text;
				if (previousFullscreen != world.Fullscreen)
				{
					Events.Publish(world.Fullscreen ? "fullscreen.entered" : "fullscreen.exited", Creature.Time);
				}
				previousFullscreen = world.Fullscreen;
			}
			if (previousPlaying != Media.State.Playing)
			{
				Events.Publish(Media.State.Playing ? "media.started" : "media.paused", Creature.Time);
				previousPlaying = Media.State.Playing;
			}
			if ((flag || flag2 || lastMediaChange != Media.State.Changes) && Environment.TickCount64 - lastContextReaction > 2000)
			{
				Events.Publish(flag ? "window.created" : (flag2 ? "window.foregroundChanged" : "media.changed"), Creature.Time);
				Creature.Notify();
				lastContextReaction = Environment.TickCount64;
			}
			lastMediaChange = Media.State.Changes;
			VisibilityDecision visibilityDecision = DesktopVisibility.Decide(Config, quiet, world.Fullscreen, world.Presenting, renderFailed, Environment.TickCount64 < revealUntil);
			QuietPolicy policy = visibilityDecision.Policy;
			bool hidden = visibilityDecision.Hidden;
			VisibilityStatus = visibilityDecision.Reason;
			if (lastVisibility != VisibilityStatus)
			{
				lastVisibility = VisibilityStatus;
				Log.Event("visibility", VisibilityStatus);
				tray.Text = "Moss · " + VisibilityStatus;
				UpdateTray();
			}
			if (hidden && Creature.Held)
			{
				Creature.Release();
			}
			if (overlay.Visible == hidden)
			{
				if (hidden)
				{
					overlay.Hide();
				}
				else
				{
					overlay.Show();
				}
			}
			Stopwatch stopwatch2 = Stopwatch.StartNew();
			int num2 = 0;
			accumulator += num;
			while (accumulator >= 0.008333333767950535 && num2 < 30)
			{
				if (!Paused || Creature.Held)
				{
					Vector2? grabTarget = (Creature.Held ? new Vector2?(PetOverlay.CursorPhysical + grabOffset) : ((Vector2?)null));
					Creature.Step(1f / 120f, world, Config, Config.OverridePersonality ? Config.Personality : Character.Personality, Config.Media && Media.State.Playing && Config.MusicStyle == MusicStyle.Dance, policy == QuietPolicy.Calm, grabTarget);
					animator.Set(Creature.Motion);
					animator.Step(1f / 120f, Config.RhythmAnalysis ? Media.Pulse : 0f, Creature.Velocity.X / Creature.Scale);
				}
				if (!Paused || props.Interacting)
				{
					props.Step(1f / 120f, world);
				}
				accumulator -= 0.008333333767950535;
				num2++;
			}
			Metrics.SimulationMs = stopwatch2.Elapsed.TotalMilliseconds;
			Metrics.Steps = num2;
			Metrics.RenderMs = 0.0;
			if (!hidden)
			{
				Stopwatch stopwatch3 = Stopwatch.StartNew();
				Bitmap image = renderer.Draw(Creature, animator, world, Config.CursorAwareness, Media.State.Playing, Config.ReducedMotion, Config.Profile.Hat, Config.Profile.Glasses, 0f, Config.MusicComments);
				overlay.Draw(image, Creature.Position + (Creature.Held ? Vector2.Zero : (Creature.Velocity * (float)accumulator)), Creature.Scale, PetAlpha);
				Metrics.RenderMs = stopwatch3.Elapsed.TotalMilliseconds;
			}
			props.AllowSurprises = !hidden && !Paused && policy != QuietPolicy.Calm;
			props.Draw(hidden);
			Metrics.FrameMs = stopwatch.Elapsed.TotalMilliseconds;
			Metrics.Frame(!hidden);
			double num3 = Math.Min(world.Nearest(Creature.Position)?.RefreshRate ?? 60f, Config.FrameLimit);
			if (Config.Performance == PerformanceMode.Balanced)
			{
				num3 = Math.Min(num3, 60.0);
			}
			if (Config.Performance == PerformanceMode.Battery)
			{
				num3 = Math.Min(num3, 30.0);
			}
			bool flag3;
			switch (Creature.Motion)
			{
			case Motion.Idle:
			case Motion.Sitting:
			case Motion.Looking:
			case Motion.Petted:
				flag3 = true;
				break;
			default:
				flag3 = false;
				break;
			}
			if (flag3 && !Creature.Held && !props.Interacting)
			{
				num3 = Math.Min(num3, 24.0);
			}
			double num4 = (hidden ? 200.0 : (Paused ? 200.0 : ((Creature.Motion == Motion.Sleeping) ? 200.0 : (1000.0 / num3))));
			if (!hidden && Metrics.Cpu > 3.0)
			{
				num4 = Math.Max(num4, 40.0);
			}
			timer.Interval = num4;
			if (Config.Debug && Environment.TickCount64 - lastDiagnostic > 30000)
			{
				lastDiagnostic = Environment.TickCount64;
				Log.Event("performance", $"cpu={Metrics.Cpu:F2}; ramMiB={Metrics.RamMiB:F1}; fps={Metrics.Fps:F1}; simMs={Metrics.SimulationMs:F3}; renderMs={Metrics.RenderMs:F3}");
			}
		}
		catch (Exception error3)
		{
			Log.Error("frame", error3);
			renderFailed = true;
			overlay.Hide();
			timer.Interval = 500.0;
			tray.ShowBalloonTip(4000, "Moss is resting", "The overlay encountered a problem. Use Settings → Reset position to try again.", ToolTipIcon.Info);
		}
	}

	public void Quit()
	{
		if (quitting)
		{
			return;
		}
		try
		{
			if (notebook != null && !notebook.IsDisposed)
			{
				notebook.Shutdown();
			}
		}
		catch (Exception error)
		{
			Log.Error("note-exit-save", error);
			MessageBox.Show("Your notebook could not be saved. Moss will stay open so you can copy your text.", "Notebook not saved");
			return;
		}
		quitting = true;
		timer.Stop();
		try
		{
			RememberPet();
			Config.Save(Paths.Settings);
		}
		catch (Exception error2)
		{
			Log.Error("settings-save", error2);
		}
		tray.Visible = false;
		settings?.Close();
		overlay.Close();
		ExitThread();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && !resourcesDisposed)
		{
			resourcesDisposed = true;
			timer.Dispose();
			Metrics.Dispose();
			musicCard?.Dispose();
			props.Dispose();
			notebook?.Dispose();
			Notifications.Dispose();
			Media.Dispose();
			Observer.Dispose();
			renderer.Dispose();
			sounds.Dispose();
			tray.Icon?.Dispose();
			tray.Dispose();
			menu.Dispose();
			overlay.Dispose();
			settings?.Dispose();
			Log.Event("runtime", "stopped");
		}
		base.Dispose(disposing);
	}
}
