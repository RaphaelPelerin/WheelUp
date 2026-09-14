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
    /// Paramètres ; GAZ, LEVER, FREIN AV et FREIN), vitesse, jauge d'angle (wheeling ou roue avant), bandeau
    /// « Chute ! », menu de chute, bouton de changement de vue et zone de glissement pour tourner la caméra.
    /// Pilote à terre, les commandes sont rangées : on ne conduit pas une moto couchée.
    ///
    /// Disposition, pensée pour un iPhone tenu en paysage :
    /// - à gauche, le cumul des prouesses, la jauge d'angle, puis la direction tout en bas ; la jauge est
    ///   plaquée contre le bord, au ras de la marge de la zone sûre ;
    /// - en haut au centre, la vitesse, le rapport et le régime ; en haut à droite, VUE et PAUSE, puis les
    ///   points de la figure en cours juste en dessous ;
    /// - en bas à droite, les quatre pédales en carré : LEVER et FREIN AV en haut, GAZ et FREIN en bas.
    ///   Les deux commandes qui lèvent la roue, LEVER et GAZ, sont l'une sur l'autre dans la colonne de
    ///   gauche, le pouce passant de l'une à l'autre sans quitter le bloc ; les deux freins occupent la
    ///   colonne de droite, chacun au niveau de la commande qu'il contre.
    /// Tout vit dans la zone sûre de l'écran (<see cref="SafeArea"/>) : la Dynamic Island et l'encoche, qui
    /// mangent un bord en paysage, la barre d'accueil et l'arc des coins arrondis en sont écartés. Rien
    /// d'important n'est posé derrière. Les boutons reprennent l'arrondi continu des coins de l'iPhone.
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
        const float PedalWidth = 230f;
        const float PedalHeight = 190f;
        const int ControlFontSize = UITheme.FontTitle;
        const int PedalFontSize = UITheme.FontHeading;
        // Rangée du haut : VUE et PAUSE, assez hauts pour un doigt (35 pt sur iPhone).
        const float TopButtonWidth = 190f;
        const float TopButtonHeight = 88f;
        const int TopFontSize = UITheme.FontLabel;
        // Cumul des prouesses en haut à gauche ; le compteur en direct est à droite, sous VUE et PAUSE.
        const float TotalsHeight = 58f;
        const float LiveScoreHeight = StuntScoreHud.LiveHeight;
        // Hauteur minimale de la barre de la jauge : en dessous, l'angle ne se lirait plus.
        const float GaugeMinBar = 120f;
        // Marge au bord de la zone sûre : jamais moins que MinMargin, et sinon cette part du petit côté de
        // l'écran. Sur un téléphone à coins arrondis, 24 unités (2 mm) frôlent l'arrondi et la barre d'accueil.
        const float MinMargin = 24f;
        const float MarginShare = 0.045f;
        // Couloir laissé libre entre les deux piles de boutons : la route doit rester visible entre les pouces.
        const float MinCorridor = 200f;

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
        TextMeshProUGUI gearText;
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
                // Régime arrondi à la centaine : au chiffre près, il clignoterait illisible.
                int rpm = Mathf.RoundToInt(controller.EngineRpm / 100f) * 100;
                gearText.text = $"{controller.Gear}e  ·  {StuntScoreHud.Format(rpm)} tr/min";
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
        /// Cale le gabarit sur l'écran réel. La marge suit la taille de l'écran, pour que rien ne colle au bord
        /// ni aux coins arrondis ; et si le gabarit complet ne tient pas dans la zone sûre (écran étroit, format
        /// inhabituel), tout est réduit d'un même facteur. Sans cela, les piles de boutons finissent par se
        /// chevaucher, recouvrir la jauge et déborder de l'écran.
        /// </summary>
        void MeasureControls()
        {
            Vector2 safe = UsableSafeAreaSize();
            margin = Mathf.Max(MinMargin, Mathf.Min(safe.x, safe.y) * MarginShare);

            float steerWidth = Mathf.Max(SteerButtonSize * 2f + Gap, JoystickSize);
            float steerHeight = Mathf.Max(SteerButtonSize, JoystickSize);
            float pedalsWidth = PedalWidth * 2f + Gap;
            float pedalsHeight = PedalHeight * 2f + Gap;

            float needWidth = 2f * margin + steerWidth + pedalsWidth + MinCorridor;
            // Colonne de gauche : cumul, jauge (titre, barre, libellés) puis direction.
            float leftHeight = TotalsHeight + Gap
                + WheelieGauge.SpaceAbove + GaugeMinBar + WheelieGauge.SpaceBelow + Gap + steerHeight;
            // Colonne de droite : VUE et PAUSE, les points en direct, puis les pédales.
            float rightHeight = TopButtonHeight + Gap + LiveScoreHeight + Gap + pedalsHeight;
            float needHeight = 2f * margin + Mathf.Max(leftHeight, rightHeight);

            controlScale = Mathf.Clamp(Mathf.Min(safe.x / needWidth, safe.y / needHeight), 0.5f, 1f);
        }

        /// <summary>
        /// Zone sûre utilisable, en unités de canvas : celle du système, moins le retrait des coins arrondis que
        /// <see cref="SafeArea"/> applique sur un écran découpé.
        /// </summary>
        static Vector2 UsableSafeAreaSize()
        {
            Vector2 safe = UIFactory.SafeAreaSize();
            bool cutout = Screen.safeArea.width < Screen.width || Screen.safeArea.height < Screen.height;
            if (cutout) safe -= Vector2.one * (2f * SafeArea.DefaultCornerInset);
            return safe;
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

            // Zone sûre avec retrait des coins : Dynamic Island, barre d'accueil et arcs des coins écartés.
            Transform safeArea = UIFactory.AddSafeArea(root, "SafeArea").transform;

            float topButtonHeight = S(TopButtonHeight);

            // En haut au centre, à la hauteur des boutons : le compteur reste dans le champ de vision.
            speedText = UIFactory.AddText(safeArea, "SpeedText", "0 km/h", UITheme.FontTitle, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-170, -margin - topButtonHeight), new Vector2(170, -margin), FontStyles.Bold);
            // Rapport et régime juste dessous : on voit la boîte monter les rapports et le moteur prendre ses tours.
            gearText = UIFactory.AddText(safeArea, "GearText", "", UITheme.FontLabel, theme.TextMuted, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-170, -margin - topButtonHeight - 34), new Vector2(170, -margin - topButtonHeight));

            // La rangée du haut est rentrée d'un cran depuis le bord : elle tombait trop près de l'angle de
            // l'écran, là où la main qui tient le téléphone mord déjà.
            float rightEdge = margin + S(Gap);

            // Pause plutôt que retour direct au menu : on peut y régler les Paramètres sans quitter la partie.
            Vector2 topRight = new Vector2(1, 1);
            float buttonWidth = S(TopButtonWidth);
            var pauseButton = UIFactory.AddButton(safeArea, "PauseButton", "II  PAUSE", ControlColor, ControlTextColor,
                Mathf.RoundToInt(S(TopFontSize)), topRight, topRight,
                new Vector2(-rightEdge - buttonWidth, -margin - topButtonHeight), new Vector2(-rightEdge, -margin),
                () => pauseMenu.Open());
            UIFactory.ApplyScreenCorners(pauseButton.image);

            // Tout ce qui ne sert qu'en roulant (commandes, jauge d'angle, changement de vue) est réuni sous
            // un même objet : la chute le range d'un coup, et les boutons restés enfoncés se relâchent avec lui
            // (HoldButton et SteerJoystick lâchent tout à la désactivation).
            var driving = UIFactory.CreateUIObject("Driving", safeArea);
            UIFactory.SetRect(driving, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            controlsRoot = driving.gameObject;

            if (cameraRig != null)
            {
                float viewRight = -rightEdge - buttonWidth - S(Gap);
                var viewButton = UIFactory.AddButton(driving, "ViewButton", "VUE", ControlColor, ControlTextColor,
                    Mathf.RoundToInt(S(TopFontSize)), topRight, topRight,
                    new Vector2(viewRight - buttonWidth, -margin - topButtonHeight), new Vector2(viewRight, -margin),
                    () => cameraRig.ToggleView());
                UIFactory.ApplyScreenCorners(viewButton.image);
                viewLabel = viewButton.GetComponentInChildren<TextMeshProUGUI>();
                RefreshViewLabel();
            }

            bool joystick = SettingsManager.Steering == SteeringControl.Joystick;
            float steerHeight = S(joystick ? JoystickSize : SteerButtonSize);
            if (controller != null)
            {
                // Points de la figure en cours dans la colonne de droite, sous VUE et PAUSE, et rentrés du
                // bord du même cran que ces boutons : c'est l'autre main, celle qui ne surveille pas l'angle.
                stunts = new StuntScoreHud();
                stunts.Build(safeArea, theme, controller,
                    edge: rightEdge,
                    liveTop: margin + topButtonHeight + S(Gap));

                // Jauge contre le bord gauche, sous le cumul et juste au-dessus des boutons de direction :
                // l'angle se surveille du coin de l'oeil, donc la barre sort du champ où les pouces passent.
                // Elle était auparavant centrée sur le bloc de direction, ce qui la ramenait vers le milieu
                // de l'écran. Le bord est sûr : la jauge est posée dans la zone sûre, qui écarte déjà la
                // Dynamic Island et l'arrondi des coins.
                gauge = new WheelieGauge();
                gauge.Build(driving, theme, controller,
                    left: margin,
                    top: margin + TotalsHeight + S(Gap),
                    bottom: margin + steerHeight + S(Gap));
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
                crashMenu.Build(root, theme, controller, OnQuitToMenu);
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
            AddHold(parent, "SteerLeft", "«", ControlFontSize, Vector2.zero,
                new Vector2(margin, margin), new Vector2(margin + size, margin + size),
                () => { leftHeld = true; UpdateSteer(); }, () => { leftHeld = false; UpdateSteer(); });

            float x = margin + size + S(Gap);
            AddHold(parent, "SteerRight", "»", ControlFontSize, Vector2.zero,
                new Vector2(x, margin), new Vector2(x + size, margin + size),
                () => { rightHeld = true; UpdateSteer(); }, () => { rightHeld = false; UpdateSteer(); });
        }

        /// <summary>
        /// Joystick horizontal : la direction suit l'écart du doigt et revient au milieu au relâchement.
        ///
        /// La zone de préhension court sur toute la moitié gauche de l'écran, et non plus sur un disque à la
        /// taille du pouce. C'est la course du doigt qui donne la finesse du braquage : un disque de
        /// JoystickSize ne laissait qu'une centaine d'unités de part et d'autre du centre, donc un tout petit
        /// déplacement suffisait à passer de tout droit à braquage maximal. Ancrée en fraction de l'écran
        /// plutôt qu'en unités fixes, la zone occupe la même moitié sur tous les formats.
        ///
        /// La piste visible, elle, reste à la hauteur de la manette : une dalle de la hauteur de la zone
        /// masquerait le quart de la route. Le joueur attrape donc bien plus large que ce qu'il voit.
        /// </summary>
        void BuildJoystick(Transform parent)
        {
            var area = UIFactory.CreateUIObject("SteerJoystick", parent);
            UIFactory.SetRect(area, new Vector2(0f, 0f), new Vector2(0.5f, 0f),
                new Vector2(margin, margin), new Vector2(-S(Gap), margin + S(JoystickSize)));

            // Transparente mais captante : c'est ce rectangle que SteerJoystick mesure pour la course.
            var surface = area.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            surface.raycastTarget = true;

            // Piste visible, centrée en hauteur sur la manette : la zone reste plus haute qu'elle, pour que
            // le pouce puisse dériver vers le haut ou le bas sans lâcher la direction.
            float trackHalf = S(JoystickKnobSize) * 0.5f;
            var track = UIFactory.AddPanel(area, "Track", ControlColor, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, -trackHalf), new Vector2(0f, trackHalf), rounded: true);
            track.raycastTarget = false;

            // Rail horizontal : le joystick ne sert qu'à diriger.
            var rail = UIFactory.AddPanel(area, "Rail", JoystickRailColor, new Vector2(0.02f, 0.5f), new Vector2(0.98f, 0.5f),
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

        /// <summary>
        /// Quatre pédales en carré dans le coin bas-droit : FREIN et FREIN AV en haut, LEVER et GAZ en bas.
        /// GAZ, la plus utilisée, occupe le coin, là où le pouce se pose ; le frein avant est juste au-dessus,
        /// comme le levier au-dessus de la poignée. LEVER et FREIN sont l'un sur l'autre : un wheeling se
        /// tient en basculant le pouce de l'un à l'autre.
        /// </summary>
        void BuildPedals(Transform parent)
        {
            Vector2 corner = new Vector2(1f, 0f);
            float width = S(PedalWidth);
            float height = S(PedalHeight);
            float gap = S(Gap);

            float rightColumn = -margin - width;
            float leftColumn = rightColumn - gap - width;
            float bottomRow = margin;
            float topRow = margin + height + gap;

            AddHold(parent, "ThrottleButton", "GAZ", PedalFontSize, corner,
                new Vector2(leftColumn, bottomRow), new Vector2(leftColumn + width, bottomRow + height),
                () => controller?.SetThrottle(true), () => controller?.SetThrottle(false));

            AddHold(parent, "LiftButton", "LEVER", PedalFontSize, corner,
                new Vector2(leftColumn, topRow), new Vector2(leftColumn + width, topRow + height),
                () => controller?.SetLift(true), () => controller?.SetLift(false));

            AddHold(parent, "FrontBrakeButton", "FREIN AV", PedalFontSize, corner,
                new Vector2(rightColumn, topRow), new Vector2(rightColumn + width, topRow + height),
                () => controller?.SetFrontBrake(true), () => controller?.SetFrontBrake(false));

            AddHold(parent, "RearBrakeButton", "FREIN", PedalFontSize, corner,
                new Vector2(rightColumn, bottomRow), new Vector2(rightColumn + width, bottomRow + height),
                () => controller?.SetRearBrake(true), () => controller?.SetRearBrake(false));
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
            // Le fond traverse l'écran d'un bord à l'autre ; ses textes, centrés, restent loin de la Dynamic Island.
            var band = UIFactory.AddPanel(root, "FallOverlay", new Color(0.55f, 0.04f, 0.04f, 0.6f),
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -75f), new Vector2(0f, 75f));
            band.raycastTarget = false;

            fallOverlay = band.gameObject.AddComponent<CanvasGroup>();
            fallOverlay.interactable = false;
            fallOverlay.blocksRaycasts = false;
            fallOverlay.alpha = 0f;

            UIFactory.AddText(band.transform, "FallTitle", "CHUTE !", UITheme.FontDisplay, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.15f, 0.5f), new Vector2(0.85f, 0.5f), new Vector2(0f, -10f), new Vector2(0f, 60f));
            // Le conseil dépend de ce qui a fait tomber le pilote : il est écrit à chaque chute (OnFell).
            fallHint = UIFactory.AddText(band.transform, "FallHint", "", UITheme.FontLabel, theme.TextMuted, TextAnchor.MiddleCenter,
                new Vector2(0.15f, 0.5f), new Vector2(0.85f, 0.5f), new Vector2(0f, -55f), new Vector2(0f, -15f));
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

        /// <summary>Bouton à maintenir, ancré sur un coin de la zone sûre, aux coins arrondis de l'iPhone.</summary>
        void AddHold(Transform parent, string name, string label, int fontSize, Vector2 anchor, Vector2 offsetMin, Vector2 offsetMax, UnityAction onPressed, UnityAction onReleased)
        {
            var btn = UIFactory.AddButton(parent, name, label, ControlColor, ControlTextColor,
                Mathf.RoundToInt(S(fontSize)), anchor, anchor, offsetMin, offsetMax, null);
            btn.transition = Selectable.Transition.None;
            var background = btn.GetComponent<Image>();
            UIFactory.ApplyScreenCorners(background);

            var hold = btn.gameObject.AddComponent<HoldButton>();
            hold.OnPressed.AddListener(() => background.color = ControlPressedColor);
            hold.OnPressed.AddListener(onPressed);
            hold.OnReleased.AddListener(() => background.color = ControlColor);
            hold.OnReleased.AddListener(onReleased);
        }
    }
}
