using UnityEngine;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Cale son RectTransform sur la zone sûre de l'écran (Screen.safeArea) : encoche ou Dynamic Island,
    /// coins arrondis et barre d'accueil des téléphones bord à bord. Suivie à chaque frame, pour les rotations.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rect;
        Rect appliedArea;
        Vector2Int appliedScreen;

        void Awake()
        {
            rect = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != appliedArea || Screen.width != appliedScreen.x || Screen.height != appliedScreen.y)
            {
                Apply();
            }
        }

        void Apply()
        {
            appliedArea = Screen.safeArea;
            appliedScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            rect.anchorMin = new Vector2(appliedArea.xMin / Screen.width, appliedArea.yMin / Screen.height);
            rect.anchorMax = new Vector2(appliedArea.xMax / Screen.width, appliedArea.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
