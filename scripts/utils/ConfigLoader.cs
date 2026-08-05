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
        if (!path.StartsWith("res://"))
        {
            return path;
        }

        // Walk up from the output/base directory to the Godot project root (found by
        // the presence of project.godot). Falls back to the historical fixed hop count
        // (5 levels) that matches the Godot C# build output layout.
        static string? WalkUp(DirectoryInfo dir)
        {
            for (var current = dir; current != null; current = current.Parent)
                if (File.Exists(Path.Combine(current.FullName, "project.godot")))
                    return current.FullName;
            return null;
        }

        var rootDir = WalkUp(new DirectoryInfo(AppContext.BaseDirectory))
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

        return Path.Combine(rootDir, path.Replace("res://", ""));
    }

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private static readonly Dictionary<string, object> _cache = new();

    public static bool Exists(string resPath) => File.Exists(ResolvePath(resPath));

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
