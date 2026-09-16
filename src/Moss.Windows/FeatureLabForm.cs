using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Moss.Core;

namespace Moss.Windows;

// Feature Lab: manual triggers for every major production system, plus the
// Advanced numeric controls, animation browser, event stream and live state.
// Every button drives the real behavior (Creature, ConstructionWorld, media,
// reminders); nothing here is a demo-only animation.
internal sealed class FeatureLabForm : Form
{
	private readonly PetApplication app;

	private readonly Panel content = new Panel
	{
		Dock = DockStyle.Fill,
		Padding = new Padding(24, 18, 24, 16),
		AutoScroll = true
	};

	private readonly ListBox nav = new ListBox
	{
		Dock = DockStyle.Fill,
		Font = new Font("Segoe UI", 10f)
	};

	private readonly TextBox search = new TextBox
	{
		Dock = DockStyle.Top,
		Height = 30,
		Font = new Font("Segoe UI", 10f)
	};

	private readonly Label hint = new Label
	{
		Dock = DockStyle.Bottom,
		Height = 44,
		Font = new Font("Segoe UI", 9f),
		ForeColor = Color.FromArgb(90, 110, 100)
	};

	private readonly Label statusBar = new Label
	{
		Dock = DockStyle.Bottom,
		Height = 30,
		Font = new Font("Segoe UI", 9f),
		BackColor = Color.FromArgb(221, 230, 207),
		TextAlign = ContentAlignment.MiddleLeft,
		Padding = new Padding(12, 0, 0, 0)
	};

	private readonly CheckBox testModeBox = new CheckBox { Text = "Test Mode", AutoSize = true };

	private readonly CheckBox bypassBox = new CheckBox { Text = "Bypass cooldowns", AutoSize = true };

	private readonly Timer live = new Timer { Interval = 500 };

	private readonly ToolTip tips = new ToolTip();

	private readonly Color paper = Color.FromArgb(246, 246, 238);

	private readonly Color ink = Color.FromArgb(38, 59, 50);

	private readonly Color sage = Color.FromArgb(221, 230, 207);

	private readonly Font bodyFont = new Font("Segoe UI", 10f);

	private readonly Font headingFont = new Font("Segoe UI", 16f, FontStyle.Bold);

	private readonly Font smallFont = new Font("Segoe UI", 9f);

	private string category = "Movement";

	private FlowLayoutPanel list = null!;

	private Label? pageStatus;

	private ListBox? motionList;

	private Label? animReadout;

	private Label? coverageBox;

	private TextBox? animSearch;

	private TrackBar? speedSlider;

	private CheckBox? loopBox;

	private ListBox? eventList;

	private Label? stateBlock;

	private ListBox? reminderList;

	private Label? musicStatus;

	private readonly List<SearchItem> index = new List<SearchItem>();

	private sealed class SearchItem
	{
		public string Category = "";

		public string Label = "";

		public string Keys = "";

		public Action Run = delegate { };
	}

	private static readonly string[] Categories = { "Movement", "Physics", "Climbing", "Construction", "Objects", "Emotion & Petting", "Music", "Desktop & Notes", "Animation", "Advanced", "Events & State" };

	private static readonly IReadOnlyDictionary<string, string[]> MotionCategories = new Dictionary<string, string[]>
	{
		["Locomotion"] = new[] { "Idle", "Walking", "Running", "Jumping", "Falling", "Airborne", "Impact", "Recovery" },
		["Interaction"] = new[] { "Grabbed", "Held", "Thrown", "Looking", "Reacting", "Startled", "Petted", "Hiding", "Investigating" },
		["Climbing"] = new[] { "Climbing", "Hanging", "Peeking", "Balancing" },
		["Rest & mood"] = new[] { "Sitting", "Sleeping", "Waking", "Dancing", "Celebrating" },
		["Objects & work"] = new[] { "Carrying", "Hammering" }
	};

	public FeatureLabForm(PetApplication application)
	{
		app = application;
		Text = "Moss · Feature Lab";
		Size = new Size(1060, 720);
		MinimumSize = new Size(900, 600);
		StartPosition = FormStartPosition.CenterScreen;
		Font = bodyFont;
		BackColor = paper;
		ForeColor = ink;
		AutoScaleMode = AutoScaleMode.Dpi;
		try
		{
			Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}
		catch
		{
		}
		Panel top = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(12, 6, 12, 4), BackColor = sage };
		search.PlaceholderText = "Search actions…  (try: climb, music, hammer, dance)";
		search.TextChanged += delegate { RefreshNav(); };
		top.Controls.Add(search);
		Panel bar = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(12, 4, 12, 4), BackColor = sage };
		testModeBox.Checked = app.TestMode;
		tips.SetToolTip(testModeBox, "Pause autonomous decisions so your triggers take priority. Surprises stop. Uncheck to resume normal life.");
		testModeBox.CheckedChanged += delegate { Safe(() => { app.TestMode = testModeBox.Checked; }); };
		bypassBox.Checked = app.BypassCooldowns;
		tips.SetToolTip(bypassBox, "Let manual triggers ignore behavior timers. Autonomous cooldowns are untouched.");
		bypassBox.CheckedChanged += delegate { Safe(() => { app.BypassCooldowns = bypassBox.Checked; if (bypassBox.Checked) { app.Creature.ResetCooldowns(); } }); };
		Button resetTests = SmallButton("Reset test state");
		tips.SetToolTip(resetTests, "Clear structures, materials and sessions and resume autonomy. Notes, reminders and settings are untouched.");
		resetTests.Click += delegate { Safe(() => { app.ResetTestState(); testModeBox.Checked = false; bypassBox.Checked = false; Page(category); }); };
		bar.Controls.Add(resetTests);
		bar.Controls.Add(bypassBox);
		bar.Controls.Add(testModeBox);
		SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 220 };
		Panel left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 6, 12) };
		left.Controls.Add(nav);
		left.Controls.Add(hint);
		nav.SelectedIndexChanged += delegate { NavChosen(); };
		nav.DoubleClick += delegate { RunSearchHit(); };
		nav.KeyDown += delegate(object? _, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { RunSearchHit(); } };
		split.Panel1.Controls.Add(left);
		split.Panel2.Controls.Add(content);
		Controls.Add(split);
		Controls.Add(bar);
		Controls.Add(top);
		Controls.Add(statusBar);
		live.Tick += delegate { UpdateLive(); };
		live.Start();
		RefreshNav();
		Page("Movement");
	}

	private Button SmallButton(string text)
	{
		return new Button
		{
			Text = text,
			AutoSize = true,
			MinimumSize = new Size(150, 30),
			FlatStyle = FlatStyle.Flat,
			BackColor = Color.White,
			Margin = new Padding(0, 2, 8, 2)
		};
	}

	private void Safe(Action action)
	{
		try
		{
			action();
		}
		catch (Exception error)
		{
			Log.Error("feature-lab", error);
			MessageBox.Show(this, "That action could not run: " + error.Message, "Feature Lab", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private void RefreshNav()
	{
		string query = search.Text.Trim().ToLowerInvariant();
		nav.Items.Clear();
		if (query.Length == 0)
		{
			foreach (string name in Categories)
			{
				nav.Items.Add(name);
			}
			nav.SelectedItem = category;
			return;
		}
		foreach (SearchItem item in index.Where(i => (i.Label + " " + i.Keys + " " + i.Category).ToLowerInvariant().Contains(query)))
		{
			nav.Items.Add(item.Category + " — " + item.Label);
		}
		hint.Text = nav.Items.Count == 0 ? "No actions match." : nav.Items.Count + " matching actions. Double-click or press Enter to run.";
	}

	private void NavChosen()
	{
		if (nav.SelectedItem == null)
		{
			return;
		}
		string picked = nav.SelectedItem.ToString() ?? "";
		if (search.Text.Trim().Length > 0)
		{
			int dash = picked.IndexOf(" — ", StringComparison.Ordinal);
			hint.Text = dash >= 0 ? picked : picked;
			return;
		}
		Page(picked);
	}

	private void RunSearchHit()
	{
		if (nav.SelectedItem == null || search.Text.Trim().Length == 0)
		{
			return;
		}
		string picked = nav.SelectedItem.ToString() ?? "";
		SearchItem? item = index.FirstOrDefault(i => (i.Category + " — " + i.Label) == picked);
		if (item != null)
		{
			Safe(item.Run);
		}
	}

	private void Page(string name)
	{
		category = name;
		pageStatus = null;
		motionList = null;
		animReadout = null;
		coverageBox = null;
		animSearch = null;
		speedSlider = null;
		loopBox = null;
		eventList = null;
		stateBlock = null;
		reminderList = null;
		musicStatus = null;
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
			Width = 760
		};
		content.Controls.Add(list);
		Heading(name);
		switch (name)
		{
		case "Movement": MovementPage(); break;
		case "Physics": PhysicsPage(); break;
		case "Climbing": ClimbingPage(); break;
		case "Construction": ConstructionPage(); break;
		case "Objects": ObjectsPage(); break;
		case "Emotion & Petting": EmotionPage(); break;
		case "Music": MusicPage(); break;
		case "Desktop & Notes": DesktopPage(); break;
		case "Animation": AnimationPage(); break;
		case "Advanced": AdvancedPage(); break;
		default: EventsPage(); break;
		}
		pageStatus = new Label { AutoSize = true, MaximumSize = new Size(720, 0), Font = smallFont, ForeColor = Color.FromArgb(90, 110, 100), Margin = new Padding(0, 6, 0, 10) };
		list.Controls.Add(pageStatus);
		UpdateLive();
	}

	private void Status(string text)
	{
		if (pageStatus != null && !pageStatus.IsDisposed)
		{
			pageStatus.Text = text;
		}
	}

	private void Heading(string text)
	{
		list.Controls.Add(new Label { Text = text, Font = headingFont, AutoSize = true, Margin = new Padding(0, 0, 0, 4) });
	}

	private void Para(string text)
	{
		list.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(720, 0), Margin = new Padding(0, 2, 0, 10) });
	}

	private void Act(string label, string keys, string help, Action run)
	{
		Button button = new Button
		{
			Text = label,
			AutoSize = true,
			MinimumSize = new Size(230, 34),
			FlatStyle = FlatStyle.Flat,
			BackColor = sage,
			Margin = new Padding(0, 2, 0, 8),
			Padding = new Padding(10, 2, 10, 2)
		};
		button.FlatAppearance.BorderColor = Color.FromArgb(180, 196, 166);
		tips.SetToolTip(button, help);
		button.Click += delegate { Safe(run); };
		list.Controls.Add(button);
		index.Add(new SearchItem { Category = category, Label = label, Keys = keys, Run = () => Safe(run) });
	}

	private void Tog(string label, Func<bool> get, Action<bool> set, string help)
	{
		CheckBox box = new CheckBox { Text = label, Checked = get(), AutoSize = true, MaximumSize = new Size(720, 0), Margin = new Padding(0, 4, 0, 8) };
		tips.SetToolTip(box, help);
		box.CheckedChanged += delegate { Safe(() => { set(box.Checked); app.Save(); }); };
		list.Controls.Add(box);
	}

	private void Num(string label, float min, float max, int decimals, Func<float> get, Action<float> set, string help)
	{
		Label name = new Label { Text = label, AutoSize = true, MaximumSize = new Size(720, 0), Margin = new Padding(0, 4, 0, 0) };
		tips.SetToolTip(name, help);
		list.Controls.Add(name);
		NumericUpDown number = new NumericUpDown
		{
			Minimum = (decimal)min,
			Maximum = (decimal)max,
			DecimalPlaces = decimals,
			Increment = (decimal)Math.Max(Math.Pow(10, -decimals), (max - min) / 200.0),
			Value = (decimal)Math.Clamp(get(), min, max),
			Width = 200,
			Margin = new Padding(0, 0, 0, 10)
		};
		tips.SetToolTip(number, help);
		number.ValueChanged += delegate
		{
			Safe(() =>
			{
				set((float)number.Value);
				app.Config.Advanced.Preset = TimingPreset.Custom;
				app.Save();
			});
		};
		list.Controls.Add(number);
	}

	private void ChoiceRow<T>(string label, T value, Action<T> change, string help) where T : struct, Enum
	{
		Label name = new Label { Text = label, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
		tips.SetToolTip(name, help);
		list.Controls.Add(name);
		ComboBox box = ChoiceControl.Create(value);
		box.Width = 250;
		box.Margin = new Padding(0, 0, 0, 12);
		tips.SetToolTip(box, help);
		box.SelectedIndexChanged += delegate { Safe(() => { change((T)box.SelectedItem); app.Save(); }); };
		list.Controls.Add(box);
	}

	private void MovementPage()
	{
		Para("Every button runs the production behavior system — the same paths autonomy uses.");
		Act("Walk", "walk move roam wander", "Walk to a nearby point using normal locomotion.", () => { app.LabMove(220f); Status("Walking."); });
		Act("Run", "run fast sprint", "Run far with full energy, the way Play does.", () => { app.Creature.SetEnergy(1f); app.LabMove(600f); Status("Running."); });
		Act("Jump", "jump hop leap", "A real autonomous-strength jump.", () => { app.LabJump(); Status("Jumped."); });
		Act("Stop", "stop halt stay", "Stop and sit. Same as the engine settling.", () => { app.LabStop(); Status("Stopped."); });
		Act("Sit", "sit rest idle", "Sit for a while.", () => { app.LabSit(); Status("Sitting."); });
		Act("Sleep", "sleep nap tired", "Drop energy and sleep, like a long quiet evening.", () => { app.LabSleep(); Status("Sleeping."); });
		Act("Wake", "wake up rouse", "Restore energy and resume wandering.", () => { app.LabWake(); Status("Awake."); });
		Act("Roam", "roam wander explore", "Wander freely for a while.", () => { app.LabRoam(); Status("Roaming."); });
		Act("Go to cursor", "cursor mouse pointer follow", "Investigate your cursor position.", () => { app.LabSeekCursor(); Status("Heading to cursor."); });
		Act("Go to taskbar", "taskbar bottom work area", "Travel to the work-area floor.", () => { app.LabGoTaskbar(); Status("Heading to taskbar."); });
		Act("Go to window", "window app surface ledge", "Investigate the first visible window.", () => { Status(app.LabGoWindow() ? "Heading to window." : "No window in reach."); });
		Act("Return home", "home preferred area reset", "Travel back to the preferred area.", () => { app.LabHome(); Status("Going home."); });
		Act("Reset position", "reset position recover place", "Reposition safely and resume. Does not touch notes or settings.", () => { app.LabResetPosition(); Status("Repositioned."); });
		Act("Fall", "fall drop gravity", "Lose support and fall with real gravity.", () => { app.LabDrop(); Status("Falling."); });
		Act("Recover", "recover stuck unstuck decide", "Clear a stuck target and decide again immediately.", () => { app.LabRecover(); Status("Recovering."); });
	}

	private void PhysicsPage()
	{
		Para("Impulses run through the real integrator: gravity, momentum, friction, landing and bounce.");
		Act("Drop", "drop fall gravity", "Release support and fall straight down.", () => { app.LabDrop(); Status("Dropped."); });
		Act("Throw left", "throw left toss fling", "Throw with your configured throw power.", () => { app.LabThrow(-700f, -120f, 1f); Status("Thrown left."); });
		Act("Throw right", "throw right toss fling", "Throw with your configured throw power.", () => { app.LabThrow(700f, -120f, 1f); Status("Thrown right."); });
		Act("Throw up", "throw up toss launch", "Throw straight up.", () => { app.LabThrow(60f, -900f, 1f); Status("Thrown up."); });
		Act("Heavy throw", "heavy hard strong throw", "A deliberately overpowered throw.", () => { app.LabThrow(900f, -300f, 1.8f); Status("Heavy throw."); });
		Act("Light throw", "light gentle soft throw", "A gentle toss.", () => { app.LabThrow(350f, -80f, 0.5f); Status("Light throw."); });
		Act("Bounce test", "bounce rebound impact floor", "Lift high and drop to watch landing and rebound.", () =>
		{
			app.Creature.Position = app.Creature.Position + new System.Numerics.Vector2(0f, -300f * app.Creature.Scale);
			app.Creature.Velocity = System.Numerics.Vector2.Zero;
			app.LabDrop();
			Status("Bounce test falling.");
		});
		Para("Fine values live under Advanced: gravity, friction, bounce, jump velocity, throw power, grip strength, simulation step.");
	}

	private void ClimbingPage()
	{
		Para("Screen edges are real surfaces. These drive the production climb session — approach, grip, hang, pull-up, climb, corner, top, balance.");
		Act("Find nearest edge", "find nearest edge locate", "Report the closest climbable screen edge.", () => { Status("Nearest: " + app.LabDescribeEdge()); });
		Act("Climb current edge", "climb current edge full sequence", "Run the complete production climb: approach to corner to top.", () => { Status(app.LabClimbNearest() ? "Climbing." : "No edge in reach."); });
		Act("Climb left edge", "climb left edge", "Travel to the left screen edge and climb it.", () => { Status(app.LabClimbSide(true) ? "Heading left." : "Could not start."); });
		Act("Climb right edge", "climb right edge", "Travel to the right screen edge and climb it.", () => { Status(app.LabClimbSide(false) ? "Heading right." : "Could not start."); });
		Act("Hang", "hang dangle grip hold", "Hang from the current grip.", () => { Status(app.LabClimbCommand(ClimbCommand.Hang) ? "Hanging." : "Not while climbing."); });
		Act("Pull up", "pull up chin lift", "Pull up from a hang or grip.", () => { Status(app.LabClimbCommand(ClimbCommand.PullUp) ? "Pulling up." : "Not while climbing."); });
		Act("Reach corner", "corner reach top fast", "Hurry the current climb toward the corner.", () => { Status(app.LabClimbCommand(ClimbCommand.ReachCorner) ? "Rushing to corner." : "Not while climbing."); });
		Act("Climb to top", "top summit over", "Hurry the current climb over the top edge.", () => { Status(app.LabClimbCommand(ClimbCommand.ClimbToTop) ? "Heading over." : "Not while climbing."); });
		Act("Peek", "peek look glance", "Pause and look around mid-climb.", () => { Status(app.LabClimbCommand(ClimbCommand.Peek) ? "Peeking." : "Not while climbing."); });
		Act("Slip", "slip slide stumble", "Force a slip to watch recovery.", () => { Status(app.LabClimbCommand(ClimbCommand.Slip) ? "Slipped — recovering." : "Not while climbing."); });
		Act("Recover", "recover regain grip", "Re-grip after a slip.", () => { Status(app.LabClimbCommand(ClimbCommand.Recover) ? "Recovering grip." : "Not while climbing."); });
		Act("Jump from edge", "jump leap off edge", "Leap away from the edge with real momentum.", () => { Status(app.LabClimbCommand(ClimbCommand.JumpFromEdge) ? "Jumped." : "Not while climbing."); });
		Act("End climb", "end stop cancel climb", "Leave the climb and resume normal life.", () => { app.LabEndClimb(); Status("Climb ended."); });
		Act("Climb status", "status phase style", "Show the live climb phase.", () => { Status(app.LabClimbStatus()); });
	}

	private void ConstructionPage()
	{
		Para("Real construction: the creature travels, gathers carried planks, places stages and hammers nails until a standable structure exists.");
		Act("Build house", "build house hut shelter construct", "Run the full production house build.", () => { Status(app.LabBuild(StructureKind.House) ? "Building a house." : "No site or busy."); });
		Act("Build platform", "build platform deck stage", "Run the full production platform build.", () => { Status(app.LabBuild(StructureKind.Platform) ? "Building a platform." : "No site or busy."); });
		Act("Build stick structure", "build stick lattice twigs", "Run the full production stick build.", () => { Status(app.LabBuild(StructureKind.StickStructure) ? "Building sticks." : "No site or busy."); });
		Act("Build here", "build here cursor mouse place where", "Start a house where your cursor is right now.", () => { Status(app.LabBuildAt(StructureKind.House) ? "Building here." : "No surface under cursor."); });
		Act("Test hammer", "hammer test swing mallet", "Put a hammer in hand and swing at the current site.", () => { Status(app.LabTestHammer() ? "Hammering." : "No site or busy."); });
		Act("Test nail", "nail test fasten spark", "Drive one nail with a spark at the current site.", () => { Status(app.LabTestNail() ? "Nail driven." : "No site or busy."); });
		Act("Test material pickup", "material pickup plank carry give", "Hand the pet a plank to carry.", () => { Status(app.LabGiveMaterial() ? "Plank in hand." : "Could not spawn."); });
		Act("Destroy structure", "destroy demolish remove structure", "Remove the active structure and its placed parts.", () => { app.LabDestroyStructure(); Status("Structure destroyed."); });
		Act("Reset construction", "reset clear construction world", "Clear every structure and material.", () => { app.LabResetConstruction(); Status("Construction world cleared."); });
		Act("Build status", "status phase stage swings", "Show the live build phase and progress.", () => { Status(app.LabBuildStatus()); });
	}

	private void ObjectsPage()
	{
		Para("Props use the production tug, strain, throw and retrieve physics.");
		Act("Twig home", "twig toy stick home spawn reset", "Return the twig near the pet.", () => { app.LabTwigHome(); Status("Twig placed nearby."); });
		Act("Grab twig", "grab twig take hold pull", "Take hold of the twig, then drag with your mouse to pull against the pet.", () => { app.LabTwigGrab(); Status("Twig grabbed — drag to pull."); });
		Act("Throw twig left", "throw twig left toss", "Throw the twig with real momentum.", () => { app.LabTwigThrow(new System.Numerics.Vector2(-700f * app.Creature.Scale, -200f * app.Creature.Scale)); Status("Twig thrown left."); });
		Act("Throw twig right", "throw twig right toss", "Throw the twig with real momentum.", () => { app.LabTwigThrow(new System.Numerics.Vector2(700f * app.Creature.Scale, -200f * app.Creature.Scale)); Status("Twig thrown right."); });
		Act("Twig status", "twig status state", "Show who holds the twig.", () => { Status("Twig: " + app.LabTwigState()); });
		Act("Roll football", "football soccer ball roll sport", "Roll a football across the work area.", () => { app.RollFootball(); Status("Football rolling."); });
		Act("Stop football", "football stop halt", "Remove the football.", () => { app.LabStopBall(); Status("Football stopped."); });
		Act("Open notebook", "notebook notes open write", "Open the real notebook window.", () => { app.OpenNotebook(); Status("Notebook opened."); });
		Act("Spawn plank", "spawn plank wood material", "Drop a loose plank near the pet.", () => { Status(app.LabSpawnMaterial(MaterialKind.Plank) ? "Plank spawned." : "Could not spawn."); });
		Act("Spawn stick", "spawn stick twig material", "Drop a loose stick near the pet.", () => { Status(app.LabSpawnMaterial(MaterialKind.Stick) ? "Stick spawned." : "Could not spawn."); });
		Act("Spawn hammer", "spawn hammer mallet tool", "Drop a loose hammer near the pet.", () => { Status(app.LabSpawnMaterial(MaterialKind.Hammer) ? "Hammer spawned." : "Could not spawn."); });
		Act("Give object to pet", "give hold carry object pet", "Hand the nearest loose material to the pet.", () => { Status(app.LabGiveMaterial() ? "Pet holds it." : "Nothing to give."); });
		Para("Tip: grab the hammer or a nail right off a built house with your mouse and throw it. A plain click on the house only knocks.");
	}

	private void EmotionPage()
	{
		Para("Emotions set the production mood, energy, attention and curiosity, then request the matching behavior.");
		string[] emotions = { "Happy", "Sad", "Angry", "Annoyed", "Curious", "Excited", "Surprised", "Sleepy", "Playful", "Scared", "Proud", "Calm" };
		foreach (string name in emotions)
		{
			string local = name;
			Act(local, "emotion feeling mood " + local.ToLowerInvariant(), "Set " + local.ToLowerInvariant() + " through the production emotional state.",
				() => { app.LabEmotion(local); Status(local + "."); });
		}
		Para("Petting runs the production petting response. Fast and repeated rubs accumulate mood.");
		Act("Head pet", "pet head rub gentle", "One gentle head pet.", () => { app.LabPet(1); Status("Petted."); });
		Act("Back pet", "pet back stroke", "One back stroke.", () => { app.LabPet(1); Status("Stroked."); });
		Act("Fast rub", "fast rub quick triple", "Three quick rubs; mood climbs each time.", () => { app.LabPet(3); Status("Fast rub."); });
		Act("Slow rub", "slow rub single gentle", "A single slow rub.", () => { app.LabPet(1); Status("Slow rub."); });
		Act("Repeated pet", "repeated pet many cuddle", "Five rubs in a row.", () => { app.LabPet(5); Status("Thoroughly petted."); });
		Act("Pet until relaxed", "relax calm soothe until", "Keep petting until mood settles (bounded).", () =>
		{
			int rounds = 0;
			while (app.Creature.Mood < 0.99f && rounds < 12)
			{
				app.Creature.Pet();
				rounds++;
			}
			app.Events.Publish("pet.petted", app.Creature.Time);
			Status("Relaxed after " + rounds + " rubs.");
		});
		Act("Pet interrupt", "interrupt stop cancel pet", "Start a pet, then interrupt it with wandering.", () => { app.LabPetInterrupt(); Status("Interrupted."); });
	}

	private void MusicPage()
	{
		Para("Simulation feeds the same playback state the real media sessions drive. No Spotify needed.");
		Act("Media started", "media started playing begin", "Simulate playback starting.", () => { app.SimulateMediaPlaying(true); Status("Simulated playing."); });
		Act("Media paused", "media paused halt", "Simulate playback pausing.", () => { app.SimulateMediaPlaying(false); Status("Simulated paused."); });
		Act("Media resumed", "media resumed continue", "Simulate playback resuming.", () => { app.SimulateMediaPlaying(true); Status("Simulated resumed."); });
		Act("Media changed", "media changed track next", "Simulate a track change.", () => { app.SimulateMediaTrack(); Status("Simulated track change."); });
		Act("Media stopped", "media stopped end live", "Return to live media observation.", () => { app.SimulateMediaLive(); Status("Back to live."); });
		Act("Energy low", "energy low tired", "Lower pet energy (dance needs enough).", () => { app.LabMusicEnergy(0.3f); Status("Energy low."); });
		Act("Energy medium", "energy medium normal", "Middle energy.", () => { app.LabMusicEnergy(0.6f); Status("Energy medium."); });
		Act("Energy high", "energy high lively", "Full energy, ready to dance.", () => { app.LabMusicEnergy(0.95f); Status("Energy high."); });
		Act("Dance", "dance boogie groove", "Request dancing directly.", () => { app.LabDance(true); Status("Dancing."); });
		Act("Stop dance", "stop dance sit", "Stop dancing and sit.", () => { app.LabDance(false); Status("Dance stopped."); });
		Act("Rhythm accent", "rhythm accent beat pulse", "Inject one onset accent through the rhythm path.", () => { app.SimulateRhythmAccent(); Status("Accent injected."); });
		Act("Headphones pet", "headphones device moss", "Switch to Moss (headphones).", () => { Status(app.LabDevice("moss") ? "Moss wears headphones." : "Switch failed."); });
		Act("CD pet", "cd player disc pip", "Switch to Pip (CD player).", () => { Status(app.LabDevice("pip") ? "Pip spins a CD." : "Switch failed."); });
		Act("Radio pet", "radio lark puck", "Switch to Lark (radio).", () => { Status(app.LabDevice("lark") ? "Lark tunes a radio." : "Switch failed."); });
		Act("Turntable pet", "turntable vinyl inky", "Switch to Inky (turntable).", () => { Status(app.LabDevice("inky") ? "Inky drops the needle." : "Switch failed."); });
		Act("Cassette pet", "cassette tape clover", "Switch to Clover (cassette).", () => { Status(app.LabDevice("clover") ? "Clover plays tape." : "Switch failed."); });
		Act("Wren pet", "wren bird experimental headphones", "Switch to Wren (headphones, experimental atlas bird).", () => { Status(app.LabDevice("wren") ? "Wren has headphones." : "Switch failed."); });
		musicStatus = new Label { AutoSize = true, MaximumSize = new Size(720, 0), Margin = new Padding(0, 6, 0, 10), Font = smallFont };
		list.Controls.Add(musicStatus);
	}

	private void DesktopPage()
	{
		Para("Event buttons publish the same event kinds the engine emits, with the same downstream handling (rescan, notify) where one exists.");
		string[] events = { "window.created", "window.closed", "window.moved", "window.resized", "window.foregroundChanged", "notification.received", "monitor.changed", "dpi.changed", "fullscreen.entered", "fullscreen.exited", "user.idle", "user.active" };
		foreach (string kind in events)
		{
			string local = kind;
			Act(local, "desktop event window monitor " + local.Replace(".", " ").Replace("_", " "), "Emit " + local + " through production handling.",
				() => { app.SimulateDesktop(local); Status("Emitted " + local + "."); });
		}
		Para("Notebook and reminders use the production store and scheduler paths.");
		Act("Open notebook", "notebook notes open", "Open the real notebook window.", () => { app.OpenNotebook(); Status("Notebook opened."); });
		Act("New 5-minute test timer", "timer test reminder create", "Create a real 5-minute timer through the command grammar.", () => { Status(app.LabTestTimer()); });
		Act("Fire 5-second proof timer", "timer proof test seconds due fast", "Create a timer due in 5 seconds through the full pipeline.", () => { Status(app.LabQuickTimer()); });
		Act("Refresh reminders", "reminders list refresh show", "List pending and due reminders below.", () => { RefreshReminders(); });
		reminderList = new ListBox { Width = 700, Height = 110, Font = smallFont };
		list.Controls.Add(reminderList);
		Act("Snooze selected", "snooze delay reminder", "Snooze the selected reminder five minutes.", () => { ReminderAction(r => app.Reminders.Snooze(r), "Snoozed."); });
		Act("Dismiss selected", "dismiss done reminder", "Dismiss the selected due reminder.", () => { ReminderAction(r => app.Reminders.Dismiss(r), "Dismissed."); });
		Act("Cancel selected", "cancel delete reminder", "Cancel the selected reminder.", () => { ReminderAction(r => app.Reminders.Cancel(r), "Cancelled."); });
		Para("Notifications: " + app.Notifications.Status);
		Act("Simulate notification", "notification simulate toast", "Run the production notification-arrival path.", () => { app.SimulateDesktop("notification.received"); Status("Notification path ran."); });
		RefreshReminders();
	}

	private void RefreshReminders()
	{
		if (reminderList == null || reminderList.IsDisposed)
		{
			return;
		}
		reminderList.Items.Clear();
		foreach (Reminder reminder in app.Reminders.Items.Where(r => r.Status == ReminderStatus.Pending || r.Status == ReminderStatus.Due).OrderBy(r => r.Due))
		{
			reminderList.Items.Add(reminder.Status + " · " + reminder.Due.ToLocalTime().ToString("ddd HH:mm") + " · " + reminder.Message);
		}
		if (reminderList.Items.Count == 0)
		{
			reminderList.Items.Add("(no pending reminders)");
		}
	}

	private void ReminderAction(Action<Reminder> action, string done)
	{
		if (reminderList == null)
		{
			return;
		}
		int at = reminderList.SelectedIndex;
		List<Reminder> active = app.Reminders.Items.Where(r => r.Status == ReminderStatus.Pending || r.Status == ReminderStatus.Due).OrderBy(r => r.Due).ToList();
		if (at < 0 || at >= active.Count)
		{
			Status("Select a reminder first.");
			return;
		}
		Safe(() => action(active[at]));
		RefreshReminders();
		Status(done);
	}

	private void AnimationPage()
	{
		Para("Every state below is a production animation from the loaded character. Preview drives the same animator the pet uses.");
		animSearch = new TextBox { Width = 300, Font = bodyFont, Margin = new Padding(0, 0, 0, 6) };
		animSearch.TextChanged += delegate { RefreshMotions(); };
		list.Controls.Add(animSearch);
		motionList = new ListBox { Width = 700, Height = 180, Font = bodyFont };
		list.Controls.Add(motionList);
		RefreshMotions();
		Act("Preview selected", "preview play show animation", "Drive the live animator with this state.", () =>
		{
			if (motionList?.SelectedItem is string name && Enum.TryParse<Motion>(name, out Motion motion))
			{
				app.AnimationPreview = motion;
				Status("Previewing " + name + ".");
			}
		});
		Act("Stop preview", "stop preview end resume", "Return animation control to the behavior system.", () => { app.AnimationPreview = null; Status("Preview stopped."); });
		Act("Pause / resume", "pause resume freeze hold", "Freeze or resume the live pose.", () =>
		{
			app.AnimationPreviewPaused = !app.AnimationPreviewPaused;
			Status(app.AnimationPreviewPaused ? "Paused." : "Resumed.");
		});
		Act("Replay", "replay restart again", "Restart the current animation from zero.", () => { app.RestartPreview(); Status("Replayed."); });
		loopBox = new CheckBox { Text = "Loop one-shots", AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
		loopBox.CheckedChanged += delegate { app.AnimationPreviewLoop = loopBox.Checked; };
		list.Controls.Add(loopBox);
		Label speedName = new Label { Text = "Preview speed", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
		list.Controls.Add(speedName);
		speedSlider = new TrackBar { Minimum = 25, Maximum = 200, Value = 100, Width = 300, TickStyle = TickStyle.None, Margin = new Padding(0, 0, 0, 8) };
		speedSlider.ValueChanged += delegate { app.AnimationPreviewSpeed = speedSlider.Value / 100f; };
		list.Controls.Add(speedSlider);
		animReadout = new Label { AutoSize = true, MaximumSize = new Size(720, 0), Font = smallFont, Margin = new Padding(0, 4, 0, 4) };
		list.Controls.Add(animReadout);
		coverageBox = new Label { AutoSize = true, MaximumSize = new Size(720, 0), Font = smallFont, Margin = new Padding(0, 4, 0, 10) };
		list.Controls.Add(coverageBox);
		RefreshCoverage();
	}

	private void RefreshMotions()
	{
		if (motionList == null || motionList.IsDisposed)
		{
			return;
		}
		string query = (animSearch?.Text ?? "").Trim().ToLowerInvariant();
		motionList.Items.Clear();
		foreach (string name in Enum.GetNames<Motion>().Where(n => n.ToLowerInvariant().Contains(query)))
		{
			motionList.Items.Add(name);
		}
	}

	private void RefreshCoverage()
	{
		if (coverageBox == null || coverageBox.IsDisposed)
		{
			return;
		}
		HashSet<string> clips = app.Character.Animations.Keys.ToHashSet();
		List<string> lines = new List<string>();
		foreach (var group in MotionCategories)
		{
			int have = group.Value.Count(m => clips.Contains(m));
			lines.Add($"{group.Key}: {group.Value.Length} states · clips {have}/{group.Value.Length}");
		}
		int loop = app.Character.Animations.Values.Count(c => c.Loop);
		lines.Add($"Total {Enum.GetNames<Motion>().Length} states · {loop} looping, {Enum.GetNames<Motion>().Length - loop} one-shot");
		coverageBox.Text = string.Join("\n", lines);
	}

	private void AdvancedPage()
	{
		Para("Exact values behind the simple settings. Everything saves immediately and persists across restarts.");
		ChoiceRow("Timing preset", app.Config.Advanced.Preset, v => { app.Config.Advanced.ApplyPreset(v); Page("Advanced"); }, "A named bundle of the numbers below. Editing any number switches to Custom.");
		ChoiceRow("Climbing", app.Config.Advanced.ClimbFrequency, v => { app.Config.Advanced.SetClimbing(v); Page("Advanced"); }, "How often Moss may independently decide to climb a screen edge.");
		ChoiceRow("Construction", app.Config.Advanced.BuildFrequency, v => { app.Config.Advanced.SetConstruction(v); Page("Advanced"); }, "How often Moss may independently decide to build something.");
		ChoiceRow("Animation energy", app.Config.Advanced.AnimationStyle, v => { app.Config.Advanced.SetAnimationStyle(v); Page("Advanced"); }, "Overall animation energy; maps to playback speed.");
		ChoiceRow("Mischief", app.Config.Advanced.MischiefLevel, v => { app.Config.Advanced.SetMischief(v); Page("Advanced"); }, "How often playful surprises happen.");
		Heading("Timing");
		Num("Climb cooldown (s)", 0f, 7200f, 0, () => app.Config.Advanced.ClimbCooldownSec, v => app.Config.Advanced.ClimbCooldownSec = v, AdvancedSettings.Help["ClimbCooldownSec"]);
		Num("Climb interval min (s)", 30f, 3600f, 0, () => app.Config.Advanced.ClimbIntervalMinSec, v => app.Config.Advanced.ClimbIntervalMinSec = v, AdvancedSettings.Help["ClimbIntervalMinSec"]);
		Num("Climb interval max (s)", 60f, 7200f, 0, () => app.Config.Advanced.ClimbIntervalMaxSec, v => app.Config.Advanced.ClimbIntervalMaxSec = v, AdvancedSettings.Help["ClimbIntervalMaxSec"]);
		Num("Build cooldown (s)", 0f, 14400f, 0, () => app.Config.Advanced.BuildCooldownSec, v => app.Config.Advanced.BuildCooldownSec = v, AdvancedSettings.Help["BuildCooldownSec"]);
		Num("Build interval min (s)", 300f, 7200f, 0, () => app.Config.Advanced.BuildIntervalMinSec, v => app.Config.Advanced.BuildIntervalMinSec = v, AdvancedSettings.Help["BuildIntervalMinSec"]);
		Num("Build interval max (s)", 600f, 14400f, 0, () => app.Config.Advanced.BuildIntervalMaxSec, v => app.Config.Advanced.BuildIntervalMaxSec = v, AdvancedSettings.Help["BuildIntervalMaxSec"]);
		Num("Climb after idle (s)", 0f, 3600f, 0, () => app.Config.Advanced.ClimbIdleSec, v => app.Config.Advanced.ClimbIdleSec = v, AdvancedSettings.Help["ClimbIdleSec"]);
		Num("Build after idle (s)", 0f, 7200f, 0, () => app.Config.Advanced.BuildIdleSec, v => app.Config.Advanced.BuildIdleSec = v, AdvancedSettings.Help["BuildIdleSec"]);
		Num("Structure lifetime (min)", 5f, 180f, 0, () => app.Config.Advanced.StructureLifetimeMin, v => app.Config.Advanced.StructureLifetimeMin = v, AdvancedSettings.Help["StructureLifetimeMin"]);
		Num("Rare-event probability", 0f, 1f, 3, () => app.Config.Advanced.RareEventProbability, v => app.Config.Advanced.RareEventProbability = v, AdvancedSettings.Help["RareEventProbability"]);
		Num("Idle decision min (s)", 1f, 30f, 1, () => app.Config.Advanced.WanderDecisionMinSec, v => app.Config.Advanced.WanderDecisionMinSec = v, AdvancedSettings.Help["WanderDecisionMinSec"]);
		Num("Idle decision max (s)", 2f, 60f, 1, () => app.Config.Advanced.WanderDecisionMaxSec, v => app.Config.Advanced.WanderDecisionMaxSec = v, AdvancedSettings.Help["WanderDecisionMaxSec"]);
		Act("Reset timing", "reset timing defaults", "Restore timing defaults.", () => { TimingGroup(new AdvancedSettings()); Page("Advanced"); });
		Heading("Physics");
		Num("Gravity scale", 0.2f, 3f, 2, () => app.Config.Advanced.GravityScale, v => app.Config.Advanced.GravityScale = v, AdvancedSettings.Help["GravityScale"]);
		Num("Friction scale", 0.2f, 3f, 2, () => app.Config.Advanced.FrictionScale, v => app.Config.Advanced.FrictionScale = v, AdvancedSettings.Help["FrictionScale"]);
		Num("Bounce scale", 0f, 1.5f, 2, () => app.Config.Advanced.BounceScale, v => app.Config.Advanced.BounceScale = v, AdvancedSettings.Help["BounceScale"]);
		Num("Jump velocity", 200f, 900f, 0, () => app.Config.Advanced.JumpVelocity, v => app.Config.Advanced.JumpVelocity = v, AdvancedSettings.Help["JumpVelocity"]);
		Num("Throw power", 0.3f, 2.5f, 2, () => app.Config.Advanced.ThrowPower, v => app.Config.Advanced.ThrowPower = v, AdvancedSettings.Help["ThrowPower"]);
		Num("Grab sensitivity", 0.3f, 3f, 2, () => app.Config.Advanced.GrabSensitivity, v => app.Config.Advanced.GrabSensitivity = v, AdvancedSettings.Help["GrabSensitivity"]);
		Num("Object grip strength", 0.3f, 3f, 2, () => app.Config.Advanced.ObjectGripStrength, v => app.Config.Advanced.ObjectGripStrength = v, AdvancedSettings.Help["ObjectGripStrength"]);
		Num("Climb speed", 0.3f, 3f, 2, () => app.Config.Advanced.ClimbSpeed, v => app.Config.Advanced.ClimbSpeed = v, AdvancedSettings.Help["ClimbSpeed"]);
		Num("Physics step (s)", 1f / 240f, 1f / 30f, 4, () => app.Config.Advanced.PhysicsStepSec, v => app.Config.Advanced.PhysicsStepSec = v, AdvancedSettings.Help["PhysicsStepSec"]);
		Act("Reset physics", "reset physics defaults", "Restore physics defaults.", () => { PhysicsGroup(new AdvancedSettings()); Page("Advanced"); });
		Heading("Behavior");
		Num("Petting sensitivity", 0.3f, 3f, 2, () => app.Config.Advanced.PettingSensitivity, v => app.Config.Advanced.PettingSensitivity = v, AdvancedSettings.Help["PettingSensitivity"]);
		Num("Music energy threshold", 0f, 0.8f, 2, () => app.Config.Advanced.MusicEnergyThreshold, v => app.Config.Advanced.MusicEnergyThreshold = v, AdvancedSettings.Help["MusicEnergyThreshold"]);
		Num("Rhythm sensitivity", 0.3f, 3f, 2, () => app.Config.Advanced.RhythmSensitivity, v => app.Config.Advanced.RhythmSensitivity = v, AdvancedSettings.Help["RhythmSensitivity"]);
		Num("Context reaction cooldown (s)", 0.5f, 30f, 1, () => app.Config.Advanced.ContextReactionCooldownSec, v => app.Config.Advanced.ContextReactionCooldownSec = v, AdvancedSettings.Help["ContextReactionCooldownSec"]);
		Num("Cursor attract radius", 50f, 600f, 0, () => app.Config.Advanced.CursorAttractRadius, v => app.Config.Advanced.CursorAttractRadius = v, AdvancedSettings.Help["CursorAttractRadius"]);
		Num("Window investigate chance", 0f, 1f, 2, () => app.Config.Advanced.WindowInvestigateProbability, v => app.Config.Advanced.WindowInvestigateProbability = v, AdvancedSettings.Help["WindowInvestigateProbability"]);
		Num("Sleep threshold", 0.05f, 0.8f, 2, () => app.Config.Advanced.SleepThreshold, v => app.Config.Advanced.SleepThreshold = v, AdvancedSettings.Help["SleepThreshold"]);
		Num("Energy recovery rate", 0f, 3f, 2, () => app.Config.Advanced.EnergyRecoveryRate, v => app.Config.Advanced.EnergyRecoveryRate = v, AdvancedSettings.Help["EnergyRecoveryRate"]);
		Num("Mood recovery rate", 0f, 3f, 2, () => app.Config.Advanced.MoodRecoveryRate, v => app.Config.Advanced.MoodRecoveryRate = v, AdvancedSettings.Help["MoodRecoveryRate"]);
		Num("Surprise interval min (s)", 30f, 600f, 0, () => app.Config.Advanced.SurpriseIntervalMinSec, v => app.Config.Advanced.SurpriseIntervalMinSec = v, AdvancedSettings.Help["SurpriseIntervalMinSec"]);
		Num("Surprise interval max (s)", 60f, 1200f, 0, () => app.Config.Advanced.SurpriseIntervalMaxSec, v => app.Config.Advanced.SurpriseIntervalMaxSec = v, AdvancedSettings.Help["SurpriseIntervalMaxSec"]);
		Num("Play score boost", 0.5f, 2f, 2, () => app.Config.Advanced.PlayScoreBoost, v => app.Config.Advanced.PlayScoreBoost = v, AdvancedSettings.Help["PlayScoreBoost"]);
		Act("Reset behavior", "reset behavior defaults", "Restore behavior defaults.", () => { BehaviorGroup(new AdvancedSettings()); Page("Advanced"); });
		Heading("Animation");
		Num("Animation speed", 0.3f, 2.5f, 2, () => app.Config.Advanced.AnimationSpeed, v => app.Config.Advanced.AnimationSpeed = v, AdvancedSettings.Help["AnimationSpeed"]);
		Num("Blend rate", 2f, 30f, 1, () => app.Config.Advanced.BlendRate, v => app.Config.Advanced.BlendRate = v, AdvancedSettings.Help["BlendRate"]);
		Act("Reset animation", "reset animation defaults", "Restore animation defaults.", () => { var d = new AdvancedSettings(); app.Config.Advanced.AnimationSpeed = d.AnimationSpeed; app.Config.Advanced.BlendRate = d.BlendRate; app.Save(); Page("Advanced"); });
		Heading("Danger zone");
		Act("Reset ALL advanced settings", "reset all advanced defaults factory", "Restore every advanced value. Normal settings and notes are untouched.", () =>
		{
			if (MessageBox.Show(this, "Reset every advanced value to defaults?", "Feature Lab", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
			{
				app.Config.Advanced = new AdvancedSettings();
				app.Save();
				Page("Advanced");
			}
		});
	}

	private void TimingGroup(AdvancedSettings d)
	{
		AdvancedSettings a = app.Config.Advanced;
		a.ClimbCooldownSec = d.ClimbCooldownSec;
		a.ClimbIntervalMinSec = d.ClimbIntervalMinSec;
		a.ClimbIntervalMaxSec = d.ClimbIntervalMaxSec;
		a.BuildCooldownSec = d.BuildCooldownSec;
		a.BuildIntervalMinSec = d.BuildIntervalMinSec;
		a.BuildIntervalMaxSec = d.BuildIntervalMaxSec;
		a.ClimbIdleSec = d.ClimbIdleSec;
		a.BuildIdleSec = d.BuildIdleSec;
		a.StructureLifetimeMin = d.StructureLifetimeMin;
		a.RareEventProbability = d.RareEventProbability;
		a.WanderDecisionMinSec = d.WanderDecisionMinSec;
		a.WanderDecisionMaxSec = d.WanderDecisionMaxSec;
		a.Preset = TimingPreset.Custom;
		app.Save();
	}

	private void PhysicsGroup(AdvancedSettings d)
	{
		AdvancedSettings a = app.Config.Advanced;
		a.GravityScale = d.GravityScale;
		a.FrictionScale = d.FrictionScale;
		a.BounceScale = d.BounceScale;
		a.JumpVelocity = d.JumpVelocity;
		a.ThrowPower = d.ThrowPower;
		a.GrabSensitivity = d.GrabSensitivity;
		a.ObjectGripStrength = d.ObjectGripStrength;
		a.ClimbSpeed = d.ClimbSpeed;
		a.PhysicsStepSec = d.PhysicsStepSec;
		a.Preset = TimingPreset.Custom;
		app.Save();
	}

	private void BehaviorGroup(AdvancedSettings d)
	{
		AdvancedSettings a = app.Config.Advanced;
		a.PettingSensitivity = d.PettingSensitivity;
		a.MusicEnergyThreshold = d.MusicEnergyThreshold;
		a.RhythmSensitivity = d.RhythmSensitivity;
		a.ContextReactionCooldownSec = d.ContextReactionCooldownSec;
		a.CursorAttractRadius = d.CursorAttractRadius;
		a.WindowInvestigateProbability = d.WindowInvestigateProbability;
		a.SleepThreshold = d.SleepThreshold;
		a.EnergyRecoveryRate = d.EnergyRecoveryRate;
		a.MoodRecoveryRate = d.MoodRecoveryRate;
		a.SurpriseIntervalMinSec = d.SurpriseIntervalMinSec;
		a.SurpriseIntervalMaxSec = d.SurpriseIntervalMaxSec;
		a.PlayScoreBoost = d.PlayScoreBoost;
		a.Preset = TimingPreset.Custom;
		app.Save();
	}

	private void EventsPage()
	{
		Para("Live production state, refreshed twice a second. Same values the engine uses.");
		stateBlock = new Label { AutoSize = true, MaximumSize = new Size(720, 0), Font = smallFont, Margin = new Padding(0, 2, 0, 10) };
		list.Controls.Add(stateBlock);
		Act("Open world inspector", "world inspector debug map", "Open the terrain, edge, structure and route view.", () => { new WorldDebugForm(app).Show(this); });
		Para("Event stream — the shared internal event log, newest last, bounded to the last 100:");
		eventList = new ListBox { Width = 700, Height = 220, Font = smallFont };
		list.Controls.Add(eventList);
		UpdateLive();
	}

	private void UpdateLive()
	{
		if (IsDisposed)
		{
			return;
		}
		Creature pet = app.Creature;
		string support = app.SupportDescription();
		string carried = pet.HandTarget.HasValue ? "holding" : "empty-handed";
		statusBar.Text = $"{pet.Activity} · {pet.Motion} · {support} · {app.Metrics.Fps:0} fps · {app.VisibilityStatus}";
		if (stateBlock != null && !stateBlock.IsDisposed)
		{
			stateBlock.Text = $"Behavior {pet.Activity}\nEmotion mood {pet.Mood:0.00} energy {pet.Energy:0.00} attention {pet.Attention:0.00} curiosity {pet.Curiosity:0.00}\nAnimation {app.AnimatorStatus()}\nTarget x {pet.TargetX:0} · support {support} · velocity {pet.Velocity}\nHands {carried} · music {(app.Media.State.Playing ? "playing" : " quiet")} · climb {app.LabClimbStatus()}\nBuild {app.LabBuildStatus()}\nPosition {pet.Position} · surfaces {app.Observer.World.Surfaces.Count} · events {app.Events.Recent.Count}";
		}
		if (eventList != null && !eventList.IsDisposed)
		{
			eventList.BeginUpdate();
			eventList.Items.Clear();
			foreach (WorldEvent entry in app.Events.Recent.TakeLast(60))
			{
				eventList.Items.Add($"{entry.Time:0.0}s  {entry.Kind}");
			}
			if (eventList.Items.Count > 0)
			{
				eventList.TopIndex = eventList.Items.Count - 1;
			}
			eventList.EndUpdate();
		}
		if (animReadout != null && !animReadout.IsDisposed)
		{
			animReadout.Text = app.AnimatorStatus();
		}
		if (musicStatus != null && !musicStatus.IsDisposed)
		{
			musicStatus.Text = app.Media.Status + "\n" + app.Media.RhythmStatus + "\nPulse " + app.Media.Pulse.ToString("0.00");
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			live.Dispose();
			tips.Dispose();
			bodyFont.Dispose();
			headingFont.Dispose();
			smallFont.Dispose();
		}
		base.Dispose(disposing);
	}
}
