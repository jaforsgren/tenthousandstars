using Godot;

namespace Tts;

public partial class AiFleetNode : FleetNodeBase
{
	private Label _idLabel = null!;

	private const string AiVisualScenePath = "res://scenes/fleet/AiFleetNode.tscn";
	private const int CountFontSize = 9;
	private const int IdFontSize = 7;
	private const float IdLabelOffsetBelowCircle = 2f;
	private const float IdLabelHeight = 10f;

	protected override string? VisualScenePath => AiVisualScenePath;

	public void Initialize(float systemRadius, float gap, float radius, float labelWidth, float labelHeight, float outlineWidth, Color dispositionColor, AiPlayerData aiPlayer)
	{
		BaseInitialize(systemRadius, gap, radius, labelWidth, labelHeight, CountFontSize);

		if (GetIconSprite() is Sprite2D sprite)
			sprite.Modulate = dispositionColor;

		_idLabel = new Label
		{
			Position = new Vector2(-labelWidth / 2f, radius + IdLabelOffsetBelowCircle),
			Size = new Vector2(labelWidth, IdLabelHeight),
			HorizontalAlignment = HorizontalAlignment.Center,
			Text = aiPlayer.FactionName,
			Visible = false
		};
		_idLabel.AddThemeColorOverride("font_color", dispositionColor);
		_idLabel.AddThemeFontSizeOverride("font_size", IdFontSize);
		AddChild(_idLabel);
	}

	public override void UpdateFleet(float ships, bool selected)
	{
		base.UpdateFleet(ships, selected);
		_idLabel.Visible = ships > 0;
	}
}
