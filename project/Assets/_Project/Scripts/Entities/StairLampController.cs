using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Fachada e apresentação do lampião do sótão. NÃO guarda estado de gameplay: o
    /// relógio do lampião pertence ao <see cref="EntityDirector"/>. Este componente
    /// (1) recebe a interação de reabastecer e a encaminha ao diretor e (2) apresenta
    /// chama, sprite, escurecimento e áudio a partir do snapshot do diretor.
    /// Vive em um objeto sempre ativo da Noite (Manager_Entities), porque precisa
    /// apresentar enquanto o ViewNode do sótão está desativado.
    /// </summary>
    public sealed class StairLampController : MonoBehaviour
    {
        [Header("Identificação")]
        [Tooltip("Nome usado apenas em log/diagnóstico.")]
        [SerializeField] private string debugName = "Lampião do sótão";

        [Header("Interação")]
        [Tooltip("FALLBACK do item exigido, usado só quando não há ExhausterDefinition na cena. A autoridade é ExhausterDefinition.requiredRelightItemId.")]
        [SerializeField] private string requiredItemId = "item_oleo";

        [Header("Visual do lampião")]
        [Tooltip("Objeto da chama acesa (pode estar dentro de um ViewNode; a referência funciona mesmo inativo).")]
        [SerializeField] private GameObject flameRoot;
        [Tooltip("Imagem do lampião. Opcional; recebe o sprite aceso/apagado.")]
        [SerializeField] private Image lampImage;
        [SerializeField] private Sprite litSprite;
        [SerializeField] private Sprite outSprite;
        [Tooltip("Sprite do lampião QUEBRADO. Usado quando o Exhauster entra em Terminal; o reabastecimento deixa de ser aceito. Vazio = usa outSprite.")]
        [SerializeField] private Sprite brokenSprite;

        [Header("Escurecimento do cenário")]
        [Tooltip("Overlays de escurecimento. Podem estar em ViewNodes diferentes; todos recebem o mesmo alpha.")]
        [SerializeField] private Graphic[] dimTargets = System.Array.Empty<Graphic>();
        [SerializeField] private Color dimColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        [Tooltip("Alpha máximo quando o lampião apaga. 1 = escuridão total.")]
        [Range(0f, 1f)][SerializeField] private float dimMaxAlpha = 0.85f;
        [Tooltip("Expoente da curva de escurecimento. 1 = linear; >1 escurece tarde e rápido.")]
        [Min(0.1f)][SerializeField] private float dimPower = 1.6f;
        [Tooltip("Suavização do alpha em segundos não escalados. 0 = imediato.")]
        [Min(0f)][SerializeField] private float dimFadeSeconds = 0.8f;

        [Header("Interação — hotspots afetados")]
        [Tooltip("Hotspots de reabastecimento que devem reavaliar as condições quando o lampião quebra (apresentação de bloqueio imediata).")]
        [SerializeField] private HotspotBase[] relightHotspots = System.Array.Empty<HotspotBase>();

        [Header("Áudio (opcional)")]
        [SerializeField] private AudioSource lampAudioSource;
        [SerializeField] private AudioClip burnLoopClip;
        [Range(0f, 2f)][SerializeField] private float burnLoopVolume = 1f;

        private EntityDirector _director;
        private float _currentDim;
        private bool _dimResetApplied;
        private bool _lastLit;
        private bool _lastTerminal;
        private bool _lastBroken;
        private int _lastRelightCount;
        private bool _visualsReady;

        private void OnEnable()
        {
            Bind();
            ApplyImmediate();
        }

        private void OnDisable()
        {
            StopBurnLoop();
            _director = null;
            _visualsReady = false;
        }

        private void Update()
        {
            if (_director == null) Bind();
            if (_director == null) return;

            LampRuntimeSnapshot snapshot;
            if (!_director.TryGetLampSnapshot(out snapshot) || !snapshot.configured)
            {
                // Noite sem Exhauster: o lampião não é gameplay nesta cena. A limpeza
                // acontece uma única vez, para não marcar os overlays como sujos a
                // cada frame em Noites que nunca usam o lampião.
                if (!_dimResetApplied)
                {
                    _dimResetApplied = true;
                    ApplyDim(0f, false);
                }
                return;
            }
            _dimResetApplied = false;

            float target = TargetDimAlpha(snapshot);
            ApplyDim(target, true);
            ApplyVisuals(snapshot);
            PlayTransitions(snapshot);
        }

        private void Bind()
        {
            GameplaySceneController scene = GameplaySceneController.Instance;
            _director = scene != null ? scene.Entities : null;
        }

        private void ApplyImmediate()
        {
            if (_director == null) Bind();
            LampRuntimeSnapshot snapshot;
            if (_director == null || !_director.TryGetLampSnapshot(out snapshot) || !snapshot.configured)
            {
                ApplyDim(0f, false);
                return;
            }

            _currentDim = TargetDimAlpha(snapshot);
            ApplyDim(_currentDim, false);
            ApplyVisuals(snapshot);
            if (snapshot.lit && !snapshot.broken) StartBurnLoop();
            _lastLit = snapshot.lit;
            _lastTerminal = snapshot.terminal;
            _lastBroken = snapshot.broken;
            _lastRelightCount = snapshot.relightCount;
        }

        /// <summary>Posição visual autorada para o recorte de luz no shader da lanterna.</summary>
        public RectTransform LightSource => lampImage != null ? lampImage.rectTransform : null;

        public bool TryRefillHeld(float seconds, float secondsToFill)
        {
            if (_director == null) Bind();
            if (_director == null) return false;
            ExhausterDefinition definition = _director.ExhausterDefinitionForPresentation;
            string item = definition != null ? definition.requiredRelightItemId : requiredItemId;
            GameSessionManager session = GameSessionManager.Instance;
            if (!string.IsNullOrWhiteSpace(item) && (session == null || !session.HasItem(item))) return false;
            return _director.TryRefillStairLamp(seconds, secondsToFill);
        }

        private float TargetDimAlpha(in LampRuntimeSnapshot snapshot)
        {
            if (!snapshot.lit) return dimMaxAlpha;
            float darkness = 1f - Mathf.Clamp01(snapshot.burnNormalized);
            return dimMaxAlpha * Mathf.Pow(darkness, Mathf.Max(0.1f, dimPower));
        }

        private void ApplyDim(float target, bool smooth)
        {
            if (smooth && dimFadeSeconds > 0.0001f)
                _currentDim = Mathf.MoveTowards(_currentDim, target, Time.unscaledDeltaTime / dimFadeSeconds);
            else
                _currentDim = target;

            if (dimTargets == null) return;
            Color color = dimColor;
            color.a = Mathf.Clamp01(dimColor.a) * _currentDim;
            bool visible = color.a > 0.001f;
            for (int index = 0; index < dimTargets.Length; index++)
            {
                Graphic graphic = dimTargets[index];
                if (graphic == null) continue;
                graphic.raycastTarget = false;
                graphic.color = color;
                graphic.enabled = visible;
            }
        }

        private void ApplyVisuals(in LampRuntimeSnapshot snapshot)
        {
            // Quebrado tem precedência absoluta: nem chama, nem sprite de apagado.
            bool showFlame = snapshot.lit && !snapshot.broken;
            if (flameRoot != null && flameRoot.activeSelf != showFlame)
                flameRoot.SetActive(showFlame);

            if (lampImage != null)
            {
                Sprite sprite = snapshot.broken
                    ? (brokenSprite != null ? brokenSprite : outSprite)
                    : (snapshot.lit ? litSprite : outSprite);
                if (sprite != null && lampImage.sprite != sprite) lampImage.sprite = sprite;
            }

            if (!_visualsReady)
            {
                _visualsReady = true;
                _lastLit = snapshot.lit;
                _lastTerminal = snapshot.terminal;
                _lastBroken = snapshot.broken;
                _lastRelightCount = snapshot.relightCount;
            }
        }

        private void PlayTransitions(in LampRuntimeSnapshot snapshot)
        {
            ExhausterDefinition definition = _director != null ? _director.ExhausterDefinitionForPresentation : null;

            if (_lastLit && !snapshot.lit)
            {
                StopBurnLoop();
                if (definition != null && definition.lampOutSfx != null)
                    PlayOneShot(definition.lampOutSfx, definition.lampOutSfxVolume);
            }

            if (snapshot.lit && !_lastLit) StartBurnLoop();

            // Reabastecer com a chama acesa também precisa de confirmação sonora:
            // o contador do diretor cobre os dois casos (aceso e apagado).
            if (snapshot.relightCount != _lastRelightCount &&
                definition != null && definition.relightSfx != null)
            {
                PlayOneShot(definition.relightSfx, definition.relightSfxVolume);
            }

            if (!_lastTerminal && snapshot.terminal) StopBurnLoop();

            if (snapshot.broken && !_lastBroken) OnLampBroke();
            else if (!snapshot.broken && _lastBroken) RefreshHotspots();

            // Garante o loop sempre que a chama deve estar acesa. Sem isso, uma Noite
            // que começa com startsLit = true nunca inicia o loop: não há transição
            // de estado no primeiro frame. StartBurnLoop é idempotente.
            if (snapshot.lit && !snapshot.broken) StartBurnLoop();

            _lastLit = snapshot.lit;
            _lastTerminal = snapshot.terminal;
            _lastBroken = snapshot.broken;
            _lastRelightCount = snapshot.relightCount;
        }

        /// <summary>
        /// O lampião quebrou: para a queima, emite o SFX de falha (se houver) e força os
        /// hotspots de reabastecimento a reavaliar — o hotspot sai do estado disponível no
        /// mesmo frame, sem esperar a próxima entrada no ViewNode. O que o jogador vê
        /// (desaparecer ou bloqueio) é decidido pelo `unavailableMode` de cada hotspot.
        /// </summary>
        private void OnLampBroke()
        {
            StopBurnLoop();
            RefreshHotspots();

            ExhausterDefinition definition = _director != null ? _director.ExhausterDefinitionForPresentation : null;
            if (definition != null && definition.lampBrokenSfx != null)
                PlayOneShot(definition.lampBrokenSfx, definition.lampBrokenSfxVolume);

            Debug.Log("[StairLampController] " + debugName + " quebrou: reabastecimento bloqueado.", this);
        }

        private void RefreshHotspots()
        {
            if (relightHotspots == null) return;
            for (int index = 0; index < relightHotspots.Length; index++)
            {
                HotspotBase hotspot = relightHotspots[index];
                if (hotspot != null) hotspot.EvaluateConditions();
            }
        }

        private void StartBurnLoop()
        {
            if (lampAudioSource == null || burnLoopClip == null) return;
            if (lampAudioSource.clip != burnLoopClip) lampAudioSource.clip = burnLoopClip;
            lampAudioSource.loop = true;
            lampAudioSource.volume = burnLoopVolume;
            if (!lampAudioSource.isPlaying) lampAudioSource.Play();
        }

        private void StopBurnLoop()
        {
            if (lampAudioSource != null && lampAudioSource.isPlaying) lampAudioSource.Stop();
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (clip == null) return;
            if (lampAudioSource != null) lampAudioSource.PlayOneShot(clip, Mathf.Clamp(volume, 0f, 2f));
        }

        // ---- Interação (alvo do UnityEvent do InteractionHotspot) ----

        /// <summary>
        /// Reabastece/reacende o lampião. Assinatura sem parâmetros para uso direto no
        /// <c>onActivated</c> do InteractionHotspot. O item exigido NÃO é consumido.
        /// </summary>
        public void RelightFromInteraction()
        {
            string reason;
            if (!TryRelight(out reason)) Debug.LogWarning("[StairLampController] " + debugName + ": " + reason, this);
        }

        /// <summary>Mesma regra de <see cref="RelightFromInteraction"/>, com motivo de falha.</summary>
        public bool TryRelight(out string reason)
        {
            reason = null;
            if (_director == null) Bind();
            if (_director == null)
            {
                reason = "EntityDirector indisponível nesta cena.";
                return false;
            }

            // Autoridade do item exigido é a definição; o campo local é apenas fallback
            // para cenas sem ExhausterDefinition (evita duas verdades divergentes).
            ExhausterDefinition definition = _director.ExhausterDefinitionForPresentation;
            string requiredItem = definition != null ? definition.requiredRelightItemId : requiredItemId;
            if (!string.IsNullOrWhiteSpace(requiredItem))
            {
                GameSessionManager session = GameSessionManager.Instance;
                if (session == null || !session.HasItem(requiredItem))
                {
                    reason = "O jogador não possui o item '" + requiredItem + "'.";
                    return false;
                }
            }

            LampRuntimeSnapshot snapshot;
            if (_director.TryGetLampSnapshot(out snapshot) && snapshot.broken)
            {
                reason = "O lampião está quebrado; o Exhauster entrou em Terminal.";
                return false;
            }

            string error;
            if (!_director.TryRelightStairLamp(out error))
            {
                reason = error;
                return false;
            }
            return true;
        }

        /// <summary>Diagnóstico: relê o estado sem interagir.</summary>
        public bool TryGetSnapshot(out LampRuntimeSnapshot snapshot)
        {
            snapshot = default;
            if (_director == null) Bind();
            if (_director == null) return false;
            return _director.TryGetLampSnapshot(out snapshot);
        }
    }
}
