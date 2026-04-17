using Godot;

namespace Tts;

internal sealed partial class LevelUi : Node
{
	internal ColorRect FadeOverlay { get; private set; } = null!;
	internal SelectionPanel SelectionPanel { get; private set; } = null!;
	internal AiSystemPanel AiSystemPanel { get; private set; } = null!;
	internal NotificationPanel NotificationPanel { get; private set; } = null!;
	internal SystemActionMenu SystemActionMenu { get; private set; } = null!;
	internal ScenarioPanel ScenarioPanel { get; private set; } = null!;
	internal ChatWindowNode? ChatWindow { get; private set; }

	public override void _Ready()
	{
		FadeOverlay = GetNode<ColorRect>("%FadeOverlay");
		SelectionPanel = GetNode<SelectionPanel>("%SelectionPanel");
		AiSystemPanel = GetNode<AiSystemPanel>("%AiSystemPanel");
		NotificationPanel = GetNode<NotificationPanel>("%NotificationPanel");
		SystemActionMenu = GetNode<SystemActionMenu>("%SystemActionMenu");
		ScenarioPanel = GetNode<ScenarioPanel>("%ScenarioPanel");
		ChatWindow = GetNode<ChatWindowNode>("%ChatWindowNode");

		SelectionPanel.Hide();
		AiSystemPanel.Hide();
		NotificationPanel.Hide();
		SystemActionMenu.Hide();
		ScenarioPanel.Hide();
	}

	internal void Initialize(CameraController camera, float systemRadius, ActionMenuConfig cfg)
	{
		SystemActionMenu.Initialize(camera, systemRadius, cfg);
	}

	internal void HideForGameEnd()
	{
		SelectionPanel.Hide();
		AiSystemPanel.Hide();
		SystemActionMenu.HideAll();
	}
}
