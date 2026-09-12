using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using Moss.Core;

namespace Moss.Windows;

// Pure structure/material painting, shared by the live overlay renderer and
// headless verification harnesses. No windows, no timers, no state — every
// input arrives as arguments, so rendered pixels are identical in both.
public static class StructureArt
{
	private static readonly Color Wood = Color.FromArgb(169, 128, 80);

	private static readonly Color DarkWood = Color.FromArgb(110, 79, 42);

	private static readonly Color NailGray = Color.FromArgb(154, 160, 166);

	private static readonly Color HammerHead = Color.FromArgb(58, 68, 78);

	private static readonly Color HandleTan = Color.FromArgb(138, 90, 43);

	private static readonly Color BeamDark = Color.FromArgb(62, 40, 18);

	private static readonly Color PlankMid = Color.FromArgb(92, 61, 33);

	private static readonly Color PlankLight = Color.FromArgb(122, 84, 44);

	private static readonly Color GrainDark = Color.FromArgb(52, 32, 14);

	private static readonly Color StoneGray = Color.FromArgb(112, 112, 108);

	public static void DrawMaterial(Graphics g, MaterialKind kind, float swing)
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

	public static void DrawStructure(Graphics g, Structure structure, string accentHtml)
	{
		// Local origin (0,0) is the site point (feet level). Callers position
		// the overlay so the origin lands exactly on the physics surface.
		float half = structure.Width / 2f;
		float top = -structure.Height;
		using (SolidBrush stone = new SolidBrush(StoneGray))
		{
			g.FillRoundedRectangle(stone, new RectangleF(-half - 2f, -4f, 12f, 7f), 1.5f);
			g.FillRoundedRectangle(stone, new RectangleF(half - 10f, -4f, 12f, 7f), 1.5f);
		}
		using (SolidBrush sill = new SolidBrush(BeamDark))
		{
			g.FillRoundedRectangle(sill, new RectangleF(-half, -9f, structure.Width, 9f), 2f);
		}
		using (Pen grain = new Pen(GrainDark, 1f))
		{
			g.DrawLine(grain, -half + 3f, -4.5f, half - 3f, -4.5f);
		}
		if (structure.Kind == StructureKind.Platform)
		{
			float deckY = -9f;
			int boards = Math.Max(3, (int)(structure.Width / 16f));
			for (int i = 0; i < boards; i++)
			{
				float bx = -half + 3f + i * ((structure.Width - 6f) / boards);
				using SolidBrush board = new SolidBrush(i % 2 == 0 ? PlankMid : PlankLight);
				g.FillRectangle(board, bx, deckY - 7f, (structure.Width - 6f) / boards - 1.5f, 7f);
				using Pen grainPen = new Pen(GrainDark, 0.9f);
				g.DrawLine(grainPen, bx + 1.5f, deckY - 3.5f, bx + (structure.Width - 6f) / boards - 2.5f, deckY - 3.5f);
			}
			using (Pen posts = new Pen(BeamDark, 5f) { StartCap = LineCap.Square, EndCap = LineCap.Square })
			{
				g.DrawLine(posts, -half + 8f, deckY, -half + 8f, 0f);
				g.DrawLine(posts, half - 8f, deckY, half - 8f, 0f);
			}
			return;
		}
		if (structure.Kind == StructureKind.StickStructure)
		{
			using (Pen sticks = new Pen(PlankLight, 3.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
			{
				g.DrawLine(sticks, -half + 4f, 0f, half - 10f, top);
				g.DrawLine(sticks, half - 4f, 0f, -half + 10f, top);
				g.DrawLine(sticks, -half + 14f, 0f, -half + 22f, top * 0.6f);
				g.DrawLine(sticks, half - 14f, 0f, half - 22f, top * 0.6f);
			}
			using (Pen lash = new Pen(BeamDark, 1.6f))
			{
				g.DrawLine(lash, -half + 2f, top * 0.55f, -half + 24f, top * 0.55f);
				g.DrawLine(lash, half - 24f, top * 0.55f, half - 2f, top * 0.55f);
			}
			using (Pen beam = new Pen(BeamDark, 5f) { StartCap = LineCap.Square, EndCap = LineCap.Square })
			{
				g.DrawLine(beam, -half - 3f, top + 3f, half + 3f, top + 3f);
			}
			return;
		}
		if (structure.Stage >= 1)
		{
			int planks = Math.Max(3, (int)(structure.Width / 15f));
			for (int i = 0; i < planks; i++)
			{
				float px = -half + 4f + i * ((structure.Width - 8f) / planks);
				float pw = (structure.Width - 8f) / planks - 1.2f;
				using SolidBrush plank = new SolidBrush(i % 2 == 0 ? PlankMid : PlankLight);
				g.FillRectangle(plank, px, top * 0.55f, pw, -9f - top * 0.55f);
				using Pen grainPen = new Pen(GrainDark, 0.9f);
				float mid = px + pw / 2f;
				g.DrawLine(grainPen, mid - 1f, top * 0.55f + 4f, mid + 1f, -14f);
			}
			using (Pen frame = new Pen(BeamDark, 2.4f))
			{
				g.DrawRectangle(frame, -half + 2f, top * 0.55f, structure.Width - 4f, -9f - top * 0.55f);
			}
		}
		if (structure.Stage >= 2)
		{
			using (SolidBrush band = new SolidBrush(BeamDark))
			{
				g.FillRectangle(band, -half + 2f, top * 0.62f, structure.Width - 4f, 5f);
			}
			using (SolidBrush knot = new SolidBrush(PlankLight))
			using (Pen ring = new Pen(GrainDark, 1f))
			{
				g.FillEllipse(knot, -half + 10f, top * 0.4f, 5f, 7f);
				g.DrawEllipse(ring, -half + 10f, top * 0.4f, 5f, 7f);
				g.FillEllipse(knot, half - 15f, top * 0.5f, 5f, 7f);
				g.DrawEllipse(ring, half - 15f, top * 0.5f, 5f, 7f);
			}
			if (structure.Kind == StructureKind.House)
			{
				using (SolidBrush door = new SolidBrush(Color.FromArgb(26, 15, 6)))
				{
					g.FillRoundedRectangle(door, new RectangleF(-8f, -26f, 16f, 26f), 5f);
				}
				using (SolidBrush trim = new SolidBrush(ColorTranslator.FromHtml(accentHtml)))
				{
					g.FillEllipse(trim, -2.5f, -16f, 5f, 5f);
				}
			}
		}
		if (structure.Finished)
		{
			PointF[] roof = { new PointF(-half - 7f, top * 0.55f), new PointF(half + 7f, top * 0.55f), new PointF(0f, top - 8f) };
			using (SolidBrush fill = new SolidBrush(BeamDark))
			{
				g.FillPolygon(fill, roof);
			}
			using (Pen shingle = new Pen(PlankLight, 1.6f))
			{
				for (int row = 1; row <= 3; row++)
				{
					float y = top * 0.55f - row * ((top * 0.55f - top + 8f) / 4f);
					float spread = (half + 7f) * (1f - row / 5f);
					g.DrawArc(shingle, -spread, y - 3f, spread * 2f, 7f, 200f, 140f);
				}
			}
			using (Pen ridge = new Pen(GrainDark, 2.4f))
			{
				g.DrawLine(ridge, -7f, top - 8f, 7f, top - 8f);
			}
		}
	}

	public static void DrawSpark(Graphics g, Vector2 at)
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
}
