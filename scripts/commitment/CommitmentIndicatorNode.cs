using Godot;

namespace Tts;

// Attached to a SystemNode as a child.
// Draws a phase-segmented arc around the system and shows an intent badge above it.
public partial class CommitmentIndicatorNode : Node2D
{
    private Button _intentButton = null!;
    private float _systemRadius;
    private Color _ownerColor;
    private float _arcProgress;
    private CommitmentPhase _phase = CommitmentPhase.Arrival;

    private static readonly Color TrackColor = new(1f, 1f, 1f, 0.07f);
    private const float ArcGap = 16f;  // sits outside ProductionArcNode (8f gap)
    private const float ArcWidth = 2f;
    private const int ArcSegments = 64;
    private const float StartAngle = -Mathf.Pi / 2f;  // 12 o'clock, clockwise
    private const int CornerRadius = 3;
    private const float BadgeHalfW = 8f;
    private const float BadgeHalfH = 7f;
    private const float BadgeAboveSystem = 22f;
    private const float BadgeSlotSpacing = 18f;
    private const int BadgeFontSize = 9;

    public override void _Ready()
    {
        _intentButton = GetNode<Button>("%IntentButton");
    }

    public void Initialize(float systemRadius, Color ownerColor, int slot)
    {
        _systemRadius = systemRadius;
        _ownerColor = ownerColor;

        var center = BadgeCenter(slot, systemRadius);
        _intentButton.OffsetLeft   = center.X - BadgeHalfW;
        _intentButton.OffsetTop    = center.Y - BadgeHalfH;
        _intentButton.OffsetRight  = center.X + BadgeHalfW;
        _intentButton.OffsetBottom = center.Y + BadgeHalfH;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(ownerColor.R, ownerColor.G, ownerColor.B, 0.88f),
            CornerRadiusTopLeft     = CornerRadius,
            CornerRadiusTopRight    = CornerRadius,
            CornerRadiusBottomLeft  = CornerRadius,
            CornerRadiusBottomRight = CornerRadius,
            CornerDetail = 3
        };
        _intentButton.AddThemeStyleboxOverride("normal", style);
        _intentButton.AddThemeStyleboxOverride("hover", style);
        _intentButton.AddThemeStyleboxOverride("pressed", style);
        _intentButton.AddThemeColorOverride("font_color", Colors.White);
        _intentButton.AddThemeFontSizeOverride("font_size", BadgeFontSize);
    }

    public void UpdateState(CommitmentPhase phase, float phaseProgress, IntentType intent)
    {
        _phase = phase;
        _arcProgress = OverallProgress(phase, phaseProgress);
        _intentButton.Text = IntentLetter(intent);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var arcRadius = _systemRadius + ArcGap;

        // Dim full-circle track so the arc has a visible background
        DrawArc(Vector2.Zero, arcRadius, 0f, Mathf.Tau, ArcSegments, TrackColor, ArcWidth);

        if (_arcProgress <= 0f) return;

        // Alpha increases each phase: Arrival dim → Engagement mid → Resolution bright
        var alpha = _phase switch
        {
            CommitmentPhase.Arrival    => 0.4f,
            CommitmentPhase.Engagement => 0.72f,
            CommitmentPhase.Resolution => 1.0f,
            _                          => 0.5f
        };
        var fillColor = new Color(_ownerColor.R, _ownerColor.G, _ownerColor.B, alpha);
        DrawArc(Vector2.Zero, arcRadius, StartAngle, StartAngle + _arcProgress * Mathf.Tau, ArcSegments, fillColor, ArcWidth);
    }

    // Maps the three phases onto a single 0–1 progress value for the arc
    private static float OverallProgress(CommitmentPhase phase, float phaseProgress)
        => phase switch
        {
            CommitmentPhase.Arrival    => phaseProgress * (1f / 3f),
            CommitmentPhase.Engagement => (1f + phaseProgress) * (1f / 3f),
            CommitmentPhase.Resolution => (2f + phaseProgress) * (1f / 3f),
            _                          => 0f
        };

    // Spread badges in a row directly above the system; centre badge first, then alternate sides
    private static Vector2 BadgeCenter(int slot, float systemRadius)
    {
        var x = slot == 0 ? 0f
            : slot % 2 == 1 ? -(slot + 1) / 2f * BadgeSlotSpacing
            : slot / 2f * BadgeSlotSpacing;
        return new Vector2(x, -(systemRadius + BadgeAboveSystem));
    }

    private static string IntentLetter(IntentType intent) => intent switch
    {
        IntentType.Attack      => "A",
        IntentType.Contest     => "C",
        IntentType.Fortify     => "F",
        IntentType.Investigate => "?",
        IntentType.Exploit     => "E",
        _                      => "?"
    };
}
