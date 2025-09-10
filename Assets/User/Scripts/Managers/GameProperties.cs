
/// <summary>
/// Contains the core features of the Game.
/// </summary>
public static class GameProperties
{
    /// <summary>
    /// Controls the overall volume scale for the game at local levels
    /// </summary>
    public static float VolumeScale = 1f;
    /// <summary>
    /// The base game mode.
    /// </summary>
    public static Mode GameMode = Mode.OneVSOne;
    /// <summary>
    /// Determines the frequency of ammo and guns supplies and determines the agents powers.
    /// </summary>
    public static Level GameLevel = Level.Easy;
    /// <summary>
    /// Determines whether there will be npcs in 1v1.
    /// </summary>
    public static bool AllowNPCsIn1v1 = true;
    /// <summary>
    /// How long the match will go for 1v1.
    /// </summary>
    public static int MatchTimeDuration = 5;
    /// <summary>
    /// How many kills ensure the victory.
    /// </summary>
    public static int WinAtKills = 5;
    public enum Mode
    {
        OneVSOne,
        Defenders,
    }
    public enum Level
    {
        Easy,
        Medium,
        Hard,
    }
}