using System;
using System.Collections.Generic;

namespace Tts.Utils;

public static class GraphUtils
{
	public static Dictionary<int, List<int>> BuildAdjacency(int nodeCount, IEnumerable<(int, int)> edges)
	{
		var adj = new Dictionary<int, List<int>>(nodeCount);
		for (var i = 0; i < nodeCount; i++)
			adj[i] = [];
		foreach (var (from, to) in edges)
		{
			adj[from].Add(to);
			adj[to].Add(from);
		}
		return adj;
	}

	public static List<int>? FindPath(
		int from, int to,
		Dictionary<int, List<int>> adjacency,
		Func<int, bool> canPassThrough)
	{
		if (from == to) return null;
		var parent = new Dictionary<int, int> { [from] = -1 };
		var queue = new Queue<int>();
		queue.Enqueue(from);
		while (queue.Count > 0)
		{
			var current = queue.Dequeue();
			if (current == to)
			{
				var path = new List<int>();
				var node = to;
				while (node != -1) { path.Add(node); node = parent[node]; }
				path.Reverse();
				return path;
			}
			if (!adjacency.TryGetValue(current, out var neighbors)) continue;
			foreach (var next in neighbors)
			{
				if (parent.ContainsKey(next)) continue;
				if (next != to && !canPassThrough(next)) continue;
				parent[next] = current;
				queue.Enqueue(next);
			}
		}
		return null;
	}

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
