namespace Tts.Config;

public record CameraConfig(
	float MinZoom,
	float MaxZoom,
	float ZoomStep,
	float StartZoom,
	float FollowZoom,
	float FocusTransitionSeconds,
	float TrackpadZoomSensitivity);
