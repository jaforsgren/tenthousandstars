using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace Tts.Utils;

public static class ConfigLoader
{

    private static string ResolvePath(string path)
    {
        if (path.StartsWith("res://"))
        {
            var root = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "../../../../..")
            );

            return Path.Combine(root, path.Replace("res://", ""));
        }

        return path;
    }

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly Dictionary<string, object> _cache = new();

    public static string LoadText(string resPath)
    {
        var path = ResolvePath(resPath);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Text file not found: {path}");
        return File.ReadAllText(path);
    }

    public static T Load<T>(string resPath) where T : class
    {
        if (_cache.TryGetValue(resPath, out var cached))
            return (T)cached;
        T result;
        try
        {
            var path = ResolvePath(resPath);

            if (!File.Exists(path))
                throw new InvalidOperationException($"Config not found: {path}");

            var json = File.ReadAllText(path);

            result = JsonSerializer.Deserialize<T>(json, _options)
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
