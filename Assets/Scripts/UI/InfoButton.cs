using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Pastille « i » posée à côté d'un titre. Elle porte le texte long que la carte n'affiche plus
    /// et l'ouvre en plein écran dans <see cref="InfoPopup"/>.
    ///
    /// Le texte est gardé ici plutôt que capturé dans le clic : les écrans qui changent d'état — le
    /// bouton « sans publicité » une fois l'achat fait, les probabilités d'un coffre après une
    /// ouverture — n'ont qu'à réaffecter <see cref="Body"/> pour que la page suive.
    /// </summary>
    [DisallowMultipleComponent]
    public class InfoButton : MonoBehaviour
    {
        public string Title;
        public string Body;

        UITheme theme;

        public static InfoButton Attach(GameObject target, UITheme theme, string title, string body)
        {
            var info = target.AddComponent<InfoButton>();
            info.theme = theme;
            info.Title = title;
            info.Body = body;
            return info;
        }

        public void Open()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            InfoPopup.Show(canvas.rootCanvas.transform, theme, Title, Body);
        }
    }
}
