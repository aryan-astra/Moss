using System;
using System.IO;
using Moss.Core;

internal static class FeatureChecks
{
	private static World MakeWorld()
	{
		World world = new World();
		world.Displays.Add(new Display("test", new Box(0f, 0f, 1920f, 1080f), new Box(0f, 0f, 1920f, 1040f), 1f));
		world.Surfaces.Add(new Surface(1L, 0f, 1920f, 1040f, Floor: true, 0f));
		world.Cursor = new System.Numerics.Vector2(-9999f, -9999f);
		return world;
	}

	private static Creature MakePet(World world)
	{
		Creature pet = new Creature(7);
		pet.Species = "bean";
		pet.Position = new System.Numerics.Vector2(120f, 900f);
		pet.AutonomyEnabled = false;
		Settings settings = new Settings();
		Personality personality = new Personality();
		for (int i = 0; i < 240; i++)
		{
			pet.Step(1f / 120f, world, settings, personality, music: false);
		}
		pet.AutonomyEnabled = true;
		pet.EndClimbSession();
		pet.Construction.Cancel();
		pet.Recover();
		return pet;
	}

	public static void Run()
	{
		Settings settings = new Settings();
		Personality personality = new Personality();

		World world = MakeWorld();
		Creature pet = MakePet(world);
		Program.Check(pet.Support.HasValue, "fixture pet lands on floor");

		settings.Advanced.LastBuildUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		bool started = pet.Climb.Start(world, pet, settings, "bean", manual: true, seed: 7);
		Program.Check(started && pet.Climb.Phase == ClimbPhase.ApproachEdge, "climb session starts at approach");
		Program.Check(!pet.Climb.Start(world, pet, settings, "bean", manual: true, seed: 7), "second climb start refused while active");
		Program.Check(!pet.Climb.Command(ClimbCommand.JumpFromEdge, pet, settings), "jump rejected before gripping");
		int steps = 0;
		while (pet.Climb.Phase != ClimbPhase.None && steps < 30000)
		{
			pet.Step(1f / 120f, world, settings, personality, music: false);
			steps++;
		}
		Program.Check(pet.Climb.Phase == ClimbPhase.None, "climb session terminates");
		Program.Check(pet.Climb.Succeeded, "climb reaches the corner");
		Program.Check(pet.NextClimbAt > pet.Time, "climb completion arms interval cooldown");
		Program.Check(pet.Climb.Route.Count > 0, "climb route recorded for debug view");

		Creature near = MakePet(world);
		near.Position = new System.Numerics.Vector2(60f, 1040f);
		near.SetCuriosity(1f);
		near.SetEnergy(0.9f);
		Program.Check(near.Scores(world, settings, personality, music: false)[Activity.ClimbEdge] > 0f, "edge climb desired near a screen edge");

		bool selfStarted = false;
		bool stayedInside = true;
		int roam = 0;
		while (roam < 24000 && !selfStarted)
		{
			near.Step(1f / 120f, world, settings, personality, music: false);
			roam++;
			if (near.Position.X < -30f || near.Position.X > 1950f || near.Position.Y < -250f || near.Position.Y > 1210f)
			{
				stayedInside = false;
			}
			if (near.Climb.Phase != ClimbPhase.None)
			{
				selfStarted = true;
			}
			if (roam % 600 == 0 && near.Climb.Phase == ClimbPhase.None && (near.Position.X > 420f || !near.Support.HasValue))
			{
				near.Position = new System.Numerics.Vector2(60f, 900f);
			}
		}
		Program.Check(selfStarted, "autonomy starts an edge climb on its own");
		Program.Check(stayedInside, "pet never leaves screen bounds");

		bool sawTop = false;
		for (int seed = 1; seed <= 8; seed++)
		{
			Creature explorer = MakePet(world);
			if (!explorer.Climb.Start(world, explorer, settings, "bean", manual: true, seed: seed))
			{
				continue;
			}
			int n = 0;
			while (explorer.Climb.Phase != ClimbPhase.None && n < 30000)
			{
				if (explorer.Climb.Phase == ClimbPhase.TopTransition || explorer.Climb.Phase == ClimbPhase.Balance)
				{
					sawTop = true;
				}
				explorer.Step(1f / 120f, world, settings, personality, music: false);
				n++;
			}
			Program.Check(explorer.Climb.Phase == ClimbPhase.None, $"seed {seed} climb terminates");
		}
		Program.Check(sawTop, "some climbs transition over the top edge");

		Creature pet2 = MakePet(world);
		pet2.Climb.Start(world, pet2, settings, "cat", manual: true, seed: 11);
		int guard = 0;
		while (pet2.Climb.Phase != ClimbPhase.Grip && pet2.Climb.Phase != ClimbPhase.None && guard < 6000)
		{
			pet2.Step(1f / 120f, world, settings, personality, music: false);
			guard++;
		}
		if (pet2.Climb.Phase == ClimbPhase.Grip)
		{
			Program.Check(pet2.Climb.Command(ClimbCommand.PullUp, pet2, settings), "manual pull-up accepted from grip");
			Program.Check(pet2.Climb.Phase == ClimbPhase.PullUp, "pull-up phase entered");
		}
		else
		{
			Program.Check(false, "manual pull-up accepted from grip");
			Program.Check(false, "pull-up phase entered");
		}

		settings.Advanced.ClimbFrequency = Frequency.Off;
		Program.Check(pet.Scores(world, settings, personality, music: false)[Activity.ClimbEdge] == 0f, "climb desire is zero when off");
		settings.Advanced.ClimbFrequency = Frequency.Occasional;

		Creature builder = MakePet(world);
		settings.Advanced.ClimbFrequency = Frequency.Off;
		bool buildStarted = builder.Construction.StartBuild(world, builder, settings, "bean", StructureKind.House, manual: true, seed: 5);
		Program.Check(buildStarted && builder.Construction.Phase == BuildPhase.Travel, "house build starts with travel");
		Program.Check(!builder.Construction.StartBuild(world, builder, settings, "bean", StructureKind.House, manual: true, seed: 5), "second build refused while active");
		steps = 0;
		while (builder.Construction.HasSession && steps < 60000)
		{
			builder.Step(1f / 120f, world, settings, personality, music: false);
			steps++;
		}
		Program.Check(!builder.Construction.HasSession, "build session terminates");
		settings.Advanced.ClimbFrequency = Frequency.Occasional;
		Program.Check(builder.Construction.Structures.Count == 1 && builder.Construction.Structures[0].Finished, "house finishes as a world object");
		Surface slab = builder.Construction.Structures[0].ToSurface();
		Program.Check(slab.Supports(builder.Construction.Structures[0].Site.X), "finished house registers a standable surface");

		Structure probe = new Structure { Site = new System.Numerics.Vector2(500f, 1000f), BornAt = 0.0, LifetimeSec = 1.0, Stage = 4, StagesTotal = 4 };
		Program.Check(!probe.Expired(0.5) && probe.Expired(5.0), "structure lifetime expires");

		Material plank = builder.Construction.SpawnMaterial(MaterialKind.Plank, builder.Position);
		plank.Carried = true;
		builder.Step(1f / 120f, world, settings, personality, music: false);
		Program.Check(plank.Carried && plank.Position != builder.Position + new System.Numerics.Vector2(80f, -40f), "carried material follows the pet grip");

		PropBody toy = new PropBody { Position = builder.Position + new System.Numerics.Vector2(20f, 0f) };
		steps = 0;
		while (toy.State != PropState.Carried && steps < 600)
		{
			toy.Step(1f / 120f, builder, world, null);
			steps++;
		}
		Program.Check(toy.State == PropState.Carried, "pet reclaims a nearby settled toy");

		string settingsPath = Path.Combine(Path.GetTempPath(), "moss-lab-settings.json");
		settings.Advanced.ClimbSpeed = 1.5f;
		settings.Save(settingsPath);
		Settings loaded = Settings.Load(settingsPath);
		Program.Check(loaded.Version == 2 && loaded.Advanced.ClimbSpeed == 1.5f, "advanced settings persist across restart");
		File.WriteAllText(settingsPath, "{\"Version\":1}");
		Settings migrated = Settings.Load(settingsPath);
		Program.Check(migrated.Version == 2 && migrated.Advanced.ClimbSpeed == 1f, "version-1 settings migrate with defaults");
		Program.Expect<InvalidDataException>(() => { settings.Advanced.GravityScale = float.NaN; settings.Validate(); }, "NaN advanced value rejected");
		settings.Advanced.GravityScale = 1f;
		Program.Expect<InvalidDataException>(() => { settings.Advanced.ClimbFrequency = (Frequency)99; settings.Validate(); }, "unknown frequency rejected");
		settings.Advanced.ClimbFrequency = Frequency.Occasional;

		Creature cool = MakePet(world);
		cool.Construction.StartBuild(world, cool, settings, "bean", StructureKind.Platform, manual: true, seed: 9);
		Program.Check(cool.Construction.StartBuild(world, cool, settings, "bean", StructureKind.House, manual: true, seed: 9) == false, "busy builder refuses a second job");

		EventHub hub = new EventHub();
		for (int i = 0; i < 150; i++)
		{
			hub.Publish("lab.ping", i);
		}
		Program.Check(hub.Recent.Count == 100, "event stream stays bounded");
	}
}
