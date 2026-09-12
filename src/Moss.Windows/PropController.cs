using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Numerics;
using Moss.Core;

namespace Moss.Windows;

internal sealed class PropController : IDisposable
{
	private readonly PetApplication app;

	private readonly PetOverlay toy = new PetOverlay(ownHotkey: false);

	private readonly PetOverlay book = new PetOverlay(ownHotkey: false);

	private readonly PetOverlay ballWindow = new PetOverlay(ownHotkey: false);

	private readonly Football ball = new Football();

	private readonly Random random = new Random();

	private static readonly PointF[] pentagon = (from i in Enumerable.Range(0, 5)
		select new PointF(MathF.Cos((float)i * ((float)Math.PI * 2f) / 5f) * 4f, MathF.Sin((float)i * ((float)Math.PI * 2f) / 5f) * 4f)).ToArray();

	private Bitmap? ballBitmap;

	private bool ballHeld;

	private Vector2 ballOffset;

	private float nextBall = 100f;

	private Bitmap? toyBitmap;

	private Bitmap? bookBitmap;

	private readonly PropBody body = new PropBody();

	private Vector2 grabOffset;

	private float paintedAngle = float.NaN;

	private Character? paintedCharacter;

	public bool AllowSurprises { get; set; }

	public bool Interacting
	{
		get
		{
			if (body.State != PropState.HeldByUser)
			{
				return ballHeld;
			}
			return true;
		}
	}

	public void RollFootball()
	{
		Display display = app.Observer.World.Nearest(app.Creature.Position);
		if (display != null)
		{
			ball.Roll(display, random.Next(2) == 0);
		}
	}

	public PropController(PetApplication owner)
	{
		app = owner;
		body.Position = app.Creature.Position + new Vector2(27f, -25f) * app.Creature.Scale;
		toy.GrabStarted += delegate(Vector2 p)
		{
			grabOffset = body.Position - p;
			body.Grab();
		};
		toy.Released += delegate
		{
			body.Release();
		};
		book.Clicked += delegate
		{
			app.OpenNotebook();
		};
		ballWindow.GrabStarted += delegate(Vector2 p)
		{
			ballHeld = true;
			ballOffset = ball.Position - p;
		};
		ballWindow.Released += delegate
		{
			ballHeld = false;
		};
		body.Lost += delegate
		{
			app.Events.Publish("object.lost", app.Creature.Time);
		};
		body.Recovered += delegate
		{
			app.Events.Publish("object.recovered", app.Creature.Time);
		};
	}

	public void Step(float dt, World w)
	{
		body.Step(dt, app.Creature, w, (body.State == PropState.HeldByUser) ? new Vector2?(PetOverlay.CursorPhysical + grabOffset) : ((Vector2?)null), app.Config.Advanced.GravityScale, app.Config.Advanced.ObjectGripStrength);
		if (AllowSurprises && app.Config.SurprisePlay && !app.Config.ReducedMotion)
		{
			nextBall -= dt;
			if (nextBall <= 0f)
			{
				if (!ball.Active && !Interacting)
				{
					RollFootball();
				}
				nextBall = app.Config.Advanced.SurpriseIntervalMinSec + (float)random.NextDouble() * Math.Max(1f, app.Config.Advanced.SurpriseIntervalMaxSec - app.Config.Advanced.SurpriseIntervalMinSec);
			}
		}
		ball.Step(dt, w, app.Creature, ballHeld ? new Vector2?(PetOverlay.CursorPhysical + ballOffset) : ((Vector2?)null), app.Config.Advanced.GravityScale, app.Config.Advanced.BounceScale);
	}

	public void StopFootball()
	{
		ball.Stop();
	}

	public string TwigState => body.State.ToString() + (body.Contested ? " (contested)" : "");

	public void TwigThrow(Vector2 velocity)
	{
		body.Velocity = velocity;
		body.Release();
		app.Events.Publish("object.thrown", app.Creature.Time);
	}

	public void TwigGrab()
	{
		body.Grab();
		app.Events.Publish("object.grabbed", app.Creature.Time);
	}

	public void TwigHome()
	{
		body.Release();
		body.Position = app.Creature.Position + new Vector2(60f, -40f) * app.Creature.Scale;
		body.Velocity = Vector2.Zero;
	}

	public void Draw(bool hidden)
	{
		if (hidden)
		{
			if (ballWindow.Visible)
			{
				ballWindow.Configure(interaction: false, app.Config.ExcludeFromCapture);
				ballWindow.Hide();
			}
			if (toy.Visible)
			{
				toy.Configure(interaction: false, app.Config.ExcludeFromCapture);
				toy.Hide();
			}
			if (book.Visible)
			{
				book.Configure(interaction: false, app.Config.ExcludeFromCapture);
				book.Hide();
			}
			return;
		}
		DrawFootball();
		toy.CheckCapture();
		book.CheckCapture();
		float scale = app.Creature.Scale;
		int num = (int)Math.Ceiling(48f * scale);
		bool flag = paintedCharacter != app.Character;
		if (toyBitmap == null || toyBitmap.Width != num)
		{
			toyBitmap?.Dispose();
			bookBitmap?.Dispose();
			toyBitmap = new Bitmap(num, num, PixelFormat.Format32bppPArgb);
			int num2 = (int)Math.Ceiling(36f * scale);
			bookBitmap = new Bitmap(num2, num2, PixelFormat.Format32bppPArgb);
			flag = true;
		}
		bool flag2 = flag || !float.IsFinite(paintedAngle) || Math.Abs(body.Angle - paintedAngle) > 0.015f;
		if (flag2)
		{
			using Graphics graphics = Graphics.FromImage(toyBitmap);
			graphics.Clear(Color.Transparent);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.ScaleTransform(scale, scale);
			graphics.TranslateTransform(24f, 24f);
			graphics.RotateTransform(body.Angle * 180f / (float)Math.PI);
			using SolidBrush brush = new SolidBrush(ColorTranslator.FromHtml(app.Character.Accent));
			using Pen pen = new Pen(Color.FromArgb(169, 128, 80), 6f)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			graphics.DrawLine(pen, -17, 0, 17, 0);
			graphics.DrawLine(pen, 4, 0, 10, -7);
			graphics.FillEllipse(brush, -19, -4, 8, 8);
			graphics.FillEllipse(brush, 13, -4, 8, 8);
			paintedAngle = body.Angle;
		}
		toy.Draw(toyBitmap, body.Position, scale, app.PetAlpha, new Vector2(24f, 24f), flag2);
		if (!toy.Visible)
		{
			toy.Configure(app.Config.Interaction, app.Config.ExcludeFromCapture);
			toy.Show();
		}
		Vector2 position = app.Creature.Position;
		Display display = app.Observer.World.Nearest(position);
		if (display == null)
		{
			return;
		}
		Vector2 feet = new Vector2(Math.Clamp(position.X + 62f * scale, display.Work.X + 20f * scale, display.Work.Right - 20f * scale), Math.Min(position.Y, display.Work.Bottom));
		if (flag)
		{
			using Graphics graphics2 = Graphics.FromImage(bookBitmap);
			graphics2.Clear(Color.Transparent);
			graphics2.SmoothingMode = SmoothingMode.AntiAlias;
			graphics2.ScaleTransform(scale, scale);
			using SolidBrush brush2 = new SolidBrush(ColorTranslator.FromHtml(app.Character.Body));
			using SolidBrush brush3 = new SolidBrush(ColorTranslator.FromHtml(app.Character.Paper));
			using Pen pen2 = new Pen(ColorTranslator.FromHtml(app.Character.Ink), 1.5f);
			graphics2.FillRoundedRectangle(brush2, new RectangleF(3f, 6f, 29f, 25f), 3f);
			graphics2.FillRectangle(brush3, 9, 9, 20, 18);
			graphics2.DrawLine(pen2, 8, 6, 8, 30);
			graphics2.DrawLine(pen2, 13, 15, 24, 15);
			graphics2.DrawLine(pen2, 13, 20, 22, 20);
		}
		book.Draw(bookBitmap, feet, scale, app.PetAlpha, new Vector2(18f, 33f), flag);
		paintedCharacter = app.Character;
		if (!book.Visible)
		{
			book.Configure(app.Config.Interaction, app.Config.ExcludeFromCapture);
			book.Show();
		}
	}

	private void DrawFootball()
	{
		if (!ball.Active)
		{
			ballWindow.Hide();
			return;
		}
		float num = app.Observer.World.ScaleAt(ball.Position);
		int num2 = (int)Math.Ceiling(30f * num);
		if (ballBitmap == null || ballBitmap.Width != num2)
		{
			ballBitmap?.Dispose();
			ballBitmap = new Bitmap(num2, num2, PixelFormat.Format32bppPArgb);
		}
		using (Graphics graphics = Graphics.FromImage(ballBitmap))
		{
			graphics.Clear(Color.Transparent);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.ScaleTransform(num, num);
			graphics.TranslateTransform(15f, 15f);
			graphics.RotateTransform(ball.Rotation * 180f / (float)Math.PI);
			graphics.FillEllipse(Brushes.Ivory, -11, -11, 22, 22);
			using Pen pen = new Pen(Color.FromArgb(48, 63, 54), 1.2f);
			graphics.DrawEllipse(pen, -11, -11, 22, 22);
			graphics.FillPolygon(Brushes.DarkSlateGray, pentagon);
			PointF[] array = pentagon;
			for (int i = 0; i < array.Length; i++)
			{
				PointF pt = array[i];
				graphics.DrawLine(pen, pt, new PointF(pt.X * 2.7f, pt.Y * 2.7f));
			}
		}
		ballWindow.Draw(ballBitmap, ball.Position, num, app.PetAlpha, new Vector2(15f, 15f));
		if (!ballWindow.Visible)
		{
			ballWindow.Configure(app.Config.Interaction, app.Config.ExcludeFromCapture);
			ballWindow.Show();
		}
		ballWindow.CheckCapture();
	}

	public void Configure()
	{
		if (ballWindow.IsHandleCreated)
		{
			ballWindow.Configure(app.Config.Interaction, app.Config.ExcludeFromCapture);
		}
		if (toy.IsHandleCreated)
		{
			toy.Configure(app.Config.Interaction, app.Config.ExcludeFromCapture);
		}
		if (book.IsHandleCreated)
		{
			book.Configure(app.Config.Interaction, app.Config.ExcludeFromCapture);
		}
	}

	public void Dispose()
	{
		toy.Dispose();
		book.Dispose();
		ballWindow.Dispose();
		ballBitmap?.Dispose();
		toyBitmap?.Dispose();
		bookBitmap?.Dispose();
	}
}
