using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace Tts;

public static class ConfigLoader
{
	private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };
	private static readonly Dictionary<string, object> _cache = new();

	public static T Load<T>(string resPath) where T : class
	{
		if (_cache.TryGetValue(resPath, out var cached))
			return (T)cached;
		using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
		var result = JsonSerializer.Deserialize<T>(file.GetAsText(), _options)!;
		_cache[resPath] = result;
		return result;
	}
}
