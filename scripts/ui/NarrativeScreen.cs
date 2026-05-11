using System;
using Godot;
using Tts.Narrative;

namespace Tts.Ui;

[Tool]
public partial class NarrativeScreen : Control
{
	private Label _chapterTitleLabel = null!;
	private Label _factionLabel = null!;
	private Control _objectiveSection = null!;
	private Label _objectiveText = null!;
	private ScrollContainer _bodyScroll = null!;
	private Label _bodyText = null!;
	private Label _pageIndicator = null!;
	private Button _nextButton = null!;
	private Button _startMissionButton = null!;
	private Control _outroButtons = null!;
	private Button _newCampaignButton = null!;
	private Button _randomMissionsButton = null!;
	private Button _quitButton = null!;

	private string[] _pages = [];
	private int _currentPage;
	private Action? _onStartMission;

	public event Action? NewCampaignPressed;
	public event Action? RandomMissionsPressed;
	public event Action? QuitPressed;

	private bool _previewInEditor;

	[Export]
	public bool PreviewInEditor
	{
		get => _previewInEditor;
		set
		{
			_previewInEditor = value;
			if (Engine.IsEditorHint() && IsNodeReady())
				ApplyEditorPreview();
		}
	}

	public override void _Ready()
	{
		_chapterTitleLabel = GetNode<Label>("%ChapterTitleLabel");
		_factionLabel = GetNode<Label>("%FactionLabel");
		_objectiveSection = GetNode<Control>("%ObjectiveSection");
		_objectiveText = GetNode<Label>("%ObjectiveText");
		_bodyScroll = GetNode<ScrollContainer>("%BodyScroll");
		_bodyText = GetNode<Label>("%BodyText");
		_pageIndicator = GetNode<Label>("%PageIndicator");
		_nextButton = GetNode<Button>("%NextButton");
		_startMissionButton = GetNode<Button>("%StartMissionButton");
		_outroButtons = GetNode<Control>("%OutroButtons");
		_newCampaignButton = GetNode<Button>("%NewCampaignButton");
		_randomMissionsButton = GetNode<Button>("%RandomMissionsButton");
		_quitButton = GetNode<Button>("%QuitButton");

		_nextButton.Pressed += OnNextPressed;
		_startMissionButton.Pressed += OnStartMissionPressed;
		_newCampaignButton.Pressed += () => NewCampaignPressed?.Invoke();
		_randomMissionsButton.Pressed += () => RandomMissionsPressed?.Invoke();
		_quitButton.Pressed += () => QuitPressed?.Invoke();

		if (Engine.IsEditorHint())
		{
			ApplyEditorPreview();
			return;
		}

		Visible = false;
	}

	public void ShowMissionBrief(NarrativePageData data, Action onStartMission)
	{
		_onStartMission = onStartMission;
		Load(data, outroMode: false);
		Visible = true;
		GameSpeed.PushUiPause();
	}

	public void ShowOutro(NarrativePageData data)
	{
		Load(data, outroMode: true);
		Visible = true;
	}

	private void Load(NarrativePageData data, bool outroMode)
	{
		_chapterTitleLabel.Text = data.ChapterTitle;
		_factionLabel.Text = data.Faction;

		_objectiveSection.Visible = !string.IsNullOrEmpty(data.MissionObjective);
		_objectiveText.Text = data.MissionObjective ?? "";

		_pages = data.Pages.Length > 0 ? data.Pages : [""];
		_currentPage = 0;

		_startMissionButton.Visible = !outroMode;
		_outroButtons.Visible = false;

		UpdatePageDisplay();
	}

	private void OnNextPressed()
	{
		if (_currentPage >= _pages.Length - 1) return;
		_currentPage++;
		_bodyScroll.ScrollVertical = 0;
		UpdatePageDisplay();
	}

	private void OnStartMissionPressed()
	{
		GameSpeed.PopUiPause();
		Visible = false;
		var callback = _onStartMission;
		_onStartMission = null;
		callback?.Invoke();
	}

	private void UpdatePageDisplay()
	{
		_bodyText.Text = _pages[_currentPage];

		var isLastPage = _currentPage >= _pages.Length - 1;

		_nextButton.Visible = !isLastPage;

		_pageIndicator.Visible = _pages.Length > 1;
		if (_pages.Length > 1)
			_pageIndicator.Text = $"{_currentPage + 1} / {_pages.Length}";

		if (!isLastPage) return;

		if (_startMissionButton.Visible)
			_startMissionButton.Disabled = false;
		else
			_outroButtons.Visible = true;
	}

	private void ApplyEditorPreview()
	{
		if (!_previewInEditor || !IsNodeReady()) return;

		_chapterTitleLabel.Text = "The Fall of Kepler";
		_factionLabel.Text = "The Falling Empire · Vance Syndicate";
		_objectiveSection.Visible = true;
		_objectiveText.Text = "Eliminate all enemy fleets before the sector falls";
		_pages = [
			"From the bridge of the Endurance, Commander Vale watched the last relay station blink offline.\n\nThe outer sectors had gone dark. Three weeks of silence from the eastern outposts.\n\nNot retreat. Consumption.",
            "High Command had sent the order via encrypted burst — the kind that only goes out when they're not sure if the recipient will be around to receive the next one.\n\nKepler was to hold. At any cost."
		];
		_currentPage = 0;
		_bodyText.Text = _pages[0];
		_nextButton.Visible = true;
		_startMissionButton.Visible = true;
		_startMissionButton.Disabled = true;
		_outroButtons.Visible = false;
		_pageIndicator.Visible = true;
		_pageIndicator.Text = "1 / 2";
	}
}
