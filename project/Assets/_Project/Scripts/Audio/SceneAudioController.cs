using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Whispers
{
    /// <summary>
    /// Autoridade local de reprodução, continuidade e mixagem de áudio da cena.
    /// Não é singleton e não mantém estado global de sessão.
    /// </summary>
    public class SceneAudioController : MonoBehaviour
    {
        [Header("Mixer")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup ambienceGroup;
        [SerializeField] private AudioMixerGroup threatsGroup;
        [SerializeField] private AudioMixerGroup interactionsGroup;
        [SerializeField] private AudioMixerGroup equipmentGroup;
        [SerializeField] private AudioMixerGroup radioGroup;
        [SerializeField] private AudioMixerGroup tapesGroup;
        [SerializeField] private AudioMixerGroup transitionGroup;
        [SerializeField] private AudioMixerGroup uiGroup;
        [SerializeField] private AudioMixerGroup musicGroup;

        [Header("Snapshots opcionais")]
        [SerializeField] private AudioMixerSnapshot normalSnapshot;
        [SerializeField] private AudioMixerSnapshot modalSnapshot;
        [SerializeField] private AudioMixerSnapshot mediaFocusSnapshot;
        [SerializeField] private AudioMixerSnapshot pausedSnapshot;
        [SerializeField] private AudioMixerSnapshot transitionSnapshot;

        [Header("Ambiente contínuo")]
        [SerializeField] private AudioContinuousLayerDefinition[] continuousLayers = Array.Empty<AudioContinuousLayerDefinition>();
        [SerializeField] private Transform runtimeRoot;
        [SerializeField] private float profileTransitionDuration = 0.2f;

        [Header("Pools")]
        [Min(1)] [SerializeField] private int oneShotPoolSize = 8;
        [Min(1)] [SerializeField] private int maxPersistentSources = 8;
        [Min(0f)] [SerializeField] private float sceneExitFadeDuration = 0.2f;

        [Header("DEBUG F8")]
        [SerializeField] private AudioDebugCatalog debugCatalog;

        private sealed class RuntimeVoice
        {
            public string runtimeId;
            public string emitterId;
            public string anchorId;
            public string debugId;
            public AudioSourceRole role;
            public AudioSource source;
            public AudioLowPassFilter lowPass;
            public AudioReverbFilter reverb;
            public float baseVolume = 1f;
            public bool reserved;
            public bool continuous;
            public float lastUseTime;
        }

        private struct VoiceTarget
        {
            public float volume;
            public float pan;
            public float lowPass;
            public float reverb;
        }

        private sealed class VoiceInterpolation
        {
            public RuntimeVoice voice;
            public float startVolume;
            public float startPan;
            public float startLowPass;
            public float startReverb;
            public VoiceTarget target;
        }

        private readonly Dictionary<string, RuntimeVoice> _continuousVoices = new Dictionary<string, RuntimeVoice>();
        private readonly Dictionary<string, RuntimeVoice> _persistentVoices = new Dictionary<string, RuntimeVoice>();
        private readonly Dictionary<string, AudioEntityRuntimeState> _entities = new Dictionary<string, AudioEntityRuntimeState>();
        private readonly List<RuntimeVoice> _allVoices = new List<RuntimeVoice>();
        private readonly List<RuntimeVoice> _oneShotVoices = new List<RuntimeVoice>();
        private readonly Dictionary<string, RuntimeVoice> _localVoices = new Dictionary<string, RuntimeVoice>();
        private readonly Dictionary<string, float> _localFadeDurations = new Dictionary<string, float>();
        private readonly HashSet<string> _missingEntityAudioWarnings = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Coroutine> _localFadeRoutines = new Dictionary<string, Coroutine>();
        private readonly Dictionary<string, int> _localFadeVersions = new Dictionary<string, int>();
        private readonly Dictionary<string, Coroutine> _entityAudioRoutines = new Dictionary<string, Coroutine>();

        private GameplaySceneDefinition _sceneDefinition;
        private ViewAudioProfile _currentProfile;
        private string _currentViewNodeId;
        private AudioMixState _mixState = AudioMixState.Normal;
        private Coroutine _profileRoutine;
        private Coroutine _sceneExitRoutine;
        private bool _initialized;
        private int _voiceSequence;

        public event Action<AudioMixState> MixStateChanged;
        public event Action<string> ViewAudioProfileChanged;
        public event Action<string> SpecialAudioRequested;

        public bool IsInitialized => _initialized;
        public ViewAudioProfile CurrentProfile => _currentProfile;
        public string CurrentViewNodeId => _currentViewNodeId;
        public AudioMixState CurrentMixState => _mixState;
        public AudioDebugCatalog DebugCatalog => debugCatalog;

        /// <summary>
        /// Indica se a cena possui uma camada-base contínua pronta para iniciar.
        /// Exposto para diagnóstico e testes: sem essa camada não há ambiência de período.
        /// </summary>
        public bool HasBaseAmbienceReady
        {
            get
            {
                if (continuousLayers == null) return false;

                int baseLayerCount = 0;
                foreach (AudioContinuousLayerDefinition definition in continuousLayers)
                {
                    if (definition == null || !definition.isBaseAmbience) continue;
                    baseLayerCount++;
                    if (definition.clip == null || !definition.loop || !definition.playOnInitialize)
                        return false;
                }
                return baseLayerCount == 1;
            }
        }

        private void OnDisable()
        {
            if (_profileRoutine != null) StopCoroutine(_profileRoutine);
            if (_sceneExitRoutine != null) StopCoroutine(_sceneExitRoutine);
            foreach (Coroutine routine in _localFadeRoutines.Values)
                if (routine != null) StopCoroutine(routine);
            _localFadeRoutines.Clear();
            _localFadeVersions.Clear();
            foreach (Coroutine routine in _entityAudioRoutines.Values)
                if (routine != null) StopCoroutine(routine);
            _entityAudioRoutines.Clear();
            StopAllVoices();
        }

        /// <summary>
        /// Inicializa as fontes locais e inicia as camadas contínuas configuradas.
        /// Deve ser chamado pelo GameplaySceneController durante o boot.
        /// </summary>
        public void Initialize(GameplaySceneDefinition sceneDefinition)
        {
            if (_initialized) return;

            _sceneDefinition = sceneDefinition;
            EnsureRuntimeRoot();
            BuildContinuousVoices();
            BuildOneShotPool();
            ValidateConfiguration();
            if (mixer != null) mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
            _initialized = true;
            ApplySnapshot(AudioMixState.Normal, true);
            StartContinuousLayers();
        }

        private void EnsureInitialized()
        {
            if (!_initialized) Initialize(_sceneDefinition);
        }

        private void EnsureRuntimeRoot()
        {
            if (runtimeRoot != null) return;

            GameObject root = new GameObject("__AudioRuntime");
            root.transform.SetParent(transform, false);
            runtimeRoot = root.transform;
        }

        private void BuildContinuousVoices()
        {
            if (continuousLayers == null) return;

            foreach (AudioContinuousLayerDefinition definition in continuousLayers)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.id)) continue;
                if (_continuousVoices.ContainsKey(definition.id))
                {
                    Debug.LogWarning($"[SceneAudioController] Camada contínua duplicada: '{definition.id}'.", this);
                    continue;
                }

                RuntimeVoice voice = CreateVoice("Continuous_" + definition.id, definition.role, true);
                voice.runtimeId = definition.id;
                voice.continuous = true;
                voice.baseVolume = Mathf.Max(0f, definition.volume);
                voice.source.clip = definition.clip;
                voice.source.loop = definition.loop;
                voice.source.panStereo = Mathf.Clamp(definition.pan, -1f, 1f);
                _continuousVoices.Add(definition.id, voice);
            }
        }

        private void BuildOneShotPool()
        {
            for (int i = 0; i < Mathf.Max(1, oneShotPoolSize); i++)
            {
                RuntimeVoice voice = CreateVoice("OneShot_" + i, AudioSourceRole.UI, false);
                voice.runtimeId = "oneshot_" + i;
                _oneShotVoices.Add(voice);
            }
        }

        private void ValidateConfiguration()
        {
            if (ambienceGroup == null && threatsGroup == null && interactionsGroup == null &&
                equipmentGroup == null && radioGroup == null && tapesGroup == null &&
                transitionGroup == null && uiGroup == null && musicGroup == null)
            {
                Debug.LogWarning("[SceneAudioController] Nenhum AudioMixerGroup foi configurado. " +
                    "As fontes ainda podem tocar, mas a mixagem por categoria está desativada.", this);
            }

            ValidateBaseAmbience();

            if (debugCatalog == null)
                Debug.LogWarning("[SceneAudioController] AudioDebugCatalog não configurado; a aba Áudio ficará sem biblioteca.", this);
        }

        /// <summary>
        /// Garante a pré-condição de autoria do ambiente contínuo: cada cena deve
        /// declarar uma única camada-base, em loop e iniciada no boot. O AudioClip
        /// continua sendo conteúdo autorado no Inspector; enquanto ele estiver vazio,
        /// o aviso identifica claramente por que a cena inicia sem ambiência.
        /// </summary>
        private void ValidateBaseAmbience()
        {
            if (continuousLayers == null || continuousLayers.Length == 0)
            {
                Debug.LogError("[SceneAudioController] Nenhuma camada contínua foi configurada; falta o ambiente-base da cena.", this);
                return;
            }

            int baseLayerCount = 0;
            AudioContinuousLayerDefinition baseLayer = null;
            foreach (AudioContinuousLayerDefinition definition in continuousLayers)
            {
                if (definition == null || !definition.isBaseAmbience) continue;
                baseLayerCount++;
                baseLayer = definition;
            }

            if (baseLayerCount != 1)
            {
                Debug.LogError($"[SceneAudioController] Configure exatamente uma camada com isBaseAmbience. Encontradas: {baseLayerCount}.", this);
                return;
            }

            if (baseLayer.clip == null)
            {
                Debug.LogWarning($"[SceneAudioController] A camada-base '{baseLayer.id}' está pronta, mas sem AudioClip. " +
                    "A cena permanecerá sem ambiência até um clip ser atribuído no Inspector.", this);
            }
            if (!baseLayer.loop)
            {
                Debug.LogWarning($"[SceneAudioController] A camada-base '{baseLayer.id}' deve usar loop para permanecer contínua.", this);
            }
            if (!baseLayer.playOnInitialize)
            {
                Debug.LogWarning($"[SceneAudioController] A camada-base '{baseLayer.id}' deve iniciar no boot (playOnInitialize).", this);
            }
        }

        private RuntimeVoice CreateVoice(string objectName, AudioSourceRole role, bool continuous)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(runtimeRoot, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = continuous;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.panStereo = 0f;
            source.outputAudioMixerGroup = ResolveGroup(role);

            AudioLowPassFilter lowPass = go.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
            lowPass.enabled = false;

            AudioReverbFilter reverb = go.AddComponent<AudioReverbFilter>();
            reverb.reverbLevel = -10000f;
            reverb.enabled = false;

            RuntimeVoice voice = new RuntimeVoice
            {
                runtimeId = "voice_" + (++_voiceSequence),
                role = role,
                source = source,
                lowPass = lowPass,
                reverb = reverb,
                continuous = continuous,
                lastUseTime = Time.unscaledTime
            };
            _allVoices.Add(voice);
            return voice;
        }

        private AudioMixerGroup ResolveGroup(AudioSourceRole role)
        {
            switch (role)
            {
                case AudioSourceRole.Ambience: return ambienceGroup;
                case AudioSourceRole.Threats: return threatsGroup;
                case AudioSourceRole.Interactions: return interactionsGroup;
                case AudioSourceRole.Equipment: return equipmentGroup;
                case AudioSourceRole.Radio: return radioGroup;
                case AudioSourceRole.Tapes: return tapesGroup;
                case AudioSourceRole.Transition: return transitionGroup;
                case AudioSourceRole.Music: return musicGroup;
                default: return uiGroup;
            }
        }

        private void ConfigureVoice(RuntimeVoice voice, AudioSourceRole role, AudioClip clip, bool loop, float volume)
        {
            if (voice == null || voice.source == null) return;

            voice.role = role;
            voice.baseVolume = Mathf.Max(0f, volume);
            voice.source.outputAudioMixerGroup = ResolveGroup(role);
            voice.source.clip = clip;
            voice.source.loop = loop;
            voice.source.playOnAwake = false;
            voice.source.spatialBlend = 0f;
            voice.source.dopplerLevel = 0f;
            voice.lastUseTime = Time.unscaledTime;
        }

        private void StartContinuousLayers()
        {
            foreach (KeyValuePair<string, RuntimeVoice> pair in _continuousVoices)
            {
                RuntimeVoice voice = pair.Value;
                AudioContinuousLayerDefinition definition = FindContinuousDefinition(pair.Key);
                if (definition == null || !definition.playOnInitialize || voice.source.clip == null) continue;
                voice.source.volume = voice.baseVolume;
                voice.source.Play();
            }
        }

        private AudioContinuousLayerDefinition FindContinuousDefinition(string id)
        {
            if (continuousLayers == null) return null;
            foreach (AudioContinuousLayerDefinition definition in continuousLayers)
                if (definition != null && string.Equals(definition.id, id, StringComparison.Ordinal)) return definition;
            return null;
        }

        /// <summary>
        /// Aplica o perfil acústico do destino. O perfil controla perspectiva;
        /// as fontes contínuas permanecem vivas quando o modo é Keep.
        /// </summary>
        public void ApplyViewAudioProfile(ViewNodeDefinition definition, AudioTransitionMode mode, string specialAudioId = null)
        {
            EnsureInitialized();

            ViewAudioProfile targetProfile = definition != null ? definition.audioProfile : null;
            // Camadas locais possuem seu próprio ciclo de entrada/saída. Elas não entram
            // nesta lista para que um Crossfade não as interrompa abruptamente.
            List<VoiceInterpolation> transitions = CaptureTransitions(targetProfile, false);

            _currentViewNodeId = definition != null ? definition.id : string.Empty;
            _currentProfile = targetProfile;
            ApplyLocalLayers(targetProfile, mode);
            ViewAudioProfileChanged?.Invoke(_currentViewNodeId);

            if (_profileRoutine != null) StopCoroutine(_profileRoutine);

            switch (mode)
            {
                case AudioTransitionMode.Keep:
                    _profileRoutine = StartCoroutine(InterpolateVoices(transitions, profileTransitionDuration));
                    break;

                case AudioTransitionMode.Crossfade:
                {
                    // Apenas a ambiência contínua participa do dip/crossfade. Equipamentos,
                    // ameaças, mídia e one-shots permanecem vivos e recebem a nova
                    // perspectiva sem serem silenciados pela navegação.
                    List<VoiceInterpolation> ambienceTransitions = new List<VoiceInterpolation>();
                    List<VoiceInterpolation> persistentTransitions = new List<VoiceInterpolation>();
                    foreach (VoiceInterpolation transition in transitions)
                    {
                        if (transition.voice != null && transition.voice.continuous &&
                            transition.voice.role == AudioSourceRole.Ambience)
                            ambienceTransitions.Add(transition);
                        else
                            persistentTransitions.Add(transition);
                    }

                    ApplyTargets(persistentTransitions, 1f);
                    _profileRoutine = StartCoroutine(CrossfadeVoices(ambienceTransitions, profileTransitionDuration));
                    break;
                }

                case AudioTransitionMode.Special:
                    if (!string.IsNullOrWhiteSpace(specialAudioId)) SpecialAudioRequested?.Invoke(specialAudioId);
                    else Debug.LogWarning("[SceneAudioController] Link em modo Special sem specialAudioId.", this);
                    ApplyCurrentProfileImmediate();
                    break;

                default:
                    ApplyCurrentProfileImmediate();
                    break;
            }
        }

        private List<VoiceInterpolation> CaptureTransitions(ViewAudioProfile targetProfile, bool includeLocalVoices)
        {
            List<VoiceInterpolation> transitions = new List<VoiceInterpolation>();
            foreach (RuntimeVoice voice in GetVoicesForPresentation())
            {
                if (voice == null || voice.source == null) continue;
                if (!includeLocalVoices && IsLocalVoice(voice)) continue;

                VoiceTarget target = CalculateTarget(voice, targetProfile);
                transitions.Add(new VoiceInterpolation
                {
                    voice = voice,
                    startVolume = voice.source.volume,
                    startPan = voice.source.panStereo,
                    startLowPass = voice.lowPass != null ? voice.lowPass.cutoffFrequency : 22000f,
                    startReverb = voice.reverb != null ? voice.reverb.reverbLevel : -10000f,
                    target = target
                });
            }
            return transitions;
        }

        private bool IsLocalVoice(RuntimeVoice voice)
        {
            if (voice != null && !string.IsNullOrWhiteSpace(voice.emitterId) &&
                voice.emitterId.StartsWith("local:", StringComparison.Ordinal))
                return true;

            foreach (RuntimeVoice localVoice in _localVoices.Values)
                if (localVoice == voice) return true;
            return false;
        }

        private IEnumerator InterpolateVoices(List<VoiceInterpolation> transitions, float duration)
        {
            if (duration <= 0f)
            {
                ApplyTargets(transitions, 1f);
                _profileRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyTargets(transitions, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            ApplyTargets(transitions, 1f);
            _profileRoutine = null;
        }

        private IEnumerator CrossfadeVoices(List<VoiceInterpolation> transitions, float duration)
        {
            float half = Mathf.Max(0.01f, duration * 0.5f);
            float elapsed = 0f;

            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                foreach (VoiceInterpolation transition in transitions)
                    if (transition.voice != null && transition.voice.source != null)
                        transition.voice.source.volume = Mathf.Lerp(transition.startVolume, 0f, t);
                yield return null;
            }

            foreach (VoiceInterpolation transition in transitions)
                if (transition.voice != null && transition.voice.source != null)
                    ApplyTarget(transition.voice, transition.target);

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                foreach (VoiceInterpolation transition in transitions)
                    if (transition.voice != null && transition.voice.source != null)
                        transition.voice.source.volume = Mathf.Lerp(0f, transition.target.volume, t);
                yield return null;
            }

            foreach (VoiceInterpolation transition in transitions)
                if (transition.voice != null && transition.voice.source != null)
                    ApplyTarget(transition.voice, transition.target);

            _profileRoutine = null;
        }

        private void ApplyTargets(List<VoiceInterpolation> transitions, float t)
        {
            foreach (VoiceInterpolation transition in transitions)
            {
                if (transition == null || transition.voice == null || transition.voice.source == null) continue;

                RuntimeVoice voice = transition.voice;
                voice.source.volume = Mathf.Lerp(transition.startVolume, transition.target.volume, t);
                voice.source.panStereo = Mathf.Lerp(transition.startPan, transition.target.pan, t);
                if (voice.lowPass != null)
                {
                    voice.lowPass.cutoffFrequency = Mathf.Lerp(transition.startLowPass, transition.target.lowPass, t);
                    voice.lowPass.enabled = transition.target.lowPass < 21950f;
                }
                if (voice.reverb != null)
                {
                    voice.reverb.reverbLevel = Mathf.Lerp(transition.startReverb, transition.target.reverb, t);
                    voice.reverb.enabled = transition.target.reverb > -9990f;
                }
            }
        }

        private void ApplyTarget(RuntimeVoice voice, VoiceTarget target)
        {
            if (voice == null || voice.source == null) return;
            voice.source.volume = target.volume;
            voice.source.panStereo = target.pan;
            if (voice.lowPass != null)
            {
                voice.lowPass.cutoffFrequency = target.lowPass;
                voice.lowPass.enabled = target.lowPass < 21950f;
            }
            if (voice.reverb != null)
            {
                voice.reverb.reverbLevel = target.reverb;
                voice.reverb.enabled = target.reverb > -9990f;
            }
        }

        private void ApplyCurrentProfileImmediate()
        {
            foreach (RuntimeVoice voice in GetVoicesForPresentation())
            {
                // Camadas locais ativas ou em saída são controladas por suas próprias
                // corrotinas; aplicar aqui causaria um salto de volume no meio do fade.
                if (IsLocalVoice(voice)) continue;
                ApplyTarget(voice, CalculateTarget(voice, _currentProfile));
            }
        }

        private VoiceTarget CalculateTarget(RuntimeVoice voice, ViewAudioProfile profile)
        {
            VoiceTarget target = new VoiceTarget
            {
                volume = voice != null ? voice.baseVolume : 0f,
                pan = 0f,
                lowPass = 22000f,
                reverb = -10000f
            };

            if (voice == null) return target;

            if (profile != null)
            {
                if (voice.role == AudioSourceRole.Ambience)
                    target.volume *= profile.baseAmbienceMultiplier;
                if (voice.role == AudioSourceRole.Threats)
                    target.volume *= profile.interferenceMultiplier;

                AudioLayerPerspective layer = profile.FindLayer(voice.runtimeId);
                if (layer == null && !string.IsNullOrWhiteSpace(voice.emitterId))
                    layer = profile.FindLayer(voice.emitterId);
                if (layer == null && voice.continuous)
                    layer = profile.FindLayer(voice.source != null ? voice.source.gameObject.name : string.Empty);

                if (layer != null)
                {
                    target.volume *= layer.volumeMultiplier;
                    target.pan = Mathf.Clamp(layer.pan, -1f, 1f);
                    target.lowPass = Mathf.Clamp(layer.lowPassFrequency, 10f, 22000f);
                    target.reverb = ReverbToDb(layer.reverbSend);
                }

                if (!string.IsNullOrWhiteSpace(voice.anchorId))
                {
                    AudioPointPerspective perspective;
                    bool audible = profile.TryGetPointPerspective(voice.anchorId, out perspective, out _);
                    if (!audible || perspective == null)
                    {
                        target.volume = 0f;
                    }
                    else
                    {
                        target.volume *= perspective.volumeMultiplier * DistanceMultiplier(perspective.distance);
                        target.pan = CalculatePan(perspective);
                        float occlusionCutoff = Mathf.Lerp(22000f, 1800f, Mathf.Clamp01(perspective.occlusion));
                        target.lowPass = Mathf.Min(target.lowPass, Mathf.Min(occlusionCutoff, perspective.lowPassFrequency));
                        target.reverb = ReverbToDb(Mathf.Max(perspective.reverbSend, DirectionReverb(perspective.direction)));
                    }
                }
            }

            target.volume *= MixMultiplier(voice.role);
            target.pan = Mathf.Clamp(target.pan, -1f, 1f);
            target.lowPass = Mathf.Clamp(target.lowPass, 10f, 22000f);
            return target;
        }

        private static float CalculatePan(AudioPointPerspective perspective)
        {
            if (perspective == null) return 0f;
            if (Mathf.Abs(perspective.pan) > 0.001f) return Mathf.Clamp(perspective.pan, -1f, 1f);

            switch (perspective.direction)
            {
                case AudioDirection.Left:
                case AudioDirection.LeftBehind: return -0.8f;
                case AudioDirection.Right:
                case AudioDirection.RightBehind: return 0.8f;
                default: return 0f;
            }
        }

        private static float DirectionReverb(AudioDirection direction)
        {
            switch (direction)
            {
                case AudioDirection.Behind:
                case AudioDirection.LeftBehind:
                case AudioDirection.RightBehind:
                    return 0.25f;
                default:
                    return 0f;
            }
        }

        private static float DistanceMultiplier(AudioDistance distance)
        {
            switch (distance)
            {
                case AudioDistance.Near: return 1f;
                case AudioDistance.Far: return 0.55f;
                default: return 0.8f;
            }
        }

        private static float ReverbToDb(float send)
        {
            return Mathf.Lerp(-10000f, 0f, Mathf.Clamp01(send));
        }

        private float MixMultiplier(AudioSourceRole role)
        {
            switch (_mixState)
            {
                case AudioMixState.Modal:
                    if (role == AudioSourceRole.Ambience) return 0.55f;
                    if (role == AudioSourceRole.Equipment) return 0.8f;
                    return 1f;

                case AudioMixState.MediaFocus:
                    if (role == AudioSourceRole.Ambience) return 0.35f;
                    if (role == AudioSourceRole.Threats) return 0.75f;
                    if (role == AudioSourceRole.Radio || role == AudioSourceRole.Tapes) return 1f;
                    return 0.85f;

                case AudioMixState.Paused:
                    if (role == AudioSourceRole.Ambience) return 0.25f;
                    if (role == AudioSourceRole.Threats) return 0.35f;
                    return 1f;

                case AudioMixState.Transition:
                    if (role == AudioSourceRole.Ambience) return 0.7f;
                    return 1f;

                default:
                    return 1f;
            }
        }

        private IEnumerable<RuntimeVoice> GetVoicesForPresentation()
        {
            HashSet<RuntimeVoice> emitted = new HashSet<RuntimeVoice>();
            foreach (RuntimeVoice voice in _continuousVoices.Values)
                if (voice != null && emitted.Add(voice)) yield return voice;
            foreach (RuntimeVoice voice in _persistentVoices.Values)
                if (voice != null && emitted.Add(voice)) yield return voice;
            foreach (RuntimeVoice voice in _localVoices.Values)
                if (voice != null && emitted.Add(voice)) yield return voice;
            foreach (RuntimeVoice voice in _oneShotVoices)
                if (voice != null && (voice.source.isPlaying || voice.reserved) && emitted.Add(voice)) yield return voice;
        }

        /// <summary>
        /// Atualiza as camadas exclusivas do ViewNode. Entradas e saídas são feitas
        /// com fade individual; assim, uma camada local antiga não é cortada quando
        /// o destino introduz outra camada durante Keep ou Crossfade.
        /// </summary>
        private void ApplyLocalLayers(ViewAudioProfile profile, AudioTransitionMode mode)
        {
            HashSet<string> activeIds = new HashSet<string>();
            if (profile != null && profile.localLayers != null)
            {
                foreach (AudioLocalLayer layer in profile.localLayers)
                {
                    if (layer == null || string.IsNullOrWhiteSpace(layer.layerId) || layer.clip == null) continue;
                    activeIds.Add(layer.layerId);
                    ApplyOrUpdateLocalLayer(layer, mode);
                }
            }

            List<string> toRemove = new List<string>();
            foreach (KeyValuePair<string, RuntimeVoice> pair in _localVoices)
            {
                if (activeIds.Contains(pair.Key)) continue;

                float fadeDuration;
                if (!_localFadeDurations.TryGetValue(pair.Key, out fadeDuration))
                    fadeDuration = profileTransitionDuration;
                if (mode == AudioTransitionMode.Immediate) fadeDuration = 0f;
                BeginLocalFadeOut(pair.Key, pair.Value, fadeDuration);
                toRemove.Add(pair.Key);
            }

            // A voz segue tocando durante o fade; apenas a chave lógica deixa de
            // representar uma camada ativa do novo perfil.
            foreach (string id in toRemove)
            {
                _localVoices.Remove(id);
                _localFadeDurations.Remove(id);
            }
        }

        private void ApplyOrUpdateLocalLayer(AudioLocalLayer layer, AudioTransitionMode mode)
        {
            RuntimeVoice voice;
            bool isNew = !_localVoices.TryGetValue(layer.layerId, out voice) || voice == null;
            if (isNew)
            {
                voice = AcquirePersistentVoice("local:" + layer.layerId, layer.role);
                if (voice == null) return;
                _localVoices[layer.layerId] = voice;
            }

            StopLocalFade(layer.layerId);

            bool clipChanged = voice.source.clip != layer.clip;
            bool wasPlaying = voice.source.isPlaying;
            if (clipChanged && wasPlaying)
            {
                // A mesma camada mudou de clip. Faz uma saída curta antes de reutilizar
                // a fonte, evitando a troca seca de conteúdo no mesmo AudioSource.
                BeginLocalClipReplacement(layer.layerId, voice, layer, LocalFadeDuration(layer, mode));
                return;
            }

            ConfigureLocalVoice(voice, layer);
            VoiceTarget target = CalculateTarget(voice, _currentProfile);
            if (isNew || !wasPlaying)
            {
                ApplyTarget(voice, target);
                voice.source.volume = 0f;
                StartLocalPlayback(voice, layer);
                BeginLocalFadeIn(layer.layerId, voice, target, LocalFadeDuration(layer, mode));
            }
            else
            {
                BeginLocalFadeToTarget(layer.layerId, voice, target, LocalFadeDuration(layer, mode));
            }
        }

        private void ConfigureLocalVoice(RuntimeVoice voice, AudioLocalLayer layer)
        {
            ConfigureVoice(voice, layer.role, layer.clip, layer.loop, layer.volume);
            voice.runtimeId = layer.layerId;
            // Mantém a mesma chave do pool persistente para poder liberar corretamente.
            voice.emitterId = "local:" + layer.layerId;
            voice.reserved = true;
            _localFadeDurations[layer.layerId] = Mathf.Max(0f, layer.fadeDuration);
        }

        private static void StartLocalPlayback(RuntimeVoice voice, AudioLocalLayer layer)
        {
            if (voice == null || voice.source == null || layer == null) return;
            if (layer.loop) voice.source.Play();
            else voice.source.PlayOneShot(layer.clip);
        }

        private float LocalFadeDuration(AudioLocalLayer layer, AudioTransitionMode mode)
        {
            if (mode == AudioTransitionMode.Immediate) return 0f;
            return Mathf.Max(0f, layer != null ? layer.fadeDuration : profileTransitionDuration);
        }

        private void BeginLocalClipReplacement(string layerId, RuntimeVoice voice, AudioLocalLayer targetLayer, float duration)
        {
            int version = BeginLocalFadeOperation(layerId);
            if (duration <= 0f)
            {
                if (voice != null && voice.source != null)
                {
                    voice.source.Stop();
                    ConfigureLocalVoice(voice, targetLayer);
                    VoiceTarget target = CalculateTarget(voice, _currentProfile);
                    StartLocalPlayback(voice, targetLayer);
                    ApplyTarget(voice, target);
                }
                CompleteLocalFadeOperation(layerId, version);
                return;
            }
            _localFadeRoutines[layerId] = StartCoroutine(ReplaceLocalClipRoutine(layerId, version, voice, targetLayer, duration));
        }

        private IEnumerator ReplaceLocalClipRoutine(string layerId, int version, RuntimeVoice voice,
            AudioLocalLayer targetLayer, float duration)
        {
            yield return FadeVoiceVolume(voice, voice != null && voice.source != null ? voice.source.volume : 0f, 0f, duration);
            if (!IsCurrentLocalOperation(layerId, version) || voice == null || voice.source == null) yield break;

            voice.source.Stop();
            ConfigureLocalVoice(voice, targetLayer);
            VoiceTarget target = CalculateTarget(voice, _currentProfile);
            ApplyTarget(voice, target);
            voice.source.volume = 0f;
            StartLocalPlayback(voice, targetLayer);
            yield return FadeVoiceToTarget(voice, target, duration);
            CompleteLocalFadeOperation(layerId, version);
        }

        private void BeginLocalFadeIn(string layerId, RuntimeVoice voice, VoiceTarget target, float duration)
        {
            int version = BeginLocalFadeOperation(layerId);
            if (duration <= 0f)
            {
                ApplyTarget(voice, target);
                CompleteLocalFadeOperation(layerId, version);
                return;
            }
            _localFadeRoutines[layerId] = StartCoroutine(FadeLocalVoiceToTargetRoutine(layerId, version, voice, target, duration));
        }

        private void BeginLocalFadeToTarget(string layerId, RuntimeVoice voice, VoiceTarget target, float duration)
        {
            int version = BeginLocalFadeOperation(layerId);
            if (duration <= 0f)
            {
                ApplyTarget(voice, target);
                CompleteLocalFadeOperation(layerId, version);
                return;
            }
            _localFadeRoutines[layerId] = StartCoroutine(FadeLocalVoiceToTargetRoutine(layerId, version, voice, target, duration));
        }

        private IEnumerator FadeLocalVoiceToTargetRoutine(string layerId, int version, RuntimeVoice voice,
            VoiceTarget target, float duration)
        {
            yield return FadeVoiceToTarget(voice, target, duration);
            CompleteLocalFadeOperation(layerId, version);
        }

        private void BeginLocalFadeOut(string layerId, RuntimeVoice voice, float duration)
        {
            StopLocalFade(layerId);
            int version = BeginLocalFadeOperation(layerId);
            if (duration <= 0f)
            {
                if (voice != null && voice.source != null) voice.source.volume = 0f;
                ReleasePersistentVoice(voice);
                CompleteLocalFadeOperation(layerId, version);
                return;
            }
            _localFadeRoutines[layerId] = StartCoroutine(FadeOutAndReleaseLocalVoice(layerId, version, voice, duration));
        }

        private IEnumerator FadeOutAndReleaseLocalVoice(string layerId, int version, RuntimeVoice voice, float duration)
        {
            float start = voice != null && voice.source != null ? voice.source.volume : 0f;
            yield return FadeVoiceVolume(voice, start, 0f, duration);
            if (!IsCurrentLocalOperation(layerId, version)) yield break;

            ReleasePersistentVoice(voice);
            CompleteLocalFadeOperation(layerId, version);
        }

        private IEnumerator FadeVoiceToTarget(RuntimeVoice voice, VoiceTarget target, float duration)
        {
            if (voice == null || voice.source == null) yield break;

            float startVolume = voice.source.volume;
            float startPan = voice.source.panStereo;
            float startLowPass = voice.lowPass != null ? voice.lowPass.cutoffFrequency : 22000f;
            float startReverb = voice.reverb != null ? voice.reverb.reverbLevel : -10000f;

            if (duration <= 0f)
            {
                ApplyTarget(voice, target);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                voice.source.volume = Mathf.Lerp(startVolume, target.volume, t);
                voice.source.panStereo = Mathf.Lerp(startPan, target.pan, t);
                if (voice.lowPass != null)
                {
                    voice.lowPass.cutoffFrequency = Mathf.Lerp(startLowPass, target.lowPass, t);
                    voice.lowPass.enabled = target.lowPass < 21950f;
                }
                if (voice.reverb != null)
                {
                    voice.reverb.reverbLevel = Mathf.Lerp(startReverb, target.reverb, t);
                    voice.reverb.enabled = target.reverb > -9990f;
                }
                yield return null;
            }
            ApplyTarget(voice, target);
        }

        private IEnumerator FadeVoiceVolume(RuntimeVoice voice, float from, float to, float duration)
        {
            if (voice == null || voice.source == null) yield break;
            if (duration <= 0f)
            {
                voice.source.volume = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                voice.source.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            voice.source.volume = to;
        }

        private int BeginLocalFadeOperation(string layerId)
        {
            StopLocalFade(layerId);
            int version;
            _localFadeVersions.TryGetValue(layerId, out version);
            version++;
            _localFadeVersions[layerId] = version;
            return version;
        }

        private void StopLocalFade(string layerId)
        {
            Coroutine routine;
            if (_localFadeRoutines.TryGetValue(layerId, out routine) && routine != null)
                StopCoroutine(routine);
            _localFadeRoutines.Remove(layerId);
        }

        private bool IsCurrentLocalOperation(string layerId, int version)
        {
            int currentVersion;
            return _localFadeVersions.TryGetValue(layerId, out currentVersion) && currentVersion == version;
        }

        private void CompleteLocalFadeOperation(string layerId, int version)
        {
            if (!IsCurrentLocalOperation(layerId, version)) return;
            _localFadeRoutines.Remove(layerId);
        }

        private RuntimeVoice AcquirePersistentVoice(string emitterId, AudioSourceRole role)
        {
            RuntimeVoice existing;
            if (!string.IsNullOrWhiteSpace(emitterId) && _persistentVoices.TryGetValue(emitterId, out existing))
                return existing;

            RuntimeVoice voice = null;
            foreach (RuntimeVoice candidate in _allVoices)
            {
                if (candidate == null || candidate.continuous || candidate.reserved || candidate.source.isPlaying) continue;
                if (candidate == null) continue;
                voice = candidate;
                break;
            }

            int persistentCount = 0;
            foreach (RuntimeVoice candidate in _allVoices)
                if (candidate != null && candidate.reserved && !candidate.continuous) persistentCount++;

            if (voice == null && persistentCount < Mathf.Max(1, maxPersistentSources))
                voice = CreateVoice("Persistent_" + emitterId, role, false);

            if (voice == null)
            {
                Debug.LogWarning("[SceneAudioController] Pool de fontes persistentes esgotado.", this);
                return null;
            }

            voice.reserved = true;
            voice.emitterId = emitterId;
            voice.role = role;
            if (!string.IsNullOrWhiteSpace(emitterId)) _persistentVoices[emitterId] = voice;
            return voice;
        }

        private void ReleasePersistentVoice(RuntimeVoice voice)
        {
            if (voice == null || voice.source == null) return;

            // Remove somente a chave que ainda aponta para esta voz. Sem isso, uma
            // camada local liberada poderia permanecer no dicionário persistente e
            // ser reutilizada/acidentalmente interrompida por outro emissor.
            string emitterId = voice.emitterId;
            RuntimeVoice mappedVoice;
            if (!string.IsNullOrWhiteSpace(emitterId) &&
                _persistentVoices.TryGetValue(emitterId, out mappedVoice) && mappedVoice == voice)
            {
                _persistentVoices.Remove(emitterId);
            }

            voice.source.Stop();
            voice.source.clip = null;
            voice.source.loop = false;
            voice.reserved = false;
            voice.emitterId = null;
            voice.anchorId = null;
            voice.debugId = null;
            voice.baseVolume = 1f;
        }

        private RuntimeVoice AcquireOneShotVoice(AudioSourceRole role)
        {
            RuntimeVoice selected = null;
            foreach (RuntimeVoice voice in _oneShotVoices)
            {
                if (voice != null && !voice.source.isPlaying)
                {
                    selected = voice;
                    break;
                }
            }

            if (selected == null && _oneShotVoices.Count < Mathf.Max(1, oneShotPoolSize * 2))
            {
                selected = CreateVoice("OneShot_" + _oneShotVoices.Count, role, false);
                _oneShotVoices.Add(selected);
            }

            if (selected == null && _oneShotVoices.Count > 0)
            {
                selected = _oneShotVoices[0];
                selected.source.Stop();
            }

            if (selected != null)
            {
                selected.role = role;
                selected.reserved = false;
                selected.lastUseTime = Time.unscaledTime;
            }
            return selected;
        }

        private bool PlayClipInternal(AudioClip clip, AudioSourceRole role, float volume, float pan,
            string anchorId, string debugId, bool loop, string emitterId = null)
        {
            if (clip == null)
            {
                Debug.LogWarning($"[SceneAudioController] Tentativa de reproduzir clip vazio ({debugId ?? "sem ID"}).", this);
                return false;
            }

            EnsureInitialized();
            RuntimeVoice voice = AcquireOneShotVoice(role);
            if (voice == null) return false;

            ConfigureVoice(voice, role, clip, loop, volume);
            voice.anchorId = anchorId;
            voice.emitterId = emitterId;
            voice.debugId = debugId;
            voice.source.panStereo = Mathf.Clamp(pan, -1f, 1f);
            ApplyTarget(voice, CalculateTarget(voice, _currentProfile));
            if (string.IsNullOrWhiteSpace(anchorId))
                voice.source.panStereo = Mathf.Clamp(pan, -1f, 1f);

            if (loop)
            {
                voice.source.Play();
                voice.reserved = true;
            }
            else
            {
                voice.source.PlayOneShot(clip);
            }
            return true;
        }

        public bool PlayUi(AudioClip clip, float volume = 1f)
            => PlayClipInternal(clip, AudioSourceRole.UI, volume, 0f, null, "ui", false);

        public bool PlayInteraction(AudioClip clip, float volume = 1f)
            => PlayClipInternal(clip, AudioSourceRole.Interactions, volume, 0f, null, "interaction", false);

        public bool PlayTransition(AudioClip clip, float volume = 1f)
            => PlayClipInternal(clip, AudioSourceRole.Transition, volume, 0f, null, "transition", false);

        public bool PlayCatalogEntry(string audioId)
        {
            AudioDebugEntry entry = debugCatalog != null ? debugCatalog.Find(audioId) : null;
            if (entry == null)
            {
                Debug.LogWarning($"[SceneAudioController] AudioDebugCatalog não possui '{audioId}'.", this);
                return false;
            }
            return PlayClipInternal(entry.clip, entry.role, entry.defaultVolume, entry.defaultPan,
                entry.anchorId, entry.id, entry.loop, entry.emitterId);
        }

        public bool StopCatalogEntry(string audioId)
        {
            bool stopped = false;
            foreach (RuntimeVoice voice in _allVoices)
            {
                if (voice == null || !string.Equals(voice.debugId, audioId, StringComparison.Ordinal)) continue;
                voice.source.Stop();
                voice.reserved = false;
                stopped = true;
            }
            return stopped;
        }

        /// <summary>
        /// Registra ou remove uma fonte persistente de equipamento ou sistema.
        /// O estado lógico é fornecido pelo sistema proprietário; este controller
        /// apenas mantém a apresentação sonora.
        /// </summary>
        public bool SetPersistentEmitter(string emitterId, AudioClip clip, AudioSourceRole role,
            string anchorId, bool active, bool loop = true, float volume = 1f)
        {
            if (string.IsNullOrWhiteSpace(emitterId)) return false;
            EnsureInitialized();

            if (!active)
            {
                RuntimeVoice old;
                if (_persistentVoices.TryGetValue(emitterId, out old))
                {
                    ReleasePersistentVoice(old);
                    _persistentVoices.Remove(emitterId);
                }
                return true;
            }

            RuntimeVoice voice = AcquirePersistentVoice(emitterId, role);
            if (voice == null) return false;

            bool shouldPlay = voice.source.clip != clip || !voice.source.isPlaying;
            ConfigureVoice(voice, role, clip, loop, volume);
            voice.anchorId = anchorId;
            if (shouldPlay && clip != null)
            {
                if (loop) voice.source.Play();
                else voice.source.PlayOneShot(clip);
            }
            ApplyTarget(voice, CalculateTarget(voice, _currentProfile));
            return true;
        }

        /// <summary>
        /// Aplica a apresentação de áudio autorada de uma entidade sem consultar o
        /// AudioDebugCatalog. A troca sempre dissolve o loop anterior, toca o SFX
        /// de entrada e então introduz o novo loop no grupo Threats.
        /// </summary>
        public bool ApplyEntityStateAudio(string entityId, string anchorId, EntityState state,
            EntityStateAudioPresentation presentation)
        {
            if (string.IsNullOrWhiteSpace(entityId)) return false;
            EnsureInitialized();

            bool presentableState = state != EntityState.Inactive && state != EntityState.Resolved;
            bool canUseAnchorLoop = presentableState && !string.IsNullOrWhiteSpace(anchorId);
            bool hasEntrySfx = presentation != null && presentation.enterSfxClip != null;
            bool hasConfiguredLoop = presentation != null && presentation.presenceLoopClip != null;
            bool hasLoop = hasConfiguredLoop && canUseAnchorLoop;
            if (!presentableState || (!hasEntrySfx && !hasLoop))
            {
                if (presentableState)
                {
                    string key = entityId + "|" + state;
                    if (_missingEntityAudioWarnings.Add(key))
                    {
                        string detail = presentation == null
                            ? "não possui apresentação de áudio"
                            : (!hasEntrySfx && hasConfiguredLoop && !canUseAnchorLoop
                                ? "possui apenas loop, mas o estado não tem anchor para reproduzi-lo"
                                : "possui apresentação de áudio sem SFX nem loop");
                        Debug.LogWarning("[SceneAudioController] Entidade '" + entityId + "' no estado " + state +
                                         " " + detail + " autorado; a lógica continua sem áudio.", this);
                    }
                }
                BeginEntityAudioTransition(entityId, null, null, EntityState.Inactive, false);
                return true;
            }

            // Light ainda não possui anchor, porém seu SFX de entrada é autorado e
            // deve tocar. Loops permanecem restritos a estados com perspectiva.
            BeginEntityAudioTransition(entityId, anchorId, presentation, state, hasLoop);
            return true;
        }

        /// <summary>Remove somente o loop persistente de apresentação da entidade indicada.</summary>
        public void ClearEntityAudio(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId)) return;
            StopEntityOneShots(entityId);
            BeginEntityAudioTransition(entityId, null, null, EntityState.Inactive, false);
        }

        /// <summary>Liberação instantânea para teardown de Noite/derrota, sem fade residual.</summary>
        public void ClearEntityAudioImmediate(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId)) return;
            string emitterId = "entity:" + entityId;
            Coroutine routine;
            if (_entityAudioRoutines.TryGetValue(emitterId, out routine) && routine != null)
                StopCoroutine(routine);
            _entityAudioRoutines.Remove(emitterId);
            StopEntityOneShots(entityId);

            RuntimeVoice loop;
            if (_persistentVoices.TryGetValue(emitterId, out loop))
                ReleasePersistentVoice(loop);
        }

        public bool HasEntityAudioPresentation(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId)) return false;
            string emitterId = "entity:" + entityId;
            RuntimeVoice voice;
            if (_persistentVoices.TryGetValue(emitterId, out voice) && voice != null &&
                voice.source != null && voice.source.isPlaying)
                return true;
            foreach (RuntimeVoice oneShot in _oneShotVoices)
                if (oneShot != null && string.Equals(oneShot.emitterId, entityId, StringComparison.Ordinal) &&
                    oneShot.source != null && oneShot.source.isPlaying)
                    return true;
            Coroutine routine;
            return _entityAudioRoutines.TryGetValue(emitterId, out routine) && routine != null;
        }

        private void StopEntityOneShots(string entityId)
        {
            foreach (RuntimeVoice oneShot in _oneShotVoices)
            {
                if (oneShot == null || !string.Equals(oneShot.emitterId, entityId, StringComparison.Ordinal)) continue;
                if (oneShot.source != null) oneShot.source.Stop();
                oneShot.anchorId = null;
                oneShot.emitterId = null;
                oneShot.debugId = null;
                oneShot.reserved = false;
            }
        }

        private void BeginEntityAudioTransition(string entityId, string anchorId,
            EntityStateAudioPresentation presentation, EntityState state, bool allowLoop)
        {
            string emitterId = "entity:" + entityId;
            // Durante teardown de cena não há corrotina válida; liberação imediata evita
            // deixar uma voz reservada e não tenta iniciar coroutine em objeto inativo.
            if (!isActiveAndEnabled)
            {
                SetPersistentEmitter(emitterId, null, AudioSourceRole.Threats, null, false);
                return;
            }
            Coroutine routine;
            if (_entityAudioRoutines.TryGetValue(emitterId, out routine) && routine != null)
                StopCoroutine(routine);
            _entityAudioRoutines[emitterId] = StartCoroutine(TransitionEntityAudio(
                emitterId, entityId, anchorId, presentation, state, allowLoop));
        }

        private IEnumerator TransitionEntityAudio(string emitterId, string entityId, string anchorId,
            EntityStateAudioPresentation presentation, EntityState state, bool allowLoop)
        {
            RuntimeVoice previous;
            if (_persistentVoices.TryGetValue(emitterId, out previous))
            {
                float fadeOut = presentation != null ? Mathf.Max(0f, presentation.fadeOutSeconds) : 0.12f;
                float volume = previous.source != null ? previous.source.volume : 0f;
                yield return FadeVoiceVolume(previous, volume, 0f, fadeOut);
                ReleasePersistentVoice(previous);
            }

            if (presentation == null)
            {
                _entityAudioRoutines.Remove(emitterId);
                yield break;
            }

            if (presentation.enterSfxClip != null)
            {
                PlayClipInternal(presentation.enterSfxClip, AudioSourceRole.Threats,
                    Mathf.Max(0f, presentation.enterSfxVolume), 0f, anchorId,
                    "entity-enter:" + entityId + ":" + state, false, entityId);
            }

            if (!allowLoop || presentation.presenceLoopClip == null)
            {
                _entityAudioRoutines.Remove(emitterId);
                yield break;
            }

            RuntimeVoice voice = AcquirePersistentVoice(emitterId, AudioSourceRole.Threats);
            if (voice == null)
            {
                _entityAudioRoutines.Remove(emitterId);
                yield break;
            }

            ConfigureVoice(voice, AudioSourceRole.Threats, presentation.presenceLoopClip, true,
                Mathf.Max(0f, presentation.presenceLoopVolume));
            voice.anchorId = anchorId;
            voice.debugId = "entity-loop:" + entityId + ":" + state;
            voice.source.volume = 0f;
            voice.source.Play();
            VoiceTarget target = CalculateTarget(voice, _currentProfile);
            yield return FadeVoiceToTarget(voice, target, Mathf.Max(0f, presentation.fadeInSeconds));
            _entityAudioRoutines.Remove(emitterId);
        }

        public bool SetThreatState(string entityId, string anchorId, ThreatAudioState state)
        {
            if (string.IsNullOrWhiteSpace(entityId)) return false;
            EnsureInitialized();

            if (state == ThreatAudioState.Inactive)
            {
                ClearThreatState(entityId);
                return true;
            }

            if (string.IsNullOrWhiteSpace(anchorId))
            {
                Debug.LogWarning($"[SceneAudioController] A entidade '{entityId}' precisa de anchorId.", this);
                return false;
            }

            AudioEntityRuntimeState entity;
            if (!_entities.TryGetValue(entityId, out entity))
            {
                entity = new AudioEntityRuntimeState { entityId = entityId };
                _entities.Add(entityId, entity);
            }

            entity.activeAnchorId = anchorId;
            entity.threatState = state;
            entity.isActive = true;

            RuntimeVoice voice = AcquirePersistentVoice("threat:" + entityId, AudioSourceRole.Threats);
            if (voice == null) return false;
            voice.anchorId = anchorId;
            voice.emitterId = entityId;
            ApplyTarget(voice, CalculateTarget(voice, _currentProfile));
            return true;
        }

        public void ClearThreatState(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId)) return;
            _entities.Remove(entityId);

            RuntimeVoice voice;
            if (_persistentVoices.TryGetValue("threat:" + entityId, out voice))
            {
                ReleasePersistentVoice(voice);
                _persistentVoices.Remove("threat:" + entityId);
            }
        }

        public bool PlayThreatSignal(string entityId, string signalId)
        {
            AudioEntityRuntimeState entity;
            if (!_entities.TryGetValue(entityId, out entity) || !entity.isActive)
            {
                Debug.LogWarning($"[SceneAudioController] Entidade '{entityId}' não está ativa.", this);
                return false;
            }

            AudioDebugEntry entry = debugCatalog != null ? debugCatalog.Find(signalId) : null;
            if (entry == null)
            {
                Debug.LogWarning($"[SceneAudioController] Sinal de ameaça não encontrado: '{signalId}'.", this);
                return false;
            }

            RuntimeVoice voice = AcquirePersistentVoice("threat:" + entityId, AudioSourceRole.Threats);
            if (voice == null) return false;
            voice.anchorId = entity.activeAnchorId;
            voice.emitterId = entityId;
            voice.debugId = entry.id;
            ConfigureVoice(voice, AudioSourceRole.Threats, entry.clip, entry.loop, entry.defaultVolume);
            ApplyTarget(voice, CalculateTarget(voice, _currentProfile));

            entity.currentSignalId = entry.id;
            if (entry.clip == null)
            {
                Debug.LogWarning($"[SceneAudioController] Sinal '{signalId}' não possui AudioClip.", this);
                return false;
            }

            if (entry.loop)
            {
                voice.source.Play();
                voice.reserved = true;
            }
            else
            {
                voice.source.PlayOneShot(entry.clip);
            }
            return true;
        }

        public void SetMixState(AudioMixState state, bool immediate = false)
        {
            EnsureInitialized();
            _mixState = state;
            ApplySnapshot(state, immediate);
            ApplyCurrentProfileImmediate();
            MixStateChanged?.Invoke(state);
        }

        private void ApplySnapshot(AudioMixState state, bool immediate)
        {
            AudioMixerSnapshot snapshot = null;
            switch (state)
            {
                case AudioMixState.Modal: snapshot = modalSnapshot; break;
                case AudioMixState.MediaFocus: snapshot = mediaFocusSnapshot; break;
                case AudioMixState.Paused: snapshot = pausedSnapshot; break;
                case AudioMixState.Transition: snapshot = transitionSnapshot; break;
                default: snapshot = normalSnapshot; break;
            }

            if (snapshot != null) snapshot.TransitionTo(immediate ? 0f : 0.15f);
        }

        public void BeginSceneExit()
        {
            EnsureInitialized();
            if (_sceneExitRoutine != null) StopCoroutine(_sceneExitRoutine);
            _sceneExitRoutine = StartCoroutine(FadeSceneExit());
        }

        private IEnumerator FadeSceneExit()
        {
            float duration = sceneExitFadeDuration;
            List<RuntimeVoice> voices = new List<RuntimeVoice>(GetVoicesForPresentation());
            List<float> starts = new List<float>();
            foreach (RuntimeVoice voice in voices) starts.Add(voice != null && voice.source != null ? voice.source.volume : 0f);

            if (duration <= 0f)
            {
                for (int i = 0; i < voices.Count; i++) if (voices[i] != null) voices[i].source.volume = 0f;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < voices.Count; i++)
                    if (voices[i] != null && voices[i].source != null)
                        voices[i].source.volume = Mathf.Lerp(starts[i], 0f, t);
                yield return null;
            }

            foreach (RuntimeVoice voice in voices)
                if (voice != null && voice.source != null) voice.source.volume = 0f;
            _sceneExitRoutine = null;
        }

        public bool TryGetPerspectiveSnapshot(string anchorId, out AudioPerspectiveSnapshot snapshot)
        {
            snapshot = null;
            if (_currentProfile == null || string.IsNullOrWhiteSpace(anchorId)) return false;

            AudioPointPerspective perspective;
            bool explicitOverride;
            bool audible = _currentProfile.TryGetPointPerspective(anchorId, out perspective, out explicitOverride);
            if (perspective == null) return false;

            snapshot = new AudioPerspectiveSnapshot
            {
                anchorId = anchorId,
                hasExplicitOverride = explicitOverride,
                isAudible = audible,
                direction = perspective.direction,
                distance = perspective.distance,
                pan = CalculatePan(perspective),
                volumeMultiplier = perspective.volumeMultiplier * DistanceMultiplier(perspective.distance),
                occlusion = perspective.occlusion,
                lowPassFrequency = perspective.lowPassFrequency,
                reverbSend = Mathf.Max(perspective.reverbSend, DirectionReverb(perspective.direction))
            };
            return true;
        }

        public void StopAllVoices()
        {
            foreach (RuntimeVoice voice in _allVoices)
            {
                if (voice == null || voice.source == null) continue;
                voice.source.Stop();
                voice.source.clip = voice.continuous ? voice.source.clip : null;
                voice.reserved = voice.continuous;
            }
        }

        public void DebugApplyProfile(ViewNodeDefinition definition)
        {
            ApplyViewAudioProfile(definition, AudioTransitionMode.Immediate);
        }

        public bool DebugPlayAudio(string audioId) => PlayCatalogEntry(audioId);

        public bool DebugStopAudio(string audioId) => StopCatalogEntry(audioId);

        public bool DebugSetEntityAnchor(string entityId, string anchorId)
        {
            if (string.IsNullOrWhiteSpace(entityId) || string.IsNullOrWhiteSpace(anchorId)) return false;
            AudioEntityRuntimeState entity;
            if (!_entities.TryGetValue(entityId, out entity))
            {
                entity = new AudioEntityRuntimeState
                {
                    entityId = entityId,
                    threatState = ThreatAudioState.Light,
                    isActive = true
                };
                _entities.Add(entityId, entity);
            }

            entity.activeAnchorId = anchorId;
            entity.isActive = true;
            if (entity.threatState == ThreatAudioState.Inactive) entity.threatState = ThreatAudioState.Light;
            return SetThreatState(entityId, anchorId, entity.threatState);
        }

        public bool DebugSetThreatState(string entityId, ThreatAudioState state)
        {
            AudioEntityRuntimeState entity;
            if (!_entities.TryGetValue(entityId, out entity) || string.IsNullOrWhiteSpace(entity.activeAnchorId))
            {
                Debug.LogWarning($"[SceneAudioController] Configure o anchor da entidade '{entityId}' antes do estado.", this);
                return false;
            }
            return SetThreatState(entityId, entity.activeAnchorId, state);
        }

        public AudioDebugSnapshot GetDebugSnapshot()
        {
            List<AudioDebugVoiceSnapshot> voices = new List<AudioDebugVoiceSnapshot>();
            foreach (RuntimeVoice voice in _allVoices)
            {
                if (voice == null || voice.source == null) continue;
                if (!voice.source.isPlaying && !voice.reserved) continue;
                voices.Add(new AudioDebugVoiceSnapshot
                {
                    runtimeId = voice.runtimeId,
                    emitterId = voice.emitterId,
                    anchorId = voice.anchorId,
                    clipName = voice.source.clip != null ? voice.source.clip.name : string.Empty,
                    role = voice.role,
                    isPlaying = voice.source.isPlaying,
                    isLoop = voice.source.loop,
                    volume = voice.source.volume,
                    pan = voice.source.panStereo
                });
            }

            List<AudioEntityRuntimeState> entities = new List<AudioEntityRuntimeState>();
            foreach (AudioEntityRuntimeState entity in _entities.Values)
                entities.Add(entity.Copy());

            return new AudioDebugSnapshot
            {
                sceneId = _sceneDefinition != null ? _sceneDefinition.sceneId : string.Empty,
                currentViewNodeId = _currentViewNodeId,
                currentProfileId = _currentProfile != null ? _currentProfile.id : string.Empty,
                currentZoneId = _currentProfile != null ? _currentProfile.zoneId : string.Empty,
                mixState = _mixState,
                activeVoiceCount = voices.Count,
                voices = voices.ToArray(),
                entities = entities.ToArray()
            };
        }
    }
}
