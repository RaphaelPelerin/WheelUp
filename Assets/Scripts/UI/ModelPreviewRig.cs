using UnityEngine;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Socle commun aux aperçus 3D affichés dans l'UI : un plateau posé loin de la scène, filmé par une
    /// caméra dédiée qui rend dans une RenderTexture affichée par un RawImage.
    /// Le cadrage se calcule sur les dimensions réelles du modèle, car les modèles importés arrivent à
    /// des échelles très variables selon leur provenance.
    /// </summary>
    public abstract class ModelPreviewRig : MonoBehaviour
    {
        // L'asset URP du projet désactive le MSAA, ce qui écrase le antiAliasing demandé sur une
        // RenderTexture. On compense en rendant à 2x la taille affichée : la réduction lisse les bords.
        const int Supersampling = 2;
        const int MinTextureSize = 256;
        const int MaxTextureSize = 2048;

        protected RawImage output;
        protected Camera previewCamera;
        protected Transform stage;

        RenderTexture texture;

        // Dernier cadrage calculé, rejoué tel quel quand le zoom change : sans lui, régler le zoom
        // obligerait à recadrer le modèle, donc à le recentrer, et l'aperçu sauterait.
        float framedRadius;
        float framedHeightFactor;
        float framedMargin;
        float zoom = 1f;

        static int rigsCreated;
        static GameObject sharedLights;

        /// <summary>
        /// Réserve une tranche d'espace inoccupée pour un nouveau plateau. Deux aperçus posés au même
        /// endroit se filmeraient mutuellement : chaque rig doit avoir son coin de scène à lui.
        /// </summary>
        protected static Vector3 AllocateRigPosition()
        {
            // Les lumières partagées disparaissent avec la scène : leur absence signale un nouveau
            // chargement. On repart de l'origine, sinon chaque retour au menu éloignerait un peu plus
            // les plateaux et finirait par coûter de la précision flottante.
            if (sharedLights == null) rigsCreated = 0;

            return new Vector3(0f, -5000f - rigsCreated++ * 400f, 0f);
        }

        protected void SetupRig(RawImage rawImage, Vector3 rigPosition, float fieldOfView = 30f)
        {
            output = rawImage;
            transform.position = rigPosition;

            stage = new GameObject("Stage").transform;
            stage.SetParent(transform, false);

            var cameraObject = new GameObject("PreviewCamera");
            cameraObject.transform.SetParent(transform, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.fieldOfView = fieldOfView;
            previewCamera.nearClipPlane = 0.01f;
            // La texture dépend de la taille réelle du RawImage, inconnue tant que le Canvas n'a pas été
            // mis à jour : elle est créée au premier Update, la caméra reste éteinte d'ici là.
            previewCamera.enabled = false;

            EnsureSharedLights();
        }

        /// <summary>
        /// Un seul jeu de lumières pour tous les aperçus. Une lumière directionnelle éclaire la scène
        /// entière quelle que soit sa position : en donner trois à chaque rig ne changeait rien au rendu
        /// mais saturait la limite de lumières additionnelles d'URP, qui n'en retient qu'une poignée par
        /// objet et choisit lesquelles arbitrairement.
        /// </summary>
        static void EnsureSharedLights()
        {
            if (sharedLights != null) return;

            sharedLights = new GameObject("PreviewLights");
            AddLight("KeyLight", new Vector3(35f, 150f, 0f), 0.9f);
            AddLight("FillLight", new Vector3(15f, -40f, 0f), 0.35f);
            AddLight("RimLight", new Vector3(-10f, 20f, 0f), 0.6f);
        }

        static void AddLight(string lightName, Vector3 eulerAngles, float intensity)
        {
            var lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(sharedLights.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(eulerAngles);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
        }

        protected GameObject LoadModel(string resourcePath, bool warnIfMissing = true)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;

            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                if (warnIfMissing) Debug.LogWarning($"Aperçu 3D : modèle introuvable dans Resources/{resourcePath}");
                return null;
            }

            var instance = Instantiate(prefab, stage);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            return instance;
        }

        /// <summary>Recentre le modèle sur le plateau et recule la caméra jusqu'à le cadrer entièrement.</summary>
        protected void Frame(GameObject model, float heightFactor = 0.35f, float margin = 1.15f)
        {
            if (!TryGetBounds(model, out var bounds)) return;

            model.transform.position -= bounds.center - stage.position;
            PlaceCamera(Mathf.Max(bounds.extents.magnitude, 0.001f), heightFactor, margin);
        }

        /// <summary>
        /// Cadre deux états d'un même modèle. La position vient de l'état au repos et le second reçoit
        /// exactement le même décalage : passer de l'un à l'autre ne déplace donc rien à l'écran. La
        /// distance caméra, elle, se calcule sur les deux réunis pour que l'état le plus encombrant
        /// (couvercle relevé) tienne dans le cadre.
        /// Les deux modèles doivent être actifs à l'appel : les Renderer désactivés n'ont pas de bornes.
        /// </summary>
        protected void FramePair(GameObject anchor, GameObject other, float heightFactor = 0.35f, float margin = 1.15f)
        {
            if (!TryGetBounds(anchor, out var anchorBounds)) return;

            var offset = stage.position - anchorBounds.center;
            anchor.transform.position += offset;
            if (other != null) other.transform.position += offset;

            var full = new Bounds(anchorBounds.center + offset, anchorBounds.size);
            if (other != null && TryGetBounds(other, out var otherBounds)) full.Encapsulate(otherBounds);

            // Rayon mesuré depuis le plateau, et non depuis le centre du volume réuni : le coffre fermé
            // reste centré, la place libérée au-dessus accueille le couvercle.
            float radius = Mathf.Max(
                (full.center - stage.position).magnitude + full.extents.magnitude, 0.001f);
            PlaceCamera(radius, heightFactor, margin);
        }

        /// <summary>
        /// Rapproche ou éloigne la caméra sans toucher au cadrage. Au-dessus de 1 le modèle occupe
        /// davantage la vue ; trop haut, les parties les plus hautes sortent du cadre.
        /// </summary>
        protected void SetZoom(float value)
        {
            zoom = Mathf.Max(0.1f, value);
            ApplyCamera();
        }

        void PlaceCamera(float radius, float heightFactor, float margin)
        {
            framedRadius = radius;
            framedHeightFactor = heightFactor;
            framedMargin = margin;
            ApplyCamera();
        }

        void ApplyCamera()
        {
            if (framedRadius <= 0f) return;

            float distance = framedRadius / Mathf.Sin(Mathf.Deg2Rad * previewCamera.fieldOfView * 0.5f) * framedMargin / zoom;

            previewCamera.transform.localPosition = new Vector3(0f, framedRadius * framedHeightFactor, -distance);
            previewCamera.transform.LookAt(stage.position);
            previewCamera.farClipPlane = distance + framedRadius * 4f;
        }

        static bool TryGetBounds(GameObject model, out Bounds bounds)
        {
            bounds = default;
            if (model == null) return false;

            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return true;
        }

        /// <summary>
        /// Rebranche l'aperçu sur une autre zone d'affichage. Les cartes de gain sont reconstruites à
        /// chaque ouverture : sans cela il faudrait un rig neuf par carte, donc une fuite de caméras.
        /// </summary>
        protected void Rebind(RawImage newOutput)
        {
            if (newOutput == null || newOutput == output) return;

            output = newOutput;
            if (texture != null) output.texture = texture;
        }

        /// <summary>Aligne la RenderTexture sur la taille réellement affichée, et la recrée si elle change.</summary>
        void EnsureTexture()
        {
            var rect = output.rectTransform.rect;
            float canvasScale = output.canvas != null ? output.canvas.scaleFactor : 1f;

            int width = Mathf.Clamp(Mathf.RoundToInt(rect.width * canvasScale) * Supersampling, MinTextureSize, MaxTextureSize);
            int height = Mathf.Clamp(Mathf.RoundToInt(rect.height * canvasScale) * Supersampling, MinTextureSize, MaxTextureSize);

            if (texture != null && texture.width == width && texture.height == height) return;

            ReleaseTexture();

            texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            texture.filterMode = FilterMode.Bilinear;
            texture.Create();

            previewCamera.targetTexture = texture;
            output.texture = texture;
        }

        void ReleaseTexture()
        {
            if (texture == null) return;

            previewCamera.targetTexture = null;
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        protected virtual void Update()
        {
            // Le RawImage suit l'activation de l'onglet : inutile de rendre quand il est caché.
            bool visible = output != null && output.isActiveAndEnabled;
            if (!visible)
            {
                previewCamera.enabled = false;
                return;
            }

            EnsureTexture();
            previewCamera.enabled = texture != null;

            OnPreviewUpdate();
        }

        /// <summary>Animation propre à chaque aperçu, appelée uniquement quand il est visible.</summary>
        protected abstract void OnPreviewUpdate();

        protected virtual void OnDestroy()
        {
            ReleaseTexture();
        }
    }
}
