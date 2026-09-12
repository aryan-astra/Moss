using System.Drawing;
using System.Drawing.Drawing2D;

namespace Moss.Windows;

internal static class DrawingExtensions
{
	public static void FillRoundedRectangle(this Graphics g, Brush brush, RectangleF r, float radius)
	{
		using GraphicsPath graphicsPath = new GraphicsPath();
		float num = radius * 2f;
		graphicsPath.AddArc(r.X, r.Y, num, num, 180f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
		graphicsPath.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
		graphicsPath.CloseFigure();
		g.FillPath(brush, graphicsPath);
	}
}
