#nullable enable

using System.Threading.Tasks;
using Godot;
using Godot.Collections;

namespace Tts;

/// <summary>
/// Manages gameplay segments triggered from Yarn via <c>&lt;&lt;gameplay "scene_name"&gt;&gt;</c>.
///
/// Scenes are loaded from <c>res://scenes/gameplay/{name}.tscn</c>.
/// When done, call <see cref="NotifyCompleted"/> with the result dictionary.
/// GameManager frees the scene automatically after that.
///
/// Add as a sibling of YarnBridge inside DialogueRoot, or register as an autoload.
/// Access via <see cref="Instance"/> after the node is ready.
/// </summary>
public partial class GameManager : Node
{
	public static GameManager? Instance { get; private set; }

	private const string GameplayBasePath = "res://scenes/gameplay/";

	private TaskCompletionSource<Dictionary>? _gameplayTcs;

	public override void _Ready()
	{
		Instance = this;
	}

	/// <summary>
	/// Called by the active gameplay scene when the player has finished.
	/// Resolves the pending <see cref="RunGameplay"/> task.
	/// </summary>
	public static void NotifyCompleted(Dictionary result)
	{
		Instance?._gameplayTcs?.TrySetResult(result);
	}

	/// <summary>
	/// Loads and runs the named gameplay scene. Suspends until the scene
	/// calls <see cref="NotifyCompleted"/>. The scene is freed automatically.
	/// </summary>
	public async Task<Dictionary> RunGameplay(string sceneName)
	{
		string path = $"{GameplayBasePath}{sceneName}.tscn";

		if (!ResourceLoader.Exists(path))
		{
			GD.PushError($"[GameManager] Gameplay scene not found: {path}");
			return new Dictionary();
		}

		PackedScene packed = ResourceLoader.Load<PackedScene>(path);
		Node gameplayScene = packed.Instantiate();

		_gameplayTcs = new TaskCompletionSource<Dictionary>(TaskCreationOptions.RunContinuationsAsynchronously);

		GetTree().Root.AddChild(gameplayScene);

		Dictionary result = await _gameplayTcs.Task;
		_gameplayTcs = null;
		gameplayScene.QueueFree();
		return result;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && Instance == this)
			Instance = null;
		base.Dispose(disposing);
	}
}
