using System;
using System.Collections.Generic;
using Godot;

namespace Tts;

// Positions all action and intent buttons in a half-circle around the selected system.
// All visible slots (named action slots + dynamic intent slots) are distributed evenly
// across the configured arc so both sets share spacing and layout.
[Tool]
public partial class SystemActionMenu : Control
{
	private const float ShowDuration   = 0.18f;
	private const float SlotStagger    = 0.03f;
	private const int   NamedSlotCount = 6;

	private CameraController _camera = null!;
	private float _systemRadius;
	private float _arcButtonGap;
	private float _minDisplayZoom;
	private Color _lineColor;
	private float _lineWidth;
	private float _arcStartDeg;
	private float _arcEndDeg;

	private AnimationPlayer   _animPlayer       = null!;
	private InfoButton        _infoSlot         = null!;
	private InfoButton        _opponentInfoSlot  = null!;
	private RerouteButtonNode _rerouteSlot      = null!;
	private UpgradeButtonNode _fortifySlot      = null!;
	private UpgradeButtonNode _forgeSlot        = null!;
	private SplitButtonNode   _splitSlot        = null!;

	private readonly List<Control> _intentSlots = [];
	private float _intentAnimElapsed;
	private bool  _required;

	private Vector2 _trackedWorldPos;
	private bool    _isActive;
	private bool    _isHiding;
	private float   _currentZoom = 1f;

#if TOOLS
	[Export] public bool PreviewAnimation
	{
		get => false;
		set { if (value && Engine.IsEditorHint()) PlayEditorPreview(); }
	}
	[Export] public float PreviewSystemRadius  = 50f;
	[Export] public float PreviewArcButtonGap  = 32f;
	[Export] public float PreviewArcStartAngleDeg = 180f;
	[Export] public float PreviewArcEndAngleDeg   = 360f;

	private static readonly string[] SlotNodeNames =
		["InfoButton", "OpponentInfoButton", "RerouteButtonNode", "FortifyButtonNode", "ForgeButtonNode", "SplitButtonNode"];

	private Control[]? _editorPreviewSlots;
	private float _editorPreviewSystemEdge;

	private void PlayEditorPreview()
	{
		var animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		var slots = Array.ConvertAll(SlotNodeNames, name => GetNode<Control>(name));

		var directions = new Vector2[NamedSlotCount];
		for (var i = 0; i < NamedSlotCount; i++)
		{
			var t   = (float)i / (NamedSlotCount - 1);
			var rad = Mathf.DegToRad(Mathf.Lerp(PreviewArcStartAngleDeg, PreviewArcEndAngleDeg, t));
			directions[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
		}

		var arcDist = PreviewSystemRadius + PreviewArcButtonGap;
		for (var i = 0; i < NamedSlotCount; i++)
			slots[i].Position = directions[i] * arcDist;

		_editorPreviewSlots     = slots;
		_editorPreviewSystemEdge = PreviewSystemRadius;
		_lineColor  = new Color(1f, 1f, 1f, 0.3f);
		_lineWidth  = 1.5f;

		if (animPlayer.HasAnimationLibrary(""))
			animPlayer.RemoveAnimationLibrary("");

		var totalDuration = ShowDuration + (NamedSlotCount - 1) * SlotStagger;
		var anim = new Animation { Length = totalDuration };
		for (var i = 0; i < NamedSlotCount; i++)
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
		_animPlayer       = GetNode<AnimationPlayer>("AnimationPlayer");
		_infoSlot         = GetNode<InfoButton>("InfoButton");
		_opponentInfoSlot = GetNode<InfoButton>("OpponentInfoButton");
		_rerouteSlot      = GetNode<RerouteButtonNode>("RerouteButtonNode");
		_fortifySlot      = GetNode<UpgradeButtonNode>("FortifyButtonNode");
		_forgeSlot        = GetNode<UpgradeButtonNode>("ForgeButtonNode");
		_splitSlot        = GetNode<SplitButtonNode>("SplitButtonNode");
		MouseFilter       = MouseFilterEnum.Ignore;
		ClipContents      = false;
	}

	public void Initialize(CameraController camera, float systemRadius, ActionMenuConfig config)
	{
		_camera         = camera;
		_systemRadius   = systemRadius;
		_arcButtonGap   = config.ArcButtonGap;
		_minDisplayZoom = config.MinDisplayZoom;
		_lineColor      = config.LineColor.ToColor();
		_lineWidth      = config.LineWidth;
		_arcStartDeg    = config.ArcStartAngleDeg;
		_arcEndDeg      = config.ArcEndAngleDeg;
		SetupAnimation();
	}

	public override void _Process(double delta)
	{
		if (!_isActive && !_isHiding) return;
		_currentZoom = _camera.Zoom.X;
		Scale        = Vector2.One * Mathf.Min(1f, _currentZoom / _minDisplayZoom);
		Position     = GetViewport().GetCanvasTransform() * _trackedWorldPos;

		var effectiveZoom = Mathf.Max(_currentZoom, _minDisplayZoom);
		var arcLocalDist  = _systemRadius * effectiveZoom + _arcButtonGap;

		UpdateSlotPositions(arcLocalDist);
		AnimateIntentSlots(delta);
		QueueRedraw();
	}

	private void UpdateSlotPositions(float arcLocalDist)
	{
		var visible = CollectVisibleSlots();
		for (var i = 0; i < visible.Count; i++)
		{
			var t   = visible.Count == 1 ? 0.5f : (float)i / (visible.Count - 1);
			var rad = Mathf.DegToRad(Mathf.Lerp(_arcStartDeg, _arcEndDeg, t));
			visible[i].Position = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * arcLocalDist;
		}
	}

	private List<Control> CollectVisibleSlots()
	{
		var result = new List<Control>();
		if (_infoSlot.Visible)         result.Add(_infoSlot);
		if (_opponentInfoSlot.Visible) result.Add(_opponentInfoSlot);
		if (_rerouteSlot.Visible)      result.Add(_rerouteSlot);
		if (_fortifySlot.Visible)      result.Add(_fortifySlot);
		if (_forgeSlot.Visible)        result.Add(_forgeSlot);
		if (_splitSlot.Visible)        result.Add(_splitSlot);
		result.AddRange(_intentSlots);
		return result;
	}

	private void AnimateIntentSlots(double delta)
	{
		if (_intentSlots.Count == 0) return;
		_intentAnimElapsed += (float)delta;
		for (var i = 0; i < _intentSlots.Count; i++)
		{
			var t = Mathf.Clamp((_intentAnimElapsed - i * SlotStagger) / ShowDuration, 0f, 1f);
			_intentSlots[i].Scale = Vector2.One * t;
		}
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
		var systemEdgeLocal = _systemRadius * _currentZoom / Scale.X;
		foreach (var slot in CollectVisibleSlots())
			DrawConnectorLine(slot, systemEdgeLocal);
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
		ClearIntentSlots();
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
		ClearIntentSlots();
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
		ClearIntentSlots();
		_trackedWorldPos = worldPos;
		_infoSlot.Configure(onSystemInfo);
		_opponentInfoSlot.Configure(onFactionInfo);
		_rerouteSlot.Visible  = false;
		_fortifySlot.Visible  = false;
		_forgeSlot.Visible    = false;
		_splitSlot.Visible    = false;
		PlayShowAnimation();
	}

	public void ShowInfoOnly(Vector2 worldPos, Action onInfo)
	{
		ClearIntentSlots();
		_trackedWorldPos = worldPos;
		_infoSlot.Configure(onInfo);
		_opponentInfoSlot.Visible = false;
		_rerouteSlot.Visible      = false;
		_fortifySlot.Visible      = false;
		_forgeSlot.Visible        = false;
		_splitSlot.Visible        = false;
		PlayShowAnimation();
	}

	// Shows only intent options with no named action slots — used for arrival commit.
	public void ShowIntentOnly(
		Vector2 worldPos,
		(string Label, IntentType Intent)[] options,
		Action<IntentType> onPick,
		bool required = false)
	{
		_trackedWorldPos          = worldPos;
		_infoSlot.Visible         = false;
		_opponentInfoSlot.Visible = false;
		_rerouteSlot.Visible      = false;
		_fortifySlot.Visible      = false;
		_forgeSlot.Visible        = false;
		_splitSlot.Visible        = false;
		AddIntentSlots(options, onPick, required);
		_isHiding = false;
		_isActive = true;
		Visible   = true;
	}

	// Appends intent slots to a currently-shown menu (own system commit options).
	public void SetIntentOptions(
		(string Label, IntentType Intent)[] options,
		Action<IntentType> onPick)
	{
		AddIntentSlots(options, onPick, required: false);
	}

	public void HideIfOptional()
	{
		if (_required) return;
		HideAll();
	}

	public void HideAll()
	{
		ClearIntentSlots();
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

	private void AddIntentSlots(
		(string Label, IntentType Intent)[] options,
		Action<IntentType> onPick,
		bool required)
	{
		ClearIntentSlots();
		_required = required;

		foreach (var (label, intent) in options)
		{
			var slot = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = false };
			var btn  = new Button
			{
				Text              = label,
				CustomMinimumSize = new Vector2(UILayout.ButtonSize, UILayout.ButtonSize),
				Position          = new Vector2(-UILayout.ButtonSize / 2f, -UILayout.ButtonSize / 2f),
				MouseFilter       = MouseFilterEnum.Stop
			};
			UILayout.ApplyGreyStyle(btn);
			var captured = intent;
			btn.Pressed += () => { onPick(captured); HideAll(); };
			slot.AddChild(btn);
			AddChild(slot);
			_intentSlots.Add(slot);
		}

		_intentAnimElapsed = 0f;
		foreach (var slot in _intentSlots)
			slot.Scale = Vector2.Zero;
	}

	private void ClearIntentSlots()
	{
		foreach (var slot in _intentSlots)
			slot.QueueFree();
		_intentSlots.Clear();
		_required = false;
	}

	private void PlayShowAnimation()
	{
		_isHiding = false;
		_isActive = true;
		Visible   = true;
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
		var totalDuration = ShowDuration + (NamedSlotCount - 1) * SlotStagger;
		var anim = new Animation { Length = totalDuration };

		var slots = new Control[] { _infoSlot, _opponentInfoSlot, _rerouteSlot, _fortifySlot, _forgeSlot, _splitSlot };
		for (var i = 0; i < NamedSlotCount; i++)
		{
			var delay   = i * SlotStagger;
			var endTime = delay + ShowDuration;
			AddScaleTrack(anim, slots[i].Name, delay, endTime);
		}

		var lib = new AnimationLibrary();
		lib.AddAnimation("show", anim);
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
			Visible   = false;
		}
	}
}
