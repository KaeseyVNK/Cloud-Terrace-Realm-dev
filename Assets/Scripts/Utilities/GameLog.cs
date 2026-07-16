using System.Diagnostics;
using Object = UnityEngine.Object;

public static class GameLog
{
    /// <summary>
    /// When true, LogVerbose messages are printed. Default false to keep the console clean.
    /// Enable via code or #define GAME_LOG_VERBOSE.
    /// </summary>
    public static bool VerboseEnabled =
#if GAME_LOG_VERBOSE
        true;
#else
        false;
#endif

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object message)
    {
        if (IsMainMenuScene()) return;
        UnityEngine.Debug.Log(message);
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object message, Object context)
    {
        if (IsMainMenuScene()) return;
        UnityEngine.Debug.Log(message, context);
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void LogVerbose(object message)
    {
        if (!VerboseEnabled || IsMainMenuScene()) return;
        UnityEngine.Debug.Log(message);
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void LogVerbose(object message, Object context)
    {
        if (!VerboseEnabled || IsMainMenuScene()) return;
        UnityEngine.Debug.Log(message, context);
    }

    private static bool IsMainMenuScene()
    {
        try
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenuScene";
        }
        catch
        {
            return false;
        }
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(object message)
    {
        UnityEngine.Debug.LogWarning(message);
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(object message, Object context)
    {
        UnityEngine.Debug.LogWarning(message, context);
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(object message)
    {
        UnityEngine.Debug.LogError(message);
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(object message, Object context)
    {
        UnityEngine.Debug.LogError(message, context);
    }
}
