using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Aperçu 3D d'un coffre et animation d'ouverture : tremblement qui monte en intensité, puis bascule
    /// du modèle fermé vers le modèle ouvert accompagnée d'un éclat à la couleur de la rareté.
    /// Le tremblement et l'éclat portent sur l'UI, donc la séquence reste lisible même sans modèles 3D.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChestPreview : ModelPreviewRig
    {
        /// <summary>Lacet de la pose trois-quarts : on voit la face avant et un flanc du coffre ouvert.</summary>
        const float OpenYaw = -34f;
        /// <summary>
        /// Rapprochement de la caméra sur l'écran d'ouverture : le coffre y est le seul sujet, le
        /// cadrage automatique le laissait bien trop au large.
        /// </summary>
        const float OpeningZoom = 1.3f;
        const float SettleDuration = 0.45f;

        const float FlashDuration = 0.55f;
        const float MaxShakeOffset = 16f;
        const float MaxShakeTilt = 5f;

        /// <summary>Degrés de rotation par unité de Canvas parcourue par le doigt.</summary>
        const float DragSensitivity = 0.45f;
        /// <summary>Vitesse d'amortissement de l'élan après un glissement, en fractions par seconde.</summary>
        const float SpinDamping = 1.6f;
        /// <summary>Au-delà, un geste très rapide enverrait le coffre en toupie illisible.</summary>
        const float MaxSpinSpeed = 900f;
        /// <summary>Le coffre reste dans cette plage de tangage : au-delà on le voit par en dessous.</summary>
        const float MaxPitch = 70f;

        public float idleSpin = 12f;
        public float idleBob = 6f;

        public bool IsPlaying { get; private set; }

        RectTransform shakeTarget;
        Image flash;
        Vector2 shakeHome;

        ChestFx fx;

        ChestInfo currentChest;
        GameObject closedModel;
        GameObject openModel;
        bool opened;

        // Orientation reconstruite à chaque image depuis un lacet et un tangage plutôt qu'accumulée
        // dans le quaternion du plateau : en accumulant, le tangage finirait par faire rouler le
        // coffre sur le côté dès qu'on mélange les deux axes.
        float yaw;
        float pitch;
        Vector2 spinVelocity;   // degrés par seconde, x = lacet, y = tangage
        bool freeSpin = true;
        float charge;

        public static ChestPreview Create(RawImage output, RectTransform shakeTarget, Image flash)
        {
            var go = new GameObject("ChestPreview");
            var preview = go.AddComponent<ChestPreview>();
            preview.SetupRig(output, AllocateRigPosition());
            preview.shakeTarget = shakeTarget;
            preview.shakeHome = shakeTarget.anchoredPosition;
            preview.flash = flash;
            preview.flash.color = new Color(1f, 1f, 1f, 0f);

            // Monté sur la racine du rig et non sur le plateau : les particules ne doivent pas tourner
            // avec le coffre, sinon la colonne de lumière pivote avec lui.
            preview.fx = ChestFx.Create(preview.transform);
            return preview;
        }

        /// <summary>Présente un coffre fermé. Retourne false si aucun modèle 3D n'est disponible.</summary>
        public bool ShowChest(ChestInfo chest)
        {
            if (currentChest == chest && !opened) return closedModel != null;

            currentChest = chest;
            opened = false;
            yaw = 0f;
            pitch = 0f;
            spinVelocity = Vector2.zero;
            freeSpin = true;
            charge = 0f;

            if (closedModel != null) Destroy(closedModel);
            if (openModel != null) Destroy(openModel);

            // Modèles optionnels : tant que ceux de Meshy ne sont pas là, l'animation se joue en UI seule.
            closedModel = LoadModel(chest?.ClosedModelPath, warnIfMissing: false);
            openModel = LoadModel(chest?.OpenModelPath, warnIfMissing: false);

            if (closedModel == null)
            {
                if (openModel != null) openModel.SetActive(false);
                output.enabled = false;
                return false;
            }

            // Cadrage unique, calculé sur les deux états : le corps du coffre ne bouge plus à
            // l'ouverture, seul le couvercle se lève. Le masquage vient après, car un Renderer
            // désactivé ne fournit pas de bornes.
            FramePair(closedModel, openModel);
            SetZoom(OpeningZoom);
            if (openModel != null) openModel.SetActive(false);

            if (fx != null) fx.Calm();

            output.enabled = true;
            return true;
        }

        /// <summary>
        /// Joue la séquence d'ouverture. onReveal est appelé au moment exact de la bascule, pour que
        /// le gain apparaisse en même temps que le coffre s'ouvre.
        /// </summary>
        public void PlayOpening(Color rarityColor, Color confettiColor, System.Action onReveal, System.Action onComplete = null)
        {
            if (IsPlaying) return;

            StartCoroutine(OpeningRoutine(rarityColor, confettiColor, onReveal, onComplete));
        }

        IEnumerator OpeningRoutine(Color rarityColor, Color confettiColor, System.Action onReveal, System.Action onComplete)
        {
            IsPlaying = true;
            if (fx != null) fx.SetRarity(rarityColor, confettiColor);

            // La montée en tension a déjà eu lieu pendant le maintien du joueur (voir SetCharge) :
            // la rejouer ici ajouterait une seconde d'attente après un geste déjà terminé.
            float elapsed;
            charge = 0f;
            shakeTarget.anchoredPosition = shakeHome;
            shakeTarget.localRotation = Quaternion.identity;

            SwapToOpen();
            if (fx != null) fx.Burst();
            onReveal?.Invoke();

            // Le coffre pivote vers sa pose trois-quarts pendant que l'éclat se dissipe : le couvercle
            // relevé et l'intérieur se lisent, ce qu'une vue de face aplatit complètement.
            // Le lacet de départ est ramené au tour le plus proche de la cible, sinon un coffre que
            // le joueur a fait tourner plusieurs fois referait tout le chemin inverse en accéléré.
            float fromYaw = OpenYaw + Mathf.DeltaAngle(OpenYaw, yaw);
            float fromPitch = pitch;

            elapsed = 0f;
            while (elapsed < FlashDuration)
            {
                elapsed += Time.deltaTime;

                float alpha = 1f - Mathf.Clamp01(elapsed / FlashDuration);
                flash.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, alpha * 0.5f);

                float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / SettleDuration));
                yaw = Mathf.Lerp(fromYaw, OpenYaw, settle);
                pitch = Mathf.Lerp(fromPitch, 0f, settle);
                stage.localRotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.right);
                yield return null;
            }

            yaw = OpenYaw;
            pitch = 0f;
            spinVelocity = Vector2.zero;
            freeSpin = false;
            stage.localRotation = Quaternion.AngleAxis(OpenYaw, Vector3.up);

            flash.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0f);
            IsPlaying = false;
            onComplete?.Invoke();
        }

        void SwapToOpen()
        {
            opened = true;
            if (closedModel == null || openModel == null) return;

            // Pas de recadrage : les deux états partagent déjà le même repère (voir FramePair).
            closedModel.SetActive(false);
            openModel.SetActive(true);
        }

        /// <summary>
        /// Montée en tension pilotée par le maintien du joueur : le tremblement, le flux de
        /// particules et le sur-régime de rotation suivent la progression, de 0 à 1. Rappeler avec
        /// une valeur qui redescend défait le tout, ce qui rend le relâchement lisible.
        /// </summary>
        public void SetCharge(float progress)
        {
            if (IsPlaying) return;

            charge = Mathf.Clamp01(progress);

            float amplitude = MaxShakeOffset * charge * charge;
            shakeTarget.anchoredPosition = shakeHome + new Vector2(
                Random.Range(-amplitude, amplitude),
                Random.Range(-amplitude, amplitude) * 0.5f);
            shakeTarget.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-MaxShakeTilt, MaxShakeTilt) * charge);

            if (fx == null) return;

            if (charge > 0f) fx.Charge(charge);
            else fx.Calm();
        }

        /// <summary>
        /// Fait tourner le coffre au doigt. L'horizontale commande le lacet, la verticale le tangage,
        /// et le geste laisse un élan qui se prolonge après le relâchement.
        /// </summary>
        public void Drag(Vector2 delta)
        {
            if (IsPlaying) return;

            freeSpin = true;

            float deltaYaw = -delta.x * DragSensitivity;
            float deltaPitch = delta.y * DragSensitivity;

            yaw += deltaYaw;
            pitch = Mathf.Clamp(pitch + deltaPitch, -MaxPitch, MaxPitch);

            // Vitesse déduite du geste lui-même : un balayage franc lance le coffre, un ajustement
            // lent le repose. Time.deltaTime est la durée pendant laquelle ce déplacement a eu lieu.
            if (Time.deltaTime <= 0f) return;

            var instant = new Vector2(deltaYaw, deltaPitch) / Time.deltaTime;
            spinVelocity = Vector2.ClampMagnitude(Vector2.Lerp(spinVelocity, instant, 0.5f), MaxSpinSpeed);
        }

        protected override void OnPreviewUpdate()
        {
            if (IsPlaying || closedModel == null) return;

            // L'élan du dernier geste s'amortit, mais le coffre ne s'arrête jamais tout à fait :
            // il retombe sur son tournoiement de repos.
            spinVelocity = Vector2.Lerp(spinVelocity, Vector2.zero, SpinDamping * Time.deltaTime);

            if (opened && !freeSpin)
            {
                // Coffre ouvert que le joueur n'a pas touché : la pose trois-quarts est tenue, avec
                // un léger balancement pour que l'image ne paraisse pas gelée.
                yaw = OpenYaw + Mathf.Sin(Time.time * 0.85f) * 2.5f;
                pitch = 0f;
            }
            else
            {
                float spin = idleSpin + charge * 140f;
                yaw += (spin + spinVelocity.x) * Time.deltaTime;
                pitch = Mathf.Clamp(pitch + spinVelocity.y * Time.deltaTime, -MaxPitch, MaxPitch);
            }

            stage.localRotation = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.right);
            stage.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 1.6f) * idleBob * 0.001f, 0f);
        }

    }
}
