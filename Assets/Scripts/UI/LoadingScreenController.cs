using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WheelingMoto.Core;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Scène tampon affichée pendant le chargement asynchrone de la scène demandée
    /// (utile surtout pour Métropole, plus lourde à charger que le menu).
    /// </summary>
    public class LoadingScreenController : MonoBehaviour
    {
        readonly UITheme theme = new UITheme();
        Image progressFill;
        Text statusText;

        void Awake()
        {
            Build();
        }

        void Start()
        {
            StartCoroutine(LoadTargetScene());
        }

        void Build()
        {
            var canvas = UIFactory.CreateRootCanvas("LoadingCanvas");
            var root = canvas.transform;

            UIFactory.AddPanel(root, "Background", theme.Background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            UIFactory.AddText(root, "Title", "WHEELING MOTO", 40, theme.Accent, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-400, 60), new Vector2(400, 130));

            statusText = UIFactory.AddText(root, "StatusText", "Chargement...", 20, theme.TextMuted, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -70), new Vector2(300, -30));

            var barBg = UIFactory.AddPanel(root, "BarBackground", theme.PanelAlt,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -20), new Vector2(300, 20));

            var barFillRect = UIFactory.CreateUIObject("BarFill", barBg.transform);
            UIFactory.SetRect(barFillRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            progressFill = barFillRect.gameObject.AddComponent<Image>();
            progressFill.color = theme.Accent;
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.fillAmount = 0f;
        }

        IEnumerator LoadTargetScene()
        {
            string target = string.IsNullOrEmpty(SceneLoader.PendingScene) ? SceneNames.MainMenu : SceneLoader.PendingScene;

            // Laisse l'UI de chargement s'afficher au moins une frame avant de bloquer sur le chargement.
            yield return null;

            var op = SceneManager.LoadSceneAsync(target);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
            {
                progressFill.fillAmount = Mathf.Clamp01(op.progress / 0.9f);
                yield return null;
            }

            progressFill.fillAmount = 1f;
            statusText.text = "Prêt !";
            yield return new WaitForSeconds(0.2f);

            op.allowSceneActivation = true;
        }
    }
}
