using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
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

	private FeatureLabForm? featureLab;

	private readonly ConstructionController construction;

	private ClimbPhase lastClimbPhase;

	private BuildPhase lastBuildPhase;

	private bool testMode;

	public bool TestMode
	{
		get { return testMode; }
		set
		{
			testMode = value;
			Creature.AutonomyEnabled = !value;
			if (value)
			{
				Paused = false;
				Creature.Recover();
			}
		}
	}

	public bool BypassCooldowns { get; set; }

	public Motion? AnimationPreview { get; set; }

	public float AnimationPreviewSpeed { get; set; } = 1f;

	public bool AnimationPreviewPaused { get; set; }

	public bool AnimationPreviewLoop { get; set; }

	public ConstructionController ConstructionView => construction;

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

	public static string ProductVersion
	{
		get
		{
			try
			{
				string? version = typeof(PetApplication).Assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
				if (!string.IsNullOrWhiteSpace(version))
				{
					int plus = version.IndexOf('+');
					return plus > 0 ? version.Substring(0, plus) : version;
				}
			}
			catch
			{
			}
			return "1.0.0";
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
			Character = LoadCharacterResilient(ActiveCharacterFile);
		}
		catch (Exception error2)
		{
			Log.Error("character-load", error2);
			Character? recovered = null;
			string[] fallbacks = new string[7] { "moss", "miso", "pip", "lark", "inky", "clover", "puck" };
			foreach (string id in fallbacks)
			{
				try
				{
					recovered = LoadCharacterResilient(Path.Combine(AppContext.BaseDirectory, "characters", id, "character.json"));
					Config.CharacterPath = "";
					Config.ActivePet = id;
					break;
				}
				catch
				{
				}
			}
			if (recovered == null)
			{
				throw new InvalidDataException("No usable character pack was found. Extract the complete Moss archive into a local folder and try again.");
			}
			Character = recovered;
			Config.PetScale = Config.Profile.Size;
		}
		Creature.Species = Character.Species;
		Creature.CharWidth = Character.Width;
		Creature.CharHeight = Character.Height;
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
		construction = new ConstructionController(this);
		construction.StructureClicked += delegate(Structure s)
		{
			Creature.Construction.HouseHit(Creature, s);
			Events.Publish("house.hit", Creature.Time);
		};
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

	private Character LoadCharacterResilient(string path)
	{
		try
		{
			return Moss.Core.Character.Load(path);
		}
		catch (InvalidDataException)
		{
			Character? parsed;
			try
			{
				parsed = JsonSerializer.Deserialize<Character>(File.ReadAllText(path), Json.Options);
			}
			catch (Exception error)
			{
				throw new InvalidDataException("Character pack is unreadable.", error);
			}
			if (parsed == null)
			{
				throw new InvalidDataException("Character pack is empty.");
			}
			parsed.FillMissingClips(Character.MigrationDefaults());
			parsed.Validate();
			try
			{
				File.Copy(path, path + ".migrated-bak", overwrite: true);
				File.WriteAllText(path, JsonSerializer.Serialize(parsed, Json.Options));
			}
			catch (Exception saveError)
			{
				Log.Error("content-migrate-save", saveError);
			}
			Log.Event("content", "migrated");
			return parsed;
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

	private static Image? trayLogo;

	private static Image? TrayLogo()
	{
		if (trayLogo != null)
		{
			return trayLogo;
		}
		try
		{
			string path = Path.Combine(AppContext.BaseDirectory, "assets", "branding", "moss-logo.png");
			if (!File.Exists(path))
			{
				return null;
			}
			using FileStream stream = File.OpenRead(path);
			trayLogo = new Bitmap(Image.FromStream(stream), new Size(16, 16));
			return trayLogo;
		}
		catch
		{
			return null;
		}
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
		menu.Items.Add(new ToolStripMenuItem("Moss " + ProductVersion + " · " + PetName)
		{
			Enabled = false,
			Image = TrayLogo()
		});
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
		menu.Items.Add("Feature Lab…", null, delegate
		{
			OpenFeatureLab();
		});
		menu.Items.Add(new ToolStripSeparator());
		menu.Items.Add("Climb now", null, delegate
		{
			if (!LabClimbNearest())
			{
				tray.ShowBalloonTip(3000, "Moss", "No climbable edge in reach.", ToolTipIcon.Info);
			}
		});
		menu.Items.Add("Build a house", null, delegate
		{
			if (!LabBuild(StructureKind.House))
			{
				tray.ShowBalloonTip(3000, "Moss", "No building site right now.", ToolTipIcon.Info);
			}
		});
		menu.Items.Add("Roll a football", null, delegate
		{
			RollFootball();
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
			Creature.Species = Character.Species;
			Creature.CharWidth = Character.Width;
			Creature.CharHeight = Character.Height;
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
			foreach (Surface s in Creature.Construction.Surfaces())
			{
				bool exists = false;
				foreach (Surface other in world.Surfaces)
				{
					if (other.Id == s.Id)
					{
						exists = true;
						break;
					}
				}
				if (!exists)
				{
					world.Surfaces.Add(s);
				}
			}
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
				if (petting.Sample(world.Cursor, Vector2.Distance(value, world.Cursor) < 28f * Creature.Scale, Creature.Time, Creature.Scale, Config.Advanced.PettingSensitivity))
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
			if ((flag || flag2 || lastMediaChange != Media.State.Changes) && Environment.TickCount64 - lastContextReaction > (long)(Config.Advanced.ContextReactionCooldownSec * 1000f))
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
			float quantum = Math.Clamp(Config.Advanced.PhysicsStepSec, 1f / 240f, 1f / 30f);
			accumulator += num;
			while (accumulator >= quantum && num2 < 30)
			{
				if (!Paused || Creature.Held)
				{
					Vector2? grabTarget = (Creature.Held ? new Vector2?(PetOverlay.CursorPhysical + grabOffset) : ((Vector2?)null));
					Creature.Step(quantum, world, Config, Config.OverridePersonality ? Config.Personality : Character.Personality, Config.Media && Media.State.Playing && Config.MusicStyle == MusicStyle.Dance, policy == QuietPolicy.Calm, grabTarget);
					if (AnimationPreview.HasValue)
					{
						animator.Preview(AnimationPreview.Value);
					}
					else
					{
						animator.Set(Creature.Motion);
					}
					if (!AnimationPreviewPaused)
					{
						animator.BlendRate = Config.Advanced.BlendRate;
						float previewSpeed = AnimationPreview.HasValue ? AnimationPreviewSpeed : Config.Advanced.AnimationSpeed;
						animator.Step(quantum * previewSpeed, Config.RhythmAnalysis ? Media.Pulse : 0f, Creature.Velocity.X / Creature.Scale);
						if (AnimationPreviewLoop && AnimationPreview.HasValue && animator.Finished)
						{
							animator.Restart();
						}
					}
				}
				if (!Paused || props.Interacting)
				{
					props.Step(quantum, world);
				}
				accumulator -= quantum;
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
			props.AllowSurprises = !hidden && !Paused && policy != QuietPolicy.Calm && !TestMode;
			if (!hidden && Config.Interaction)
			{
				Creature.Construction.PokeAt(world.Cursor, Creature.Time);
			}
			props.Draw(hidden);
			construction.Draw(hidden);
			TrackSessions();
			Metrics.FrameMs = stopwatch.Elapsed.TotalMilliseconds;
			Metrics.Frame(!hidden);
			if (policy == QuietPolicy.Calm)
			{
				if (Creature.Climb.Phase != ClimbPhase.None)
				{
					Creature.EndClimbSession();
				}
				if (Creature.Construction.HasSession)
				{
					Creature.Construction.Cancel();
				}
			}
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

	private void TrackSessions()
	{
		if (Creature.Climb.Phase != lastClimbPhase)
		{
			Events.Publish(Creature.Climb.Phase == ClimbPhase.None ? "climb.ended" : "climb." + Creature.Climb.Phase.ToString().ToLowerInvariant(), Creature.Time);
			lastClimbPhase = Creature.Climb.Phase;
		}
		BuildPhase buildPhase = Creature.Construction.Phase;
		if (buildPhase != lastBuildPhase)
		{
			Events.Publish(buildPhase == BuildPhase.None ? "construction.ended" : "construction." + buildPhase.ToString().ToLowerInvariant(), Creature.Time);
			lastBuildPhase = buildPhase;
		}
	}

	public void OpenFeatureLab()
	{
		if (featureLab == null || featureLab.IsDisposed)
		{
			featureLab = new FeatureLabForm(this);
		}
		featureLab.Show();
		featureLab.Activate();
	}

	private bool StartClimbSession(bool? side)
	{
		if (Creature.Held)
		{
			return false;
		}
		if (Creature.Climb.Phase != ClimbPhase.None)
		{
			Creature.EndClimbSession();
		}
		bool ok = Creature.Climb.Start(Observer.World, Creature, Config, Character.Species, manual: true, preferLeft: side);
		if (ok)
		{
			Creature.RequestActivity(Activity.ClimbEdge, 40f);
			Events.Publish("climb.started", Creature.Time);
		}
		return ok;
	}

	public bool LabClimbNearest() => StartClimbSession(null);

	public bool LabClimbSide(bool left) => StartClimbSession(left);

	public bool LabClimbCommand(ClimbCommand command) => Creature.Climb.Phase != ClimbPhase.None && Creature.Climb.Command(command, Creature, Config);

	public void LabEndClimb() => Creature.EndClimbSession();

	public string LabClimbStatus() => Creature.Climb.Phase == ClimbPhase.None ? "idle" : $"{Creature.Climb.Phase} · {Creature.Climb.Style}";

	public string LabDescribeEdge()
	{
		if (ClimbSession.TryFindEdge(Observer.World, Creature.Position, Creature.Scale, out ClimbEdge edge, null, 2400f))
		{
			return (edge.LeftSide ? "left" : "right") + $" edge x={edge.X:0}, top={edge.Top:0}";
		}
		return "no edge in reach";
	}

	public bool LabBuild(StructureKind kind)
	{
		if (Creature.Held)
		{
			return false;
		}
		bool hadHouse = Creature.Construction.Structures.Count > 0;
		bool ok = Creature.Construction.StartBuild(Observer.World, Creature, Config, Character.Species, kind, manual: true, charWidth: Character.Width, charHeight: Character.Height);
		if (ok)
		{
			if (hadHouse)
			{
				Events.Publish("construction.demolished", Creature.Time);
			}
			Creature.RequestActivity(Activity.Build, 90f);
			Events.Publish("construction.started", Creature.Time);
		}
		return ok;
	}

	public bool LabBuildAt(StructureKind kind)
	{
		if (Creature.Held)
		{
			return false;
		}
		bool hadHouse = Creature.Construction.Structures.Count > 0;
		bool ok = Creature.Construction.StartBuildAt(Observer.World, Creature, Config, Character.Species, kind, Observer.World.Cursor, manual: true, charWidth: Character.Width, charHeight: Character.Height);
		if (ok)
		{
			if (hadHouse)
			{
				Events.Publish("construction.demolished", Creature.Time);
			}
			Creature.RequestActivity(Activity.Build, 90f);
			Events.Publish("construction.started", Creature.Time);
		}
		return ok;
	}

	public bool LabTestHammer()
	{
		if (!Creature.Construction.HasSession && !LabBuild(StructureKind.Platform))
		{
			return false;
		}
		return Creature.Construction.CommandTestHammer(Creature);
	}

	public bool LabTestNail()
	{
		if (!Creature.Construction.HasSession && !LabBuild(StructureKind.Platform))
		{
			return false;
		}
		return Creature.Construction.CommandTestNail(Creature);
	}

	public void LabDestroyStructure() => Creature.Construction.DestroyActive();

	public void LabResetConstruction() => Creature.Construction.ResetWorld();

	public string LabBuildStatus()
	{
		ConstructionWorld world = Creature.Construction;
		if (!world.HasSession && world.Active == null)
		{
			return $"idle · {world.Structures.Count} structures · {world.Materials.Count} materials";
		}
		return $"{world.Phase} · {world.StyleNote} · stage {(world.Active?.Stage ?? 0)}/{(world.Active?.StagesTotal ?? 0)} · {world.HammerSwings} swings";
	}

	public void LabMove(float dx) => Creature.MoveTo(Creature.Position.X + dx * Creature.Scale);

	public void LabSeekCursor() => Creature.Seek(Observer.World.Cursor);

	public void LabGoTaskbar()
	{
		Display? display = Observer.World.Nearest(Creature.Position);
		if (display != null)
		{
			Creature.Seek(new Vector2(display.Work.X + display.Work.W / 2f, display.Work.Bottom - 30f * Creature.Scale));
		}
	}

	public bool LabGoWindow()
	{
		WindowInfo? window = Observer.World.Windows.FirstOrDefault();
		if (window == null)
		{
			return false;
		}
		Creature.Seek(new Vector2(window.Bounds.X + window.Bounds.W / 2f, window.Bounds.Y));
		return true;
	}

	public void LabHome()
	{
		Display? display = Observer.World.Nearest(Creature.Position);
		if (display != null)
		{
			Creature.Seek(new Vector2(display.Work.X + display.Work.W * 0.65f, display.Work.Y + display.Work.H * 0.5f));
		}
	}

	public void LabStop() => Creature.Halt();

	public void LabDrop() => Creature.Drop();

	public void LabJump() => Creature.Jump(Config.Advanced.JumpVelocity / 550f);

	public void LabThrow(float dx, float dy, float mult)
	{
		Creature.Velocity = new Vector2(dx * Creature.Scale * mult, dy * Creature.Scale * mult);
		Creature.Release(Config.Advanced.ThrowPower);
		Events.Publish("pet.thrown", Creature.Time);
	}

	public void LabWake()
	{
		Creature.SetEnergy(0.65f);
		Creature.RequestActivity(Activity.Wander, 3f);
	}

	public void LabSleep()
	{
		Creature.SetEnergy(0.05f);
		Creature.RequestActivity(Activity.Sleep, 30f);
	}

	public void LabSit() => Creature.RequestActivity(Activity.Sit, 6f);

	public void LabRoam() => Creature.RequestActivity(Activity.Wander, 20f);

	public void LabRecover() => Creature.Recover();

	public void LabResetPosition()
	{
		Creature.Reset(Observer.World);
		Events.Publish("lab.repositioned", Creature.Time);
	}

	public void LabEmotion(string name)
	{
		switch (name)
		{
		case "Happy":
			Creature.SetMood(1f);
			Creature.Pet();
			break;
		case "Sad":
			Creature.SetMood(0.2f);
			Creature.SetEnergy(0.35f);
			Creature.RequestActivity(Activity.Hide, 6f);
			break;
		case "Angry":
			Creature.SetMood(0.12f);
			Creature.SetEnergy(0.95f);
			Creature.RequestActivity(Activity.Play, 5f);
			break;
		case "Annoyed":
			Creature.SetMood(0.3f);
			Creature.SetAttention(0.9f);
			Creature.RequestActivity(Activity.Sit, 3f);
			break;
		case "Curious":
			Creature.SetCuriosity(1f);
			Creature.RequestActivity(Activity.Investigate, 6f);
			break;
		case "Excited":
			Creature.SetMood(0.9f);
			Creature.SetEnergy(1f);
			Creature.RequestActivity(Activity.Play, 6f);
			break;
		case "Surprised":
			Creature.Notify();
			Creature.RequestActivity(Activity.Attention, 3f);
			break;
		case "Sleepy":
			Creature.SetEnergy(0.08f);
			Creature.RequestActivity(Activity.Sleep, 30f);
			break;
		case "Playful":
			Creature.SetEnergy(1f);
			Creature.SetMood(0.85f);
			Creature.RequestActivity(Activity.Play, 8f);
			break;
		case "Scared":
			Creature.SetMood(0.2f);
			Creature.Notify();
			Creature.RequestActivity(Activity.Hide, 8f);
			break;
		case "Proud":
			Creature.SetMood(1f);
			Creature.SetEnergy(0.8f);
			Creature.RequestActivity(Activity.Play, 4f);
			break;
		default:
			Creature.SetMood(0.7f);
			Creature.SetEnergy(0.5f);
			Creature.SetCuriosity(0.3f);
			Creature.RequestActivity(Activity.Sit, 4f);
			break;
		}
		Events.Publish("emotion." + name.ToLowerInvariant(), Creature.Time);
	}

	public void LabPet(int times)
	{
		for (int i = 0; i < Math.Clamp(times, 1, 12); i++)
		{
			Creature.Pet();
		}
		Events.Publish("pet.petted", Creature.Time);
	}

	public void LabPetInterrupt()
	{
		Creature.Pet();
		Creature.RequestActivity(Activity.Wander, 2f);
		Events.Publish("pet.petted", Creature.Time);
	}

	public void SimulateMediaPlaying(bool playing) => Media.SimulatePlaying(playing);

	public void SimulateMediaLive()
	{
		bool was = Media.State.Playing;
		Media.SimulatePlaying(null);
		if (was)
		{
			Events.Publish("media.paused", Creature.Time);
		}
	}

	public void SimulateMediaTrack()
	{
		Media.SimulateTrackChange();
		Events.Publish("media.changed", Creature.Time);
	}

	public void SimulateRhythmAccent()
	{
		Media.SimulatePulse(1f);
		Events.Publish("music.accent", Creature.Time);
	}

	public void LabDance(bool dance)
	{
		if (dance)
		{
			Creature.RequestActivity(Activity.Dance, 10f);
		}
		else
		{
			Creature.RequestActivity(Activity.Sit, 2f);
		}
	}

	public void LabMusicEnergy(float value) => Creature.SetEnergy(value);

	public bool LabDevice(string pet)
	{
		try
		{
			SelectPet(pet);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public void SimulateDesktop(string kind)
	{
		Events.Publish(kind, Creature.Time);
		switch (kind)
		{
		case "window.created":
		case "window.foregroundChanged":
			Observer.Invalidate();
			Creature.Notify();
			break;
		case "window.closed":
		case "window.moved":
		case "window.resized":
			Observer.Invalidate();
			break;
		case "monitor.changed":
		case "dpi.changed":
			Observer.Invalidate();
			Media.Update(Config);
			break;
		case "notification.received":
			Creature.Notify();
			break;
		}
	}

	public string LabTestTimer()
	{
		CommandPreview preview = NoteCommands.Parse("@timer 5m", DateTimeOffset.UtcNow, TimeZoneInfo.Local);
		Reminders.Create(preview, null);
		Events.Publish("reminder.created", Creature.Time);
		return "Test timer for " + preview.Due.ToLocalTime().ToString("HH:mm");
	}

	public string LabQuickTimer()
	{
		CommandPreview preview = NoteCommands.Parse("@timer 5s", DateTimeOffset.UtcNow, TimeZoneInfo.Local);
		Reminders.Create(preview, null);
		Events.Publish("reminder.created", Creature.Time);
		return "Due in 5 seconds — watch the tray.";
	}

	public void LabTwigThrow(Vector2 velocity) => props.TwigThrow(velocity);

	public void LabTwigGrab() => props.TwigGrab();

	public void LabTwigHome() => props.TwigHome();

	public string LabTwigState() => props.TwigState;

	public void LabStopBall() => props.StopFootball();

	public bool LabGiveMaterial()
	{
		Material? loose = Creature.Construction.Materials.FirstOrDefault(m => !m.Carried && !m.Placed);
		if (loose == null)
		{
			loose = Creature.Construction.SpawnMaterial(MaterialKind.Plank, Creature.Position + new Vector2(80f * Creature.Scale, -40f * Creature.Scale));
		}
		if (loose == null)
		{
			return false;
		}
		loose.Carried = true;
		Events.Publish("object.given", Creature.Time);
		return true;
	}

	public bool LabSpawnMaterial(MaterialKind kind) => Creature.Construction.SpawnMaterial(kind, Creature.Position + new Vector2(100f * Creature.Scale, -60f * Creature.Scale)) != null;

	public void RestartPreview() => animator.Restart();

	public string AnimatorStatus()
	{
		Clip clip = Character.Animations[animator.State.ToString()];
		return $"{animator.State} · {animator.Time:0.00}/{clip.Duration:0.00}s · {Metrics.Fps:0} fps · bob {animator.Pose.Bob:0.0} lean {animator.Pose.Lean:0.0} arms {animator.Pose.Arms:0.0}";
	}

	public string SupportDescription()
	{
		if (!Creature.Support.HasValue)
		{
			return "airborne";
		}
		long id = Creature.Support.Value;
		if (id < -8000000L)
		{
			return "structure";
		}
		Surface? surface = Observer.World.Surfaces.FirstOrDefault(s => s.Id == id);
		if (surface == null)
		{
			return "unknown";
		}
		return surface.Floor ? "floor" : "window ledge";
	}

	public void ResetTestState()
	{
		Creature.Construction.ResetWorld();
		Creature.EndClimbSession();
		AnimationPreview = null;
		AnimationPreviewPaused = false;
		TestMode = false;
		BypassCooldowns = false;
		Creature.Recover();
		Events.Publish("lab.reset", Creature.Time);
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
			construction.Dispose();
			featureLab?.Dispose();
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
