using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>
    /// La route des paliers, ouverte par le badge de niveau du coin haut droit. Elle répond à la
    /// seule question que pose un niveau : qu'est-ce que je gagne, et dans combien de temps.
    ///
    /// Le tableau de lignes qu'elle était avant disait la même chose, mais ne la montrait pas : six
    /// libellés empilés ne donnent pas le sentiment d'un parcours. Ici les paliers sont posés le long
    /// d'une route qu'on remonte du doigt, la portion déjà faite est peinte en rouge derrière soi, et
    /// le prochain palier de moto se voit venir de loin.
    ///
    /// La route porte le parcours entier, du premier niveau au centième — pas une fenêtre glissante
    /// autour du joueur. C'est ce qui en fait un parcours et non un extrait : on voit d'où l'on vient,
    /// et on peut aller regarder ce qui attend tout au bout.
    ///
    /// La route est du bitume, ligne médiane comprise, et non un sentier de carte au trésor : sur un
    /// jeu de moto, le chemin de la progression est une route.
    ///
    /// Elle court à l'horizontale parce que l'écran est couché. À la verticale, à la manière dont ce
    /// genre de parcours se présente d'ordinaire, le panneau ne montrerait que quatre paliers.
    ///
    /// Bâtie comme <see cref="InfoPopup"/> : un exemplaire par canvas, construit à la première
    /// demande et réutilisé, voile sombre qui avale les clics et referme au toucher. Le champ
    /// statique redevient nul au changement de scène, ce qui déclenche sa reconstruction.
    ///
    /// Les cent nœuds sont bâtis une seule fois, avec le reste de la page : leur place sur la route
    /// ne dépend que de leur numéro, elle ne change jamais. Chaque ouverture ne fait que remettre à
    /// jour ce qui bouge — l'état de chacun et la portion de route parcourue. Les reconstruire à
    /// chaque fois coûterait six cents objets et deux cents textes à l'ouverture d'un menu.
    ///
    /// C'est ici que les récompenses de palier se récupèrent, et nulle part ailleurs : le nœud d'un
    /// palier dû porte un bouton. La page ne remet rien elle-même — elle n'a ni l'écran de choix de
    /// moto ni celui d'ouverture de coffre — elle dit lequel le joueur réclame et laisse le menu
    /// s'en charger.
    /// </summary>
    public static class LevelPopup
    {
        const float PanelWidth = 1320f;

        /// <summary>
        /// Hauteur calée sur ce que la route occupe réellement. Le cas le plus haut est un palier de
        /// moto dû : crête à 62, disque de 88, puis la légende de 34 et le bouton de 56 empilés avec
        /// leurs gouttières, soit 212 unités de part et d'autre de l'axe. Les 220 que laisse cette
        /// hauteur tiennent sans serrer.
        /// </summary>
        const float PanelHeight = 600f;
        const float PanelPadding = 24f;

        /// <summary>Hauteur prise par le titre et la jauge : la route commence dessous.</summary>
        const float HeaderHeight = 136f;
        const float BarHeight = 26f;

        const float NodeSpacing = 196f;
        const float RoadSideMargin = 130f;

        /// <summary>Écart vertical d'un nœud au centre de la route : c'est lui qui la fait serpenter.</summary>
        const float RoadAmplitude = 62f;

        /// <summary>Points calculés par segment de courbe. Assez dense pour que les tirets tombent juste.</summary>
        const int StepsPerSegment = 32;

        const float RoadWidth = 34f;
        const float LaneWidth = 3f;
        const float LaneDash = 16f;
        const float LaneGap = 18f;

        /// <summary>
        /// Taille d'un nœud. Elle ne dépend que de ce que le palier donne, jamais de l'état du
        /// joueur : un nœud qui grossirait parce qu'on vient de l'atteindre ferait bouger sa légende
        /// et son bouton, qui sont posés une fois pour toutes. Ce qu'on a à récupérer se signale par
        /// le remplissage et l'auréole, pas par la taille.
        /// </summary>
        const float NodeSize = 62f;
        const float MilestoneSize = 88f;

        /// <summary>Épaisseur de l'anneau d'un nœud à venir : le cœur sombre est d'autant plus petit.</summary>
        const float NodeRingWidth = 5f;

        /// <summary>Débordement de l'auréole au-delà du disque, en fraction de sa taille.</summary>
        const float HaloSpread = 0.32f;

        const float CaptionWidth = 190f;
        const float CaptionHeight = 34f;

        // Le bouton de récupération se range derrière la légende, et prend la place qu'il faut pour
        // se viser au pouce : une légende de 34 unités de haut ne se touche pas.
        const float ClaimButtonWidth = 214f;
        const float ClaimButtonHeight = 56f;

        /// <summary>Entre la légende et le bouton, assez pour qu'ils ne se lisent pas comme un bloc.</summary>
        const float ClaimButtonGap = 6f;

        /// <summary>Où se place le palier visé dans la fenêtre à l'ouverture, en fraction de largeur.</summary>
        const float FocusAnchor = 0.28f;

        // Métaux des caisses. Ils vivent ici et non dans le catalogue : c'est une couleur de cette
        // page, pas une propriété du coffre — ailleurs, un coffre se montre par son modèle 3D.
        static readonly Color Bronze = new Color(0.72f, 0.45f, 0.20f);
        static readonly Color Argent = new Color(0.78f, 0.79f, 0.82f);
        static readonly Color Or = new Color(0.96f, 0.77f, 0.26f);

        /// <summary>
        /// Un palier sur la route : sa place, ses pièces, et ce qu'il donne. Tout est créé une fois ;
        /// <see cref="RefreshNode"/> ne fait ensuite qu'allumer, éteindre et recolorier.
        /// </summary>
        class RoadNode
        {
            public int Level;
            public LevelReward Reward;
            public Color Tone;
            public string RewardName;

            /// <summary>Place du nœud sur la route, et place où viennent se poser l'auréole et le bouton.</summary>
            public Vector2 Position;
            public float Size;
            public Vector2 ClaimPosition;

            public Image Ring;
            public Image Core;
            public TextMeshProUGUI Number;
            public GameObject CaptionSlot;
            public TextMeshProUGUI Caption;
        }

        static GameObject root;
        static TextMeshProUGUI levelText;
        static TextMeshProUGUI xpText;
        static Image barFill;
        static RectTransform content;
        static RectTransform nodesRoot;
        static RoadRibbon asphalt;
        static RoadRibbon travelled;
        static RoadRibbon lane;
        static UITheme activeTheme;
        static Action<int> claimHandler;

        // Auréoles et bouton sont partagés et se déplacent d'un nœud à l'autre : au plus un palier
        // est à récupérer, et un seul porte le joueur. En donner un exemplaire à chacun des cent
        // paliers, ce serait cinq cents objets construits pour n'en montrer que deux.
        static Image claimHalo;
        static Image currentHalo;
        static GameObject claimSlot;
        static RectTransform claimRect;
        static int claimTarget;

        static readonly List<RoadNode> nodes = new List<RoadNode>();
        static readonly List<Vector2> samples = new List<Vector2>();
        static Vector2[] anchors;
        static float roadWidthTotal;

        /// <summary>
        /// Ouvre la route. <paramref name="onClaim"/> reçoit le palier que le joueur veut encaisser :
        /// la page ne remet rien elle-même, elle n'a ni l'écran de choix de moto ni celui d'ouverture
        /// de coffre. Elle se referme avant d'appeler, pour que l'écran de récompense arrive sur un
        /// menu propre.
        /// </summary>
        public static void Show(Transform canvasRoot, UITheme theme, Action<int> onClaim = null)
        {
            if (canvasRoot == null || theme == null) return;

            activeTheme = theme;
            claimHandler = onClaim;
            if (root == null) Build(canvasRoot, theme);

            Refresh();

            // Remise au premier plan : les écrans construits après elle passeraient sinon par-dessus.
            root.transform.SetAsLastSibling();
            root.SetActive(true);
        }

        public static void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        /// <summary>Referme la route avant de rendre la main : l'écran de récompense doit arriver seul.</summary>
        static void RequestClaim(int level)
        {
            Hide();
            claimHandler?.Invoke(level);
        }

        // -------------------------------------------------------------------------- mise à jour

        static void Refresh()
        {
            // Lus une fois pour les cent nœuds : chacune de ces trois valeurs repasse par la boucle
            // qui déduit le niveau de l'XP, et les interroger par nœud la ferait tourner mille fois.
            int level = LevelManager.Level;
            int claimedThrough = LevelManager.ClaimedThrough;
            int nextClaimable = LevelManager.NextClaimable;
            bool maxed = LevelManager.IsMaxLevel;

            levelText.text = maxed ? $"NIVEAU {level} · MAX" : $"NIVEAU {level}";
            xpText.text = maxed
                ? "PARCOURS TERMINÉ"
                : $"{LevelManager.XpIntoLevel} / {LevelManager.XpForNextLevel} XP";
            barFill.rectTransform.anchorMax = new Vector2(LevelManager.Ratio, 1f);

            // La portion parcourue s'arrête au nœud du joueur. Le premier nœud est le niveau 1, à
            // l'indice zéro des points, et chaque suivant tombe StepsPerSegment plus loin.
            int reached = Mathf.Clamp(level - 1, 0, LevelManager.MaxLevel - 1);
            travelled.SetPath(samples.GetRange(0, reached * StepsPerSegment + 1));

            foreach (var node in nodes)
            {
                RefreshNode(node, level, claimedThrough, nextClaimable);
            }

            // Un palier qui est à la fois dû et celui du joueur ne prend qu'une auréole : la sienne
            // est à la couleur du lot, et deux halos superposés ne feraient qu'une tache plus dense.
            RoadNode claimNode = NodeAt(nextClaimable);
            RoadNode currentNode = claimNode != null && claimNode.Level == level ? null : NodeAt(level);

            PlaceHalo(claimHalo, claimNode, claimNode != null ? claimNode.Tone : Color.white);
            PlaceHalo(currentHalo, currentNode, activeTheme.Accent);

            if (claimNode != null)
            {
                claimTarget = claimNode.Level;
                claimRect.anchoredPosition = claimNode.ClaimPosition;
            }
            claimSlot.SetActive(claimNode != null);

            // La fenêtre se cale sur la première récompense à récupérer plutôt que sur le joueur : un
            // joueur qui a laissé traîner quatre paliers derrière lui les trouve sous les yeux en
            // ouvrant la page, au lieu de devoir remonter la route pour comprendre qu'il a du retard.
            int focus = nextClaimable > 0 ? nextClaimable : level;
            ScrollTo(anchors[Mathf.Clamp(focus - 1, 0, anchors.Length - 1)].x);
        }

        /// <summary>
        /// Quatre états, dans cet ordre de priorité : à récupérer (plein, à la couleur du lot,
        /// auréolé, cliquable), courant (plein, à la couleur de marque), franchi et encaissé (éteint,
        /// la route rouge dit déjà qu'il l'est), à venir (anneau de la couleur du lot sur un cœur
        /// sombre — le lot se lit sans être encore acquis).
        ///
        /// « À récupérer » passe devant « courant » quand un palier est les deux à la fois : la
        /// position du joueur se lit déjà à l'endroit où le rouge de la route s'arrête, alors qu'une
        /// récompense qui attend et ne se voit pas ne sera jamais récupérée.
        ///
        /// Un palier dû dont ce n'est pas encore le tour reste dessiné comme à venir : son lot est
        /// toujours là, mais les paliers se récupèrent dans l'ordre.
        /// </summary>
        static void RefreshNode(RoadNode node, int level, int claimedThrough, int nextClaimable)
        {
            bool pending = node.Reward != null && node.Level > claimedThrough && node.Level <= level;
            bool claimable = pending && node.Level == nextClaimable;
            bool current = node.Level == level;
            bool settled = node.Level <= level && !pending;

            // Cœur sombre seulement sur les nœuds creux : un palier vif est un disque plein.
            bool hollow = !claimable && !current && !settled;
            node.Core.gameObject.SetActive(hollow);

            node.Ring.color = claimable ? node.Tone
                : current ? activeTheme.Accent
                : settled ? activeTheme.PanelAlt
                : node.Tone;

            // Sur un disque plein et clair, le chiffre se lit en sombre : en blanc il disparaîtrait
            // dans l'or d'un palier de moto.
            node.Number.color = claimable ? activeTheme.Background
                : current ? Color.white
                : settled ? activeTheme.NavTextInactive
                : activeTheme.Text;

            // Sur le nœud du joueur, la position prime sur le lot : ce palier est encaissé, et ce
            // qu'il rapportait n'apprend plus rien. Un palier dû prime sur les deux — là, c'est le
            // lot qu'il faut lire, puisqu'on s'apprête à le prendre.
            string caption = current && !claimable ? "TU ES ICI" : node.RewardName;
            node.CaptionSlot.SetActive(!string.IsNullOrEmpty(caption));

            if (!string.IsNullOrEmpty(caption))
            {
                node.Caption.text = caption;
                node.Caption.color = claimable ? node.Tone
                    : current ? activeTheme.Accent
                    : settled ? activeTheme.NavTextLocked
                    : node.Tone;
            }

        }

        static RoadNode NodeAt(int level)
        {
            return level >= 1 && level <= nodes.Count ? nodes[level - 1] : null;
        }

        /// <summary>Pose une auréole partagée sur un nœud, ou l'éteint si aucun ne la réclame.</summary>
        static void PlaceHalo(Image halo, RoadNode node, Color color)
        {
            if (node == null)
            {
                halo.gameObject.SetActive(false);
                return;
            }

            float span = node.Size * (1f + HaloSpread * 2f);
            halo.rectTransform.sizeDelta = new Vector2(span, span);
            halo.rectTransform.anchoredPosition = node.Position;
            halo.color = Tint(color, 0.34f);
            halo.gameObject.SetActive(true);
        }

        static Color Tint(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        /// <summary>Amène le palier visé à <see cref="FocusAnchor"/> de la fenêtre.</summary>
        static void ScrollTo(float nodeX)
        {
            float viewport = PanelWidth - PanelPadding * 2f;
            float target = viewport * FocusAnchor - (nodeX + roadWidthTotal * 0.5f);

            content.anchoredPosition = new Vector2(
                Mathf.Clamp(target, viewport - roadWidthTotal, 0f), 0f);
        }

        // -------------------------------------------------------------------------- construction

        static void Build(Transform canvasRoot, UITheme theme)
        {
            var container = UIFactory.CreateUIObject("LevelPopup", canvasRoot);
            UIFactory.SetRect(container, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root = container.gameObject;

            // Le voile couvre la dalle entière et referme au toucher : pas de cible à viser.
            var veil = UIFactory.AddPanel(container, "Veil", new Color(0f, 0f, 0f, 0.82f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            veil.raycastTarget = true;
            var dismiss = veil.gameObject.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(Hide);

            var panel = UIFactory.AddPanel(container, "Panel", theme.Background,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-PanelWidth * 0.5f, -PanelHeight * 0.5f),
                new Vector2(PanelWidth * 0.5f, PanelHeight * 0.5f), rounded: true);
            panel.raycastTarget = true;
            Transform card = panel.transform;

            levelText = UIFactory.AddText(card, "Level", "", UITheme.FontDisplay, theme.Accent,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -96f), new Vector2(-40f, -32f), FontStyles.Bold | FontStyles.Italic);

            xpText = UIFactory.AddText(card, "Xp", "", UITheme.FontBody, theme.TextMuted,
                TextAnchor.MiddleRight, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -96f), new Vector2(-40f, -32f));

            var track = UIFactory.AddPanel(card, "BarTrack", theme.PanelAlt,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -104f - BarHeight), new Vector2(-40f, -104f), rounded: true);
            track.raycastTarget = false;

            barFill = UIFactory.AddPanel(track.transform, "BarFill", theme.Accent,
                Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, rounded: true);
            barFill.raycastTarget = false;

            BuildRoadView(card, theme);
            BuildRoad(theme);
        }

        /// <summary>
        /// Fenêtre de la route : un masque, une zone défilante à l'horizontale, et dedans les trois
        /// rubans — bitume, portion parcourue, ligne médiane — puis les nœuds par-dessus.
        /// </summary>
        static void BuildRoadView(Transform card, UITheme theme)
        {
            var viewport = UIFactory.CreateUIObject("RoadViewport", card);
            UIFactory.SetRect(viewport, Vector2.zero, Vector2.one,
                new Vector2(PanelPadding, PanelPadding), new Vector2(-PanelPadding, -HeaderHeight));

            // Fond transparent mais capteur : c'est lui qui reçoit le glissement du doigt.
            var surface = viewport.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            surface.raycastTarget = true;

            viewport.gameObject.AddComponent<RectMask2D>();

            content = UIFactory.CreateUIObject("RoadContent", viewport);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.12f;
            scroll.inertia = true;

            // Glisse longtemps après le lâcher : la route fait cent paliers, et un freinage sec
            // obligerait à la balayer une vingtaine de fois pour aller en voir le bout.
            scroll.decelerationRate = 0.5f;
            scroll.scrollSensitivity = 60f;

            asphalt = AddRibbon(content, "Asphalt", new Color(0.14f, 0.14f, 0.157f), RoadWidth);
            travelled = AddRibbon(content, "Travelled", theme.Accent, RoadWidth);

            lane = AddRibbon(content, "Lane", new Color(0.43f, 0.43f, 0.45f), LaneWidth);
            lane.DashLength = LaneDash;
            lane.GapLength = LaneGap;

            nodesRoot = UIFactory.CreateUIObject("Nodes", content);
            UIFactory.SetRect(nodesRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        /// <summary>
        /// Pose la route entière : les cent nœuds et le bitume qui les relie. Appelé une seule fois —
        /// rien là-dedans ne dépend de l'avancement du joueur.
        /// </summary>
        static void BuildRoad(UITheme theme)
        {
            int count = LevelManager.MaxLevel;
            roadWidthTotal = RoadSideMargin * 2f + (count - 1) * NodeSpacing;
            content.sizeDelta = new Vector2(roadWidthTotal, 0f);

            anchors = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                anchors[i] = new Vector2(
                    -roadWidthTotal * 0.5f + RoadSideMargin + i * NodeSpacing,
                    i % 2 == 0 ? -RoadAmplitude : RoadAmplitude);
            }

            Sample(anchors);
            asphalt.SetPath(samples);
            lane.SetPath(samples);

            // L'ordre de création fait l'ordre de rendu : les auréoles d'abord, donc derrière les
            // disques ; le bouton en dernier, donc par-dessus la route et ses légendes.
            claimHalo = BuildSharedHalo("ClaimHalo");
            currentHalo = BuildSharedHalo("CurrentHalo");

            nodes.Clear();
            for (int i = 0; i < count; i++)
            {
                nodes.Add(BuildNode(i + 1, anchors[i], theme));
            }

            BuildSharedClaimButton(theme);
        }

        static Image BuildSharedHalo(string name)
        {
            var halo = UIFactory.AddGlow(nodesRoot, name, Color.clear,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            halo.gameObject.SetActive(false);
            return halo;
        }

        /// <summary>
        /// Le bouton unique, déplacé sur le palier dû. Son écouteur lit <c>claimTarget</c> plutôt que
        /// de capturer un niveau : il est posé une fois pour toutes, alors que le palier qu'il réclame
        /// change à chaque récupération.
        /// </summary>
        static void BuildSharedClaimButton(UITheme theme)
        {
            claimRect = AddSlot("ClaimSlot", Vector2.zero, ClaimButtonWidth, ClaimButtonHeight);
            claimSlot = claimRect.gameObject;

            UIFactory.AddButton(claimRect, "Action", "RÉCUPÉRER", theme.Accent, Color.white,
                UITheme.FontLabel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                () => RequestClaim(claimTarget), ButtonKind.Primary);

            claimSlot.SetActive(false);
        }

        static RoadNode BuildNode(int level, Vector2 position, UITheme theme)
        {
            var reward = LevelRewardCatalog.RewardFor(level);
            bool milestone = reward != null && reward.IsMotoChoice;
            float size = milestone ? MilestoneSize : NodeSize;

            var node = new RoadNode
            {
                Level = level,
                Reward = reward,
                Tone = RewardColor(reward),
                RewardName = DescribeReward(reward),
            };

            node.Position = position;
            node.Size = size;

            var body = UIFactory.CreateUIObject("Node_" + level, nodesRoot);
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
            body.pivot = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(size, size);
            body.anchoredPosition = position;

            node.Ring = AddDisc(body, "Ring", node.Tone, size);
            node.Core = AddDisc(body, "Core", theme.Background, size - NodeRingWidth * 2f);

            // Le disque répond au doigt, en plus du bouton posé plus loin : on vise naturellement la
            // récompense, pas l'étiquette à côté. Il ne fait rien tant que le palier n'est pas dû —
            // le bouton, lui, disparaît.
            node.Ring.raycastTarget = true;
            var press = node.Ring.gameObject.AddComponent<Button>();
            press.targetGraphic = node.Ring;
            press.onClick.AddListener(() => TryClaim(level));

            node.Number = UIFactory.AddText(body, "Level", level.ToString(), UITheme.FontBody,
                theme.Text, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                FontStyles.Bold | FontStyles.Italic);

            // Tout se range du côté extérieur de la courbe : sous les nœuds du creux, au-dessus de
            // ceux de la crête. Toujours dessous, la légende tombait en travers du bitume une fois
            // sur deux — la route redescend précisément là où le texte se serait écrit.
            float side = position.y > 0f ? 1f : -1f;
            float edge = position.y + side * (size * 0.5f + 10f);

            var caption = AddSlot("Caption_" + level,
                new Vector2(position.x, edge + side * CaptionHeight * 0.5f), CaptionWidth, CaptionHeight);
            node.CaptionSlot = caption.gameObject;
            node.Caption = UIFactory.AddText(caption, "Label", node.RewardName, UITheme.FontLabel,
                node.Tone, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                FontStyles.Bold | FontStyles.Italic);

            // Le bouton partagé viendra se poser ici : derrière la légende, plus loin du nœud. On
            // lit ce qu'on prend, puis on le prend — le bouton seul laissait cliquer sans savoir sur
            // quoi.
            node.ClaimPosition = new Vector2(position.x,
                edge + side * (CaptionHeight + ClaimButtonGap + ClaimButtonHeight * 0.5f));

            return node;
        }

        /// <summary>
        /// Appui sur le disque d'un nœud. Il ne réclame que si ce palier est bien celui qui est dû :
        /// les cent disques sont cliquables en permanence, c'est l'état qui décide, pas le bouton.
        /// </summary>
        static void TryClaim(int level)
        {
            if (LevelManager.NextClaimable != level) return;

            RequestClaim(level);
        }

        static RectTransform AddSlot(string name, Vector2 centre, float width, float height)
        {
            var slot = UIFactory.CreateUIObject(name, nodesRoot);
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.sizeDelta = new Vector2(width, height);
            slot.anchoredPosition = centre;
            return slot;
        }

        static Image AddDisc(Transform parent, string name, Color color, float size)
        {
            var rt = UIFactory.CreateUIObject(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;

            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = FxAssets.Disc;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static RoadRibbon AddRibbon(Transform parent, string name, Color color, float thickness)
        {
            var rt = UIFactory.CreateUIObject(name, parent);
            UIFactory.SetRect(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var ribbon = rt.gameObject.AddComponent<RoadRibbon>();
            ribbon.Thickness = thickness;
            ribbon.color = color;
            ribbon.raycastTarget = false;
            return ribbon;
        }

        // ------------------------------------------------------------------------------ géométrie

        /// <summary>
        /// Échantillonne la serpentine passant par les nœuds. Chaque segment est une cubique dont les
        /// tangentes sont horizontales aux deux bouts : la route arrive et repart à plat de chaque
        /// nœud, donc la pastille s'y pose droite et la courbe ne fait pas de coude.
        /// </summary>
        static void Sample(Vector2[] points)
        {
            samples.Clear();
            samples.Add(points[0]);

            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 p0 = points[i];
                Vector2 p3 = points[i + 1];
                var p1 = new Vector2(p0.x + NodeSpacing * 0.5f, p0.y);
                var p2 = new Vector2(p3.x - NodeSpacing * 0.5f, p3.y);

                for (int step = 1; step <= StepsPerSegment; step++)
                {
                    samples.Add(Cubic(p0, p1, p2, p3, (float)step / StepsPerSegment));
                }
            }
        }

        static Vector2 Cubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        // ---------------------------------------------------------------------------- récompenses

        static string DescribeReward(LevelReward reward)
        {
            if (reward == null) return string.Empty;
            if (reward.IsMotoChoice) return "MOTO AU CHOIX";

            var chest = ChestCatalog.Find(reward.ChestId);
            return chest != null ? chest.Name.ToUpperInvariant() : reward.ChestId.ToUpperInvariant();
        }

        static Color RewardColor(LevelReward reward)
        {
            if (reward == null) return activeTheme.PanelAlt;
            if (reward.IsMotoChoice) return Or;

            if (reward.ChestId == LevelRewardCatalog.GoldChest) return Or;
            if (reward.ChestId == LevelRewardCatalog.SilverChest) return Argent;
            return Bronze;
        }
    }
}
