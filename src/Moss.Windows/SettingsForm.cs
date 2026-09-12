using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using Moss.Core;

namespace Moss.Windows;

internal sealed class SettingsForm : Form
{
	private readonly PetApplication app;

	private readonly Panel content = new Panel
	{
		Dock = DockStyle.Fill,
		Padding = new Padding(28, 22, 28, 20),
		AutoScroll = true
	};

	private readonly Color paper = Color.FromArgb(246, 246, 238);

	private readonly Color ink = Color.FromArgb(38, 59, 50);

	private readonly Color sage = Color.FromArgb(221, 230, 207);

	private readonly Timer timer = new Timer
	{
		Interval = 1000
	};

	private Label? diagnostics;

	private Label? petStatus;

	private readonly Font bodyFont = new Font("Segoe UI", 10f);

	private readonly Font brandFont = new Font("Segoe UI", 29f, FontStyle.Bold);

	private readonly Font headingFont = new Font("Segoe UI", 22f, FontStyle.Bold);

	private string currentPage = "Pet";

	private readonly List<(Reminder Reminder, Label Label, ReminderStatus Status)> reminderLabels = new List<(Reminder, Label, ReminderStatus)>();

	private FlowLayoutPanel list;

	public SettingsForm(PetApplication application)
	{
		app = application;
		Text = "Moss · A small life on your desktop";
		base.Size = new Size(760, 660);
		MinimumSize = new Size(680, 560);
		base.StartPosition = FormStartPosition.CenterScreen;
		Font = bodyFont;
		BackColor = paper;
		ForeColor = ink;
		base.AutoScaleMode = AutoScaleMode.Dpi;
		Panel panel = new Panel
		{
			Dock = DockStyle.Left,
			Width = 184,
			BackColor = sage,
			Padding = new Padding(16, 28, 16, 16)
		};
		Label value = new Label
		{
			Text = "moss",
			Font = brandFont,
			Height = 60,
			Dock = DockStyle.Top
		};
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Padding = new Padding(0, 22, 0, 0)
		};
		string[] array = new string[7] { "Pet", "Notebook", "Customize", "Sounds", "Behavior", "Settings", "Advanced" };
		foreach (string name in array)
		{
			Button button = new Button
			{
				Text = name,
				Width = 150,
				Height = 44,
				FlatStyle = FlatStyle.Flat,
				TextAlign = ContentAlignment.MiddleLeft,
				BackColor = sage,
				Margin = new Padding(0, 0, 0, 7)
			};
			button.FlatAppearance.BorderSize = 0;
			button.Click += delegate
			{
				Page(name);
			};
			flowLayoutPanel.Controls.Add(button);
		}
		panel.Controls.Add(flowLayoutPanel);
		panel.Controls.Add(value);
		base.Controls.Add(content);
		base.Controls.Add(panel);
		timer.Tick += delegate
		{
			UpdateDiagnostics();
		};
		timer.Start();
		Page("Pet");
	}

	private void Page(string name)
	{
		currentPage = name;
		Text = "Moss · " + app.PetName;
		reminderLabels.Clear();
		petStatus = null;
		diagnostics = null;
		while (content.Controls.Count > 0)
		{
			content.Controls[0].Dispose();
		}
		list = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Width = 480
		};
		content.Controls.Add(list);
		Heading(name);
		Button("Save changes", delegate
		{
			app.Save();
			MessageBox.Show(this, "Changes saved on this computer.", "Moss");
		});
		if (name == "Pet")
		{
			petStatus = TextBlock(app.PetName + " · " + app.VisibilityStatus);
		}
		string text = name;
		if (text == null)
		{
			return;
		}
		switch (text.Length)
		{
		case 8:
			switch (text[0])
			{
			case 'B':
				if (text == "Behavior")
				{
					TextBlock("Personality changes the choices Moss makes—not obligations you must fulfill.");
					Choice("Temperament", (!(app.Config.Activity < 0.8f)) ? ((!(app.Config.Activity > 1.2f)) ? BehaviorPreset.Playful : BehaviorPreset.Spirited) : BehaviorPreset.Calm, delegate(BehaviorPreset v)
					{
						Settings config = app.Config;
						config.Activity = v switch
						{
							BehaviorPreset.Calm => 0.6f, 
							BehaviorPreset.Spirited => 1.4f, 
							_ => 1f, 
						};
						app.Config.OverridePersonality = true;
						config = app.Config;
						config.Personality = v switch
						{
							BehaviorPreset.Calm => new Personality
							{
								Calmness = 0.9f,
								Playfulness = 0.3f,
								Curiosity = 0.5f,
								Sociability = 0.5f
							}, 
							BehaviorPreset.Spirited => new Personality
							{
								Calmness = 0.2f,
								Playfulness = 0.95f,
								Curiosity = 0.95f,
								Sociability = 0.8f
							}, 
							_ => new Personality
							{
								Calmness = 0.5f,
								Playfulness = 0.7f,
								Curiosity = 0.75f,
								Sociability = 0.65f
							}, 
						};
					});
					Button("Use this character’s own personality", delegate
					{
						app.Config.OverridePersonality = false;
						app.Config.Activity = 1f;
						app.Save();
					});
					Toggle("Occasional tiny music compliments", app.Config.MusicComments, delegate(bool v)
					{
						app.Config.MusicComments = v;
					});
					Toggle("Occasional football play", app.Config.SurprisePlay, delegate(bool v)
					{
						app.Config.SurprisePlay = v;
					});
					Choice("Music", app.Config.Media ? app.Config.MusicStyle : MusicStyle.Off, delegate(MusicStyle v)
					{
						app.Config.MusicStyle = v;
						app.Config.Media = v != MusicStyle.Off;
					});
					Toggle("Reduced motion", app.Config.ReducedMotion, delegate(bool v)
					{
						app.Config.ReducedMotion = v;
					});
				}
				break;
			case 'S':
				if (!(text == "Settings"))
				{
					break;
				}
				TextBlock("Entirely local. No telemetry, accounts, uploads, screen capture, microphone, clipboard, personal-file scanning or browser-history access.");
				Toggle("Notice cursor proximity", app.Config.CursorAwareness, delegate(bool v)
				{
					app.Config.CursorAwareness = v;
				});
				Toggle("Use window geometry as physical surfaces", app.Config.WindowGeometry, delegate(bool v)
				{
					app.Config.WindowGeometry = v;
				});
				Toggle("Notice foreground / fullscreen applications", app.Config.ForegroundAwareness, delegate(bool v)
				{
					app.Config.ForegroundAwareness = v;
				});
				Toggle("Read application names & window titles (in memory)", app.Config.ApplicationIdentity, delegate(bool v)
				{
					app.Config.ApplicationIdentity = v;
				});
				Toggle("React to system media playback", app.Config.Media, delegate(bool v)
				{
					app.Config.Media = v;
				});
				Toggle("Browser/audio fallback: output levels only (no recording)", app.Config.AudioLevelFallback, delegate(bool v)
				{
					app.Config.AudioLevelFallback = v;
				});
				Button("Open now playing card", delegate
				{
					app.OpenMusic();
				});
				Toggle("Read media metadata & artwork (in memory)", app.Config.MediaMetadata, delegate(bool v)
				{
					app.Config.MediaMetadata = v;
				});
				Toggle("Analyze speaker output for rhythmic accents", app.Config.RhythmAnalysis, delegate(bool v)
				{
					app.Config.RhythmAnalysis = v;
				});
				TextBlock("Rhythm analysis listens to the output mix locally and discards every buffer. It detects onsets, not guaranteed beats or BPM. It is never required for dancing.");
				Toggle("Exclude Moss from supported screen captures", app.Config.ExcludeFromCapture, delegate(bool v)
				{
					app.Config.ExcludeFromCapture = v;
				});
				TextBlock("Capture exclusion depends on the sharing app. Automatic sharing detection is not universal: use Ctrl + Shift + F12 before sensitive sharing. Fullscreen detection needs foreground awareness.");
				if (NotificationObserver.HasPackageIdentity)
				{
					Button(app.Config.Notifications ? "Turn notification reactions off" : "Allow notification reactions…", async delegate
					{
						if (app.Config.Notifications)
						{
							app.Config.Notifications = false;
							app.Notifications.Disable();
						}
						else
						{
							Settings config = app.Config;
							config.Notifications = await app.Notifications.EnableAsync();
						}
						app.Save();
						Page(name);
					});
				}
				TextBlock(app.Notifications.Status + ". The portable edition cannot obtain this capability. The signed MSIX edition uses notification IDs, never text.");
				break;
			case 'N':
				if (!(text == "Notebook"))
				{
					break;
				}
				TextBlock("A pocket notebook for your thoughts. All pets share these pages. A normal editor, local autosave and recoverable backups are built in.");
				Button("Open notebook", delegate
				{
					app.OpenNotebook();
				});
				TextBlock("On a page, type @timer 25m or @remind tomorrow 9am call home, then press Ctrl+Enter. You always confirm the exact time before anything is scheduled.");
				Toggle("Let Windows deliver reminders when Moss is closed", app.Config.ClosedAppReminders, delegate(bool v)
				{
					app.Config.ClosedAppReminders = v;
					app.Reminders.Reconcile();
					Page(name);
				});
				TextBlock(app.Reminders.SchedulerStatus + ". Delivery requires your Windows session; the PC is not woken from sleep. Windows may delay missed tasks.");
				{
					foreach (Reminder r in from reminder in app.Reminders.Items.Where(delegate(Reminder reminder)
						{
							ReminderStatus status = reminder.Status;
							return (uint)status <= 1u;
						})
						orderby reminder.Due
						select reminder)
					{
						Label item = TextBlock(r.Message + "\n" + r.Due.ToLocalTime().ToString("ddd, dd MMM · HH:mm"));
						reminderLabels.Add((r, item, r.Status));
						if (r.Status == ReminderStatus.Due)
						{
							Button("Dismiss", delegate
							{
								app.Reminders.Dismiss(r);
								Page(name);
							});
							Button("Snooze five minutes", delegate
							{
								app.Reminders.Snooze(r);
								Page(name);
							});
						}
						else
						{
							Button("Cancel", delegate
							{
								app.Reminders.Cancel(r);
								Page(name);
							});
						}
					}
					break;
				}
			case 'A':
				if (text == "Advanced")
				{
					Slider("Activity", app.Config.Activity, 0.3f, 1.7f, delegate(float v)
					{
						app.Config.Activity = v;
					});
					Slider("Cursor sensitivity", app.Config.Sensitivity, 0.5f, 2f, delegate(float v)
					{
						app.Config.Sensitivity = v;
					});
					Slider("Curiosity", app.Config.Personality.Curiosity, 0f, 1f, delegate(float v)
					{
						app.Config.Personality.Curiosity = v;
					});
					Toggle("Use advanced personality override", app.Config.OverridePersonality, delegate(bool v)
					{
						app.Config.OverridePersonality = v;
					});
					Button("Open world inspector", delegate
					{
						new WorldDebugForm(app).Show(this);
					});
					Choice("Performance", app.Config.Performance, delegate(PerformanceMode v)
					{
						app.Config.Performance = v;
					});
					NumericUpDown limit = new NumericUpDown
					{
						Minimum = 15m,
						Maximum = 360m,
						Value = app.Config.FrameLimit,
						Width = 120
					};
					TextBlock("Maximum rendering frequency (Hz)");
					limit.ValueChanged += delegate
					{
						app.Config.FrameLimit = (int)limit.Value;
						app.Save();
					};
					list.Controls.Add(limit);
					Toggle("Show live diagnostics here", app.Config.Debug, delegate(bool v)
					{
						app.Config.Debug = v;
						Page(name);
					});
					if (app.Config.Debug)
					{
						diagnostics = TextBlock("");
						UpdateDiagnostics();
					}
					Button("Open local data & logs", delegate
					{
						Process.Start(new ProcessStartInfo(Paths.Root)
						{
							UseShellExecute = true
						});
					});
					TextBlock("Balanced follows the display up to 60 fps. Idle caps at 24 fps. Smooth follows its reported rate up to your advanced limit. Battery caps at 30 fps. Sleeping renders at 5 fps. Hidden pets stop rendering; fixed simulation steps are batched. A busy process lowers its frame rate.");
				}
				break;
			}
			break;
		case 3:
		{
			if (!(text == "Pet"))
			{
				break;
			}
			TextBlock("A small life on your desktop. No chores, no accounts, no need to keep it entertained.");
			ShowPortrait(app.Character);
			TextBox petName = new TextBox
			{
				Text = app.PetName,
				Width = 280,
				MaxLength = 40,
				AccessibleName = "Pet name",
				BorderStyle = BorderStyle.FixedSingle,
				Margin = new Padding(0, 0, 0, 14)
			};
			petName.Leave += delegate
			{
				Safe(delegate
				{
					app.Config.Profile.Name = petName.Text.Trim();
					app.Save();
				});
			};
			list.Controls.Add(petName);
			Choice("Size", (!(app.Config.Profile.Size < 0.9f)) ? ((!(app.Config.Profile.Size > 1.1f)) ? PetSize.Default : PetSize.Large) : PetSize.Small, delegate(PetSize v)
			{
				PetProfile profile = app.Config.Profile;
				profile.Size = v switch
				{
					PetSize.Small => 0.75f, 
					PetSize.Large => 1.35f, 
					_ => 1f, 
				};
				app.Config.PetScale = app.Config.Profile.Size;
			});
			Button("Roll a football", delegate
			{
				app.RollFootball();
			});
			Button("Open the notebook", delegate
			{
				app.OpenNotebook();
			});
			Toggle("Show Moss", app.Config.Visible, delegate(bool v)
			{
				app.Config.Visible = v;
			});
			StartupControl();
			Toggle("Allow grabbing, throwing and petting", app.Config.Interaction, delegate(bool v)
			{
				app.Config.Interaction = v;
			});
			Toggle("Reduced motion", app.Config.ReducedMotion, delegate(bool v)
			{
				app.Config.ReducedMotion = v;
			});
			Choice("When an app is fullscreen", app.Config.Fullscreen, delegate(QuietPolicy v)
			{
				app.Config.Fullscreen = v;
			});
			Choice("When Windows reports a presentation", app.Config.Presentation, delegate(QuietPolicy v)
			{
				app.Config.Presentation = v;
			});
			Button("Reset position & wake up", delegate
			{
				app.Reset();
			});
			Button(app.Paused ? "Resume autonomous behavior" : "Pause autonomous behavior", delegate
			{
				app.Paused = !app.Paused;
				Page(name);
			});
			TextBlock("Right-click Moss for its menu. Ctrl + Shift + F12 toggles quiet mode, hiding it until you return. Settings changes save immediately.");
			break;
		}
		case 9:
		{
			if (!(text == "Customize"))
			{
				break;
			}
			TextBlock("Seven small personalities. One companion at a time. Notes belong to you and stay with every pet.");
			string[] directories = Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, "characters"));
			foreach (string text2 in directories)
			{
				string id = Path.GetFileName(text2);
				string text3 = Path.Combine(text2, "character.json");
				if (!File.Exists(text3))
				{
					continue;
				}
				try
				{
					Character character = Character.Load(text3);
					FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel
					{
						AutoSize = true,
						Width = 440,
						Height = 122,
						WrapContents = false,
						Margin = new Padding(0, 0, 0, 10)
					};
					PictureBox value = Portrait(character, 110);
					flowLayoutPanel.Controls.Add(value);
					Button button = new Button
					{
						Text = character.Name + "  ·  " + ((character.Species == "bean") ? "woodland creature" : character.Species),
						Width = 290,
						Height = 66,
						FlatStyle = FlatStyle.Flat,
						TextAlign = ContentAlignment.MiddleLeft,
						BackColor = paper
					};
					button.FlatAppearance.BorderSize = 0;
					button.Click += delegate
					{
						Safe(delegate
						{
							app.SelectPet(id);
						});
					};
					flowLayoutPanel.Controls.Add(button);
					list.Controls.Add(flowLayoutPanel);
				}
				catch (Exception error)
				{
					Log.Error("gallery", error);
				}
			}
			Toggle("Little hat", app.Config.Profile.Hat, delegate(bool v)
			{
				app.Config.Profile.Hat = v;
			});
			Toggle("Glasses", app.Config.Profile.Glasses, delegate(bool v)
			{
				app.Config.Profile.Glasses = v;
			});
			Button("Import character.json…", Import);
			break;
		}
		case 6:
			if (text == "Sounds")
			{
				TextBlock("A little sound, never a demand. Each character has its own synthesized timbre. No microphones are used.");
				Choice("Sound", app.Config.SoundEffects ? app.Config.SoundLevel : SoundLevel.Off, delegate(SoundLevel v)
				{
					app.Config.SoundLevel = v;
					app.Config.SoundEffects = v != SoundLevel.Off;
					app.RefreshSound();
				});
				Toggle("Show reminder notifications", app.Config.ReminderAlerts, delegate(bool v)
				{
					app.Config.ReminderAlerts = v;
				});
			}
			break;
		case 5:
			if (!(text == "About"))
			{
				break;
			}
			TextBlock("MOSS\nA small life on your desktop\n\nNative Windows overlay · Local simulation · One creature\n\nOriginal vector character, procedural animation and sound. MIT-licensed source. No online runtime services.\n\nWindows 10 2004+ or Windows 11, x64.\n\nThis build was cross-compiled on Linux. Windows integration and performance acceptance are not certified; see the verification report in the archive.");
			Button("Open user guide", delegate
			{
				string text4 = Path.Combine(AppContext.BaseDirectory, "README.md");
				if (File.Exists(text4))
				{
					Process.Start(new ProcessStartInfo(text4)
					{
						UseShellExecute = true
					});
				}
			});
			Button("Completely exit Moss", delegate
			{
				app.Quit();
			});
			break;
		case 4:
		case 7:
			break;
		}
	}

	private PictureBox Portrait(Character c, int size)
	{
		Creature creature = new Creature(42);
		Animator animation = new Animator(c);
		using CreatureRenderer creatureRenderer = new CreatureRenderer(c);
		Bitmap bitmap = creatureRenderer.Draw(creature, animation, new World(), cursorAware: false, music: false, reduced: false, hat: false, glasses: false, 2f);
		int num = bitmap.Width;
		int num2 = bitmap.Height;
		int num3 = 0;
		int num4 = 0;
		for (int i = 0; i < bitmap.Height; i++)
		{
			for (int j = 0; j < bitmap.Width; j++)
			{
				if (bitmap.GetPixel(j, i).A > 32)
				{
					num = Math.Min(num, j);
					num2 = Math.Min(num2, i);
					num3 = Math.Max(num3, j);
					num4 = Math.Max(num4, i);
				}
			}
		}
		Rectangle rect = Rectangle.FromLTRB(Math.Max(0, num - 10), Math.Max(0, num2 - 10), Math.Min(bitmap.Width, num3 + 11), Math.Min(bitmap.Height, num4 + 11));
		Bitmap image = bitmap.Clone(rect, PixelFormat.Format32bppPArgb);
		PictureBox pictureBox = new PictureBox();
		pictureBox.Size = new Size(size, size);
		pictureBox.Image = image;
		pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
		pictureBox.BackColor = paper;
		pictureBox.AccessibleName = c.Name;
		pictureBox.Disposed += delegate
		{
			image.Dispose();
		};
		return pictureBox;
	}

	private void ShowPortrait(Character c)
	{
		list.Controls.Add(Portrait(c, 160));
	}

	private void Heading(string text)
	{
		list.Controls.Add(new Label
		{
			Text = text,
			Font = headingFont,
			AutoSize = true,
			Margin = new Padding(0, 0, 0, 14)
		});
	}

	private Label TextBlock(string text)
	{
		Label label = new Label
		{
			Text = text,
			AutoSize = true,
			MaximumSize = new Size(455, 0),
			Margin = new Padding(0, 2, 0, 18)
		};
		list.Controls.Add(label);
		return label;
	}

	private void StartupControl()
	{
		CheckBox c = new CheckBox
		{
			Text = "Launch when I sign in",
			Checked = Startup.Enabled,
			AutoSize = true,
			Margin = new Padding(0, 4, 0, 10)
		};
		bool changing = false;
		c.CheckedChanged += async delegate
		{
			if (changing)
			{
				return;
			}
			changing = true;
			c.Enabled = false;
			try
			{
				await Startup.SetAsync(c.Checked);
			}
			catch (Exception error)
			{
				Log.Error("startup-setting", error);
				MessageBox.Show(this, "Windows couldn't change startup. Check Settings → Apps → Startup and your organization policies.", "Moss");
			}
			finally
			{
				if (!c.IsDisposed)
				{
					c.Checked = Startup.Enabled;
					c.Enabled = true;
				}
				changing = false;
			}
		};
		list.Controls.Add(c);
	}

	private void Toggle(string text, bool value, Action<bool> change)
	{
		CheckBox c = new CheckBox
		{
			Text = text,
			Checked = value,
			AutoSize = true,
			MaximumSize = new Size(455, 0),
			Margin = new Padding(0, 4, 0, 10),
			Padding = new Padding(0, 3, 0, 3)
		};
		c.CheckedChanged += delegate
		{
			Safe(delegate
			{
				change(c.Checked);
				app.Save();
			});
		};
		list.Controls.Add(c);
	}

	private void Button(string text, Action click)
	{
		Button button = new Button
		{
			Text = text,
			AutoSize = true,
			MinimumSize = new Size(230, 37),
			FlatStyle = FlatStyle.Flat,
			BackColor = sage,
			Margin = new Padding(0, 2, 0, 12),
			Padding = new Padding(10, 3, 10, 3)
		};
		button.FlatAppearance.BorderColor = Color.FromArgb(180, 196, 166);
		button.Click += delegate
		{
			Safe(click);
		};
		list.Controls.Add(button);
	}

	private void Choice<T>(string label, T value, Action<T> change) where T : struct, Enum
	{
		TextBlock(label);
		ComboBox c = ChoiceControl.Create(value);
		c.Width = 250;
		c.Margin = new Padding(0, -10, 0, 16);
		c.SelectedIndexChanged += delegate
		{
			Safe(delegate
			{
				change((T)c.SelectedItem);
				app.Save();
			});
		};
		list.Controls.Add(c);
	}

	private void Slider(string label, float value, float min, float max, Action<float> change)
	{
		Label l = TextBlock($"{label}  {value:0.00}");
		TrackBar t = new TrackBar
		{
			Minimum = 0,
			Maximum = 100,
			Value = (int)Math.Clamp((value - min) / (max - min) * 100f, 0f, 100f),
			Width = 360,
			Height = 36,
			TickStyle = TickStyle.None,
			Margin = new Padding(0, -10, 0, 10)
		};
		t.ValueChanged += delegate
		{
			Safe(delegate
			{
				float num = min + (float)t.Value / 100f * (max - min);
				change(num);
				l.Text = $"{label}  {num:0.00}";
				app.Save();
			});
		};
		list.Controls.Add(t);
	}

	private void Import()
	{
		using OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "Character package (character.json)|*.json",
			Title = "Choose a data-only character"
		};
		if (openFileDialog.ShowDialog(this) == DialogResult.OK)
		{
			Character value = Character.Load(openFileDialog.FileName);
			string text = Path.Combine(Paths.Root, "characters", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, "character.json");
			File.WriteAllText(text2, JsonSerializer.Serialize(value, Json.Options));
			app.RememberPet();
			app.Config.ActivePet = "custom-" + Path.GetFileName(text);
			app.Config.CharacterPath = text2;
			app.Config.PetScale = app.Config.Profile.Size;
			app.ReloadCharacter();
			app.Save();
			Page("Customize");
		}
	}

	private void Safe(Action action)
	{
		try
		{
			action();
		}
		catch (Exception error)
		{
			Log.Error("settings", error);
			MessageBox.Show(this, "That change could not be applied. Check that the file is valid and the folder is writable.", "Moss", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private void UpdateDiagnostics()
	{
		if (petStatus != null && !petStatus.IsDisposed)
		{
			petStatus.Text = app.PetName + " · " + app.VisibilityStatus;
		}
		if (currentPage == "Notebook" && reminderLabels.Any<(Reminder, Label, ReminderStatus)>(((Reminder Reminder, Label Label, ReminderStatus Status) x) => x.Reminder.Status != x.Status))
		{
			Page("Notebook");
			return;
		}
		foreach (var reminderLabel in reminderLabels)
		{
			if (!reminderLabel.Label.IsDisposed)
			{
				TimeSpan timeSpan = reminderLabel.Reminder.Due - DateTimeOffset.UtcNow;
				reminderLabel.Label.Text = reminderLabel.Reminder.Message + "\n" + reminderLabel.Reminder.Due.ToLocalTime().ToString("ddd, dd MMM · HH:mm") + ((timeSpan > TimeSpan.Zero) ? $" · {Math.Floor(timeSpan.TotalHours):0}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00} left" : " · Due");
			}
		}
		if (diagnostics != null && !diagnostics.IsDisposed)
		{
			Metrics metrics = app.Metrics;
			diagnostics.Text = $"CPU (whole machine): {metrics.Cpu:0.0}%\nWorking set: {metrics.RamMiB:0.0} MiB\nRendering: {metrics.Fps:0.0} fps · last frame {metrics.FrameMs:0.00} ms\nSimulation / physics: {metrics.SimulationMs:0.000} ms ({metrics.Steps} steps)\nRender + compositor submission: {metrics.RenderMs:0.000} ms\nLast world scan: {app.Observer.LastScanMs:0.000} ms\nSurfaces: {app.Observer.World.Surfaces.Count} · displays: {app.Observer.World.Displays.Count}\nVisibility: {app.VisibilityStatus}\nState: {app.Creature.Motion} · energy: {app.Creature.Energy:0.00}\nGDI handles: {metrics.GdiHandles} · reusable DIBs: {LayeredSurface.Active}\nGPU: not sampled (GDI+/DWM)\nOverlay bitmap: {Math.Pow(Math.Ceiling(180f * app.Creature.Scale), 2.0) * 4.0 / 1024.0:0} KiB\n{app.Media.Status}\n{app.Media.RhythmStatus}\n{app.Notifications.Status}";
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			timer.Dispose();
		}
		base.Dispose(disposing);
		if (disposing)
		{
			bodyFont.Dispose();
			brandFont.Dispose();
			headingFont.Dispose();
		}
	}
}
