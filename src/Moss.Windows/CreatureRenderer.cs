using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using Moss.Core;

namespace Moss.Windows;

internal sealed class CreatureRenderer : IDisposable
{
	private readonly Random commentsRandom = new Random();

	private static readonly string[] compliments = new string[3] { "Lovely tune!", "Great song!", "Nice sound!" };

	private float nextComment = 12f;

	private float commentUntil;

	private string comment = "";

	private readonly Character character;

	private readonly SolidBrush body;

	private readonly SolidBrush belly;

	private readonly SolidBrush ink;

	private readonly SolidBrush accent;

	private readonly SolidBrush white = new SolidBrush(Color.FromArgb(247, 248, 232));

	private readonly Pen outline;

	private readonly Font sleepFont = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Pixel);

	private readonly LinearGradientBrush coat;

	private readonly SolidBrush blush = new SolidBrush(Color.FromArgb(85, 233, 139, 120));

	private Bitmap? bitmap;

	private Animator? activeAnimation;

	private Vector2? handLocal;

	private bool reducedMotion;

	private float stride;

	private void Effects(Graphics g, Creature c, bool playing, bool reduced, bool comments)
	{
		float height = character.Height;
		float num = (float)c.Time;
		if (!playing)
		{
			commentUntil = 0f;
			nextComment = Math.Max(nextComment, num + 10f);
		}
		if (c.Affection > 0f)
		{
			int num2 = (reduced ? 2 : 7);
			for (int i = 0; i < num2; i++)
			{
				float num3 = (2.4f - c.Affection + (float)i * 0.22f) % 1.7f;
				float num4 = ((i % 2 != 0) ? 1 : (-1)) * (30 + i * 4);
				float num5 = (0f - height) * 0.6f - num3 * 23f;
				using SolidBrush brush = new SolidBrush(Color.FromArgb((int)(210f * (1f - num3 / 1.7f)), 220, 108, 129));
				using GraphicsPath graphicsPath = new GraphicsPath();
				float num6 = 4 + i % 3;
				graphicsPath.AddBezier(num4, num5, num4 - num6 * 2f, num5 - num6 * 2f, num4 - num6 * 2f, num5 + num6, num4, num5 + num6 * 2f);
				graphicsPath.AddBezier(num4, num5 + num6 * 2f, num4 + num6 * 2f, num5 + num6, num4 + num6 * 2f, num5 - num6 * 2f, num4, num5);
				g.FillPath(brush, graphicsPath);
			}
		}
		if (!playing || c.Activity != Activity.Dance)
		{
			return;
		}
		for (int j = 0; j < (reduced ? 1 : 4); j++)
		{
			float num7 = (reduced ? 0.3f : ((num * 0.45f + (float)j * 0.25f) % 1f));
			float num8 = (float)((j % 2 != 0) ? 1 : (-1)) * (character.Width * 0.48f + num7 * 17f);
			float num9 = (0f - height) * 0.65f - num7 * 36f;
			using SolidBrush brush2 = new SolidBrush(Color.FromArgb((int)(200f * (1f - num7)), accent.Color));
			using Pen pen = new Pen(brush2, 1.6f);
			g.FillEllipse(brush2, num8 - 4f, num9, 7f, 4f);
			g.DrawLine(pen, num8 + 2f, num9 + 1f, num8 + 2f, num9 - 12f);
			g.DrawBezier(pen, num8 + 2f, num9 - 12f, num8 + 10f, num9 - 10f, num8 + 7f, num9 - 5f, num8 + 5f, num9 - 5f);
		}
		if (comments && num >= nextComment)
		{
			comment = compliments[commentsRandom.Next(compliments.Length)];
			commentUntil = num + 3f;
			nextComment = num + 55f + (float)commentsRandom.Next(75);
		}
		if (comments && num < commentUntil)
		{
			g.FillRoundedRectangle(belly, new RectangleF(-43f, 0f - height - 27f, 86f, 21f), 7f);
			g.DrawString(comment, sleepFont, ink, -37f, 0f - height - 24f);
		}
	}

	public CreatureRenderer(Character c)
	{
		character = c;
		body = new SolidBrush(ColorTranslator.FromHtml(c.Body));
		belly = new SolidBrush(ColorTranslator.FromHtml(c.Belly));
		ink = new SolidBrush(ColorTranslator.FromHtml(c.Ink));
		accent = new SolidBrush(ColorTranslator.FromHtml(c.Accent));
		coat = new LinearGradientBrush(new RectangleF((0f - c.Width) * 0.6f, (0f - c.Height) * 1.15f, c.Width * 1.2f, c.Height * 1.3f), Tint(body.Color, Color.White, 0.24f), Tint(body.Color, ink.Color, 0.08f), LinearGradientMode.ForwardDiagonal);
		outline = new Pen(Color.FromArgb(160, ink.Color), 1.45f)
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round,
			LineJoin = LineJoin.Round
		};
		static Color Tint(Color a, Color b, float f)
		{
			return Color.FromArgb((int)((float)(int)a.R + (float)(b.R - a.R) * f), (int)((float)(int)a.G + (float)(b.G - a.G) * f), (int)((float)(int)a.B + (float)(b.B - a.B) * f));
		}
	}

	public Bitmap Draw(Creature creature, Animator animation, World world, bool cursorAware, bool music, bool reduced = false, bool hat = false, bool glasses = false, float rasterScale = 0f, bool musicComments = true)
	{
		activeAnimation = animation;
		reducedMotion = reduced;
		stride = Math.Clamp(Math.Abs(creature.Velocity.X) / creature.Scale / 65f, 0f, 1f);
		float num = ((rasterScale > 0f) ? rasterScale : creature.Scale);
		int num2 = (int)Math.Ceiling(180f * num);
		if (bitmap == null || bitmap.Width != num2)
		{
			bitmap?.Dispose();
			bitmap = new Bitmap(num2, num2, PixelFormat.Format32bppPArgb);
		}
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.Clear(Color.Transparent);
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.ScaleTransform(num, num);
		graphics.TranslateTransform(90f, 142f);
		Pose pose = animation.Pose;
		float num3 = (float)creature.Time;
		if (reduced)
		{
			pose = new Pose(0f, pose.Lean * 0.2f, pose.Crouch, pose.Eyes, 0f, pose.Arms * 0.3f);
			num3 = 0f;
		}
		if (creature.Disappointment > 0.1f)
		{
			pose = pose with
			{
				Lean = pose.Lean - creature.Disappointment * 5f,
				Eyes = Math.Max(0.4f, pose.Eyes - creature.Disappointment * 0.3f),
				Ears = pose.Ears + 0.45f * creature.Disappointment
			};
		}
		if (creature.Tug > 0.01f)
		{
			pose = pose with
			{
				Lean = pose.Lean - (float)creature.Facing * creature.Tug * 12f,
				Crouch = pose.Crouch + creature.Tug * 0.1f
			};
		}
		float num4 = (reduced ? 0f : MathF.Sin(animation.Phase));
		float num5 = Math.Clamp(Math.Abs(creature.Velocity.X) / (90f * creature.Scale), 0f, 1f);
		float height = character.Height;
		float width = character.Width;
		float num6 = pose.Bob;
		float num7 = 1f - pose.Crouch;
		if (animation.State == Motion.Sleeping && !reduced)
		{
			num6 = MathF.Sin(num3 * 1.5f) * 1.1f;
		}
		if (creature.Support.HasValue)
		{
			using SolidBrush brush = new SolidBrush(Color.FromArgb(35, 35, 48, 36));
			graphics.FillEllipse(brush, (0f - width) * 0.4f, -3f, width * 0.8f, 6f);
		}
		GraphicsState gstate = graphics.Save();
		graphics.TranslateTransform(0f, 0f - num6);
		graphics.RotateTransform(pose.Lean + creature.Balance);
		graphics.ScaleTransform(1f + pose.Crouch * 0.25f, num7);
		handLocal = null;
		Vector2? handTarget = creature.HandTarget;
		if (handTarget.HasValue)
		{
			Vector2 valueOrDefault = handTarget.GetValueOrDefault();
			Vector2 position = (valueOrDefault - creature.Position) / creature.Scale + new Vector2(0f, num6);
			position = Vector2.Transform(position, Matrix3x2.CreateRotation((0f - (pose.Lean + creature.Balance)) * (float)Math.PI / 180f));
			handLocal = new Vector2(position.X / (1f + pose.Crouch * 0.25f), position.Y / num7);
		}
		if (character.Species != "bean")
		{
			DrawSpecies(graphics, creature, animation, world, cursorAware, music, pose, num4, num5, reduced);
			DrawWearables(graphics, width, height, hat, glasses);
			graphics.Restore(gstate);
			Effects(graphics, creature, music, reduced, musicComments);
			return bitmap;
		}
		using (GraphicsPath graphicsPath = new GraphicsPath())
		{
			float num8 = MathF.Sin(num3 * 3f) * 5f;
			graphicsPath.AddBezier((0f - width) * 0.35f, -22f, (0f - width) * 0.8f, -35f + num8, (0f - width) * 0.73f, -3f + num8, (0f - width) * 0.32f, -12f);
			graphicsPath.CloseFigure();
			graphics.FillPath(coat, graphicsPath);
			graphics.DrawPath(outline, graphicsPath);
		}
		DrawEar(graphics, (0f - width) * 0.25f, 0f - height + 10f, -18f + pose.Ears * 30f + MathF.Sin(num3 * 3f) * 3f, character.EarLength);
		DrawEar(graphics, width * 0.25f, 0f - height + 10f, 18f - pose.Ears * 30f + MathF.Sin(num3 * 3f + 0.8f) * 3f, character.EarLength * 0.84f);
		DrawFoot(graphics, (0f - width) * 0.25f, num5 * num4 * 6f, creature.Facing, num5);
		DrawFoot(graphics, width * 0.25f, (0f - num5) * num4 * 6f, creature.Facing, num5);
		using (GraphicsPath graphicsPath2 = new GraphicsPath())
		{
			graphicsPath2.AddBezier((0f - width) / 2f, (0f - height) * 0.42f, (0f - width) * 0.56f, (0f - height) * 0.98f, (0f - width) * 0.23f, 0f - height, width * 0.02f, 0f - height);
			graphicsPath2.AddBezier(width * 0.02f, 0f - height, width * 0.48f, 0f - height, width * 0.54f, (0f - height) * 0.7f, width / 2f, (0f - height) * 0.35f);
			graphicsPath2.AddBezier(width / 2f, (0f - height) * 0.35f, width * 0.48f, -3f, width * 0.28f, -4f, 0f, -4f);
			graphicsPath2.AddBezier(0f, -4f, (0f - width) * 0.4f, -2f, (0f - width) * 0.52f, -12f, (0f - width) / 2f, (0f - height) * 0.42f);
			graphicsPath2.CloseFigure();
			graphics.FillPath(coat, graphicsPath2);
			graphics.DrawPath(outline, graphicsPath2);
		}
		graphics.FillEllipse(belly, (0f - width) * 0.31f, (0f - height) * 0.49f, width * 0.64f, height * 0.39f);
		float num9 = pose.Arms * 3f + num5 * num4 * 7f;
		DrawArm(graphics, (0f - width) * 0.46f, (0f - height) * 0.48f, -14f - num9);
		DrawArm(graphics, width * 0.46f, (0f - height) * 0.48f, 14f + num9);
		float num10 = 0f;
		float num11 = 0f;
		if (cursorAware)
		{
			Vector2 vector = world.Cursor - (creature.Position - new Vector2(0f, height * 0.65f * creature.Scale));
			num10 = Math.Clamp(vector.X / 100f, -3f, 3f);
			num11 = Math.Clamp(vector.Y / 140f, -2.5f, 2.5f);
		}
		float num12 = (0f - height) * 0.64f;
		float open = ((MathF.Sin(num3 * 0.91f) > 0.994f) ? 0.08f : pose.Eyes);
		Eye(graphics, (0f - width) * 0.17f + num10, num12 + num11, open);
		Eye(graphics, width * 0.17f + num10, num12 + num11, open);
		graphics.FillEllipse(accent, (0f - width) * 0.33f, num12 + 8f, 10f, 5f);
		graphics.FillEllipse(accent, width * 0.18f, num12 + 8f, 10f, 5f);
		Motion state = animation.State;
		if ((state == Motion.Falling || state == Motion.Grabbed || state == Motion.Startled) ? true : false)
		{
			graphics.FillEllipse(ink, -3f, num12 + 12f, 6f, 8f);
		}
		else
		{
			graphics.DrawArc(outline, -5f + num10 * 0.3f, num12 + 8f, 10f, 7f, 5f, 170f);
		}
		using (Pen pen = new Pen(ink.Color, 1.7f))
		{
			graphics.DrawLine(pen, -4f, 0f - height + 12f, -5f, 0f - height + 17f);
			graphics.DrawLine(pen, 1f, 0f - height + 10f, 1f, 0f - height + 16f);
			graphics.DrawLine(pen, 6f, 0f - height + 12f, 7f, 0f - height + 17f);
		}
		if (character.Headphones && music && creature.Activity == Activity.Dance)
		{
			using Pen pen2 = new Pen(ink.Color, 5f);
			graphics.DrawArc(pen2, (0f - width) * 0.53f, 0f - height - 4f, width * 1.06f, height * 0.7f, 180f, 180f);
			graphics.FillRoundedRectangle(ink, new RectangleF((0f - width) * 0.55f, (0f - height) * 0.79f, 10f, 22f), 4f);
			graphics.FillRoundedRectangle(ink, new RectangleF(width * 0.4f, (0f - height) * 0.79f, 10f, 22f), 4f);
			graphics.FillRectangle(accent, (0f - width) * 0.55f + 3f, (0f - height) * 0.79f + 5f, 4f, 11f);
			graphics.FillRectangle(accent, width * 0.4f + 3f, (0f - height) * 0.79f + 5f, 4f, 11f);
		}
		DrawWearables(graphics, width, height, hat, glasses);
		graphics.Restore(gstate);
		if (animation.State == Motion.Sleeping)
		{
			float num13 = num3 % 3f / 3f;
			using SolidBrush brush2 = new SolidBrush(Color.FromArgb((int)(180f * (1f - num13)), ink.Color));
			graphics.DrawString("z", sleepFont, brush2, 37f, 0f - height - 12f - num13 * 18f);
		}
		if (animation.State == Motion.Celebrating && !reduced)
		{
			float num14 = animation.Time % 1.4f / 1.4f;
			using GraphicsPath graphicsPath3 = new GraphicsPath();
			float num15 = 36f;
			float num16 = 0f - height - 5f - num14 * 15f;
			graphicsPath3.AddBezier(num15, num16, num15 - 10f, num16 - 8f, num15 - 13f, num16 + 3f, num15, num16 + 10f);
			graphicsPath3.AddBezier(num15, num16 + 10f, num15 + 13f, num16 + 3f, num15 + 10f, num16 - 8f, num15, num16);
			graphics.FillPath(accent, graphicsPath3);
		}
		Effects(graphics, creature, music, reduced, musicComments);
		return bitmap;
	}

	private void DrawEar(Graphics g, float x, float y, float angle, float length)
	{
		GraphicsState gstate = g.Save();
		g.TranslateTransform(x, y);
		g.RotateTransform(angle);
		using GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddBezier(-7f, 3f, -14f, (0f - length) * 0.65f, -3f, 0f - length - 5f, 1f, 0f - length);
		graphicsPath.AddBezier(1f, 0f - length, 12f, (0f - length) * 0.5f, 9f, -3f, 7f, 3f);
		graphicsPath.CloseFigure();
		g.FillPath(coat, graphicsPath);
		g.DrawPath(outline, graphicsPath);
		g.DrawLine(outline, 0f, -4f, 0f, (0f - length) * 0.55f);
		g.Restore(gstate);
	}

	private void DrawFoot(Graphics g, float x, float y, int facing, float move)
	{
		Vector2 vector = Vector2.Zero;
		bool flag = !reducedMotion && activeAnimation != null;
		if (flag)
		{
			Motion state = activeAnimation.State;
			bool flag2 = (((uint)(state - 1) <= 1u || state == Motion.Investigating) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			vector = PoseDynamics.Foot(activeAnimation.GaitCycle + ((x > 0f) ? 0.5f : 0f), 23f, 7f) * stride;
		}
		Animator? animator = activeAnimation;
		float num = ((animator != null && animator.State == Motion.Dancing) ? Math.Min(0f, y) : 0f);
		g.FillEllipse(ink, x - 7f + vector.X * (float)facing, -5f + vector.Y + num, 15f, 7f);
	}

	private void Paw(Graphics g, Brush color, float x, float y, float width, float height, int facing)
	{
		Vector2 vector = Vector2.Zero;
		bool flag = !reducedMotion && activeAnimation != null;
		if (flag)
		{
			Motion state = activeAnimation.State;
			bool flag2 = (((uint)(state - 1) <= 1u || state == Motion.Investigating) ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			vector = PoseDynamics.Foot(activeAnimation.GaitCycle + ((x > 0f) ? 0.5f : 0f), 23f, 7f) * stride;
		}
		g.FillEllipse(color, x - width / 2f + vector.X * (float)facing, y + vector.Y, width, height);
	}

	private void DrawArm(Graphics g, float x, float y, float angle)
	{
		Vector2? vector = handLocal;
		if (vector.HasValue)
		{
			Vector2 valueOrDefault = vector.GetValueOrDefault();
			if (Math.Sign(x) == Math.Sign(valueOrDefault.X))
			{
				(Vector2, Vector2) tuple = PoseDynamics.Arm(new Vector2(x, y), valueOrDefault, 17f, 17f, (!(x < 0f)) ? 1 : (-1));
				using Pen pen = new Pen(body.Color, 8f)
				{
					StartCap = LineCap.Round,
					EndCap = LineCap.Round,
					LineJoin = LineJoin.Round
				};
				g.DrawLines(pen, new PointF[3]
				{
					new PointF(x, y),
					new PointF(tuple.Item1.X, tuple.Item1.Y),
					new PointF(tuple.Item2.X, tuple.Item2.Y)
				});
				using Pen pen2 = new Pen(Color.FromArgb(90, ink.Color), 1f);
				g.DrawArc(pen2, tuple.Item2.X - 3f, tuple.Item2.Y - 3f, 6f, 6f, 20f, 110f);
				return;
			}
		}
		GraphicsState gstate = g.Save();
		g.TranslateTransform(x, y);
		g.RotateTransform(angle);
		g.FillEllipse(body, -4, 0, 8, 17);
		g.DrawArc(outline, -4, 0, 8, 17, 0, 180);
		g.Restore(gstate);
	}

	private void Eye(Graphics g, float x, float y, float open)
	{
		if (open < 0.2f)
		{
			g.DrawArc(outline, x - 4f, y - 2f, 8f, 4f, 0f, 180f);
			return;
		}
		g.FillEllipse(ink, x - 3.7f, y - 6f * open, 7.4f, 12f * open);
		if (open > 0.6f)
		{
			g.FillEllipse(white, x - 1.8f, y - 4f * open, 2.1f, 2.1f);
		}
	}

	public void Dispose()
	{
		bitmap?.Dispose();
		coat.Dispose();
		blush.Dispose();
		body.Dispose();
		belly.Dispose();
		ink.Dispose();
		accent.Dispose();
		white.Dispose();
		outline.Dispose();
		sleepFont.Dispose();
	}

	private void DrawSpecies(Graphics g, Creature c, Animator a, World world, bool cursor, bool music, Pose p, float gait, float move, bool reduced)
	{
		float width = character.Width;
		float height = character.Height;
		float num = (reduced ? 0f : ((float)c.Time));
		if (a.State == Motion.Dancing)
		{
			move = 0.7f;
		}
		float num2 = (cursor ? Math.Clamp((world.Cursor.X - c.Position.X) / (100f * c.Scale), -3f, 3f) : 0f);
		switch (character.Species)
		{
		case "cat":
		{
			using (Pen pen = new Pen(body.Color, 9f))
			{
				LineCap startCap = (pen.EndCap = LineCap.Round);
				pen.StartCap = startCap;
				g.DrawBezier(pen, (0f - width) * 0.3f, -12f, (0f - width) * 0.9f, -18f, (0f - width) * 0.8f, -58f, (0f - width) * 0.65f, -45f + (reduced ? 0f : (gait * 4f)));
			}
			DrawFoot(g, (0f - width) * 0.23f, gait * move * 5f, c.Facing, move);
			DrawFoot(g, width * 0.23f, (0f - gait) * move * 5f, c.Facing, move);
			Oval(body, (0f - width) * 0.43f, (0f - height) * 0.72f, width * 0.86f, height * 0.68f, stroke: true);
			Shape(body, new PointF[3]
			{
				new PointF((0f - width) * 0.43f, (0f - height) * 0.6f),
				new PointF((0f - width) * 0.48f, (0f - height) * 1.03f),
				new PointF((0f - width) * 0.08f, (0f - height) * 0.84f)
			});
			Shape(body, new PointF[3]
			{
				new PointF(width * 0.43f, (0f - height) * 0.6f),
				new PointF(width * 0.48f, (0f - height) * 1.03f),
				new PointF(width * 0.08f, (0f - height) * 0.84f)
			});
			Oval(body, (0f - width) * 0.5f, (0f - height) * 0.92f, width, height * 0.56f, stroke: true);
			Oval(belly, (0f - width) * 0.26f, (0f - height) * 0.45f, width * 0.52f, height * 0.35f);
			using (Pen pen2 = new Pen(ink.Color, 1.2f))
			{
				for (int j = -1; j <= 1; j += 2)
				{
					g.DrawLine(pen2, (float)j * width * 0.31f, (0f - height) * 0.58f, (float)j * width * 0.6f, (0f - height) * 0.62f);
					g.DrawLine(pen2, (float)j * width * 0.31f, (0f - height) * 0.54f, (float)j * width * 0.6f, (0f - height) * 0.52f);
				}
			}
			DrawArm(g, (0f - width) * 0.35f, (0f - height) * 0.36f, -15f - p.Arms * 4f);
			DrawArm(g, width * 0.35f, (0f - height) * 0.36f, 15f + p.Arms * 4f);
			break;
		}
		case "dog":
		{
			using (Pen pen6 = new Pen(body.Color, 10f))
			{
				pen6.EndCap = LineCap.Round;
				g.DrawLine(pen6, width * 0.35f, -17f, width * 0.66f, -31f + (reduced ? 0f : (MathF.Sin(num * 8f) * 8f)));
			}
			DrawFoot(g, (0f - width) * 0.23f, gait * move * 5f, c.Facing, move);
			DrawFoot(g, width * 0.23f, (0f - gait) * move * 5f, c.Facing, move);
			Oval(body, (0f - width) * 0.43f, (0f - height) * 0.74f, width * 0.86f, height * 0.7f, stroke: true);
			Oval(body, (0f - width) * 0.5f, 0f - height, width, height * 0.61f, stroke: true);
			Oval(ink, (0f - width) * 0.65f, (0f - height) * 0.95f, width * 0.25f, height * 0.49f);
			Oval(ink, width * 0.4f, (0f - height) * 0.95f, width * 0.25f, height * 0.49f);
			Oval(belly, (0f - width) * 0.31f, (0f - height) * 0.67f, width * 0.62f, height * 0.3f);
			Oval(ink, -5f, (0f - height) * 0.62f, 10f, 7f);
			using (Pen pen7 = new Pen(accent.Color, 5f))
			{
				g.DrawLine(pen7, (0f - width) * 0.33f, (0f - height) * 0.4f, width * 0.33f, (0f - height) * 0.4f);
			}
			Oval(accent, -4f, (0f - height) * 0.39f, 8f, 10f);
			break;
		}
		case "bird":
		{
			using (Pen pen3 = new Pen(ink.Color, 2.4f))
			{
				for (int k = -1; k <= 1; k += 2)
				{
					float num3 = k * 12;
					g.DrawLine(pen3, num3, -12f, num3, 1f + gait * (float)k * move * 3f);
					g.DrawLine(pen3, num3 - 5f, 1f, num3 + 5f, 1f);
				}
			}
			Shape(body, new PointF[3]
			{
				new PointF((0f - width) * 0.32f, -17f),
				new PointF((0f - width) * 0.76f, -28f),
				new PointF((0f - width) * 0.58f, -5f)
			});
			Oval(body, (0f - width) * 0.47f, (0f - height) * 0.87f, width * 0.94f, height * 0.81f, stroke: true);
			Oval(belly, (0f - width) * 0.25f, (0f - height) * 0.48f, width * 0.6f, height * 0.34f);
			GraphicsState gstate2 = g.Save();
			g.TranslateTransform((0f - width) * 0.32f, (0f - height) * 0.43f);
			g.RotateTransform(-20f - (reduced ? 0f : (gait * (float)((a.State == Motion.Dancing) ? 35 : 8))));
			Oval(ink, -12f, -5f, 19f, height * 0.34f);
			g.Restore(gstate2);
			Shape(accent, new PointF[3]
			{
				new PointF(-4f, (0f - height) * 0.59f),
				new PointF(13f, (0f - height) * 0.55f),
				new PointF(-4f, (0f - height) * 0.49f)
			});
			using (Pen pen4 = new Pen(body.Color, 5f))
			{
				pen4.EndCap = LineCap.Round;
				g.DrawLine(pen4, 0f, (0f - height) * 0.86f, 5f, (0f - height) * 1.03f);
				g.DrawLine(pen4, -4f, (0f - height) * 0.87f, -5f, 0f - height);
			}
			break;
		}
		case "octopus":
		{
			for (int l = 0; l < 8; l++)
			{
				float num4 = ((float)l - 3.5f) * width * 0.11f;
				using Pen pen5 = new Pen(body.Color, 9f)
				{
					StartCap = LineCap.Round,
					EndCap = LineCap.Round,
					LineJoin = LineJoin.Round
				};
				float num5 = (reduced ? 0f : (MathF.Sin(num * 3f + (float)l) * 7f));
				g.DrawBezier(pen5, num4, -24f, num4 * 1.6f, -9f + num5, num4 * 1.4f + 5f, -5f, num4 * 1.9f + num5, -8f);
				Oval(belly, num4 * 1.9f + num5 - 2f, -10f, 4f, 3f);
			}
			Oval(body, (0f - width) * 0.49f, 0f - height, width * 0.98f, height * 0.8f, stroke: true);
			Oval(belly, (0f - width) * 0.3f, (0f - height) * 0.47f, width * 0.6f, height * 0.2f);
			break;
		}
		case "rabbit":
			Oval(belly, width * 0.28f, -22f, 23f, 22f, stroke: true);
			DrawEar(g, (0f - width) * 0.2f, (0f - height) * 0.73f, -7f + p.Ears * 20f, character.EarLength);
			DrawEar(g, width * 0.2f, (0f - height) * 0.73f, 13f - p.Ears * 20f, character.EarLength);
			Oval(body, (0f - width) * 0.43f, (0f - height) * 0.64f, width * 0.86f, height * 0.6f, stroke: true);
			Oval(body, (0f - width) * 0.48f, (0f - height) * 0.87f, width * 0.96f, height * 0.55f, stroke: true);
			Paw(g, belly, (0f - width) * 0.21f, -9f, width * 0.34f, 13f, c.Facing);
			Paw(g, belly, width * 0.21f, -9f, width * 0.34f, 13f, c.Facing);
			Oval(accent, -4f, (0f - height) * 0.49f, 8f, 5f);
			DrawArm(g, (0f - width) * 0.33f, (0f - height) * 0.35f, (0f - p.Arms) * 5f);
			DrawArm(g, width * 0.33f, (0f - height) * 0.35f, p.Arms * 5f);
			break;
		case "penguin":
		{
			Paw(g, accent, (0f - width) * 0.205f, -8f, width * 0.35f, 12f, c.Facing);
			Paw(g, accent, width * 0.195f, -8f, width * 0.35f, 12f, c.Facing);
			Oval(body, (0f - width) * 0.48f, 0f - height, width * 0.96f, height * 0.97f, stroke: true);
			Oval(belly, (0f - width) * 0.35f, (0f - height) * 0.82f, width * 0.7f, height * 0.72f);
			for (int i = -1; i <= 1; i += 2)
			{
				GraphicsState gstate = g.Save();
				g.TranslateTransform((float)i * width * 0.4f, (0f - height) * 0.5f);
				g.RotateTransform((float)i * (-20f - p.Arms * 4f) + (reduced ? 0f : (gait * 8f)));
				Oval(body, -7f, 0f, 14f, height * 0.37f);
				g.Restore(gstate);
			}
			Shape(accent, new PointF[3]
			{
				new PointF(-6f, (0f - height) * 0.51f),
				new PointF(6f, (0f - height) * 0.51f),
				new PointF(0f, (0f - height) * 0.43f)
			});
			break;
		}
		}
		bool flag;
		switch (character.Species)
		{
		case "dog":
		case "bird":
		case "octopus":
		case "penguin":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag && handLocal.HasValue)
		{
			DrawArm(g, (float)Math.Sign(handLocal.Value.X) * width * 0.32f, (0f - height) * 0.39f, p.Arms);
		}
		float num6 = character.Species switch
		{
			"penguin" => -0.62f, 
			"bird" => -0.69f, 
			"rabbit" => -0.61f, 
			"cat" => -0.66f, 
			_ => -0.74f, 
		};
		float open = ((a.State == Motion.Sleeping || a.State == Motion.Petted) ? 0.1f : p.Eyes);
		if (!reduced && MathF.Sin(num * 0.91f) > 0.994f)
		{
			open = 0.1f;
		}
		Oval(blush, (0f - width) * 0.32f, height * num6 + 6f, 9f, 5f);
		Oval(blush, width * 0.18f, height * num6 + 6f, 9f, 5f);
		Eye(g, (0f - width) * 0.16f + num2, height * num6, open);
		Eye(g, width * 0.16f + num2, height * num6, open);
		switch (character.Species)
		{
		case "cat":
		case "rabbit":
		case "octopus":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			g.DrawArc(outline, -4f, height * num6 + (float)((c.Disappointment > 0.3f) ? 14 : 10), 8f, 6f, (c.Disappointment > 0.3f) ? 180 : 0, 180f);
		}
		if (!music || c.Activity != Activity.Dance)
		{
			return;
		}
		if (character.MusicProp == "headphones")
		{
			using (Pen pen8 = new Pen(ink.Color, 4f))
			{
				g.DrawArc(pen8, (0f - width) * 0.55f, (0f - height) * 1.04f, width * 1.1f, height * 0.65f, 180f, 180f);
				Oval(ink, (0f - width) * 0.57f, (0f - height) * 0.84f, 9f, 21f);
				Oval(ink, width * 0.43f, (0f - height) * 0.84f, 9f, 21f);
				return;
			}
		}
		if (character.MusicProp == "cd")
		{
			g.FillRoundedRectangle(ink, new RectangleF(-23f, -29f, 46f, 30f), 6f);
			Oval(belly, -13f, -26f, 26f, 26f);
			Oval(accent, -4f, -17f, 8f, 8f);
			GraphicsState gstate3 = g.Save();
			g.TranslateTransform(0f, -13f);
			g.RotateTransform(reduced ? 0f : (num * 65f));
			using Pen pen9 = new Pen(Color.FromArgb(125, 255, 255, 255), 3f);
			g.DrawArc(pen9, -10, -10, 20, 20, 15, 90);
			g.Restore(gstate3);
			return;
		}
		g.FillRoundedRectangle(ink, new RectangleF(-22f, -27f, 44f, 24f), 4f);
		g.FillRoundedRectangle(belly, new RectangleF(-18f, -23f, 36f, 16f), 2f);
		if (character.MusicProp == "turntable")
		{
			Oval(ink, -15f, -22f, 15f, 15f);
			Oval(ink, 3f, -22f, 15f, 15f);
			Oval(accent, -10f, -17f, 5f, 5f);
			Oval(accent, 8f, -17f, 5f, 5f);
		}
		else if (character.MusicProp == "cassette")
		{
			g.DrawRectangle(outline, -14, -20, 28, 10);
			Oval(ink, -11f, -18f, 6f, 6f);
			Oval(ink, 5f, -18f, 6f, 6f);
			g.DrawLine(outline, -5, -15, 5, -15);
		}
		else
		{
			Oval(ink, -15f, -20f, 12f, 12f);
			g.DrawRectangle(outline, 2, -19, 11, 5);
			g.DrawLine(outline, 14, -27, 23, -43);
		}
		void Oval(Brush b, float x, float y, float width2, float height2, bool stroke = false)
		{
			g.FillEllipse((b == body) ? coat : b, x, y, width2, height2);
			if (stroke)
			{
				g.DrawEllipse(outline, x, y, width2, height2);
			}
		}
		void Shape(Brush b, PointF[] points)
		{
			g.FillPolygon((b == body) ? coat : b, points);
			g.DrawPolygon(outline, points);
		}
	}

	private void DrawWearables(Graphics g, float w, float h, bool hat, bool glasses)
	{
		if (hat)
		{
			g.FillRoundedRectangle(accent, new RectangleF(-18f, 0f - h - 11f, 36f, 13f), 4f);
			g.FillRoundedRectangle(ink, new RectangleF(-25f, 0f - h - 2f, 50f, 5f), 2f);
		}
		if (glasses)
		{
			using (Pen pen = new Pen(ink.Color, 1.6f))
			{
				float num = (0f - h) * 0.65f;
				g.DrawEllipse(pen, (0f - w) * 0.17f - 7f, num - 7f, 14f, 13f);
				g.DrawEllipse(pen, w * 0.17f - 7f, num - 7f, 14f, 13f);
				g.DrawLine(pen, (0f - w) * 0.17f + 7f, num - 1f, w * 0.17f - 7f, num - 1f);
			}
		}
	}
}
