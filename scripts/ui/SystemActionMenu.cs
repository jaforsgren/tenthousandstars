using System;
using Godot;

namespace Tts;

// Positions all action buttons in a half-circle around the selected system.
// Button placement is always computed from the system's screen-space edge so
// buttons never overlap the system circle.
// Below MinDisplayZoom the buttons scale down and converge toward the system border.
[Tool]
public partial class SystemActionMenu : Control
{
	private const float ShowDuration = 0.18f;
	private const float SlotStagger = 0.03f;
	private const int SlotCount = 6;

	private CameraController _camera = null!;
	private float _systemRadius;
	private float _arcButtonGap;
	private float _minDisplayZoom;
	private Color _lineColor;
	private float _lineWidth;

	private AnimationPlayer _animPlayer = null!;
	private InfoButton _infoSlot = null!;
	private InfoButton _opponentInfoSlot = null!;
	private RerouteButtonNode _rerouteSlot = null!;
	private UpgradeButtonNode _fortifySlot = null!;
	private UpgradeButtonNode _forgeSlot = null!;
	private SplitButtonNode _splitSlot = null!;

	// Unit vectors for each slot's arc position (constant across zoom levels).
	private readonly Vector2[] _slotDirections = new Vector2[SlotCount];
	private Vector2 _trackedWorldPos;
	private bool _isActive;
	private bool _isHiding;
	private float _currentZoom = 1f;

#if TOOLS
	[Export] public bool PreviewAnimation
	{
		get => false;
		set { if (value && Engine.IsEditorHint()) PlayEditorPreview(); }
	}
	[Export] public float PreviewSystemRadius = 50f;
	[Export] public float PreviewArcButtonGap = 32f;
	[Export] public float PreviewArcStartAngleDeg = 180f;
	[Export] public float PreviewArcEndAngleDeg = 360f;

	private static readonly string[] SlotNodeNames =
		["InfoButton", "OpponentInfoButton", "RerouteButtonNode", "FortifyButtonNode", "ForgeButtonNode", "SplitButtonNode"];

	private Control[]? _editorPreviewSlots;
	private float _editorPreviewSystemEdge;

	// Typed casts on instanced child scenes fail in tool context; use Control base type throughout.
	private void PlayEditorPreview()
	{
		var animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		var slots = Array.ConvertAll(SlotNodeNames, name => GetNode<Control>(name));

		var directions = new Vector2[SlotCount];
		for (var i = 0; i < SlotCount; i++)
		{
			var t = (float)i / (SlotCount - 1);
			var rad = Mathf.DegToRad(Mathf.Lerp(PreviewArcStartAngleDeg, PreviewArcEndAngleDeg, t));
			directions[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
		}

		var arcDist = PreviewSystemRadius + PreviewArcButtonGap;
		for (var i = 0; i < SlotCount; i++)
			slots[i].Position = directions[i] * arcDist;

		_editorPreviewSlots = slots;
		_editorPreviewSystemEdge = PreviewSystemRadius;
		_lineColor = new Color(1f, 1f, 1f, 0.3f);
		_lineWidth = 1.5f;

		if (animPlayer.HasAnimationLibrary(""))
			animPlayer.RemoveAnimationLibrary("");

		var totalDuration = ShowDuration + (SlotCount - 1) * SlotStagger;
		var anim = new Animation { Length = totalDuration };
		for (var i = 0; i < SlotCount; i++)
			AddScaleTrack(anim, slots[i].Name, i * SlotStagger, i * SlotStagger + ShowDuration);

		var lib = new AnimationLibrary();
		lib.AddAnimation("show", anim);
		animPlayer.AddAnimationLibrary("", lib);

		Visible = true;
		foreach (var slot in slots)
			slot.Scale = Vector2.Zero;
		animPlayer.Play("show");
		QueueRedraw();
	}
#endif

	public override void _Ready()
	{
		_animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		_infoSlot = GetNode<InfoButton>("InfoButton");
		_opponentInfoSlot = GetNode<InfoButton>("OpponentInfoButton");
		_rerouteSlot = GetNode<RerouteButtonNode>("RerouteButtonNode");
		_fortifySlot = GetNode<UpgradeButtonNode>("FortifyButtonNode");
		_forgeSlot = GetNode<UpgradeButtonNode>("ForgeButtonNode");
		_splitSlot = GetNode<SplitButtonNode>("SplitButtonNode");
		MouseFilter = MouseFilterEnum.Ignore;
		ClipContents = false;
	}

	public void Initialize(CameraController camera, float systemRadius, ActionMenuConfig config)
	{
		_camera = camera;
		_systemRadius = systemRadius;
		_arcButtonGap = config.ArcButtonGap;
		_minDisplayZoom = config.MinDisplayZoom;
		_lineColor = config.LineColor.ToColor();
		_lineWidth = config.LineWidth;
		ComputeSlotDirections(config.ArcStartAngleDeg, config.ArcEndAngleDeg);
		SetupAnimation();
	}

	private void ComputeSlotDirections(float startDeg, float endDeg)
	{
		for (var i = 0; i < SlotCount; i++)
		{
			var t = (float)i / (SlotCount - 1);
			var rad = Mathf.DegToRad(Mathf.Lerp(startDeg, endDeg, t));
			_slotDirections[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
		}
	}

	public override void _Process(double delta)
	{
		if (!_isActive && !_isHiding) return;
		_currentZoom = _camera.Zoom.X;

		// Below MinDisplayZoom the whole menu scales down so buttons converge toward
		// the system border. Above it they stay at full button size.
		var menuScale = Mathf.Min(1f, _currentZoom / _minDisplayZoom);
		Scale = Vector2.One * menuScale;
		Position = GetViewport().GetCanvasTransform() * _trackedWorldPos;

		// Slot positions (in local/pre-scale coords) are set so that their screen-space
		// distance from the system centre equals systemRadius*zoom + ArcButtonGap.
		// When zoom < MinDisplayZoom the effective radius is clamped to minDisplayZoom,
		// which (combined with the scale applied above) makes the buttons converge.
		var effectiveZoom = Mathf.Max(_currentZoom, _minDisplayZoom);
		var arcLocalDist = _systemRadius * effectiveZoom + _arcButtonGap;

		UpdateSlotPositions(arcLocalDist);
		QueueRedraw();
	}

	private void UpdateSlotPositions(float arcLocalDist)
	{
		_infoSlot.Position         = _slotDirections[0] * arcLocalDist;
		_opponentInfoSlot.Position = _slotDirections[1] * arcLocalDist;
		_rerouteSlot.Position      = _slotDirections[2] * arcLocalDist;
		_fortifySlot.Position      = _slotDirections[3] * arcLocalDist;
		_forgeSlot.Position        = _slotDirections[4] * arcLocalDist;
		_splitSlot.Position        = _slotDirections[5] * arcLocalDist;
	}

	public override void _Draw()
	{
#if TOOLS
		if (Engine.IsEditorHint())
		{
			if (_editorPreviewSlots == null) return;
			foreach (var slot in _editorPreviewSlots)
				DrawConnectorLine(slot, _editorPreviewSystemEdge);
			return;
		}
#endif
		if (!_isActive && !_isHiding) return;
		// System edge in local coords (pre-scale) so it maps to the correct screen radius.
		var systemEdgeLocal = _systemRadius * _currentZoom / Scale.X;
		DrawConnectorLine(_infoSlot,         systemEdgeLocal);
		DrawConnectorLine(_opponentInfoSlot, systemEdgeLocal);
		DrawConnectorLine(_rerouteSlot,      systemEdgeLocal);
		DrawConnectorLine(_fortifySlot,      systemEdgeLocal);
		DrawConnectorLine(_forgeSlot,        systemEdgeLocal);
		DrawConnectorLine(_splitSlot,        systemEdgeLocal);
	}

	private void DrawConnectorLine(Control slot, float systemEdgeLocal)
	{
		if (!slot.Visible) return;
		var dir = slot.Position.Normalized();
		DrawLine(dir * systemEdgeLocal, slot.Position, _lineColor, _lineWidth);
	}

	public void ShowForPlayerFleet(
		Vector2 worldPos,
		Action onInfo,
		bool hasReroute, Action onReroute,
		bool fortifyActive, bool fortifyDisabled, Action onFortify,
		bool forgeActive, bool forgeDisabled, Action onForge,
		bool splitDisabled, Action onSplit)
	{
		_trackedWorldPos = worldPos;
		_infoSlot.Configure(onInfo);
		_opponentInfoSlot.Visible = false;
		_rerouteSlot.Configure(hasReroute, onReroute);
		_fortifySlot.Configure(fortifyActive, fortifyDisabled, onFortify);
		_forgeSlot.Configure(forgeActive, forgeDisabled, onForge);
		_splitSlot.Configure(splitDisabled, onSplit);
		PlayShowAnimation();
	}

	public void ShowForPlayerSystem(
		Vector2 worldPos,
		Action onInfo,
		bool hasReroute, Action onReroute,
		bool fortifyActive, bool fortifyDisabled, Action onFortify,
		bool forgeActive, bool forgeDisabled, Action onForge,
		bool splitDisabled, Action onSplit)
	{
		_trackedWorldPos = worldPos;
		_infoSlot.Configure(onInfo);
		_opponentInfoSlot.Visible = false;
		_rerouteSlot.Configure(hasReroute, onReroute);
		_fortifySlot.Configure(fortifyActive, fortifyDisabled, onFortify);
		_forgeSlot.Configure(forgeActive, forgeDisabled, onForge);
		_splitSlot.Configure(splitDisabled, onSplit);
		PlayShowAnimation();
	}

	public void ShowForAiSystem(Vector2 worldPos, Action onSystemInfo, Action onFactionInfo)
	{
		_trackedWorldPos = worldPos;
		_infoSlot.Configure(onSystemInfo);
		_opponentInfoSlot.Configure(onFactionInfo);
		_rerouteSlot.Visible = false;
		_fortifySlot.Visible = false;
		_forgeSlot.Visible = false;
		_splitSlot.Visible = false;
		PlayShowAnimation();
	}

	public void ShowInfoOnly(Vector2 worldPos, Action onInfo)
	{
		_trackedWorldPos = worldPos;
		_infoSlot.Configure(onInfo);
		_opponentInfoSlot.Visible = false;
		_rerouteSlot.Visible = false;
		_fortifySlot.Visible = false;
		_forgeSlot.Visible = false;
		_splitSlot.Visible = false;
		PlayShowAnimation();
	}

	public void HideAll()
	{
		if (!_isActive) return;
		_isActive = false;
		if (Engine.TimeScale == 0.0)
		{
			Visible = false;
			return;
		}
		_isHiding = true;
		_animPlayer.PlayBackwards("show");
	}

	public void RefreshUpgradeButtons(
		bool fortifyActive, bool fortifyDisabled, Action onFortify,
		bool forgeActive, bool forgeDisabled, Action onForge)
	{
		_fortifySlot.Configure(fortifyActive, fortifyDisabled, onFortify);
		_forgeSlot.Configure(forgeActive, forgeDisabled, onForge);
	}

	public void RefreshSplitButton(bool disabled, Action onSplit)
	{
		_splitSlot.Configure(disabled, onSplit);
	}

	public void RefreshRerouteButton(bool hasActiveRoute, Action onReroute)
	{
		_rerouteSlot.Configure(hasActiveRoute, onReroute);
	}

	private void PlayShowAnimation()
	{
		_isHiding = false;
		_isActive = true;
		Visible = true;
		var slots = new Control[] { _infoSlot, _opponentInfoSlot, _rerouteSlot, _fortifySlot, _forgeSlot, _splitSlot };
		if (Engine.TimeScale == 0.0)
		{
			foreach (var slot in slots)
				slot.Scale = Vector2.One;
			return;
		}
		foreach (var slot in slots)
			slot.Scale = Vector2.Zero;
		_animPlayer.Play("show");
	}

	private void SetupAnimation()
	{
		var totalDuration = ShowDuration + (SlotCount - 1) * SlotStagger;
		var anim = new Animation { Length = totalDuration };

		var slots = new Control[] { _infoSlot, _opponentInfoSlot, _rerouteSlot, _fortifySlot, _forgeSlot, _splitSlot };
		for (var i = 0; i < SlotCount; i++)
		{
			var delay = i * SlotStagger;
			var endTime = delay + ShowDuration;
			AddScaleTrack(anim, slots[i].Name, delay, endTime);
		}

		var lib = new AnimationLibrary();
		lib.AddAnimation("show", anim);
		// AddAnimationLibrary returns an error (and does not replace) if the key already
		// exists — the scene ships with an empty default library, so remove it first.
		if (_animPlayer.HasAnimationLibrary(""))
			_animPlayer.RemoveAnimationLibrary("");
		_animPlayer.AddAnimationLibrary("", lib);
		_animPlayer.AnimationFinished += OnAnimationFinished;
	}

	private static void AddScaleTrack(Animation anim, string nodeName, float delay, float endTime)
	{
		var track = anim.AddTrack(Animation.TrackType.Value);
		anim.TrackSetPath(track, $"{nodeName}:scale");
		anim.TrackSetInterpolationType(track, Animation.InterpolationType.Linear);
		anim.TrackInsertKey(track, 0f, Vector2.Zero);
		if (delay > 0f)
			anim.TrackInsertKey(track, delay, Vector2.Zero);
		anim.TrackInsertKey(track, endTime, Vector2.One);
	}

	private void OnAnimationFinished(StringName _animName)
	{
		if (_isHiding)
		{
			_isHiding = false;
			Visible = false;
		}
	}
}
