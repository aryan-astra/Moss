using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Numerics;
using Moss.Core;

namespace Moss.Windows;

// Draws construction materials, the hammer swing, nail sparks and finished
// structures. One small overlay follows the carried item; one overlay per
// structure draws its staged visuals. All geometry comes from the Core
// ConstructionWorld; this class only paints it.
internal sealed class ConstructionController : IDisposable
{
	private readonly PetApplication app;

	private readonly PetOverlay carry = new PetOverlay(ownHotkey: false);

	private readonly Dictionary<long, (PetOverlay Overlay, Bitmap Canvas, float W, float H)> sites = new Dictionary<long, (PetOverlay, Bitmap, float, float)>();

	private Bitmap? carryBitmap;

	private Character? paintedCharacter;

	private int lastSwings;

	private float sparkUntil;

	private Material? drag;

	private Vector2 dragOffset;

	private Vector2 dragVelocity;

	private Vector2 lastCursor;

	private long lastCursorAt;

	public event Action<Structure>? StructureClicked;

	public ConstructionController(PetApplication owner)
	{
		app = owner;
	}

	public void Draw(bool hidden)
	{
		ConstructionWorld world = app.Creature.Construction;
		if (hidden)
		{
			if (drag != null)
			{
				drag.Velocity = Vector2.Zero;
				drag = null;
			}
			carry.Hide();
			foreach (var entry in sites.Values)
			{
				entry.Overlay.Hide();
			}
			return;
		}
		if (drag != null)
		{
			Vector2 cursor = PetOverlay.CursorPhysical;
			long now = Environment.TickCount64;
			float dt = Math.Max(0.001f, (now - lastCursorAt) / 1000f);
			dragVelocity = dragVelocity * 0.6f + ((cursor - lastCursor) / dt) * 0.4f;
			lastCursor = cursor;
			lastCursorAt = now;
			drag.Position = cursor + dragOffset;
			drag.Velocity = Vector2.Zero;
		}
		DrawCarried(world);
		DrawSites(world);
		carry.CheckCapture();
	}

	private void DrawCarried(ConstructionWorld world)
	{
		Material? held = world.Materials.FirstOrDefault(m => m.Carried);
		if (held == null)
		{
			if (carry.Visible)
			{
				carry.Hide();
			}
			return;
		}
		float scale = app.Creature.Scale;
		int size = (int)Math.Ceiling(56f * scale);
		if (carryBitmap == null || carryBitmap.Width != size)
		{
			carryBitmap?.Dispose();
			carryBitmap = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
		}
		using (Graphics graphics = Graphics.FromImage(carryBitmap))
		{
			graphics.Clear(Color.Transparent);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.ScaleTransform(scale, scale);
			graphics.TranslateTransform(28f, 28f);
			float swing = 0f;
			if (held.Kind == MaterialKind.Hammer && world.Phase == BuildPhase.Hammer)
			{
				swing = MathF.Sin(world.SwingAngle * MathF.PI * 2f) * 0.7f;
			}
			StructureArt.DrawMaterial(graphics, held.Kind, swing);
		}
		carry.Draw(carryBitmap, held.Position, scale, app.PetAlpha, new Vector2(28f, 28f), true);
		if (!carry.Visible)
		{
			carry.Configure(interaction: false, app.Config.ExcludeFromCapture);
			carry.Show();
		}
	}

	private void DrawSites(ConstructionWorld world)
	{
		List<Structure> visible = new List<Structure>();
		foreach (Structure structure in world.Structures.Take(6))
		{
			visible.Add(structure);
		}
		if (world.Active != null && world.Active.Stage > 0 && !visible.Contains(world.Active))
		{
			visible.Add(world.Active);
		}
		HashSet<long> wanted = visible.Select(s => s.SurfaceId).ToHashSet();
		foreach (long key in sites.Keys.Where(k => !wanted.Contains(k)).ToList())
		{
			sites[key].Overlay.Dispose();
			sites[key].Canvas.Dispose();
			sites.Remove(key);
		}
		float scale = app.Creature.Scale;
		if (paintedCharacter != app.Character)
		{
			paintedCharacter = app.Character;
		}
		if (world.HammerSwings != lastSwings)
		{
			lastSwings = world.HammerSwings;
			sparkUntil = (float)app.Creature.Time + 0.5f;
		}
		foreach (Structure structure in visible)
		{
			float w = structure.Width + 44f;
			float h = structure.Height + 84f;
			int pw = Math.Max(8, (int)Math.Ceiling(w * scale));
			int ph = Math.Max(8, (int)Math.Ceiling(h * scale));
			if (!sites.TryGetValue(structure.SurfaceId, out var view) || view.Canvas.Width != pw || view.Canvas.Height != ph)
			{
				if (sites.TryGetValue(structure.SurfaceId, out var old))
				{
					old.Overlay.Dispose();
					old.Canvas.Dispose();
					sites.Remove(structure.SurfaceId);
				}
				PetOverlay overlay = new PetOverlay(ownHotkey: false);
				Structure captured = structure;
				overlay.Clicked += delegate
				{
					StructureClicked?.Invoke(captured);
				};
				overlay.GrabStarted += delegate(Vector2 point)
				{
					BeginDrag(point);
				};
				overlay.Released += delegate
				{
					EndDrag();
				};
				Bitmap canvas = new Bitmap(pw, ph, PixelFormat.Format32bppPArgb);
				view = (overlay, canvas, w, h);
				sites[structure.SurfaceId] = view;
			}
			float tremble = structure.ShakeNow(app.Creature.Time);
			float shakeX = tremble > 0f ? MathF.Sin((float)app.Creature.Time * 40f) * 6f * tremble : 0f;
			using (Graphics graphics = Graphics.FromImage(view.Canvas))
			{
				graphics.Clear(Color.Transparent);
				graphics.SmoothingMode = SmoothingMode.AntiAlias;
				graphics.ScaleTransform(scale, scale);
				graphics.TranslateTransform(w / 2f + shakeX, h - 32f);
				StructureArt.DrawStructure(graphics, structure, app.Character.Accent);
				foreach (Material tool in world.Materials.Where(m => m.Placed && (m.Kind == MaterialKind.Hammer || m.Kind == MaterialKind.Nail) && Math.Abs(m.Position.X - structure.Site.X) < structure.Width && Math.Abs(m.Position.Y - structure.Site.Y) < structure.Height + 20f))
				{
					GraphicsState kept = graphics.Save();
					graphics.TranslateTransform(tool.Position.X - structure.Site.X, tool.Position.Y - structure.Site.Y - 4f);
					if (tool.Kind == MaterialKind.Hammer)
					{
						graphics.RotateTransform(72f);
					}
					StructureArt.DrawMaterial(graphics, tool.Kind, 0f);
					graphics.Restore(kept);
				}
				if (world.Active == structure && world.Phase == BuildPhase.Hammer && app.Creature.Time < sparkUntil)
				{
					StructureArt.DrawSpark(graphics, world.NailPoint - new Vector2(structure.Site.X, structure.Site.Y));
				}
			}
			Vector2 at = new Vector2(structure.Site.X, structure.Site.Y - structure.Height / 2f - 8f * scale);
			view.Overlay.Draw(view.Canvas, at, scale, app.PetAlpha, new Vector2(w / 2f, h / 2f), true);
			view.Overlay.BringToFront();
			if (!view.Overlay.Visible)
			{
				view.Overlay.Configure(interaction: true, app.Config.ExcludeFromCapture);
				view.Overlay.Show();
			}
			view.Overlay.CheckCapture();
		}
	}

	private void BeginDrag(Vector2 cursor)
	{
		float scale = app.Creature.Scale;
		Material? best = null;
		float bestDist = 34f * scale;
		foreach (Material material in app.Creature.Construction.Materials)
		{
			if (material.Kind != MaterialKind.Hammer && material.Kind != MaterialKind.Nail)
			{
				continue;
			}
			float distance = Vector2.Distance(material.Position, cursor);
			if (distance < bestDist)
			{
				bestDist = distance;
				best = material;
			}
		}
		if (best == null)
		{
			return;
		}
		best.Carried = false;
		best.Placed = false;
		drag = best;
		dragOffset = best.Position - cursor;
		lastCursor = cursor;
		lastCursorAt = Environment.TickCount64;
		dragVelocity = Vector2.Zero;
		app.Events.Publish("object.grabbed", app.Creature.Time);
	}

	private void EndDrag()
	{
		if (drag == null)
		{
			return;
		}
		drag.Velocity = Vector2.Clamp(dragVelocity, new Vector2(-1600f), new Vector2(1600f));
		drag = null;
		app.Events.Publish("object.thrown", app.Creature.Time);
	}

	public void Dispose()
	{
		carry.Dispose();
		carryBitmap?.Dispose();
		foreach (var entry in sites.Values)
		{
			entry.Overlay.Dispose();
			entry.Canvas.Dispose();
		}
		sites.Clear();
	}
}
