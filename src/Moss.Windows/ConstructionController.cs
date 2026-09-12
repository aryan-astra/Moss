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

	private static readonly Color Wood = Color.FromArgb(169, 128, 80);

	private static readonly Color DarkWood = Color.FromArgb(110, 79, 42);

	private static readonly Color NailGray = Color.FromArgb(154, 160, 166);

	private static readonly Color HammerHead = Color.FromArgb(58, 68, 78);

	private static readonly Color HandleTan = Color.FromArgb(201, 162, 39);

	public ConstructionController(PetApplication owner)
	{
		app = owner;
	}

	public void Draw(bool hidden)
	{
		ConstructionWorld world = app.Creature.Construction;
		if (hidden)
		{
			carry.Hide();
			foreach (var entry in sites.Values)
			{
				entry.Overlay.Hide();
			}
			return;
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
			DrawMaterial(graphics, held.Kind, swing);
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
				Bitmap canvas = new Bitmap(pw, ph, PixelFormat.Format32bppPArgb);
				view = (overlay, canvas, w, h);
				sites[structure.SurfaceId] = view;
			}
			using (Graphics graphics = Graphics.FromImage(view.Canvas))
			{
				graphics.Clear(Color.Transparent);
				graphics.SmoothingMode = SmoothingMode.AntiAlias;
				graphics.ScaleTransform(scale, scale);
				graphics.TranslateTransform(w / 2f, h - 12f);
				DrawStructure(graphics, structure);
				if (world.Active == structure && world.Phase == BuildPhase.Hammer && app.Creature.Time < sparkUntil)
				{
					DrawSpark(graphics, world.NailPoint - new Vector2(structure.Site.X, structure.Site.Y));
				}
			}
			Vector2 at = new Vector2(structure.Site.X, structure.Site.Y - structure.Height / 2f - 8f * scale);
			view.Overlay.Draw(view.Canvas, at, scale, app.PetAlpha, new Vector2(w / 2f, h / 2f), true);
			if (!view.Overlay.Visible)
			{
				view.Overlay.Configure(interaction: false, app.Config.ExcludeFromCapture);
				view.Overlay.Show();
			}
			view.Overlay.CheckCapture();
		}
	}

	private void DrawMaterial(Graphics g, MaterialKind kind, float swing)
	{
		switch (kind)
		{
		case MaterialKind.Plank:
			using (SolidBrush brush = new SolidBrush(Wood))
			using (Pen pen = new Pen(DarkWood, 1.4f))
			{
				g.FillRoundedRectangle(brush, new RectangleF(-18f, -4.5f, 36f, 9f), 2f);
				g.DrawLine(pen, -18f, 0f, 18f, 0f);
				g.DrawLine(pen, -14f, -4.5f, -14f, 4.5f);
				g.DrawLine(pen, 14f, -4.5f, 14f, 4.5f);
			}
			break;
		case MaterialKind.Stick:
			using (Pen pen2 = new Pen(DarkWood, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
			{
				g.DrawLine(pen2, -15f, 4f, 13f, -5f);
			}
			using (SolidBrush brush2 = new SolidBrush(Wood))
			{
				g.FillEllipse(brush2, 11f, -8f, 5f, 5f);
			}
			break;
		case MaterialKind.Nail:
			using (Pen pen3 = new Pen(NailGray, 2f))
			{
				g.DrawLine(pen3, 0f, -6f, 0f, 6f);
			}
			using (SolidBrush brush3 = new SolidBrush(NailGray))
			{
				g.FillEllipse(brush3, -2.5f, -8.5f, 5f, 3f);
			}
			break;
		case MaterialKind.Panel:
			using (SolidBrush brush4 = new SolidBrush(Wood))
			using (Pen pen4 = new Pen(DarkWood, 1.4f))
			{
				g.FillRoundedRectangle(brush4, new RectangleF(-20f, -8f, 40f, 16f), 2f);
				g.DrawRectangle(pen4, -20, -8, 40, 16);
				g.DrawLine(pen4, 0f, -8f, 0f, 8f);
			}
			break;
		default:
			GraphicsState state = g.Save();
			g.RotateTransform(swing * 57f);
			using (Pen pen5 = new Pen(HandleTan, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
			{
				g.DrawLine(pen5, 0f, 12f, 0f, -10f);
			}
			using (SolidBrush brush5 = new SolidBrush(HammerHead))
			{
				g.FillRoundedRectangle(brush5, new RectangleF(-9f, -18f, 18f, 9f), 2f);
			}
			g.Restore(state);
			break;
		}
	}

	private void DrawStructure(Graphics g, Structure structure)
	{
		float half = structure.Width / 2f;
		using (SolidBrush slab = new SolidBrush(DarkWood))
		{
			g.FillRoundedRectangle(slab, new RectangleF(-half, -7f, structure.Width, 9f), 2f);
		}
		if (structure.Stage >= 1)
		{
			using (Pen walls = new Pen(Wood, 6f) { StartCap = LineCap.Square, EndCap = LineCap.Square })
			{
				g.DrawLine(walls, -half + 6f, -7f, -half + 6f, -structure.Height * 0.55f);
				g.DrawLine(walls, half - 6f, -7f, half - 6f, -structure.Height * 0.55f);
			}
		}
		if (structure.Stage >= 2)
		{
			using (SolidBrush panels = new SolidBrush(Color.FromArgb(190, 150, 100)))
			{
				g.FillRectangle(panels, -half + 9f, -structure.Height * 0.55f, structure.Width - 18f, 7f);
			}
			if (structure.Kind == StructureKind.House)
			{
				using (SolidBrush trim = new SolidBrush(ColorTranslator.FromHtml(app.Character.Accent)))
				{
					g.FillRectangle(trim, -7f, -structure.Height * 0.42f, 14f, structure.Height * 0.42f - 7f);
				}
			}
		}
		if (structure.Finished)
		{
			PointF[] roof = { new PointF(-half - 6f, -structure.Height * 0.55f), new PointF(half + 6f, -structure.Height * 0.55f), new PointF(0f, -structure.Height - 8f) };
			using (SolidBrush brush = new SolidBrush(DarkWood))
			{
				g.FillPolygon(brush, roof);
			}
			using (Pen pen = new Pen(Wood, 2f))
			{
				g.DrawPolygon(pen, roof);
			}
		}
	}

	private void DrawSpark(Graphics g, Vector2 at)
	{
		using (Pen pen = new Pen(Color.FromArgb(230, 220, 160), 2f))
		{
			for (int i = 0; i < 4; i++)
			{
				float angle = i * (float)Math.PI / 4f;
				Vector2 dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
				g.DrawLine(pen, at.X - dir.X * 3f, at.Y - dir.Y * 3f, at.X + dir.X * 9f, at.Y + dir.Y * 9f);
			}
		}
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
