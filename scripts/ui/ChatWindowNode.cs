using Godot;

namespace Tts.Ui;

public partial class ChatWindowNode : Control
{
	private Label _line1 = null!;
	private Label _line2 = null!;
	private Label _line3 = null!;

	public override void _Ready()
	{
		_line1 = GetNode<Label>("%Line1");
		_line2 = GetNode<Label>("%Line2");
		_line3 = GetNode<Label>("%Line3");
	}

	public void PostMessage(string npc, string message)
	{
		_line1.Text = _line2.Text;
		_line2.Text = _line3.Text;
		_line3.Text = $"[{npc}]: {message}";
	}
}
