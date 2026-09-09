using UnityEngine;
using WheelingMoto.Core;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Scène de jeu actuellement vide : confirme le mode/carte choisis dans le menu
    /// et permet d'y revenir. Le vrai gameplay (physique du wheeling, etc.) reste à implémenter ici.
    /// </summary>
    public class GameplayBootstrap : MonoBehaviour
    {
        void Awake()
        {
            var theme = new UITheme();
            var canvas = UIFactory.CreateRootCanvas("GameplayCanvas");

            string modeLabel = GameSession.SelectedMode == GameMode.Defis ? "Défis" : "Course";
            string mapLabel = GameSession.SelectedMap switch
            {
                MapId.Metropole => "Métropole Dense",
                MapId.Montagne => "Montagne",
                MapId.CoteAzur => "Ville Côtière",
                _ => GameSession.SelectedMap.ToString()
            };

            UIFactory.AddText(canvas.transform, "Info",
                $"Scène de jeu (vide)\nMode : {modeLabel}\nCarte : {mapLabel}",
                26, theme.Text, TextAnchor.UpperLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -170), new Vector2(-30, -30));

            UIFactory.AddButton(canvas.transform, "BackButton", "◀ RETOUR AU MENU", theme.PanelAlt, theme.Text, 18,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -70), new Vector2(340, -20), SceneLoader.LoadMainMenu);
        }
    }
}
