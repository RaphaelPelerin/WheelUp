using TMPro;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// HUD de la scène Métropole : commandes tactiles (direction par flèches ou par joystick selon les
    /// Paramètres, GAZ / LEVER / FREIN), vitesse, jauge d'angle (wheeling ou roue avant), bandeau « Chute ! »,
    /// menu de chute (repartir ou racheter la prouesse), bouton de changement de vue et zone de glissement
    /// pour tourner la caméra. Pilote à terre, les commandes sont rangées : on ne conduit pas une moto couchée.
    /// Tout ce qui se touche ou se lit est cadré dans la zone sûre de l'écran (encoche, coins arrondis,
    /// barre d'accueil), avec une marge : rien n'est rogné sur les téléphones à écran bord à bord.
    /// </summary>
    public class MetropoleHUD : MonoBehaviour
    {
        const float FallOverlayHold = 0.9f;
        const float FallOverlayFade = 0.5f;

        // Gabarit des commandes, en unités du canvas (référence 1920 x 1080), mesuré depuis la zone sûre et
        // ramené à l'échelle de l'écran par MeasureControls : rien ne doit jamais sortir des bords.
        const float Gap = 20f;
        const float SteerButtonSize = 250f;
        const float JoystickSize = 330f;
        const float JoystickKnobSize = 140f;
        const float PedalWidth = 250f;
        const float ThrottleHeight = 310f;
        const float BrakeHeight = 210f;
        const float LiftHeight = 180f;
        const int ControlFontSize = 34;
        // Marge au bord de la zone sûre : jamais moins que MinMargin, et sinon cette part du petit côté de
        // l'écran. Sur un téléphone à coins arrondis, 24 unités (2 mm) frôlent l'arrondi et la barre d'accueil.
        const float MinMargin = 24f;
        const float MarginShare = 0.045f;
        // Couloir laissé libre entre les deux piles de boutons : la route doit rester visible entre les pouces.
        const float MinCorridor = 200f;
        // Bande réservée en haut de l'écran (vitesse, MENU, VUE) au-dessus des commandes.
        const float TopRowHeight = 120f;

        // Boutons translucides : la route reste visible sous les pouces. À l'appui, le fond passe au
        // rouge de la marque, l'assombrissement par défaut du Button ne se verrait pas sur ce fond.
        static readonly Color ControlColor = new Color(0.19f, 0.19f, 0.2f, 0.3f);
        static readonly Color ControlPressedColor = new Color(UITheme.Brand.r, UITheme.Brand.g, UITheme.Brand.b, 0.5f);
        static readonly Color ControlTextColor = new Color(1f, 1f, 1f, 0.85f);
        static readonly Color JoystickRailColor = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color KnobColor = new Color(1f, 1f, 1f, 0.45f);

        MotorcycleController controller;
        MotoCameraRig cameraRig;
        UITheme theme;

        /// <summary>Racine du canvas : le récapitulatif de fin de session s'y pose, par-dessus tout le HUD.</summary>
        Transform hudRoot;
        TextMeshProUGUI speedText;
        TextMeshProUGUI viewLabel;
        CameraView displayedView;
        WheelieGauge gauge;
        StuntScoreHud stunts;
        CrashMenu crashMenu;
        PauseMenu pauseMenu;
        GameObject controlsRoot;
        RectTransform steeringRoot;
        SteeringControl builtSteering;
        CanvasGroup fallOverlay;
        TextMeshProUGUI fallHint;
        float fallOverlayTimer;
        bool leftHeld;
        bool rightHeld;
        float margin = MinMargin;
        float controlScale = 1f;

        void Awake()
        {
            controller = FindAnyObjectByType<MotorcycleController>();
            cameraRig = FindAnyObjectByType<MotoCameraRig>();
            theme = new UITheme();
            Build();

            if (controller != null)
            {
                controller.Fell += OnFell;

                // Ouvre la session de mesure et branche les missions sur la conduite. Posé ici plutôt
                // que dans la scène : ajouter un composant à un fichier .unity pour un script qui se
                // suffit à lui-même compliquerait les fusions pour rien.
                RunStatsCollector.Install(controller);
            }
        }

        void OnDestroy()
        {
            if (controller != null)
            {
                controller.Fell -= OnFell;
            }
            stunts?.Dispose();
            crashMenu?.Dispose();
            pauseMenu?.Dispose();
        }

        void Update()
        {
            // Échap sur PC, bouton retour sur Android (Unity le rapporte comme Échap) : pause, puis retour arrière.
            if (BackPressed())
            {
                if (pauseMenu.Showing) pauseMenu.Back();
                else pauseMenu.Open();
            }

            if (controller != null)
            {
                speedText.text = $"{Mathf.RoundToInt(controller.SpeedKmh)} km/h";
                gauge?.Tick();
                stunts?.Tick();
            }

            crashMenu?.Tick();
            // Pilote à terre ou jeu en pause : les commandes disparaissent, et les boutons maintenus se
            // relâchent avec elles.
            bool riding = !(crashMenu != null && crashMenu.Showing) && !pauseMenu.Showing;
            if (controlsRoot != null && controlsRoot.activeSelf != riding) controlsRoot.SetActive(riding);

            // La vue peut aussi changer au clavier (C / V) : le libellé suit.
            if (cameraRig != null && viewLabel != null && cameraRig.CurrentView != displayedView)
            {
                RefreshViewLabel();
            }

            if (fallOverlayTimer > 0f)
            {
                fallOverlayTimer -= Time.deltaTime;
                fallOverlay.alpha = Mathf.Clamp01(fallOverlayTimer / FallOverlayFade);
            }
        }

        static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        void OnFell()
        {
            fallHint.text = CrashMenu.Hint(controller);
            fallOverlayTimer = FallOverlayHold + FallOverlayFade;
            fallOverlay.alpha = 1f;
        }

        /// <summary>
        /// Cale le gabarit des commandes sur l'écran réel. La marge suit la taille de l'écran, pour que rien
        /// ne colle au bord ni aux coins arrondis ; et si le gabarit complet ne tient pas dans la zone sûre
        /// (écran étroit, format inhabituel), tout est réduit d'un même facteur. Sans cela, les deux piles de
        /// boutons finissent par se chevaucher et par déborder de l'écran.
        /// </summary>
        void MeasureControls()
        {
            Vector2 safe = UIFactory.SafeAreaSize();
            margin = Mathf.Max(MinMargin, Mathf.Min(safe.x, safe.y) * MarginShare);

            // Pile de gauche (flèches ou joystick), pile de droite (FREIN/LEVER puis GAZ), et le couloir central.
            float left = Mathf.Max(SteerButtonSize * 2f + Gap, JoystickSize);
            float right = PedalWidth * 2f + Gap;
            float needWidth = 2f * margin + left + right + MinCorridor;
            float needHeight = TopRowHeight + margin + BrakeHeight + Gap + LiftHeight;

            controlScale = Mathf.Clamp(Mathf.Min(safe.x / needWidth, safe.y / needHeight), 0.5f, 1f);
        }

        /// <summary>Mesure du gabarit ramenée à l'échelle retenue pour cet écran.</summary>
        float S(float value) => value * controlScale;

        void Build()
        {
            MeasureControls();

            var canvas = UIFactory.CreateRootCanvas("MetropoleHUDCanvas");
            var root = canvas.transform;
            hudRoot = root;

            // Créée en premier, donc dessinée derrière : les boutons restent prioritaires.
            // Plein écran : glisser depuis les bords tourne aussi la caméra.
            BuildCameraDragZone(root);
            BuildFallOverlay(root);

            var safeArea = UIFactory.CreateSafeArea(root);

            // En haut au centre : le compteur reste dans le champ de vision sans empiéter sur les coins,
            // occupés par les commandes et les compteurs de prouesses.
            speedText = UIFactory.AddText(safeArea, "SpeedText", "0 km/h", 30, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-170, -64), new Vector2(170, -20), FontStyles.Bold);

            // Pause plutôt que retour direct au menu : on peut y régler les Paramètres sans quitter la partie.
            int topFont = Mathf.RoundToInt(S(16f));
            UIFactory.AddButton(safeArea, "PauseButton", "II  PAUSE", ControlColor, ControlTextColor, topFont,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-margin - S(160f), -S(60f)), new Vector2(-margin, -S(20f)),
                () => pauseMenu.Open());

            // Tout ce qui ne sert qu'en roulant (commandes, jauge d'angle, changement de vue) est réuni sous
            // un même objet : la chute le range d'un coup, et les boutons restés enfoncés se relâchent avec lui
            // (HoldButton et SteerJoystick lâchent tout à la désactivation).
            var driving = UIFactory.CreateUIObject("Driving", safeArea);
            UIFactory.SetRect(driving, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            controlsRoot = driving.gameObject;

            if (cameraRig != null)
            {
                var viewButton = UIFactory.AddButton(driving, "ViewButton", "VUE", ControlColor, ControlTextColor, topFont,
                    new Vector2(1, 1), new Vector2(1, 1), new Vector2(-margin - S(310f), -S(60f)), new Vector2(-margin - S(170f), -S(20f)),
                    () => cameraRig.ToggleView());
                viewLabel = viewButton.GetComponentInChildren<TextMeshProUGUI>();
                RefreshViewLabel();
            }

            if (controller != null)
            {
                gauge = new WheelieGauge();
                gauge.Build(driving, theme, controller);

                // Le compteur de prouesses reste, lui : c'est là que s'annoncent les points perdus à la chute.
                stunts = new StuntScoreHud();
                stunts.Build(safeArea, theme, controller);
            }

            // Direction dans son propre conteneur : elle se reconstruit si on change de commande en pause.
            steeringRoot = UIFactory.CreateUIObject("Steering", driving);
            UIFactory.SetRect(steeringRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BuildSteering();

            BuildPedals(driving);

            // Au-dessus des commandes mais sous les menus : une mission validée doit rester lisible
            // pendant la conduite, sans recouvrir la pause ni le menu de chute.
            MissionToast.Create(safeArea, theme);

            // Créés en dernier, donc dessinés par-dessus tout le reste : ce sont des menus, ils passent devant
            // le HUD — la pause devant le menu de chute, qu'elle peut interrompre.
            if (controller != null)
            {
                crashMenu = new CrashMenu();
                crashMenu.Build(root, theme, controller);
            }
            pauseMenu = new PauseMenu();
            pauseMenu.Build(root, theme, OnPauseClosed, OnQuitToMenu);
        }

        void BuildSteering()
        {
            builtSteering = SettingsManager.Steering;
            if (builtSteering == SteeringControl.Joystick)
            {
                BuildJoystick(steeringRoot);
            }
            else
            {
                BuildSteerArrows(steeringRoot);
            }
        }

        /// <summary>
        /// Clôt la session et montre le bilan avant de rendre la main au menu. Le récapitulatif ne
        /// distribue rien : si le joueur quitte l'application au lieu de le valider, il n'a rien
        /// perdu, tout a déjà été crédité pendant la conduite.
        /// </summary>
        void OnQuitToMenu()
        {
            var stats = MissionTracker.EndRun();

            if (!RunSummaryScreen.TryShow(hudRoot, theme, stats, SceneLoader.LoadMainMenu))
            {
                SceneLoader.LoadMainMenu();
            }
        }

        /// <summary>Reprise après la pause : la commande de direction a pu changer dans les Paramètres.</summary>
        void OnPauseClosed()
        {
            if (SettingsManager.Steering == builtSteering) return;

            // Détruits en fin de frame : détachés d'abord, pour que les nouveaux prennent leur place tout de suite.
            for (int i = steeringRoot.childCount - 1; i >= 0; i--)
            {
                Transform old = steeringRoot.GetChild(i);
                old.SetParent(null, false);
                Destroy(old.gameObject);
            }
            leftHeld = false;
            rightHeld = false;
            controller?.SetSteer(0f);
            BuildSteering();
        }

        void BuildSteerArrows(Transform parent)
        {
            float size = S(SteerButtonSize);
            AddHold(parent, "SteerLeft", "◀", Vector2.zero, Vector2.zero,
                new Vector2(margin, margin), new Vector2(margin + size, margin + size),
                () => { leftHeld = true; UpdateSteer(); }, () => { leftHeld = false; UpdateSteer(); });

            float x = margin + size + S(Gap);
            AddHold(parent, "SteerRight", "▶", Vector2.zero, Vector2.zero,
                new Vector2(x, margin), new Vector2(x + size, margin + size),
                () => { rightHeld = true; UpdateSteer(); }, () => { rightHeld = false; UpdateSteer(); });
        }

        /// <summary>Joystick horizontal : la direction suit l'écart du doigt et revient au milieu au relâchement.</summary>
        void BuildJoystick(Transform parent)
        {
            var area = UIFactory.CreateUIObject("SteerJoystick", parent);
            UIFactory.SetRect(area, Vector2.zero, Vector2.zero,
                new Vector2(margin, margin), new Vector2(margin + S(JoystickSize), margin + S(JoystickSize)));
            var background = area.gameObject.AddComponent<Image>();
            background.sprite = SteerJoystick.CircleSprite;
            background.color = ControlColor;
            background.raycastTarget = true;

            // Rail horizontal : le joystick ne sert qu'à diriger.
            var rail = UIFactory.AddPanel(area, "Rail", JoystickRailColor, new Vector2(0.12f, 0.5f), new Vector2(0.88f, 0.5f),
                new Vector2(0f, -6f), new Vector2(0f, 6f), rounded: true);
            rail.raycastTarget = false;

            float half = S(JoystickKnobSize) * 0.5f;
            var knob = UIFactory.CreateUIObject("Knob", area);
            UIFactory.SetRect(knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-half, -half), new Vector2(half, half));
            var knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = SteerJoystick.CircleSprite;
            knobImage.color = KnobColor;
            knobImage.raycastTarget = false;

            area.gameObject.AddComponent<SteerJoystick>().Init(knob,
                value => controller?.SetSteer(value),
                held => knobImage.color = held ? ControlPressedColor : KnobColor);
        }

        /// <summary>GAZ dans le coin, FREIN à sa gauche, LEVER au-dessus de FREIN.</summary>
        void BuildPedals(Transform parent)
        {
            Vector2 corner = new Vector2(1f, 0f);

            float width = S(PedalWidth);
            float throttleLeft = -margin - width;
            AddHold(parent, "ThrottleButton", "GAZ", corner, corner,
                new Vector2(throttleLeft, margin), new Vector2(-margin, margin + S(ThrottleHeight)),
                () => controller?.SetThrottle(true), () => controller?.SetThrottle(false));

            float brakeLeft = throttleLeft - S(Gap) - width;
            AddHold(parent, "BrakeButton", "FREIN", corner, corner,
                new Vector2(brakeLeft, margin), new Vector2(brakeLeft + width, margin + S(BrakeHeight)),
                () => controller?.SetBrake(true), () => controller?.SetBrake(false));

            float liftBottom = margin + S(BrakeHeight) + S(Gap);
            AddHold(parent, "LiftButton", "LEVER", corner, corner,
                new Vector2(brakeLeft, liftBottom), new Vector2(brakeLeft + width, liftBottom + S(LiftHeight)),
                () => controller?.SetLift(true), () => controller?.SetLift(false));
        }

        void BuildCameraDragZone(Transform root)
        {
            if (cameraRig == null) return;

            var zone = UIFactory.AddPanel(root, "CameraDragZone", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            zone.raycastTarget = true;
            // Transparente mais capte les glissements ; pas de coût de rendu plein écran sur mobile.
            zone.canvasRenderer.cullTransparentMesh = true;
            zone.gameObject.AddComponent<CameraDragZone>().rig = cameraRig;
        }

        /// <summary>Bandeau d'échec : ne capte aucun toucher, le joueur peut repartir pendant qu'il s'estompe.</summary>
        void BuildFallOverlay(Transform root)
        {
            var band = UIFactory.AddPanel(root, "FallOverlay", new Color(0.55f, 0.04f, 0.04f, 0.6f),
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -75f), new Vector2(0f, 75f));
            band.raycastTarget = false;

            fallOverlay = band.gameObject.AddComponent<CanvasGroup>();
            fallOverlay.interactable = false;
            fallOverlay.blocksRaycasts = false;
            fallOverlay.alpha = 0f;

            UIFactory.AddText(band.transform, "FallTitle", "CHUTE !", 56, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -10f), new Vector2(0f, 60f));
            // Le conseil dépend de ce qui a fait tomber le pilote : il est écrit à chaque chute (OnFell).
            fallHint = UIFactory.AddText(band.transform, "FallHint", "", 18, theme.TextMuted, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -55f), new Vector2(0f, -15f));
        }

        void RefreshViewLabel()
        {
            displayedView = cameraRig.CurrentView;
            viewLabel.text = displayedView == CameraView.Exterior ? "VUE : EXT." : "VUE : 1re P.";
        }

        void UpdateSteer()
        {
            float value = (rightHeld ? 1f : 0f) - (leftHeld ? 1f : 0f);
            controller?.SetSteer(value);
        }

        void AddHold(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, UnityAction onPressed, UnityAction onReleased)
        {
            var btn = UIFactory.AddButton(parent, name, label, ControlColor, ControlTextColor,
                Mathf.RoundToInt(S(ControlFontSize)), anchorMin, anchorMax, offsetMin, offsetMax, null);
            btn.transition = Selectable.Transition.None;
            var background = btn.GetComponent<Image>();

            var hold = btn.gameObject.AddComponent<HoldButton>();
            hold.OnPressed.AddListener(() => background.color = ControlPressedColor);
            hold.OnPressed.AddListener(onPressed);
            hold.OnReleased.AddListener(() => background.color = ControlColor);
            hold.OnReleased.AddListener(onReleased);
        }
    }
}
