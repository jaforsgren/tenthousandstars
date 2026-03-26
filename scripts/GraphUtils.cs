using System;
using System.Collections.Generic;

namespace Tts;

public static class GraphUtils
{
	public static int[] BfsHopDistances(int startIndex, int nodeCount, IEnumerable<(int From, int To)> edges)
	{
		var distances = new int[nodeCount];
		Array.Fill(distances, -1);
		distances[startIndex] = 0;

		var queue = new Queue<int>();
		queue.Enqueue(startIndex);

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();
			foreach (var (from, to) in edges)
			{
				var neighbor = from == current ? to : to == current ? from : -1;
				if (neighbor < 0 || distances[neighbor] >= 0) continue;
				distances[neighbor] = distances[current] + 1;
				queue.Enqueue(neighbor);
			}
		}

		return distances;
	}
}
