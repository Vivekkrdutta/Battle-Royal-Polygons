
using System;
using Unity.Netcode;
using UnityEngine.SceneManagement;
public static class Loader
{
    public static event EventHandler<Scene> OnSceneLoaded;
    public enum Scene
    {
        GameScene,
        CharacterSelectScene,
        LobbyScene,
    }

    public static void NetworkLoadScene(Scene scene,LoadSceneMode loadSceneMode)
    {
        // loading scene
        NetworkManager.Singleton.SceneManager.LoadScene(scene.ToString(), loadSceneMode);
    }
    public static void LoadScene(Scene scene,LoadSceneMode loadSceneMode = LoadSceneMode.Single)
    {
        SceneManager.LoadScene(scene.ToString());
        OnSceneLoaded?.Invoke(null, scene);
    }
}