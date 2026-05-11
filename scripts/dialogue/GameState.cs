#nullable enable

using System;
using System.Collections.Generic;
using Godot;

namespace Tts.Dialogue;

/// <summary>
/// Central persistent state for skills, flags, and history.
/// Add as a child of DialogueRoot or register as an autoload.
///
/// Access via <see cref="Instance"/> after the node is ready.
/// </summary>
public partial class GameState : Node
{
	public static GameState? Instance { get; private set; }

	/// <summary>Player skill values keyed by lowercase name.</summary>
	public Dictionary<string, int> Skills { get; } = new()
	{
		["logic"] = 3,
		["empathy"] = 4,
		["perception"] = 2,
		["authority"] = 1,
		["endurance"] = 2,
	};

	/// <summary>Arbitrary flags set by gameplay and Yarn commands.</summary>
	public Dictionary<string, Variant> Flags { get; } = new();

	/// <summary>Timestamped log of notable events.</summary>
	public List<string> History { get; } = new();

	public override void _Ready()
	{
		Instance = this;
	}

	public int GetSkill(string name) =>
		Skills.TryGetValue(name.ToLowerInvariant(), out int v) ? v : 0;

	public void SetSkill(string name, int value) =>
		Skills[name.ToLowerInvariant()] = value;

	public void SetFlag(string key, Variant value) => Flags[key] = value;

	public Variant GetFlag(string key, Variant fallback = default) =>
		Flags.TryGetValue(key, out Variant v) ? v : fallback;

	public void AddHistory(string entry)
	{
		string timestamped = $"[{DateTime.Now:HH:mm:ss}] {entry}";
		History.Add(timestamped);
		GD.Print($"[GameState] {timestamped}");
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && Instance == this)
			Instance = null;
		base.Dispose(disposing);
	}
}
