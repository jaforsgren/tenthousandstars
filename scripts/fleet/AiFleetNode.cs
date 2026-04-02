using Godot;

namespace Tts;

public partial class AiFleetNode : FleetNodeBase
{
	private Label _factionLabel = null!;

	public override void _Ready()
	{
		base._Ready();
		_factionLabel = GetNode<Label>("%FactionLabel");
	}

	public void Initialize(float systemRadius, float gap, Color dispositionColor, AiPlayerData aiPlayer)
	{
		BaseInitialize(systemRadius, gap, dispositionColor with { A = 0.3f }, dispositionColor);
		_factionLabel.Text = aiPlayer.FactionName;
		_factionLabel.AddThemeColorOverride("font_color", dispositionColor);
	}

	public override void UpdateFleet(float ships, bool selected)
	{
		base.UpdateFleet(ships, selected);
		_factionLabel.Visible = ships > 0;
	}
}
