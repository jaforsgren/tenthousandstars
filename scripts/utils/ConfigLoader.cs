using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Tts;

public static class ConfigLoader
{
	private static readonly JsonSerializerOptions _options = new()
	{
		PropertyNameCaseInsensitive = true,
		Converters = { new JsonStringEnumConverter() }
	};
	private static readonly Dictionary<string, object> _cache = new();

	public static T Load<T>(string resPath) where T : class
	{
		if (_cache.TryGetValue(resPath, out var cached))
			return (T)cached;
		using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
		T result;
		try
		{
			result = JsonSerializer.Deserialize<T>(file.GetAsText(), _options)
				?? throw new InvalidOperationException($"Failed to load config '{resPath}': deserialized to null");
		}
		catch (JsonException ex)
		{
			throw new InvalidOperationException($"Failed to load config '{resPath}': {ex.Message}", ex);
		}
		_cache[resPath] = result;
		return result;
	}
}
