using System;
using System.Collections.Generic;

namespace Moss.Core;

public sealed class EventHub
{
	private readonly Queue<WorldEvent> recent = new Queue<WorldEvent>();

	public IReadOnlyCollection<WorldEvent> Recent => recent;

	public event Action<WorldEvent>? Received;

	public void Publish(string kind, double time)
	{
		WorldEvent worldEvent = new WorldEvent(kind, time);
		recent.Enqueue(worldEvent);
		while (recent.Count > 100)
		{
			recent.Dequeue();
		}
		this.Received?.Invoke(worldEvent);
	}
}
