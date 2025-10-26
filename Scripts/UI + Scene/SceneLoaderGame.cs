using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoaderGame
{
    static string _targetScene;

    public static void LoadAsyncViaLoadingScreen(string sceneName)
    {
        _targetScene = sceneName;
        SceneManager.LoadScene("Loading");
    }

    public static string ConsumeTarget()
    {
        var t = _targetScene;
        _targetScene = null;
        return t;
    }
}
