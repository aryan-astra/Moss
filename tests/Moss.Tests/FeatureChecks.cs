using System;
using System.IO;
using System.Linq;
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

	private static World MakeLedgeWorld()
	{
		World world = MakeWorld();
		world.Surfaces.Add(new Surface(2L, 800f, 1100f, 700f, Floor: false, 800f));
		return world;
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
		float loX = 99999f, hiX = -99999f, loY = 99999f, hiY = -99999f;
		while (pet.Climb.Phase != ClimbPhase.None && steps < 30000)
		{
			pet.Step(1f / 120f, world, settings, personality, music: false);
			steps++;
			loX = Math.Min(loX, pet.Position.X);
			hiX = Math.Max(hiX, pet.Position.X);
			loY = Math.Min(loY, pet.Position.Y);
			hiY = Math.Max(hiY, pet.Position.Y);
		}
		Program.Check(pet.Climb.Phase == ClimbPhase.None, "climb session terminates");
		Program.Check(loX >= 15f && hiX <= 1890f && loY >= 135f && hiY <= 1230f, "climb stays on the visible screen");
		Program.Check(pet.Climb.Succeeded, "climb reaches the corner");
		Program.Check(pet.NextClimbAt > pet.Time, "climb completion arms interval cooldown");
		Program.Check(pet.Climb.Route.Count > 0, "climb route recorded for debug view");

		Creature fresh = MakePet(world);
		fresh.Position = new System.Numerics.Vector2(60f, 900f);
		fresh.AutonomyEnabled = false;
		for (int i = 0; i < 15000; i++)
		{
			fresh.Step(1f / 120f, world, settings, personality, music: false);
		}
		settings.Advanced.LastBuildUnix = 0;
		float firstScore = fresh.Scores(world, settings, personality, music: false)[Activity.Build];
		settings.Advanced.LastBuildUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		float laterScore = fresh.Scores(world, settings, personality, music: false)[Activity.Build];
		Program.Check(firstScore > 0f && firstScore > laterScore, "first build desired more than later ones");
		settings.Advanced.LastBuildUnix = 0;

		Creature busy = MakePet(world);
		busy.Position = new System.Numerics.Vector2(60f, 900f);
		busy.Pet();
		Program.Check(busy.IdleSeconds < 1.0, "touching the pet resets idle time");
		Program.Check(busy.Scores(world, settings, personality, music: false)[Activity.ClimbEdge] == 0f, "fresh touch blocks autonomous climbing");
		for (int i = 0; i < 240; i++)
		{
			busy.Step(1f / 120f, world, settings, personality, music: false);
		}
		Program.Check(busy.IdleSeconds > 1f, "quiet time accumulates");
		Program.Check(settings.Advanced.ClimbIdleSec == 60f && settings.Advanced.BuildIdleSec == 120f, "idle defaults are one and two minutes");

		Creature near = MakePet(world);
		near.Position = new System.Numerics.Vector2(60f, 900f);
		near.AutonomyEnabled = false;
		for (int i = 0; i < 7500; i++)
		{
			near.Step(1f / 120f, world, settings, personality, music: false);
		}
		near.SetCuriosity(1f);
		near.SetEnergy(0.9f);
		Program.Check(near.Scores(world, settings, personality, music: false)[Activity.ClimbEdge] > 0f, "edge climb desired near a screen edge");
		near.AutonomyEnabled = true;

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

		string oldPack = Path.Combine(Path.GetTempPath(), "moss-old-pack.json");
		string mossText = File.ReadAllText(Path.Combine(Program.RepoRoot, "characters", "moss", "character.json")).Replace("\r\n", "\n");
		foreach (string extra in new[] { "Hanging", "Carrying", "Hammering", "Peeking", "Balancing" })
		{
			string marker = ",\n    \"" + extra + "\": {";
			int at = mossText.IndexOf(marker);
			int open = mossText.IndexOf("{", at);
			int depth = 0;
			int end = open;
			for (int i = open; i < mossText.Length; i++)
			{
				if (mossText[i] == '{') depth++;
				else if (mossText[i] == '}')
				{
					depth--;
					if (depth == 0) { end = i + 1; break; }
				}
			}
			mossText = mossText.Remove(at, end - at);
		}
		File.WriteAllText(oldPack, mossText);
		Program.Expect<InvalidDataException>(() => Character.Load(oldPack), "old pack without new clips rejected");
		Character migratedPack = System.Text.Json.JsonSerializer.Deserialize<Character>(File.ReadAllText(oldPack), Json.Options) ?? throw new InvalidOperationException("test setup failed");
		migratedPack.FillMissingClips(Character.Load(Path.Combine(Program.RepoRoot, "characters", "moss", "character.json")));
		migratedPack.Validate();
		Program.Check(migratedPack.Animations.Count == 28, "migrated pack validates with full motion set");
		Program.Check(Character.MigrationDefaults().Animations.Count == 5, "migration defaults cover the new motions");

		World ledges = MakeLedgeWorld();
		Creature high = MakePet(ledges);
		high.Position = new System.Numerics.Vector2(950f, 900f);
		Program.Check(ConstructionWorld.TryFindSite(ledges, high.Position, 1f, "bean", out System.Numerics.Vector2 ledgeSite, out Surface? ledgeGround) && ledgeSite.Y < 1000f, "elevated ledge preferred for building");
		Program.Check(high.Construction.StartBuild(ledges, high, settings, "bean", StructureKind.House, manual: true, seed: 21), "ledge build starts");
		bool perched = false;
		steps = 0;
		while (high.Construction.HasSession && steps < 40000)
		{
			high.Step(1f / 120f, ledges, settings, personality, music: false);
			steps++;
			if (high.Support == 2L)
			{
				perched = true;
			}
		}
		Program.Check(perched, "pet climbs to the ledge site");
		Program.Check(high.Construction.Structures.Count == 1 && high.Construction.Structures[0].Finished, "ledge house finishes");
		Structure small = high.Construction.Structures[0];
		Program.Check(small.Width <= 100f, "house is small");
		Program.Check(builder.Construction.StartBuild(world, builder, settings, "bean", StructureKind.House, manual: true, seed: 77, charWidth: 100f, charHeight: 90f), "big-species build starts");
		Program.Check(builder.Construction.Active != null && builder.Construction.Active.Width > 140f && builder.Construction.Active.Width < 150f, "house scales with character size");
		builder.Construction.Cancel();
		Program.Check(high.Construction.Materials.Any(m => m.Kind == MaterialKind.Hammer && m.Placed), "hammer kept visible in the house");
		Program.Check(high.Construction.Materials.Any(m => m.Kind == MaterialKind.Nail && m.Placed), "nail kept visible in the house");
		small.Poke(1.0f, high.Time);
		Program.Check(small.ShakeNow(high.Time) > 0f, "poked house shakes");
		high.Construction.HouseHit(high, small);
		Program.Check(high.Mood <= 0.3f, "hit makes the pet afraid");
		high.Position = new System.Numerics.Vector2(small.Site.X + 400f, 1040f);
		steps = 0;
		while (steps < 3000 && !(high.Activity == Activity.Sit && System.Math.Abs(high.Position.X - small.Site.X) < 120f))
		{
			high.Step(1f / 120f, ledges, settings, personality, music: false);
			steps++;
		}
		Program.Check(high.Activity == Activity.Sit, "frightened pet shelters at its house");

		Creature planner = MakePet(ledges);
		Program.Check(planner.Construction.StartBuildAt(ledges, planner, settings, "bean", StructureKind.Platform, new System.Numerics.Vector2(900f, 700f), manual: true, seed: 3), "build-here accepts a real ledge point");
		planner.Construction.Cancel();
		Program.Check(!planner.Construction.StartBuildAt(ledges, planner, settings, "bean", StructureKind.Platform, new System.Numerics.Vector2(900f, 300f), manual: true, seed: 3), "build-here rejects mid-air");
		Program.Check(high.Construction.StartBuild(ledges, high, settings, "bean", StructureKind.Platform, manual: true, seed: 41), "second build starts");
		Program.Check(high.Construction.Structures.Count == 0, "old house demolished for the new one");
		high.Construction.Cancel();
	}
}
