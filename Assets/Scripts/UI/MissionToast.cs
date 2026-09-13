using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Bandeau de récompense qui glisse depuis le haut pendant la conduite, puis s'efface. C'est lui
    /// qui fait le lien entre la figure que le joueur vient de réussir et le gain : les pièces sont
    /// déjà créditées quand il apparaît, il ne demande aucune action.
    ///
    /// Il écoute <see cref="RewardFeed"/> et non les missions : un palier de succès s'annonce par le
    /// même bandeau, sans que celui-ci ait à distinguer les deux systèmes.
    ///
    /// Plusieurs gains peuvent tomber dans la même seconde — un long wheeling valide souvent la
    /// distance, la série et un palier de succès d'un coup. Les annonces sont donc mises en file
    /// plutôt que superposées, et défilent l'une après l'autre.
    ///
    /// Le bandeau se place en haut au centre, seule bande libre du HUD : la vitesse occupe le coin
    /// gauche, les boutons Vue et Menu le coin droit, la jauge et les commandes le bas de l'écran.
    /// </summary>
    [DisallowMultipleComponent]
    public class MissionToast : MonoBehaviour
    {
        const float Width = 780f;
        const float Height = 104f;

        const float SlideDuration = 0.28f;
        const float HoldDuration = 2.1f;
        const float FadeDuration = 0.45f;

        /// <summary>Position de repos, sous le bord haut de la zone sûre.</summary>
        const float RestY = -26f;

        /// <summary>Position de départ, hors écran : le bandeau descend de là.</summary>
        const float HiddenY = 150f;

        enum Phase
        {
            Idle,
            SlidingIn,
            Holding,
            FadingOut,
        }

        readonly Queue<RewardNotice> queue = new Queue<RewardNotice>();

        RectTransform rect;
        CanvasGroup group;
        TextMeshProUGUI titleLabel;
        TextMeshProUGUI missionLabel;
        TextMeshProUGUI coinsLabel;

        Phase phase = Phase.Idle;
        float timer;

        /// <summary>Construit le bandeau dans le conteneur donné (la zone sûre du HUD) et l'abonne aux missions.</summary>
        public static MissionToast Create(Transform parent, UITheme theme)
        {
            var host = UIFactory.CreateUIObject("MissionToast", parent);
            UIFactory.SetRect(host, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-Width / 2f, HiddenY - Height), new Vector2(Width / 2f, HiddenY));

            var toast = host.gameObject.AddComponent<MissionToast>();
            toast.Build(theme);
            return toast;
        }

        void Build(UITheme theme)
        {
            rect = GetComponent<RectTransform>();

            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            // Le bandeau traverse la zone de glissement de la caméra : il ne doit jamais avaler un toucher.
            group.blocksRaycasts = false;

            var panel = UIFactory.AddPanel(transform, "Background", new Color(0.047f, 0.047f, 0.055f, 0.92f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, rounded: true);
            panel.raycastTarget = false;

            // Liseré aux couleurs de la marque : il distingue l'annonce de récompense du bandeau de
            // chute, qui occupe le même genre de bande mais en rouge sombre plein.
            var stripe = UIFactory.AddPanel(panel.transform, "Stripe", theme.Accent,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 12f), new Vector2(9f, -12f), rounded: true);
            stripe.raycastTarget = false;

            titleLabel = UIFactory.AddText(panel.transform, "Title", "MISSION ACCOMPLIE", UITheme.FontLabel, theme.Accent,
                TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(30f, 52f), new Vector2(-190f, -14f), FontStyles.Bold);

            missionLabel = UIFactory.AddText(panel.transform, "Mission", "", UITheme.FontBody, theme.Text,
                TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(30f, 14f), new Vector2(-190f, -52f));

            coinsLabel = UIFactory.AddText(panel.transform, "Coins", "", UITheme.FontHeading, theme.Coin,
                TextAnchor.MiddleRight, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-180f, 0f), new Vector2(-26f, 0f), FontStyles.Bold);
        }

        void OnEnable() => RewardFeed.Granted += Enqueue;

        void OnDisable() => RewardFeed.Granted -= Enqueue;

        void Enqueue(RewardNotice notice)
        {
            if (notice == null) return;
            queue.Enqueue(notice);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (phase == Phase.Idle)
            {
                if (queue.Count == 0) return;

                Show(queue.Dequeue());
                return;
            }

            timer += dt;

            switch (phase)
            {
                case Phase.SlidingIn:
                    // Amorti en sortie : le bandeau arrive vite puis se pose, au lieu de s'arrêter net.
                    float slide = Mathf.Clamp01(timer / SlideDuration);
                    SetY(Mathf.Lerp(HiddenY, RestY, 1f - Mathf.Pow(1f - slide, 3f)));
                    group.alpha = slide;

                    if (slide >= 1f) Enter(Phase.Holding);
                    break;

                case Phase.Holding:
                    // Une annonce en attente raccourcit l'attente : la file ne doit pas s'allonger
                    // au point que le joueur lise un gain trois figures plus tard.
                    float hold = queue.Count > 0 ? HoldDuration * 0.45f : HoldDuration;
                    if (timer >= hold) Enter(Phase.FadingOut);
                    break;

                case Phase.FadingOut:
                    float fade = Mathf.Clamp01(timer / FadeDuration);
                    group.alpha = 1f - fade;
                    SetY(Mathf.Lerp(RestY, RestY + 40f, fade));

                    if (fade >= 1f)
                    {
                        SetY(HiddenY);
                        group.alpha = 0f;
                        Enter(Phase.Idle);
                    }
                    break;
            }
        }

        void Show(RewardNotice notice)
        {
            titleLabel.text = notice.Headline;
            missionLabel.text = notice.Label;
            coinsLabel.text = $"+{notice.Coins}";

            SetY(HiddenY);
            group.alpha = 0f;
            Enter(Phase.SlidingIn);
        }

        void Enter(Phase next)
        {
            phase = next;
            timer = 0f;
        }

        void SetY(float top)
        {
            rect.offsetMin = new Vector2(-Width / 2f, top - Height);
            rect.offsetMax = new Vector2(Width / 2f, top);
        }
    }
}
