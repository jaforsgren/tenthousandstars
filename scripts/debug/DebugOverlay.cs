using Godot;
using System.Collections.Generic;

namespace Tts;

public partial class DebugOverlay : CanvasLayer
{
	private const int MaxLines = 12;
	private const Key ToggleKey = Key.N;
	private const float PanelWidth = 230f;
	private const float PanelPad = 8f;

	private static DebugOverlay? _instance;

	private Panel _panel = null!;
	private Label _label = null!;
	private readonly Queue<string> _lines = new();

	public static void Log(string message) => _instance?.AppendLine(message);

	public override void _Ready()
	{
		_instance = this;
		Layer = 99;

		var viewportSize = GetViewport().GetVisibleRect().Size;
		var panelHeight = MaxLines * 14f + 12f;

		_panel = new Panel
		{
			Position = new Vector2(PanelPad, viewportSize.Y - panelHeight - PanelPad),
			Size = new Vector2(PanelWidth, panelHeight),
			Visible = false,
			Modulate = new Color(1f, 1f, 1f, 0.85f)
		};

		_label = new Label
		{
			Position = new Vector2(6f, 4f),
			Size = new Vector2(PanelWidth - 12f, panelHeight - 8f),
			AutowrapMode = TextServer.AutowrapMode.Off,
			LabelSettings = new LabelSettings { FontSize = 11 }
		};

		_panel.AddChild(_label);
		AddChild(_panel);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true, Echo: false } key && key.Keycode == ToggleKey)
		{
			_panel.Visible = !_panel.Visible;
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _ExitTree()
	{
		if (_instance == this)
			_instance = null;
	}

	private void AppendLine(string message)
	{
		_lines.Enqueue(message);
		while (_lines.Count > MaxLines)
			_lines.Dequeue();
		_label.Text = string.Join("\n", _lines);
	}
}
