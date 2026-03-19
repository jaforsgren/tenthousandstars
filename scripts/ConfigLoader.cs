using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace Tts;

public static class ConfigLoader
{
	private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

	public static T Load<T>(string resPath) where T : class
	{
		using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
		var result = JsonSerializer.Deserialize<T>(file.GetAsText(), _options)!;
		return result;
	}
}
