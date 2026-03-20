using Godot;

namespace Tts;

public partial class AiFleetNode : FleetNodeBase
{
	private Color _fill;
	private Color _outline;
	private Label _idLabel = null!;

	private const int CountFontSize = 9;
	private const int IdFontSize = 7;
	private const float IdLabelOffsetBelowCircle = 2f;
	private const float IdLabelHeight = 10f;

	// No background sprite — color is driven entirely by disposition
	protected override string? VisualScenePath => null;

	public void Initialize(float systemRadius, float gap, float radius, float labelWidth, float labelHeight, float outlineWidth, Color dispositionColor, AiPlayerData aiPlayer)
	{
		_fill = new Color(dispositionColor.R, dispositionColor.G, dispositionColor.B, 0.3f);
		_outline = dispositionColor;

		var index = (int)aiPlayer.Owner - (int)SystemOwner.Ai1 + 1;
		var idText = $"AI{index} {aiPlayer.Name}";

		BaseInitialize(systemRadius, gap, radius, labelWidth, labelHeight, outlineWidth, CountFontSize);

		_idLabel = new Label
		{
			Position = new Vector2(-labelWidth / 2f, radius + IdLabelOffsetBelowCircle),
			Size = new Vector2(labelWidth, IdLabelHeight),
			HorizontalAlignment = HorizontalAlignment.Center,
			Text = idText,
			Visible = false
		};
		_idLabel.AddThemeColorOverride("font_color", _outline);
		_idLabel.AddThemeFontSizeOverride("font_size", IdFontSize);
		AddChild(_idLabel);
	}

	public override void UpdateFleet(float ships, bool selected)
	{
		base.UpdateFleet(ships, selected);
		_idLabel.Visible = ships > 0;
	}

	public override void _Draw() => DrawFleetCircle(_fill, _outline);
}
