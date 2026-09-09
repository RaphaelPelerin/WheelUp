namespace WheelingMoto.Core
{
    // Nommé GameMode (et non PlayMode) pour éviter le conflit avec UnityEngine.PlayMode.
    public enum GameMode
    {
        Defis,
        Course
    }

    public enum MapId
    {
        Metropole,
        Montagne,
        CoteAzur
    }

    /// <summary>Choix du joueur dans le menu Jouer, conservés le temps de la session pour la scène de jeu.</summary>
    public static class GameSession
    {
        public static GameMode SelectedMode = GameMode.Course;
        public static MapId SelectedMap = MapId.Metropole;
    }
}
