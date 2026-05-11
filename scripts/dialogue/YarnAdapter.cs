#nullable enable

using System;
using System.Threading.Tasks;

namespace Tts.Dialogue;

/// <summary>
/// Decoupling layer between <see cref="YarnBridge"/> (Yarn Spinner side)
/// and <see cref="DialogueController"/> (UI side).
///
/// Each "emit" method creates its TaskCompletionSource BEFORE firing the event,
/// preventing any race between the event handler completing synchronously
/// and the caller awaiting the returned Task.
/// </summary>
public sealed class YarnAdapter
{
	public event Action<YarnLine>? LineReady;
	public event Action<YarnOption[]>? OptionsReady;
	public event Action<SkillCheckResult>? SkillCheckReady;

	private TaskCompletionSource<bool>? _lineTcs;
	private TaskCompletionSource<int>? _optionTcs;
	private TaskCompletionSource<bool>? _skillCheckTcs;

	internal Task EmitLineAndWait(YarnLine line)
	{
		_lineTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		LineReady?.Invoke(line);
		return _lineTcs.Task;
	}

	internal Task<int> EmitOptionsAndWait(YarnOption[] options)
	{
		_optionTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
		OptionsReady?.Invoke(options);
		return _optionTcs.Task;
	}

	internal Task EmitSkillCheckAndWait(SkillCheckResult result)
	{
		_skillCheckTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		SkillCheckReady?.Invoke(result);
		return _skillCheckTcs.Task;
	}

	/// <summary>Called by <see cref="DialogueController"/> after a line finishes rendering.</summary>
	public void AcknowledgeLine() => _lineTcs?.TrySetResult(true);

	/// <summary>Called by <see cref="DialogueController"/> after a skill check entry finishes rendering.</summary>
	public void AcknowledgeSkillCheck() => _skillCheckTcs?.TrySetResult(true);

	/// <summary>Called by <see cref="DialogueController"/> when the player picks an option.</summary>
	public void SelectOption(int optionId) => _optionTcs?.TrySetResult(optionId);

	/// <summary>Called when dialogue ends before an option is selected.</summary>
	public void CancelOptions() => _optionTcs?.TrySetCanceled();
}
