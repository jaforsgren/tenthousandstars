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

	internal void Initialize(CameraController camera, float systemRadius, ActionMenuConfig cfg)
	{
		var viewportSize = GetViewport().GetVisibleRect().Size;
		SpawnFadeOverlay(viewportSize);
		SpawnSelectionPanel();
		SpawnAiSystemPanel();
		SpawnNotificationPanel();
		SpawnSystemActionMenu(camera, systemRadius, cfg);
		SpawnScenarioPanel();
		SpawnChatWindow(viewportSize);
	}

	internal void HideForGameEnd()
	{
		SelectionPanel.Hide();
		AiSystemPanel.Hide();
		SystemActionMenu.HideAll();
	}

	private void SpawnFadeOverlay(Vector2 viewportSize)
	{
		var layer = new CanvasLayer { Layer = 9 };
		AddChild(layer);
		FadeOverlay = new ColorRect
		{
			Color = Colors.Black,
			Modulate = new Color(1f, 1f, 1f, 0f),
			Size = viewportSize,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		layer.AddChild(FadeOverlay);
	}

	private void SpawnSelectionPanel()
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);
		SelectionPanel = GD.Load<PackedScene>("res://scenes/ui/SelectionPanel.tscn").Instantiate<SelectionPanel>();
		layer.AddChild(SelectionPanel);
	}

	private void SpawnAiSystemPanel()
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);
		AiSystemPanel = GD.Load<PackedScene>("res://scenes/ui/AiSystemPanel.tscn").Instantiate<AiSystemPanel>();
		layer.AddChild(AiSystemPanel);
	}

	private void SpawnNotificationPanel()
	{
		var layer = new CanvasLayer { Layer = 11 };
		AddChild(layer);
		NotificationPanel = GD.Load<PackedScene>("res://scenes/ui/NotificationPanel.tscn").Instantiate<NotificationPanel>();
		layer.AddChild(NotificationPanel);
	}

	private void SpawnSystemActionMenu(CameraController camera, float systemRadius, ActionMenuConfig cfg)
	{
		var layer = new CanvasLayer { Layer = 12 };
		AddChild(layer);
		SystemActionMenu = GD.Load<PackedScene>("res://scenes/ui/SystemActionMenu.tscn").Instantiate<SystemActionMenu>();
		layer.AddChild(SystemActionMenu);
		SystemActionMenu.Initialize(camera, systemRadius, cfg);
	}

	private void SpawnScenarioPanel()
	{
		var layer = new CanvasLayer { Layer = 13 };
		AddChild(layer);
		ScenarioPanel = GD.Load<PackedScene>("res://scenes/ui/ScenarioPanel.tscn").Instantiate<ScenarioPanel>();
		layer.AddChild(ScenarioPanel);
	}

	private void SpawnChatWindow(Vector2 viewportSize)
	{
		var layer = new CanvasLayer { Layer = 10 };
		AddChild(layer);
		ChatWindow = GD.Load<PackedScene>("res://scenes/ui/ChatWindowNode.tscn").Instantiate<ChatWindowNode>();
		layer.AddChild(ChatWindow);
		const float chatHeight = 54f;
		const float sidePad = 8f;
		const float bottomPad = 8f;
		ChatWindow.Position = new Vector2(sidePad, viewportSize.Y - chatHeight - bottomPad);
	}
}
