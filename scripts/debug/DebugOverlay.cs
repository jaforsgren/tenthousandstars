using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tts.Debug;

public enum DebugCategory { General, Ai, Combat, Narrative }

public partial class DebugOverlay : CanvasLayer
{
	private const int MaxLines = 10;
	private const Key ToggleKey = Key.N;
	private const float PanelWidth = 270f;
	private const float PanelPad = 8f;
	private const float StatusBarHeight = 16f;
	private const float LineHeight = 14f;

	private static DebugOverlay? _instance;

	private Panel _panel = null!;
	private Label _statusLabel = null!;
	private Label _logLabel = null!;

	private readonly Queue<(DebugCategory Category, string Text)> _lines = new();
	private readonly Dictionary<DebugCategory, bool> _visible = new()
	{
		[DebugCategory.General]   = true,
		[DebugCategory.Ai]        = true,
		[DebugCategory.Combat]    = true,
		[DebugCategory.Narrative] = true,
	};

	private static readonly Dictionary<DebugCategory, Key> CategoryKeys = new()
	{
		[DebugCategory.Ai]        = Key.Key1,
		[DebugCategory.Combat]    = Key.Key2,
		[DebugCategory.Narrative] = Key.Key3,
	};

	private static readonly Dictionary<DebugCategory, string> CategoryPrefix = new()
	{
		[DebugCategory.General]   = "GEN",
		[DebugCategory.Ai]        = "AI ",
		[DebugCategory.Combat]    = "CMB",
		[DebugCategory.Narrative] = "NAR",
	};

	public static void Log(string message)          => _instance?.AppendLine(DebugCategory.General, message);
	public static void LogAi(string message)        => _instance?.AppendLine(DebugCategory.Ai, message);
	public static void LogCombat(string message)    => _instance?.AppendLine(DebugCategory.Combat, message);
	public static void LogNarrative(string message) => _instance?.AppendLine(DebugCategory.Narrative, message);

	public static void ShowHelp()
	{
		if (_instance == null) return;
		_instance._panel.Visible = true;
		_instance.AppendLine(DebugCategory.General, "N=overlay  1=AI  2=CMB  3=NAR");
		_instance.AppendLine(DebugCategory.General, "F2=player  F3=fog  F4=story  F5=AI state");
	}

	public override void _Ready()
	{
		_instance = this;
		Layer = 99;

		var viewportSize = GetViewport().GetVisibleRect().Size;
		var logHeight = MaxLines * LineHeight;
		var panelHeight = StatusBarHeight + logHeight + 12f;

		_panel = new Panel
		{
			Position = new Vector2(PanelPad, viewportSize.Y - panelHeight - PanelPad),
			Size = new Vector2(PanelWidth, panelHeight),
			Visible = false,
			Modulate = new Color(1f, 1f, 1f, 0.85f)
		};

		_statusLabel = new Label
		{
			Position = new Vector2(6f, 2f),
			Size = new Vector2(PanelWidth - 12f, StatusBarHeight),
			LabelSettings = new LabelSettings { FontSize = 9 }
		};

		_logLabel = new Label
		{
			Position = new Vector2(6f, StatusBarHeight + 4f),
			Size = new Vector2(PanelWidth - 12f, logHeight),
			AutowrapMode = TextServer.AutowrapMode.Off,
			LabelSettings = new LabelSettings { FontSize = 11 }
		};

		_panel.AddChild(_statusLabel);
		_panel.AddChild(_logLabel);
		AddChild(_panel);

		RefreshDisplay();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

		if (key.Keycode == ToggleKey)
		{
			_panel.Visible = !_panel.Visible;
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!_panel.Visible) return;

		foreach (var (category, toggleKey) in CategoryKeys)
		{
			if (key.Keycode != toggleKey) continue;
			_visible[category] = !_visible[category];
			RefreshDisplay();
			GetViewport().SetInputAsHandled();
			return;
		}
	}

	public override void _ExitTree()
	{
		if (_instance == this)
			_instance = null;
	}

	private void AppendLine(DebugCategory category, string message)
	{
		var timestamp = DateTime.Now.ToString("HH:mm:ss");
		_lines.Enqueue((category, $"{timestamp} {CategoryPrefix[category]} {message}"));
		while (_lines.Count > MaxLines)
			_lines.Dequeue();
		RefreshDisplay();
	}

	private void RefreshDisplay()
	{
		if (_statusLabel == null) return;
		_statusLabel.Text = BuildStatusLine();
		_logLabel.Text = string.Join("\n",
			_lines.Where(l => _visible[l.Category]).Select(l => l.Text));
	}

	private string BuildStatusLine()
	{
		var parts = new List<string> { "N:log" };
		foreach (var (category, toggleKey) in CategoryKeys)
		{
			var keyNum = toggleKey.ToString().Replace("Key", "");
			var label = CategoryPrefix[category].TrimEnd();
			parts.Add(_visible[category] ? $"{keyNum}:{label}" : $"{keyNum}:---");
		}
		return string.Join("  ", parts);
	}
}
