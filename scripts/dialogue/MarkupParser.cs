#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Yarn.Markup;

namespace Tts;

/// <summary>
/// Converts a Yarn <see cref="MarkupParseResult"/> into a BBCode string
/// for <see cref="Godot.RichTextLabel"/> and a per-character delay table
/// for the typewriter effect.
///
/// Supported markup attributes:
///   [slow]…[/slow]   – slows typewriter by <see cref="SlowMultiplier"/>
///   [fast]…[/fast]   – speeds typewriter by <see cref="FastMultiplier"/>
///   [pause=N]         – inserts N-second pause before the character at that position
///   [color=X]…[/color]– wraps the range in a BBCode [color=X] tag
/// </summary>
public static class MarkupParser
{
	private const float NormalDelay = 0.04f;
	private const float SpaceDelay = 0.008f;
	private const float CommaDelay = 0.10f;
	private const float PunctuationDelay = 0.20f;
	private const float SlowMultiplier = 2.5f;
	private const float FastMultiplier = 0.25f;

	public readonly record struct ParseResult(string BBCodeText, float[] CharDelays);

	public static ParseResult Parse(MarkupParseResult markup)
	{
		string text = markup.Text;
		int n = text.Length;

		var speedMult = new float[n];
		Array.Fill(speedMult, 1.0f);
		var extraPause = new float[n];
		var colorRanges = new List<ColorRange>();

		foreach (MarkupAttribute attr in markup.Attributes)
		{
			switch (attr.Name)
			{
				case "slow":
					ScaleRange(speedMult, attr.Position, attr.Length, n, SlowMultiplier);
					break;

				case "fast":
					ScaleRange(speedMult, attr.Position, attr.Length, n, FastMultiplier);
					break;

				case "pause":
					// Yarn markup [pause=N] produces attribute.Name="pause", property "pause"=N
					int pausePos = Math.Clamp(attr.Position, 0, Math.Max(n - 1, 0));
					if (n > 0)
						extraPause[pausePos] += ReadFloat(attr, "pause", 0.5f);
					break;

				case "color":
					if (attr.Properties.TryGetValue("color", out MarkupValue colorVal))
						colorRanges.Add(new ColorRange(attr.Position, attr.Position + attr.Length, colorVal.StringValue));
					break;
			}
		}

		var charDelays = new float[n];
		for (int i = 0; i < n; i++)
		{
			float baseDelay = text[i] switch
			{
				' ' or '\t' => SpaceDelay,
				',' or ';' => CommaDelay,
				'.' or '!' or '?' or ':' => PunctuationDelay,
				_ => NormalDelay
			};
			charDelays[i] = baseDelay * speedMult[i] + extraPause[i];
		}

		return new ParseResult(BuildBBCode(text, colorRanges), charDelays);
	}

	private static void ScaleRange(float[] arr, int start, int length, int n, float multiplier)
	{
		for (int i = start; i < start + length && i < n; i++)
			arr[i] *= multiplier;
	}

	private static float ReadFloat(MarkupAttribute attr, string key, float fallback)
	{
		if (!attr.Properties.TryGetValue(key, out MarkupValue val))
			return fallback;

		return val.Type switch
		{
			MarkupValueType.Float => val.FloatValue,
			MarkupValueType.Integer => val.IntegerValue,
			_ => fallback
		};
	}

	private static string BuildBBCode(string text, List<ColorRange> colorRanges)
	{
		if (colorRanges.Count == 0)
			return text;

		var events = new List<(int Pos, bool IsOpen, string Color)>(colorRanges.Count * 2);
		foreach (ColorRange r in colorRanges)
		{
			events.Add((r.Start, true, r.Color));
			events.Add((r.End, false, r.Color));
		}

		// At the same position, close before open so tags don't interleave
		events.Sort((a, b) => a.Pos != b.Pos
			? a.Pos.CompareTo(b.Pos)
			: a.IsOpen.CompareTo(b.IsOpen));

		var sb = new StringBuilder(text.Length + events.Count * 20);
		int n = text.Length;
		int ei = 0;

		for (int i = 0; i <= n; i++)
		{
			while (ei < events.Count && events[ei].Pos == i)
			{
				var (_, isOpen, color) = events[ei++];
				sb.Append(isOpen ? $"[color={color}]" : "[/color]");
			}
			if (i < n) sb.Append(text[i]);
		}

		return sb.ToString();
	}

	private readonly record struct ColorRange(int Start, int End, string Color);
}
