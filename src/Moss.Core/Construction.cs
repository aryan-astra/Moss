using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Moss.Core;

// Wooden-house construction. The creature picks a real site on a work-area
// floor or window ledge, travels there, gathers carried planks, places them
// in stages and hammers nails until a persistent Structure exists in the
// world model. Finished structures register a standable Surface, so the
// existing landing/support physics applies with no special cases.
public enum BuildPhase
{
	None,
	ChooseSite,
	Travel,
	Investigate,
	Gather,
	Carry,
	Place,
	Hammer,
	Inspect,
	Celebrate,
	Done
}

public enum MaterialKind
{
	Plank,
	Stick,
	Nail,
	Hammer,
	Panel
}

public enum StructureKind
{
	House,
	Platform,
	StickStructure
}

public sealed class Material
{
	private static long nextId = -1000;

	public long Id { get; } = nextId -= 7;

	public MaterialKind Kind { get; set; }

	public Vector2 Position { get; set; }

	public Vector2 Velocity { get; set; }

	public bool Carried { get; set; }

	public bool Placed { get; set; }

	public Vector2 PlaceAt { get; set; }

	public void Step(float dt, World w, float scale)
	{
		if (Carried || Placed)
		{
			Velocity = Vector2.Zero;
			return;
		}
		Velocity = new Vector2(Velocity.X * MathF.Exp(0f - dt * 2f), Velocity.Y + 1300f * scale * dt);
		Position += Velocity * dt;
		Display? display = w.Nearest(Position);
		if (display != null && display.Bounds.DistanceSquared(Position) > 90000f * scale * scale)
		{
			Position = display.Work.Clamp(Position, 15f * scale);
			Velocity = Vector2.Zero;
		}
		Vector2 before = Position - Velocity * dt;
		foreach (Surface surface in w.Surfaces)
		{
			if (Velocity.Y >= 0f && before.Y <= surface.Y + 0.5f && Position.Y >= surface.Y && surface.Supports(Position.X) && Position.Y - surface.Y < 60f * scale)
			{
				Position = new Vector2(Position.X, surface.Y);
				Velocity = new Vector2(Velocity.X * 0.6f, 0f);
				break;
			}
		}
	}
}

public sealed class Structure
{
	public StructureKind Kind { get; set; }

	public Vector2 Site { get; set; }

	public float Width { get; set; } = 120f;

	public float Height { get; set; } = 90f;

	public int Stage { get; set; }

	public int StagesTotal { get; set; } = 4;

	public long SurfaceId { get; set; }

	public double BornAt { get; set; }

	public double LifetimeSec { get; set; } = 1800.0;

	public string OwnerSpecies { get; set; } = "bean";

	public bool Finished => Stage >= StagesTotal;

	public float Shake { get; private set; }

	public double ShakeUntil { get; private set; }

	public void Poke(float strength, double now)
	{
		Shake = Math.Clamp(strength, 0f, 1f);
		ShakeUntil = now + 0.9;
	}

	public float ShakeNow(double now)
	{
		if (now >= ShakeUntil)
		{
			return 0f;
		}
		return Shake * (float)(ShakeUntil - now);
	}

	public bool Expired(double now)
	{
		return Finished && now - BornAt > LifetimeSec;
	}

	public Surface ToSurface()
	{
		return new Surface(SurfaceId, Site.X - Width / 2f, Site.X + Width / 2f, Site.Y, Floor: false, Site.X);
	}

	public Vector2 Slot(int index)
	{
		float spread = Width * 0.7f;
		float x = Site.X + (StagesTotal <= 1 ? 0f : (index / (float)(StagesTotal - 1) - 0.5f) * spread);
		return new Vector2(x, Site.Y - 6f);
	}
}

public sealed class ConstructionWorld
{
	private float sessionTime;

	private float totalTime;

	private int nailsThisStage;

	private int swings;

	private float swingPhase;

	private Vector2 nailPoint;

	private int gatherIndex;

	private readonly List<MaterialKind> bill = new List<MaterialKind>();

	public List<Material> Materials { get; } = new List<Material>();

	public List<Structure> Structures { get; } = new List<Structure>();

	public BuildPhase Phase { get; private set; }

	public Structure? Active { get; private set; }

	public bool Manual { get; private set; }

	public string StyleNote { get; private set; } = "";

	public int HammerSwings => swings;

	public float SwingAngle => swingPhase;

	public Vector2 NailPoint => nailPoint;

	public bool HasSession => Phase != BuildPhase.None && Phase != BuildPhase.Done;

	public static string StyleFor(string species, StructureKind kind)
	{		return (species, kind) switch
		{
			("cat", _) => "cozy shelter",
			("dog", _) => "playful fort",
			("bird", _) => "high perch",
			("octopus", _) => "multi-part frame",
			("rabbit", _) => "snug hut",
			("penguin", _) => "compact platform",
			(_, StructureKind.Platform) => "lookout platform",
			(_, StructureKind.StickStructure) => "stick lattice",
			_ => "timber hut",
		};
	}

	public static bool TryFindSite(World w, Vector2 pos, float scale, string species, out Vector2 site)
	{
		bool found = TryFindSite(w, pos, scale, species, out site, out _);
		return found;
	}

	public static bool TryFindSite(World w, Vector2 pos, float scale, string species, out Vector2 site, out Surface? ground)
	{
		site = pos;
		ground = null;
		Display? display = w.Nearest(pos);
		if (display == null)
		{
			return false;
		}
		float need = (species == "octopus" ? 150f : 110f) * scale;
		float headroom = (w.Displays.Count > 0 ? w.Displays.Min((Display d) => d.Bounds.Y) : float.MinValue) + 150f * scale;
		var elevated = w.Surfaces.Where(s => !s.Floor && s.Right - s.Left > need && s.Y > headroom && s.Y < pos.Y - 60f * scale && s.Y > pos.Y - 480f * scale).ToList();
		var floors = w.Surfaces.Where(s => s.Floor && s.Right - s.Left > need).ToList();
		var candidates = elevated.Count > 0 ? elevated : floors;
		Surface? best = null;
		float bestScore = float.MaxValue;
		foreach (Surface surface in candidates)
		{
			float cx = Math.Clamp(pos.X, surface.Left + need / 2f, surface.Right - need / 2f);
			float score = Math.Abs(cx - pos.X) + Math.Abs(surface.Y - pos.Y) * 0.4f;
			if (score < bestScore)
			{
				bestScore = score;
				best = surface;
				site = new Vector2(cx, surface.Y);
			}
		}
		ground = best;
		return best != null;
	}

	public static float StructureWidth(StructureKind kind, float charWidth, string species)
	{
		float logical = kind == StructureKind.Platform ? charWidth * 2.2f : kind == StructureKind.StickStructure ? charWidth * 1.2f : charWidth * 1.45f;
		float floor = kind == StructureKind.Platform ? 120f : kind == StructureKind.StickStructure ? 80f : 88f;
		return Math.Max(logical, floor) * (species == "octopus" ? 1.25f : 1f);
	}

	public static float StructureHeight(StructureKind kind, float charHeight)
	{
		float logical = kind == StructureKind.Platform ? 22f : kind == StructureKind.StickStructure ? charHeight * 0.9f : charHeight;
		float floor = kind == StructureKind.Platform ? 22f : kind == StructureKind.StickStructure ? 56f : 64f;
		return Math.Max(logical, floor);
	}

	public bool StartBuild(World w, Creature c, Settings s, string species, StructureKind kind, bool manual, int seed = 0, float charWidth = 66f, float charHeight = 74f)
	{
		if (HasSession)
		{
			return false;
		}
		if (!TryFindSite(w, c.Position, c.Scale, species, out Vector2 site, out Surface? ground) || ground == null)
		{
			return false;
		}
		return BeginJob(w, c, s, species, kind, site, ground, manual, seed, charWidth, charHeight);
	}

	public bool StartBuildAt(World w, Creature c, Settings s, string species, StructureKind kind, Vector2 site, bool manual, int seed = 0, float charWidth = 66f, float charHeight = 74f)
	{
		if (HasSession)
		{
			return false;
		}
		float half = (StructureWidth(kind, charWidth, species) + 20f) / 2f * c.Scale;
		float topLimit = (w.Displays.Count > 0 ? w.Displays.Min((Display d) => d.Bounds.Y) : float.MinValue) + 150f * c.Scale;
		Surface? ground = null;
		foreach (Surface surface in w.Surfaces)
		{
			if (surface.Y > topLimit && Math.Abs(surface.Y - site.Y) <= 80f * c.Scale && surface.Left - 4f <= site.X - half && site.X + half <= surface.Right + 4f)
			{
				ground = surface;
				break;
			}
		}
		if (ground == null)
		{
			return false;
		}
		Vector2 snapped = new Vector2(Math.Clamp(site.X, ground.Left + half, ground.Right - half), ground.Y);
		return BeginJob(w, c, s, species, kind, snapped, ground, manual, seed, charWidth, charHeight);
	}

	private bool BeginJob(World w, Creature c, Settings s, string species, StructureKind kind, Vector2 site, Surface ground, bool manual, int seed, float charWidth, float charHeight)
	{
		DestroyAllStructures();
		ClearShelter();
		Random random = ((seed == 0) ? new Random() : new Random(seed));
		Active = new Structure
		{
			Kind = kind,
			Site = site,
			Width = StructureWidth(kind, charWidth, species) * c.Scale,
			Height = StructureHeight(kind, charHeight) * c.Scale,
			StagesTotal = kind == StructureKind.Platform ? 3 : 4,
			SurfaceId = -9000000L - (long)(Math.Abs(site.X * 13f + site.Y) % 899999),
			BornAt = c.Time,
			LifetimeSec = s.Advanced.StructureLifetimeMin * 60.0,
			OwnerSpecies = species
		};
		StyleNote = StyleFor(species, kind);
		bill.Clear();
		if (kind == StructureKind.Platform)
		{
			bill.Add(MaterialKind.Plank);
			bill.Add(MaterialKind.Plank);
		}
		else if (kind == StructureKind.StickStructure)
		{
			bill.Add(MaterialKind.Stick);
			bill.Add(MaterialKind.Stick);
			bill.Add(MaterialKind.Stick);
		}
		else
		{
			bill.Add(MaterialKind.Stick);
			bill.Add(MaterialKind.Plank);
			bill.Add(MaterialKind.Plank);
			bill.Add(MaterialKind.Panel);
		}
		foreach (MaterialKind want in bill)
		{
			float side = random.Next(2) == 0 ? -1f : 1f;
			float dropX = site.X + side * (140f + (float)random.NextDouble() * 120f) * c.Scale;
			dropX = Math.Clamp(dropX, ground.Left + 30f * c.Scale, ground.Right - 30f * c.Scale);
			Materials.Add(new Material
			{
				Kind = want,
				Position = new Vector2(dropX, site.Y - (60f + (float)random.NextDouble() * 120f) * c.Scale),
				Velocity = Vector2.Zero
			});
		}
		Materials.Add(new Material
		{
			Kind = MaterialKind.Hammer,
			Position = new Vector2(site.X - 40f * c.Scale, site.Y - 80f * c.Scale),
			Velocity = Vector2.Zero
		});
		Materials.Add(new Material
		{
			Kind = MaterialKind.Nail,
			Position = new Vector2(site.X + 40f * c.Scale, site.Y - 80f * c.Scale),
			Velocity = Vector2.Zero
		});
		Manual = manual;
		gatherIndex = 0;
		nailsThisStage = 0;
		swings = 0;
		sessionTime = 0f;
		totalTime = 0f;
		SetPhase(BuildPhase.Travel);
		return true;
	}

	public void Cancel()
	{
		foreach (Material material in Materials.Where(m => m.Carried))
		{
			material.Carried = false;
		}
		Phase = BuildPhase.None;
		Active = null;
	}

	public void ClearShelter()
	{
		ShelterUntil = 0.0;
	}

	public bool CommandTestHammer(Creature c)
	{
		if (!HasSession || Active == null)
		{
			return false;
		}
		if (!Materials.Any(m => m.Kind == MaterialKind.Hammer && m.Carried))
		{
			Materials.Add(new Material { Kind = MaterialKind.Hammer, Carried = true, Position = c.Position });
		}
		SetPhase(BuildPhase.Hammer);
		swings = 0;
		nailsThisStage = 0;
		nailPoint = Active.Slot(Math.Min(Active.Stage, Active.StagesTotal - 1));
		return true;
	}

	public bool CommandTestNail(Creature c)
	{
		if (!HasSession || Active == null)
		{
			return false;
		}
		nailPoint = Active.Slot(Math.Min(Active.Stage, Active.StagesTotal - 1));
		nailsThisStage++;
		ApplyNail(c);
		return true;
	}

	public int StandingStructures => Structures.Count;

	public void DestroyAllStructures()
	{
		Structures.Clear();
		foreach (Material material in Materials.ToList())
		{
			if (material.Placed || material.Carried)
			{
				Materials.Remove(material);
			}
		}
		if (Active != null && !Structures.Contains(Active))
		{
			Active = null;
		}
		Phase = BuildPhase.None;
	}

	public void DestroyActive()
	{		if (Active != null)
		{
			Structures.Remove(Active);
		}
		foreach (Material material in Materials.ToList())
		{
			if (material.Placed || material.Carried)
			{
				Materials.Remove(material);
			}
		}
		Phase = BuildPhase.None;
		Active = null;
	}

	public void ResetWorld()
	{
		Materials.Clear();
		Structures.Clear();
		Phase = BuildPhase.None;
		Active = null;
	}

	public Material? SpawnMaterial(MaterialKind kind, Vector2 near)
	{
		Material material = new Material { Kind = kind, Position = near, Velocity = Vector2.Zero };
		Materials.Add(material);
		return material;
	}

	public void Step(float dt, Creature c, World w, Settings s, string species)
	{
		float scale = c.Scale;
		foreach (Material material in Materials)
		{
			if (material.Carried)
			{
				material.Position = c.Position + new Vector2(c.Facing * 24f, -30f) * scale;
			}
			else
			{
				material.Step(dt, w, scale);
			}
		}
		for (int i = Structures.Count - 1; i >= 0; i--)
		{
			if (Structures[i].Expired(c.Time))
			{
				if (Active == Structures[i])
				{
					Active = null;
				}
				Structures.RemoveAt(i);
			}
		}
		if (!HasSession && ShelterUntil > c.Time)
		{
			Structure? home = Structures.FirstOrDefault(x => x.SurfaceId == ShelterId && x.Finished);
			if (home == null)
			{
				ShelterUntil = 0.0;
			}
			else
			{
				Surface? perch = FindSiteSurface(w, home);
				bool onSite = (perch != null && c.Support == perch.Id)
					|| (Math.Abs(c.Position.X - home.Site.X) < 70f * scale && Math.Abs(c.Position.Y - home.Site.Y) < 150f * scale);
				if (onSite)
				{
					c.RequestActivity(Activity.Sit, 2f);
					c.SetFacing(home.Site.X >= c.Position.X ? 1 : -1);
				}
				else if (home.Site.Y < c.Position.Y - 100f * scale)
				{
					RideToSite(w, c, scale, home);
				}
				else
				{
					c.Seek(home.Site);
				}
			}
		}
		if (!HasSession || Active == null)
		{
			return;
		}
		if (c.Activity != Activity.Build && c.Activity != Activity.Climb)
		{
			c.RequestActivity(Activity.Build, 10f);
		}
		Structure job = Active;
		sessionTime += dt;
		totalTime += dt;
		switch (Phase)
		{
		case BuildPhase.Travel:
		{
			if (!RideToSite(w, c, scale, job))
			{
				if (sessionTime > 25f)
				{
					Cancel();
				}
				break;
			}
			c.SetTargetX(job.Site.X);
			c.SetMotionOverride(null);
			if (Math.Abs(c.Position.X - job.Site.X) < 40f * scale && c.Support.HasValue)
			{
				SetPhase(BuildPhase.Investigate);
			}
			else if (sessionTime > 25f)
			{
				Cancel();
			}
			break;
		}
		case BuildPhase.Investigate:
		{
			c.SetMotionOverride(Motion.Peeking);
			c.SetVelocity(new Vector2(c.Velocity.X * MathF.Exp(0f - dt * 6f), c.Velocity.Y));
			if (sessionTime >= 1.6f)
			{
				SetPhase(BuildPhase.Gather);
				sessionTime = 0f;
			}
			break;
		}
		case BuildPhase.Gather:
		{
			Material? need = Materials.FirstOrDefault(m => !m.Carried && !m.Placed && m.Kind == MaterialKind.Hammer)
				?? Materials.FirstOrDefault(m => !m.Carried && !m.Placed && m.Kind != MaterialKind.Hammer);
			if (need == null)
			{
				SetPhase(BuildPhase.Place);
				sessionTime = 0f;
				break;
			}
			c.SetTargetX(need.Position.X);
			c.SetMotionOverride(null);
			if (Vector2.Distance(c.Position, need.Position) < 46f * scale)
			{
				need.Carried = true;
				gatherIndex++;
				c.SetMotionOverride(Motion.Carrying);
				SetPhase(BuildPhase.Carry);
				sessionTime = 0f;
			}
			else if (sessionTime > 20f)
			{
				Cancel();
			}
			break;
		}
		case BuildPhase.Carry:
		{
			if (!RideToSite(w, c, scale, job))
			{
				if (sessionTime > 35f)
				{
					Cancel();
				}
				break;
			}
			c.SetTargetX(job.Site.X);
			if (Math.Abs(c.Position.X - job.Site.X) < 44f * scale)
			{
				SetPhase(BuildPhase.Place);
			}
			else if (sessionTime > 35f)
			{
				Cancel();
			}
			break;
		}
		case BuildPhase.Place:
		{
			if (!RideToSite(w, c, scale, job))
			{
				if (sessionTime > 30f)
				{
					Cancel();
				}
				break;
			}
			Material? held = Materials.FirstOrDefault(m => m.Carried && m.Kind != MaterialKind.Hammer);
			if (held == null)
			{
				if (Materials.Any(m => !m.Carried && !m.Placed && m.Kind != MaterialKind.Hammer))
				{
					SetPhase(BuildPhase.Gather);
				}
				else if (Materials.Any(m => m.Kind == MaterialKind.Hammer && m.Carried))
				{
					SetPhase(BuildPhase.Hammer);
					nailsThisStage = 0;
					swings = 0;
					nailPoint = job.Slot(Math.Min(job.Stage, job.StagesTotal - 1));
				}
				else
				{
					SpawnHammer(c);
					SetPhase(BuildPhase.Hammer);
					nailsThisStage = 0;
					swings = 0;
					nailPoint = job.Slot(Math.Min(job.Stage, job.StagesTotal - 1));
				}
				break;
			}
			held.Carried = false;
			held.Placed = true;
			held.Position = job.Slot(Math.Min(job.Stage + Materials.Count(m => m.Placed) % 2, job.StagesTotal - 1));
			held.PlaceAt = held.Position;
			c.SetMotionOverride(null);
			SetPhase(Materials.Any(m => !m.Carried && !m.Placed && m.Kind != MaterialKind.Hammer) ? BuildPhase.Gather : BuildPhase.Place);
			break;
		}
		case BuildPhase.Hammer:
		{
			if (!RideToSite(w, c, scale, job))
			{
				if (sessionTime > 50f)
				{
					Cancel();
				}
				break;
			}
			swingPhase += dt * (5f + job.Stage);
			float bob = MathF.Sin(swingPhase * (float)Math.PI * 2f);
			nailPoint = job.Slot(Math.Min(job.Stage, job.StagesTotal - 1));
			c.SetHandTarget(new Vector2(nailPoint.X, nailPoint.Y - 14f * scale + bob * 10f * scale));
			c.SetMotionOverride(Motion.Hammering);
			c.SetFacing(nailPoint.X >= c.Position.X ? 1 : -1);
			if (bob > 0.92f)
			{
				swings++;
				if (swings % 3 == 0)
				{
					nailsThisStage++;
					ApplyNail(c);
				}
			}
			if (job.Finished)
			{
				FinishJob(c, s, job);
			}
			else if (sessionTime > 40f)
			{
				Cancel();
			}
			break;
		}
		case BuildPhase.Inspect:
		{
			c.SetHandTarget(null);
			c.SetMotionOverride(Motion.Peeking);
			c.SetTargetX(job.Site.X + (job.Site.X < c.Position.X ? 60f : -60f) * scale);
			if (sessionTime >= 2.4f)
			{
				SetPhase(BuildPhase.Celebrate);
				sessionTime = 0f;
				c.SetMood(Math.Min(1f, c.Mood + 0.25f));
			}
			break;
		}
		case BuildPhase.Celebrate:
		{
			c.SetMotionOverride(Motion.Celebrating);
			if (sessionTime >= 1.6f)
			{
				SetPhase(BuildPhase.Done);
			}
			break;
		}
		case BuildPhase.Done:
		{
			TidyTools(job);
			c.SetHandTarget(null);
			c.SetMotionOverride(null);
			c.SetTargetX(job.Site.X);
			c.SetActivity(Activity.Sit, 12f);
			if (!Manual)
			{
				s.Advanced.LastBuildUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
				c.AddCooldown(Activity.Build, s.Advanced.BuildCooldownSec);
			}
			c.NextBuildAt = c.Time + s.Advanced.BuildCooldownSec + (s.Advanced.BuildIntervalMinSec + s.Advanced.BuildIntervalMaxSec) / 2.0;
			Phase = BuildPhase.None;
			Active = null;
			break;
		}
		}
		if (sessionTime > 120f && HasSession)
		{
			Cancel();
		}
		if (totalTime > 300f && HasSession)
		{
			Cancel();
		}
	}

	private void SpawnHammer(Creature c)
	{
		if (!Materials.Any(m => m.Kind == MaterialKind.Hammer))
		{
			Materials.Add(new Material { Kind = MaterialKind.Hammer, Carried = true, Position = c.Position });
		}
		else
		{
			Material hammer = Materials.First(m => m.Kind == MaterialKind.Hammer);
			hammer.Carried = true;
			hammer.Placed = false;
		}
	}

	private void ApplyNail(Creature c)
	{
		if (Active == null)
		{
			return;
		}
		if (nailsThisStage >= 2)
		{
			nailsThisStage = 0;
			Active.Stage = Math.Min(Active.Stage + 1, Active.StagesTotal);
			if (Active.Finished && !Structures.Contains(Active))
			{
				Active.BornAt = c.Time;
				Structures.Add(Active);
				foreach (Material material in Materials.Where(m => m.Kind == MaterialKind.Hammer))
				{
					material.Carried = false;
				}
				c.SetHandTarget(null);
				SetPhase(BuildPhase.Inspect);
				sessionTime = 0f;
			}
		}
	}

	private void FinishJob(Creature c, Settings s, Structure job)
	{
		if (!Structures.Contains(job))
		{
			job.BornAt = c.Time;
			Structures.Add(job);
		}
		TidyTools(job);
		c.SetHandTarget(null);
		SetPhase(BuildPhase.Inspect);
		sessionTime = 0f;
	}

	private void TidyTools(Structure job)
	{
		Material? hammer = Materials.FirstOrDefault(m => m.Kind == MaterialKind.Hammer);
		if (hammer != null)
		{
			hammer.Carried = false;
			hammer.Placed = true;
			hammer.Position = new Vector2(job.Site.X + job.Width * 0.32f, job.Site.Y);
			hammer.PlaceAt = hammer.Position;
			hammer.Velocity = Vector2.Zero;
		}
		Material? nail = Materials.FirstOrDefault(m => m.Kind == MaterialKind.Nail);
		if (nail == null)
		{
			nail = new Material { Kind = MaterialKind.Nail, Position = new Vector2(job.Site.X - job.Width * 0.3f, job.Site.Y) };
			Materials.Add(nail);
		}
		nail.Carried = false;
		nail.Placed = true;
		nail.Position = new Vector2(job.Site.X - job.Width * 0.3f, job.Site.Y);
		nail.PlaceAt = nail.Position;
		nail.Velocity = Vector2.Zero;
	}

	public long ShelterId { get; private set; } = -1;

	public double ShelterUntil { get; private set; }

	private double nextGentle;

	public void HouseHit(Creature c, Structure s)
	{
		s.Poke(1.0f, c.Time);
		c.Notify();
		c.SetMood(0.25f);
		c.Seek(s.Site);
		ShelterId = s.SurfaceId;
		ShelterUntil = c.Time + 25.0;
	}

	public bool PokeAt(Vector2 cursor, double now)
	{
		if (now < nextGentle)
		{
			return false;
		}
		foreach (Structure s in Structures)
		{
			if (!s.Finished)
			{
				continue;
			}
			if (cursor.X >= s.Site.X - s.Width / 2f - 6f && cursor.X <= s.Site.X + s.Width / 2f + 6f && cursor.Y >= s.Site.Y - s.Height - 6f && cursor.Y <= s.Site.Y + 6f)
			{
				s.Poke(0.3f, now);
				nextGentle = now + 1.5;
				return true;
			}
		}
		return false;
	}

	private static Surface? FindSiteSurface(World w, Structure job)
	{
		foreach (Surface surface in w.Surfaces)
		{
			if (Math.Abs(surface.Y - job.Site.Y) < 8f && surface.Left - 4f <= job.Site.X && job.Site.X <= surface.Right + 4f)
			{
				return surface;
			}
		}
		return null;
	}

	private static bool RideToSite(World w, Creature c, float scale, Structure job)
	{
		Surface? perch = FindSiteSurface(w, job);
		if (perch == null || c.Support == perch.Id)
		{
			return true;
		}
		if (job.Site.Y < c.Position.Y - 100f * scale)
		{
			c.SetClimbTarget(perch);
			c.SetTargetX(job.Site.X);
			c.RequestActivity(Activity.Climb, 15f);
			c.SetMotionOverride(null);
			return false;
		}
		return true;
	}

	public IEnumerable<Surface> Surfaces()
	{
		foreach (Structure structure in Structures)
		{
			if (structure.Finished)
			{
				yield return structure.ToSurface();
			}
		}
	}

	private void SetPhase(BuildPhase phase)
	{
		Phase = phase;
		sessionTime = 0f;
	}
}
