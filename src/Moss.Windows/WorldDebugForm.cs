using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Moss.Core;

namespace Moss.Windows;

internal sealed class WorldDebugForm : Form
{
	private readonly PetApplication app;

	private readonly Timer redraw = new Timer
	{
		Interval = 200
	};

	public WorldDebugForm(PetApplication owner)
	{
		app = owner;
		Text = "Moss · World inspector";
		base.Size = new Size(850, 540);
		DoubleBuffered = true;
		BackColor = Color.FromArgb(24, 31, 30);
		redraw.Tick += delegate
		{
			Invalidate();
		};
		redraw.Start();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		World world = app.Observer.World;
		if (world.Displays.Count == 0)
		{
			return;
		}
		Graphics graphics = e.Graphics;
		float minX = world.Displays.Min((Display d) => d.Bounds.X);
		float minY = world.Displays.Min((Display d) => d.Bounds.Y);
		float num = world.Displays.Max((Display d) => d.Bounds.Right);
		float num2 = world.Displays.Max((Display d) => d.Bounds.Bottom);
		float s = Math.Min((float)(base.ClientSize.Width - 40) / (num - minX), (float)(base.ClientSize.Height - 130) / (num2 - minY));
		using (Pen pen = new Pen(Color.FromArgb(109, 164, 139)))
		{
			using Pen pen2 = new Pen(Color.FromArgb(64, 85, 78));
			using Pen pen3 = new Pen(Color.FromArgb(223, 193, 118), 2f);
			foreach (Display display in world.Displays)
			{
				PointF pointF = P(display.Bounds.X, display.Bounds.Y);
				graphics.DrawRectangle(pen, pointF.X, pointF.Y, display.Bounds.W * s, display.Bounds.H * s);
				pointF = P(display.Work.X, display.Work.Y);
				graphics.DrawRectangle(pen2, pointF.X, pointF.Y, display.Work.W * s, display.Work.H * s);
			}
			foreach (Surface surface in world.Surfaces)
			{
				graphics.DrawLine(pen3, P(surface.Left, surface.Y), P(surface.Right, surface.Y));
			}
			Creature creature = app.Creature;
			PointF pt = P(creature.Position.X, creature.Position.Y);
			graphics.FillEllipse(Brushes.Coral, pt.X - 5f, pt.Y - 5f, 10f, 10f);
			using Pen pen4 = new Pen(Color.Coral)
			{
				DashStyle = DashStyle.Dash
			};
			graphics.DrawLine(pen4, pt, P(creature.TargetX, creature.Position.Y));
			PointF pointF2 = P(world.Cursor.X, world.Cursor.Y);
			graphics.DrawLine(Pens.White, pointF2.X - 4f, pointF2.Y, pointF2.X + 4f, pointF2.Y);
			graphics.DrawLine(Pens.White, pointF2.X, pointF2.Y - 4f, pointF2.X, pointF2.Y + 4f);
			string s2 = $"Position {creature.Position}  Velocity {creature.Velocity}  Target X {creature.TargetX:0}\n{creature.Activity} / {creature.Motion}  |  {world.Surfaces.Count} surfaces  |  {world.Displays.Count} displays\n" + string.Join(" · ", from ev in app.Events.Recent.TakeLast(4)
				select ev.Kind);
			graphics.DrawString(s2, Font, Brushes.White, new RectangleF(20f, base.ClientSize.Height - 95, base.ClientSize.Width - 40, 90f));
		}
		PointF P(float x, float y)
		{
			return new PointF(20f + (x - minX) * s, 20f + (y - minY) * s);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			redraw.Dispose();
		}
		base.Dispose(disposing);
	}
}
