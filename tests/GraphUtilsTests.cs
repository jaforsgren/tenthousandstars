using System.Collections.Generic;
using Tts.Utils;
using Xunit;

namespace Tts.Tests;

public class GraphUtilsTests
{
	[Fact]
	public void BuildAdjacency_AddsBothDirections()
	{
		var adj = GraphUtils.BuildAdjacency(3, [(0, 1), (1, 2)]);
		Assert.Equal([1], adj[0]);
		Assert.Equal([0, 2], adj[1]);
		Assert.Equal([1], adj[2]);
	}

	[Fact]
	public void BuildAdjacency_IncludesIsolatedNodes()
	{
		var adj = GraphUtils.BuildAdjacency(2, []);
		Assert.Empty(adj[0]);
		Assert.Empty(adj[1]);
	}

	[Fact]
	public void FindPath_ReturnsShortestPath()
	{
		var adj = GraphUtils.BuildAdjacency(4, [(0, 1), (1, 2), (2, 3), (0, 3)]);
		var path = GraphUtils.FindPath(0, 3, adj, _ => true);
		Assert.NotNull(path);
		Assert.Equal([0, 3], path);
	}

	[Fact]
	public void FindPath_RespectsCanPassThrough()
	{
		var adj = GraphUtils.BuildAdjacency(3, [(0, 1), (1, 2)]);
		var path = GraphUtils.FindPath(0, 2, adj, i => i != 1);
		Assert.Null(path);
	}

	[Fact]
	public void FindPath_SameNode_ReturnsNull()
	{
		var adj = GraphUtils.BuildAdjacency(1, []);
		Assert.Null(GraphUtils.FindPath(0, 0, adj, _ => true));
	}

	[Fact]
	public void FindPath_NoRoute_ReturnsNull()
	{
		var adj = GraphUtils.BuildAdjacency(2, []);
		Assert.Null(GraphUtils.FindPath(0, 1, adj, _ => true));
	}

	[Fact]
	public void BfsHopDistances_FromStart()
	{
		var distances = GraphUtils.BfsHopDistances(0, 4, [(0, 1), (1, 2), (0, 3)]);
		Assert.Equal(0, distances[0]);
		Assert.Equal(1, distances[1]);
		Assert.Equal(2, distances[2]);
		Assert.Equal(1, distances[3]);
	}

	[Fact]
	public void NormalizedEdge_OrdersAscending()
	{
		Assert.Equal((2, 5), GraphUtils.NormalizedEdge(5, 2));
		Assert.Equal((2, 5), GraphUtils.NormalizedEdge(2, 5));
	}

	[Fact]
	public void NormalizedEdge_EqualEndpoints_Unchanged()
	{
		Assert.Equal((4, 4), GraphUtils.NormalizedEdge(4, 4));
	}
}
