using Godot;
using Tts.Level;

namespace Tts.Fleet;

public partial class AiFleetNode : FleetNodeBase
{
	private Label _factionLabel = null!;

	public override void _Ready()
	{
		base._Ready();
		_factionLabel = GetNode<Label>("%FactionLabel");
	}

	private static readonly Color AiFill = new(0.5f, 0.5f, 0.5f, 0.9f);

	public void Initialize(float systemRadius, float gap, Color dispositionColor, AiPlayerData aiPlayer)
	{
		BaseInitialize(systemRadius, gap, AiFill);
		_factionLabel.Text = aiPlayer.FactionName;
		_factionLabel.AddThemeColorOverride("font_color", dispositionColor);
	}

	public override void UpdateFleet(float ships)
	{
		base.UpdateFleet(ships);
		_factionLabel.Visible = ships > 0;
	}
}
