using UnityEngine.SceneManagement;

namespace WheelingMoto.Core
{
    public static class SceneLoader
    {
        /// <summary>Scène demandée, lue par LoadingScreenController une fois la scène Loading active.</summary>
        public static string PendingScene { get; private set; }

        public static void LoadGameplay() => LoadWithLoadingScreen(SceneNames.Gameplay);

        public static void LoadMetropole() => LoadWithLoadingScreen(SceneNames.Metropole);

        public static void LoadMainMenu() => LoadWithLoadingScreen(SceneNames.MainMenu);

        static void LoadWithLoadingScreen(string targetScene)
        {
            PendingScene = targetScene;
            SceneManager.LoadScene(SceneNames.Loading);
        }
    }
}
