using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Efeito visual da lanterna de dínamo Halógena (controlado pelo LanternController): um ÚNICO
    /// sistema composto de duas camadas de UI criadas em runtime no Canvas alvo:
    ///
    /// 1) Halo — Image com o sprite da lanterna que segue o cursor (glow quente);
    /// 2) Escuridão — Image de tela cheia com o shader "Whispers/UI/LanternDarkness", que escurece
    ///    todo o conteúdo abaixo dela (os ViewNodes em world space) EXCETO a área iluminada.
    ///
    /// A área iluminada é recortada pelo MESMO sprite do halo: o shader amostra o alpha da textura
    /// do sprite exatamente no retângulo que o halo ocupa na tela (mesmo centro = cursor, mesmo
    /// tamanho = raio efetivo). Ou seja, a forma do spot de luz — falloff, bordas, arte — vem da
    /// mesma fonte visual, sem duplicação de parâmetros.
    ///
    /// Regras (documento de arquitetura §16.3):
    /// - Não interage com hotspots nem com o InteractionManager: as duas camadas têm
    ///   raycastTarget desligado e a cobertura geométrica é exposta exclusivamente para a
    ///   defesa do Voyeur, cuja transição continua sob autoridade do EntityDirector.
    /// - A escuridão acompanha a bateria: o raio encolhe conforme <see cref="dischargeMinRadiusFactor"/>
    ///   e a intensidade zera a luz; com carga esgotada a cena fica completamente escura.
    /// - Com qualquer bloqueio de gameplay a bateria congela (visual preservado); no GameOver
    ///   (Hide) as duas camadas são escondidas juntas.
    /// - Ambas as camadas ficam ABAIXO do restante da UI (TransitionCutout, modais, Debug F8,
    ///   GameOverOverlay), para que os fades as cubram.
    ///
    /// Sprite do halo opcional via Inspector; ausente, gera um gradiente radial procedural em runtime
    /// (que também passa a ser a máscara da escuridão).
    /// </summary>
    public class LanternEffect : MonoBehaviour
    {
        private const string DarknessShaderName = "Whispers/UI/LanternDarkness";

        private static readonly int DarknessCenterId = Shader.PropertyToID("_LanternDarknessCenter");
        private static readonly int DarknessSizeId = Shader.PropertyToID("_LanternDarknessSize");
        private static readonly int DarknessIntensityId = Shader.PropertyToID("_LanternLightIntensity");
        private static readonly int LightTexId = Shader.PropertyToID("_LanternTex");
        private static readonly int LightTexUVId = Shader.PropertyToID("_LanternTexUV");

        [Header("Configuração")]
        [Tooltip("Sprite do halo de luz (gradiente radial). Vazio = gerado proceduralmente em runtime. "
               + "Este mesmo sprite também é a máscara que define a área iluminada da escuridão.")]
        [SerializeField] private Sprite lightSprite;

        [Tooltip("Canvas de UI onde as camadas da lanterna são criadas (padrão: canvas pai).")]
        [SerializeField] private Canvas targetCanvas;

        [Header("Halógeno")]
        [SerializeField] private float halogenRadius = 240f;
        [SerializeField] private Color halogenColor = new Color(1f, 0.93f, 0.75f, 0.55f);

        [Tooltip("Fator mínimo do raio do halo com a bateria zerada (0–1). "
               + "0,35 = o halo encolhe até 35% do raio cheio conforme a bateria descarrega.")]
        [Range(0f, 1f)]
        [SerializeField] private float dischargeMinRadiusFactor = 0.35f;

        [Header("Escurecimento")]
        [Tooltip("Escurece a cena deixando iluminada somente a área do sprite da lanterna. "
               + "Avaliado uma única vez no Awake (autoria por cena).")]
        [SerializeField] private bool darknessEnabled;

        [Tooltip("Cor e opacidade da escuridão fora da área iluminada.")]
        [SerializeField] private Color darknessColor = new Color(0f, 0f, 0f, 0.93f);

        [Tooltip("Material da camada de escuridão (shader 'Whispers/UI/LanternDarkness'). "
               + "Vazio = localiza o shader em runtime via Shader.Find.")]
        [SerializeField] private Material darknessMaterial;

        private Image _halo;
        private Image _darkness;
        private Material _darknessMat; // instância exclusiva: mutações de runtime não sujam o asset
        private float _currentIntensity = 1f;
        private bool _visible; // Show/Hide é a autoridade; a intensidade só modula as camadas visíveis.
        private readonly Vector3[] _coverageCorners = new Vector3[4];
        private readonly HashSet<int> _coverageWarnings = new HashSet<int>();

        public bool IsVisible => _visible;
        public bool IsHalogenActive => _visible && _halo != null && _halo.gameObject.activeInHierarchy && _currentIntensity > 0.01f;
        public float CurrentIntensity => _currentIntensity;
        public Canvas TargetCanvas => targetCanvas;
        public float DischargeMinRadiusFactor => dischargeMinRadiusFactor;

        /// <summary>Raio efetivo do halo (unidades do Canvas), já modulado pela carga da bateria.</summary>
        public float EffectiveRadius => halogenRadius * Mathf.Lerp(dischargeMinRadiusFactor, 1f, _currentIntensity);

        public float RadiusInCanvasSpace => Mathf.Max(0f, EffectiveRadius);
        public Vector2 ScreenCenter => Input.mousePosition;

        /// <summary>Atualiza a intensidade do feixe de luz proporcional à carga da bateria do dínamo.</summary>
        public void SetIntensity(float intensity)
        {
            _currentIntensity = Mathf.Clamp01(intensity);
            ApplyHalogen();
        }

        /// <summary>
        /// Calcula área(círculo ∩ encounterRegion) / área(encounterRegion) no espaço
        /// local do Canvas que desenha o halo. Assim o CanvasScaler e a resolução
        /// aplicada ao halo visual também são aplicados à regra de gameplay.
        /// </summary>
        public bool TryGetCoverage(RectTransform target, out float coverage)
        {
            coverage = 0f;
            if (!IsHalogenActive || _currentIntensity <= 0.05f || target == null || targetCanvas == null || halogenRadius <= 0f) return false;

            float rotation = Mathf.Abs(Mathf.DeltaAngle(target.eulerAngles.z, 0f));
            if (rotation > 0.01f)
            {
                WarnCoverageOnce(target, "EncounterRegion rotacionada não é compatível com cobertura circular retangular.");
                return false;
            }

            RectTransform haloCanvasRect = targetCanvas.transform as RectTransform;
            if (haloCanvasRect == null)
            {
                WarnCoverageOnce(target, "Canvas do Halógeno não possui RectTransform válido.");
                return false;
            }

            Canvas targetOwner = target.GetComponentInParent<Canvas>();
            if (targetOwner == null)
            {
                WarnCoverageOnce(target, "EncounterRegion não pertence a um Canvas.");
                return false;
            }

            Camera targetCamera = ResolveCanvasCamera(targetOwner);
            Camera haloCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : ResolveCanvasCamera(targetCanvas);
            Vector2 center;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    haloCanvasRect, Input.mousePosition, haloCamera, out center))
            {
                WarnCoverageOnce(target, "Não foi possível converter o centro do Halógeno para o Canvas alvo.");
                return false;
            }

            target.GetWorldCorners(_coverageCorners);
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            for (int index = 0; index < _coverageCorners.Length; index++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCamera, _coverageCorners[index]);
                Vector2 localPoint;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        haloCanvasRect, screenPoint, haloCamera, out localPoint))
                {
                    WarnCoverageOnce(target, "EncounterRegion não pôde ser projetada no Canvas do Halógeno.");
                    return false;
                }
                minX = Mathf.Min(minX, localPoint.x);
                minY = Mathf.Min(minY, localPoint.y);
                maxX = Mathf.Max(maxX, localPoint.x);
                maxY = Mathf.Max(maxY, localPoint.y);
            }

            float width = maxX - minX;
            float height = maxY - minY;
            if (width <= 0.01f || height <= 0.01f)
            {
                WarnCoverageOnce(target, "EncounterRegion sem área projetável para cobertura Halógena.");
                return false;
            }

            const int slices = 64;
            float step = width / slices;
            float effectiveRadius = EffectiveRadius;
            float radiusSquared = effectiveRadius * effectiveRadius;
            float intersection = 0f;
            for (int index = 0; index < slices; index++)
            {
                float x = minX + (index + 0.5f) * step;
                float dx = x - center.x;
                float verticalSquared = radiusSquared - dx * dx;
                if (verticalSquared <= 0f) continue;
                float halfHeight = Mathf.Sqrt(verticalSquared);
                float lower = Mathf.Max(minY, center.y - halfHeight);
                float upper = Mathf.Min(maxY, center.y + halfHeight);
                if (upper > lower) intersection += (upper - lower) * step;
            }

            coverage = Mathf.Clamp01((intersection / (width * height)) * _currentIntensity);
            return true;
        }

        private static Camera ResolveCanvasCamera(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            if (canvas.worldCamera != null) return canvas.worldCamera;
            GameplaySceneController scene = GameplaySceneController.Instance;
            return scene != null && scene.ViewCamera != null ? scene.ViewCamera.TargetCamera : Camera.main;
        }

        private void WarnCoverageOnce(RectTransform target, string message)
        {
            int key = target != null ? target.GetInstanceID() : 0;
            if (_coverageWarnings.Add(key))
                Debug.LogWarning("[LanternEffect] " + message, target != null ? target : this);
        }

        private void Awake()
        {
            if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
            CreateVisuals();
        }

        private void CreateVisuals()
        {
            if (targetCanvas == null)
            {
                Debug.LogWarning("[LanternEffect] Canvas de UI não encontrado.", this);
                return;
            }

            // A escuridão é criada ANTES do halo: ao final, o halo fica como primeiro filho do Canvas
            // (desenhado abaixo de toda a UI) e a escuridão logo acima dele — as duas camadas continuam
            // cobertas pelo TransitionCutout, modais, Debug F8 e GameOverOverlay, conforme a arquitetura.
            if (darknessEnabled) CreateDarkness();

            GameObject haloGo = new GameObject("LanternHalo");
            haloGo.transform.SetParent(targetCanvas.transform, false);
            haloGo.transform.SetAsFirstSibling();
            if (_darkness != null) _darkness.transform.SetSiblingIndex(haloGo.transform.GetSiblingIndex() + 1);

            _halo = haloGo.AddComponent<Image>();
            _halo.sprite = lightSprite != null ? lightSprite : CreateRadialSprite();
            _halo.raycastTarget = false; // nunca intercepta o cursor
            _halo.gameObject.SetActive(false);

            ApplyLightTextureToDarkness();
            ApplyHalogen();
        }

        private void CreateDarkness()
        {
            Shader shader = darknessMaterial != null ? darknessMaterial.shader : null;
            if (shader == null) shader = Shader.Find(DarknessShaderName);
            if (shader == null)
            {
                Debug.LogWarning("[LanternEffect] Shader '" + DarknessShaderName + "' não encontrado; escurecimento desativado.", this);
                return;
            }

            // Instância exclusiva do material: as mutações por frame (centro/tamanho/intensidade)
            // não podem sujar o asset compartilhado no Editor. Com template, copia os valores autorados.
            _darknessMat = darknessMaterial != null ? new Material(darknessMaterial) : new Material(shader);

            GameObject darknessGo = new GameObject("LanternDarkness", typeof(RectTransform));
            darknessGo.transform.SetParent(targetCanvas.transform, false);

            RectTransform rt = (RectTransform)darknessGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _darkness = darknessGo.AddComponent<Image>();
            _darkness.material = _darknessMat;
            _darkness.raycastTarget = false; // nunca intercepta o cursor
            _darkness.gameObject.SetActive(false);
        }

        /// <summary>
        /// O recorte da escuridão usa a MESMA textura de sprite do halo: repassa a textura e a
        /// sub-região UV (resolvida com o sprite procedural, quando houver) para o material.
        /// </summary>
        private void ApplyLightTextureToDarkness()
        {
            if (_darknessMat == null || _halo == null || _halo.sprite == null || _halo.sprite.texture == null) return;

            _darknessMat.SetTexture(LightTexId, _halo.sprite.texture);
            Vector4 outerUV = DataUtility.GetOuterUV(_halo.sprite); // (minU, minV, maxU, maxV)
            _darknessMat.SetVector(LightTexUVId, new Vector4(outerUV.x, outerUV.y, outerUV.z - outerUV.x, outerUV.w - outerUV.y));
        }

        /// <summary>Exibe as camadas da lanterna (halo e, se habilitada, a escuridão).</summary>
        public void Show()
        {
            _visible = true;
            ApplyHalogen();
        }

        /// <summary>Esconde o halo e a escuridão.</summary>
        public void Hide()
        {
            _visible = false;
            if (_halo != null && _halo.gameObject.activeSelf) _halo.gameObject.SetActive(false);
            if (_darkness != null && _darkness.gameObject.activeSelf) _darkness.gameObject.SetActive(false);
        }

        private void ApplyHalogen()
        {
            bool haloActive = _visible && _currentIntensity > 0.001f;
            float effectiveRadius = EffectiveRadius;

            if (_halo != null)
            {
                if (haloActive)
                {
                    if (!_halo.gameObject.activeSelf)
                        _halo.gameObject.SetActive(true);

                    RectTransform rt = (RectTransform)_halo.transform;
                    rt.sizeDelta = new Vector2(effectiveRadius * 2f, effectiveRadius * 2f);

                    Color c = halogenColor;
                    c.a = halogenColor.a * _currentIntensity;
                    _halo.color = c;
                }
                else if (_halo.gameObject.activeSelf)
                {
                    _halo.gameObject.SetActive(false);
                }
            }

            // Escuridão vive junto com o halo: exibida enquanto a lanterna estiver visível,
            // com o MESMO retângulo do halo (centro = cursor, diâmetro = raio efetivo × 2).
            // A intensidade da bateria modula a máscara: sem carga, a cena escurece por completo.
            if (_darkness == null) return;

            if (!_visible)
            {
                if (_darkness.gameObject.activeSelf) _darkness.gameObject.SetActive(false);
                return;
            }

            _darkness.color = darknessColor;
            if (_darknessMat != null)
            {
                _darknessMat.SetFloat(DarknessSizeId, Mathf.Max(effectiveRadius * 2f, 0.0001f));
                _darknessMat.SetFloat(DarknessIntensityId, _currentIntensity);
            }

            if (!_darkness.gameObject.activeSelf) _darkness.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (targetCanvas == null) return;

            bool haloActive = _halo != null && _halo.gameObject.activeSelf;
            bool darknessActive = _darkness != null && _darkness.gameObject.activeSelf;
            if (!haloActive && !darknessActive) return;

            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            if (canvasRect == null) return;
            Camera camera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : ResolveCanvasCamera(targetCanvas);
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, Input.mousePosition, camera, out localPoint))
                return;

            // Mesmo centro para as duas camadas: o spot iluminado coincide exatamente com o sprite do halo.
            if (haloActive)
                ((RectTransform)_halo.transform).anchoredPosition = localPoint;

            if (darknessActive && _darknessMat != null)
                _darknessMat.SetVector(DarknessCenterId, localPoint);
        }

        private void OnDestroy()
        {
            if (_darkness != null && _darkness.gameObject != null) Destroy(_darkness.gameObject);
            if (_darknessMat != null) Destroy(_darknessMat);
            _darkness = null;
            _darknessMat = null;
        }

        /// <summary>Gradiente radial branco (colorido pelo tint Halógeno).</summary>
        private static Sprite CreateRadialSprite()
        {
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f - 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a; // falloff suave
                    byte alpha = (byte)(255f * a);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
