using UnityEngine;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Barre verticale de la jauge d'angle, dessinée du même trait que le cadran du compteur (<see cref="SoftMesh"/>) :
    /// une piste fine aux bouts arrondis, cernée d'un liseré sombre, sur laquelle les zones se lisent en teinte
    /// (équilibre en vert, critique en rouge). L'angle la remplit depuis le bas, chaque tronçon à la couleur de sa
    /// zone, et une pastille en travers marque l'angle exact. Les graduations, à droite, s'allument au passage.
    ///
    /// Angles exprimés en part de l'angle de chute, de 0 (en bas) à 1 (en haut).
    /// </summary>
    public class WheelieBar : MaskableGraphic
    {
        public static readonly Color BalanceColor = new Color(0.3f, 0.95f, 0.45f, 1f);
        public static readonly Color CriticalColor = new Color(1f, 0.25f, 0.25f, 1f);
        static readonly Color RisingColor = new Color(1f, 1f, 1f, 0.96f);
        static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.2f);
        static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.35f);
        static readonly Color AlertOutlineColor = new Color(1f, 0.25f, 0.25f, 0.85f);
        static readonly Color MarkerOutlineColor = new Color(0f, 0f, 0f, 0.55f);
        // Teinte des zones sur la piste : assez présente pour se lire, assez légère pour que le remplissage domine.
        // La zone critique, bien plus haute que celle d'équilibre, est plus discrète : c'est le vert qu'on cherche.
        const float BalanceZoneAlpha = 0.5f;
        const float CriticalZoneAlpha = 0.3f;

        // Gabarit, en unités de canvas, depuis le bord gauche du rectangle : la pastille part du bord, la piste
        // est au milieu de la pastille, les graduations après elle.
        const float TrackX = 14f;
        const float TrackWidth = 10f;
        const float OutlineWidth = 18f;
        const float MarkerHalfLength = 10f;
        const float MarkerWidth = 8f;
        const float MarkerOutline = 5f;
        const float TickX = 32f;
        const float MajorTickLength = 10f;
        const float MinorTickLength = 6f;
        const float MajorTickWidth = 3f;
        const float MinorTickWidth = 2f;
        /// <summary>Largeur dessinée, graduations comprises.</summary>
        public const float DrawnWidth = TickX + MajorTickLength;
        // Retrait en haut et en bas : les bouts arrondis et la pastille restent dans le rectangle.
        const float Inset = 10f;

        // Graduations tous les 5°, les dizaines plus longues. Plus serrées que MinTickSpacing, les petites
        // disparaissent, puis les grandes s'espacent.
        const float MinorStepDegrees = 5f;
        const float MinTickSpacing = 14f;
        // Une graduation s'allume sur cette part de la barre, à l'approche de l'angle.
        const float LightSpan = 0.01f;
        const float Epsilon = 0.0005f;

        float angle;
        float sweetMin = 0.45f;
        float sweetMax = 0.55f;
        float fallAngle = 60f;
        bool alert;

        /// <summary>Angle courant, en part de l'angle de chute.</summary>
        public float Value
        {
            get => angle;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Abs(value - angle) < Epsilon) return;
                angle = value;
                SetVerticesDirty();
            }
        }

        /// <summary>Liseré rouge : l'éclat du clignotement en zone critique.</summary>
        public bool Alert
        {
            get => alert;
            set
            {
                if (value == alert) return;
                alert = value;
                SetVerticesDirty();
            }
        }

        /// <summary>Bornes de la zone d'équilibre et angle de chute, en degrés. Sans effet si rien n'a changé.</summary>
        public void SetZones(float sweetMinDegrees, float sweetMaxDegrees, float fallDegrees)
        {
            float fall = Mathf.Max(1f, fallDegrees);
            float min = Mathf.Clamp01(sweetMinDegrees / fall);
            float max = Mathf.Clamp(sweetMaxDegrees / fall, min, 1f);
            if (Mathf.Approximately(min, sweetMin) && Mathf.Approximately(max, sweetMax) && Mathf.Approximately(fall, fallAngle)) return;

            sweetMin = min;
            sweetMax = max;
            fallAngle = fall;
            SetVerticesDirty();
        }

        Color ZoneColor(float t) => t >= sweetMax ? CriticalColor : t >= sweetMin ? BalanceColor : RisingColor;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            float x = rect.xMin + TrackX;
            float bottom = rect.yMin + Inset;
            float top = rect.yMax - Inset;
            if (top <= bottom) return;

            var mesh = new SoftMesh(this, vh);
            float half = TrackWidth * 0.5f;
            Vector2 across = Vector2.right;
            Vector2 At(float t) => new Vector2(x, Mathf.Lerp(bottom, top, t));

            // Liseré sombre sous la piste : il la détache de la ville, et passe au rouge au bord de la chute.
            mesh.Capsule(At(0f), At(1f), OutlineWidth, alert ? AlertOutlineColor : OutlineColor);
            mesh.Capsule(At(0f), At(1f), TrackWidth, TrackColor);

            // Zones teintées sur la piste. La zone critique monte jusqu'au bout arrondi du haut.
            Color balance = BalanceColor;
            balance.a = BalanceZoneAlpha;
            if (sweetMax > sweetMin) mesh.Line(At(sweetMin), At(sweetMax), TrackWidth, balance);
            if (sweetMax < 1f)
            {
                Color critical = CriticalColor;
                critical.a = CriticalZoneAlpha;
                mesh.Row(At(sweetMax), across, half, critical, false);
                mesh.Row(At(1f), across, half, critical, true);
                mesh.Cap(At(1f), across, Vector2.up, half, critical);
            }

            AddFill(mesh, At(0f), At(angle), half);
            AddTicks(mesh, rect.xMin + TickX, bottom, top);

            // Pastille de l'angle, cernée de sombre pour se détacher du remplissage de même couleur.
            Vector2 marker = At(angle);
            Vector2 reach = across * MarkerHalfLength;
            mesh.Capsule(marker - reach, marker + reach, MarkerWidth + MarkerOutline, MarkerOutlineColor);
            mesh.Capsule(marker - reach, marker + reach, MarkerWidth, ZoneColor(angle));
        }

        /// <summary>
        /// Remplissage du bas jusqu'à l'angle, chaque tronçon à la couleur de sa zone : le changement de teinte tombe
        /// net sur la borne, deux rangées au même endroit.
        /// </summary>
        void AddFill(SoftMesh mesh, Vector2 from, Vector2 to, float half)
        {
            Vector2 across = Vector2.right;
            Color start = ZoneColor(0f);
            Color end = ZoneColor(angle);

            mesh.Row(from, across, half, start, false);
            if (sweetMin > 0f && angle > sweetMin)
            {
                Vector2 border = Vector2.Lerp(from, to, sweetMin / angle);
                mesh.Row(border, across, half, RisingColor, true);
                mesh.Row(border, across, half, BalanceColor, true);
            }
            if (angle > sweetMax)
            {
                Vector2 border = Vector2.Lerp(from, to, sweetMax / angle);
                mesh.Row(border, across, half, BalanceColor, true);
                mesh.Row(border, across, half, CriticalColor, true);
            }
            mesh.Row(to, across, half, end, true);

            mesh.Cap(from, across, Vector2.down, half, start);
            mesh.Cap(to, across, Vector2.up, half, end);
        }

        void AddTicks(SoftMesh mesh, float left, float bottom, float top)
        {
            float height = top - bottom;
            float step = MinorStepDegrees;
            bool minors = step / fallAngle * height >= MinTickSpacing;
            if (!minors) step *= 2f;
            while (step / fallAngle * height < MinTickSpacing && step < fallAngle) step *= 2f;

            for (int i = 0; i * step <= fallAngle + 0.01f; i++)
            {
                float t = Mathf.Clamp01(i * step / fallAngle);
                bool major = !minors || i % 2 == 0;
                float lit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(t - LightSpan, t, angle));

                // Éteintes, elles gardent la teinte de leur zone : l'échelle dit elle aussi où est l'équilibre.
                Color on = ZoneColor(t);
                Color off = on;
                on.a = major ? 1f : 0.7f;
                off.a = major ? 0.34f : 0.18f;

                float y = Mathf.Lerp(bottom, top, t);
                float length = major ? MajorTickLength : MinorTickLength;
                mesh.Line(new Vector2(left, y), new Vector2(left + length, y), major ? MajorTickWidth : MinorTickWidth,
                    Color.Lerp(off, on, lit));
            }
        }
    }
}
