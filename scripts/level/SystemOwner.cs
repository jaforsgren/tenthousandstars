namespace Tts.Level;

public enum SystemOwner { None, Player, Ai1, Ai2, Ai3, Ai4 }

public static class SystemOwnerExtensions
{
    public static bool IsAi(this SystemOwner owner) =>
        owner is SystemOwner.Ai1 or SystemOwner.Ai2 or SystemOwner.Ai3 or SystemOwner.Ai4;
}
