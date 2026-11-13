using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Evento de rádio como VN dentro da cena do Dia.
    ///
    /// Não carrega cena: é uma camada de UI que cobre o gameplay, roda o evento e devolve
    /// o controle ao jogador. O ViewNode atual continua exatamente onde estava, porque nada
    /// é recarregado.
    ///
    /// Sequência (plano §8.2):
    ///  1. A sala escurece sob o fader (curto, não é estado permanente).
    ///  2. Jingle e espera de `jingleDelaySeconds` (sem jingle, sem espera).
    ///  3. Revela a arte e o Frame_01.
    ///  4. Para cada linha: frame, typewriter com som, indicador em laço até o avanço.
    ///  5. Fim da última linha: fade para preto, esconde a VN.
    ///  6. Retorno: devolve a sala (ReturnToDay) ou encerra o Dia (AdvanceToNight).
    ///
    /// O jogador pula o typewriter, nunca o evento. Todo o fluxo usa tempo não escalado.
    /// </summary>
    public class RadioEventController : MonoBehaviour
    {
        [Header("Dados")]
        [Tooltip("Usado somente quando Play é chamado sem definição (Play direto na cena, teste).")]
        [SerializeField] private RadioEventDefinition fallbackDefinition;

        [Header("Visual")]
        [Tooltip("Raiz da arte da VN. Fica inativa fora do evento.")]
        [SerializeField] private GameObject viewRoot;
        [Tooltip("Frames da arte, um por linha. frameIndex 0 = Frame_01.")]
        [SerializeField] private Image[] frames = new Image[3];
        [SerializeField] private TMP_Text dialogueText;
        [Tooltip("Indicador de avanço. Pulsa em laço quando a linha termina.")]
        [SerializeField] private CanvasGroup indicatorGroup;
        [Tooltip("Imagem preta que cobre a tela durante as transições.")]
        [SerializeField] private Image fader;
        [SerializeField] private CanvasGroup faderGroup;

        [Header("Áudio")]
        [SerializeField] private AudioSource jingleSource;
        [SerializeField] private AudioSource typewriterSource;

        [Header("Transição")]
        [Tooltip("Escurecimento da sala antes do jingle. Zero gera corte seco.")]
        [SerializeField] private float blackoutSeconds = 0.4f;

        [Header("Cena")]
        [Tooltip("Opcional: usado para bloquear a entrada de gameplay durante a VN.")]
        [SerializeField] private GameplaySceneController scene;

        /// <summary>Disparado quando a VN termina e o controle volta ao jogador.</summary>
        public event Action Finished;

        /// <summary>Verdadeiro enquanto a VN estiver em execução.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>Definição em execução. Nulo fora do evento.</summary>
        public RadioEventDefinition CurrentDefinition { get; private set; }

        private RadioEventDefinition _definition;
        private bool _advanceRequested;
        private bool _acceptAdvance;
        private bool _running;
        private bool _inputBlocked;
        private float _indicatorAlpha;
        private bool _indicatorGoingUp = true;

        private GameSessionManager Session => GameSessionManager.Instance;

        private void Awake()
        {
            if (scene == null) scene = GetComponentInParent<GameplaySceneController>();
            if (scene == null) scene = FindFirstObjectByType<GameplaySceneController>(FindObjectsInactive.Include);
            HideInstant();
        }

        private void OnDisable()
        {
            // Cena descarregada no meio da VN (troca de período): devolve o bloqueio.
            if (_inputBlocked) SetInputBlocked(false);
        }

        /// <summary>
        /// Inicia a VN com a definição informada. Devolve falso quando já existe evento em
        /// execução ou quando a definição não tem linhas.
        /// </summary>
        public bool Play(RadioEventDefinition definition = null)
        {
            if (IsPlaying)
            {
                Debug.LogWarning("[RadioEventController] Já existe um evento de rádio em execução.", this);
                return false;
            }

            RadioEventDefinition chosen = definition != null ? definition : fallbackDefinition;
            if (chosen == null || !chosen.HasLines)
            {
                Debug.LogWarning("[RadioEventController] Definição ausente ou sem linhas: a VN não inicia.", this);
                return false;
            }

            _definition = chosen;
            CurrentDefinition = chosen;
            _advanceRequested = false;
            _acceptAdvance = false;
            _indicatorGoingUp = true;
            IsPlaying = true;
            _running = true;

            if (typewriterSource != null && chosen.typewriterMixerGroup != null)
                typewriterSource.outputAudioMixerGroup = chosen.typewriterMixerGroup;
            if (!chosen.HasTypewriterAudio)
                Debug.LogWarning("[RadioEventController] Evento sem clips de typewriter: o texto aparece sem som.", this);

            SetInputBlocked(true);
            Debug.Log("[RadioEventController] Play aceito: " + chosen.eventId + ", " +
                      chosen.lines.Length + " linha(s).", this);
            StartCoroutine(Run());
            return true;
        }

        private void Update()
        {
            // Cliques durante o preto e o jingle não contam: sem linha na tela, um clique
            // guardado completaria a primeira linha no mesmo instante em que ela aparecesse.
            if (!_running || !_acceptAdvance) return;
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
                _advanceRequested = true;
        }

        private IEnumerator Run()
        {
            // 1. A sala escurece e a arte começa escondida.
            yield return FadeFader(0f, 1f, blackoutSeconds);
            if (viewRoot != null) viewRoot.SetActive(false);
            ShowFrame(-1);
            if (dialogueText != null) dialogueText.text = string.Empty;
            SetIndicator(0f);

            // 2. Jingle antes da arte aparecer. Sem jingle não existe espera: seria silêncio.
            if (_definition.jingle != null && jingleSource != null)
            {
                Debug.Log("[RadioEventController] Jingle, espera de " + _definition.jingleDelaySeconds + "s.", this);
                jingleSource.PlayOneShot(_definition.jingle);
                yield return WaitUnscaled(_definition.jingleDelaySeconds);
            }

            // 3. Arte e Frame_01.
            if (viewRoot != null) viewRoot.SetActive(true);
            ShowFrame(0);
            yield return FadeFader(1f, 0f, _definition.revealSeconds);

            // 4. Linhas.
            for (int index = 0; index < _definition.lines.Length; index++)
                yield return PlayLine(index);

            // 5. Fecha a VN sob o preto.
            yield return FadeFader(0f, 1f, _definition.endFadeSeconds);
            if (viewRoot != null) viewRoot.SetActive(false);
            if (dialogueText != null) dialogueText.text = string.Empty;
            SetIndicator(0f);

            _running = false;

            // 6. Retorno.
            yield return Finish();
        }

        private IEnumerator Finish()
        {
            RadioReturnMode mode = _definition != null ? _definition.returnMode : RadioReturnMode.ReturnToDay;

            if (mode == RadioReturnMode.AdvanceToNight)
            {
                // Troca de período: a cena da Noite entra e esta é descarregada.
                if (Session == null)
                {
                    Debug.LogError("[RadioEventController] Sessão ausente: não há como encerrar o Dia.", this);
                    yield break;
                }
                if (!Session.TryEndPeriod(out string error))
                    Debug.LogError("[RadioEventController] Falha ao encerrar o Dia: " + error, this);
                yield break;
            }

            // ReturnToDay: devolve a sala sob o mesmo preto, sem recarregar nada.
            float reveal = _definition != null ? _definition.revealSeconds : 0.6f;
            yield return FadeFader(1f, 0f, reveal);

            IsPlaying = false;
            CurrentDefinition = null;
            SetInputBlocked(false);
            Finished?.Invoke();
        }

        private IEnumerator PlayLine(int index)
        {
            RadioLine line = _definition.lines[index];

            ShowFrame(line.frameIndex);
            SetIndicator(0f);
            _advanceRequested = false;
            _acceptAdvance = true; // a partir daqui o clique avança, não inicia

            if (line.sfx != null && typewriterSource != null)
                typewriterSource.PlayOneShot(line.sfx);

            string text = line.text ?? string.Empty;
            float charactersPerSecond = Mathf.Max(1f, _definition.charactersPerSecond);
            float accumulator = 0f;
            int visible = 0;
            if (dialogueText != null) dialogueText.text = string.Empty;

            // Typewriter. O avanço completa a linha no mesmo instante.
            while (visible < text.Length)
            {
                if (ConsumeAdvance())
                {
                    visible = text.Length;
                    break;
                }

                accumulator += Time.unscaledDeltaTime * charactersPerSecond;
                int target = Mathf.Min(text.Length, Mathf.FloorToInt(accumulator));
                while (visible < target)
                {
                    if (!char.IsWhiteSpace(text[visible])) PlayTypewriterTick();
                    visible++;
                }

                if (dialogueText != null) dialogueText.text = text.Substring(0, visible);
                yield return null;
            }

            if (dialogueText != null) dialogueText.text = text;

            // Linha completa: o indicador pulsa até o jogador avançar.
            while (!ConsumeAdvance())
            {
                UpdateIndicator();
                yield return null;
            }

            SetIndicator(0f);
            _advanceRequested = false; // impede avançar duas linhas no mesmo frame
            _acceptAdvance = false;
        }

        // ---------------- Entrada ----------------

        private void SetInputBlocked(bool blocked)
        {
            if (_inputBlocked == blocked) return;
            if (scene == null || scene.Blocker == null) { _inputBlocked = blocked; return; }

            if (blocked) scene.Blocker.AddReason(InputBlockReason.Cutscene);
            else scene.Blocker.RemoveReason(InputBlockReason.Cutscene);
            _inputBlocked = blocked;
        }

        private bool ConsumeAdvance()
        {
            if (!_advanceRequested) return false;
            _advanceRequested = false;
            return true;
        }

        // ---------------- Apresentação ----------------

        private void HideInstant()
        {
            if (viewRoot != null) viewRoot.SetActive(false);
            // Em repouso o fader é transparente: fora do evento a sala aparece normalmente.
            SetFaderAlpha(0f);
            SetIndicator(0f);
            if (dialogueText != null) dialogueText.text = string.Empty;
        }

        private void ShowFrame(int index)
        {
            if (frames == null) return;
            for (int frame = 0; frame < frames.Length; frame++)
            {
                if (frames[frame] == null) continue;
                frames[frame].gameObject.SetActive(index >= 0 && frame == index);
            }
        }

        private void PlayTypewriterTick()
        {
            if (typewriterSource == null || _definition == null || !_definition.HasTypewriterAudio) return;
            AudioClip[] clips = _definition.typewriterClips;
            typewriterSource.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)]);
        }

        private void UpdateIndicator()
        {
            if (indicatorGroup == null) return;

            float duration = Mathf.Max(0.05f, _definition.indicatorFadeSeconds);
            float step = Time.unscaledDeltaTime / duration;
            _indicatorAlpha += _indicatorGoingUp ? step : -step;

            if (_indicatorAlpha >= 1f) { _indicatorAlpha = 1f; _indicatorGoingUp = false; }
            else if (_indicatorAlpha <= 0f) { _indicatorAlpha = 0f; _indicatorGoingUp = true; }

            indicatorGroup.alpha = _indicatorAlpha;
        }

        private void SetIndicator(float alpha)
        {
            _indicatorAlpha = alpha;
            _indicatorGoingUp = true;
            if (indicatorGroup != null) indicatorGroup.alpha = alpha;
        }

        private void SetFaderAlpha(float alpha)
        {
            if (faderGroup != null) faderGroup.alpha = alpha;
            else if (fader != null)
            {
                Color color = fader.color;
                color.a = alpha;
                fader.color = color;
            }
        }

        private IEnumerator FadeFader(float from, float to, float seconds)
        {
            SetFaderAlpha(from);
            if (seconds <= 0f) { SetFaderAlpha(to); yield break; }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFaderAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds)));
                yield return null;
            }
            SetFaderAlpha(to);
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            if (seconds <= 0f) yield break;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
