using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Moss.Core;

public sealed class World
{
	public List<Display> Displays { get; set; } = new List<Display>();

	public List<Surface> Surfaces { get; set; } = new List<Surface>();

	public List<WindowInfo> Windows { get; set; } = new List<WindowInfo>();

	public Vector2 Cursor { get; set; }

	public bool Fullscreen { get; set; }

	public bool Presenting { get; set; }

	public long Foreground { get; set; }

	public long Revision { get; set; }

	public Display? Nearest(Vector2 p)
	{
		return Displays.MinBy((Display d) => d.Bounds.DistanceSquared(p));
	}

	public float ScaleAt(Vector2 p)
	{
		return Nearest(p)?.Scale ?? 1f;
	}

	public static List<(float Left, float Right)> Subtract(float left, float right, IEnumerable<(float Left, float Right)> blockers)
	{
		List<(float, float)> list = new List<(float, float)> { (left, right) };
		foreach (var blocker in blockers)
		{
			List<(float, float)> list2 = new List<(float, float)>();
			foreach (var item in list)
			{
				if (blocker.Right <= item.Item1 || blocker.Left >= item.Item2)
				{
					list2.Add(item);
					continue;
				}
				if (blocker.Left > item.Item1)
				{
					list2.Add((item.Item1, blocker.Left));
				}
				if (blocker.Right < item.Item2)
				{
					list2.Add((blocker.Right, item.Item2));
				}
			}
			list = list2;
		}
		return list;
	}
}
