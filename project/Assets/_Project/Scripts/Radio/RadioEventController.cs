using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Evento de rádio. Cena própria, sem ViewNode, sem GameplaySceneController e sem
    /// HUD. Todo o fluxo usa tempo não escalado.
    ///
    /// Sequência (plano §8.2):
    ///  1. Tela coberta e preta.
    ///  2. Jingle e espera de `jingleDelaySeconds`.
    ///  3. Revela o rádio e o Frame_01.
    ///  4. Para cada linha: frame, typewriter com som, indicador em laço até o avanço.
    ///  5. Fim da última linha: fade para preto.
    ///  6. Retorno pela sessão (Dia no ViewNode de origem, ou Noite).
    ///
    /// O jogador pula o typewriter, nunca o evento.
    /// </summary>
    public class RadioEventController : MonoBehaviour
    {
        [Header("Dados")]
        [Tooltip("Usado somente quando a sessão não tem evento pendente (Play direto na cena).")]
        [SerializeField] private RadioEventDefinition fallbackDefinition;

        [Header("Visual")]
        [SerializeField] private GameObject viewRoot;
        [SerializeField] private Image[] frames = new Image[3];
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private CanvasGroup indicatorGroup;
        [SerializeField] private Image fader;
        [SerializeField] private CanvasGroup faderGroup;

        [Header("Áudio")]
        [SerializeField] private AudioSource jingleSource;
        [SerializeField] private AudioSource typewriterSource;

        private RadioEventDefinition _definition;
        private bool _advanceRequested;
        private bool _running;
        private float _indicatorAlpha;
        private bool _indicatorGoingUp = true;

        private GameSessionManager Session => GameSessionManager.Instance;

        private IEnumerator Start()
        {
            _definition = Session != null ? Session.PendingRadioEvent : null;
            if (_definition == null) _definition = fallbackDefinition;

            if (_definition == null || !_definition.HasLines)
            {
                Debug.LogWarning("[RadioEventController] Evento de rádio sem linhas. Voltando ao Dia.", this);
                Finish();
                yield break;
            }

            if (typewriterSource != null && _definition.typewriterMixerGroup != null)
                typewriterSource.outputAudioMixerGroup = _definition.typewriterMixerGroup;
            if (!_definition.HasTypewriterAudio)
                Debug.LogWarning("[RadioEventController] Evento sem clips de typewriter: o texto aparece sem som.", this);

            yield return Run();
        }

        private void Update()
        {
            if (!_running) return;
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
                _advanceRequested = true;
        }

        private IEnumerator Run()
        {
            _running = true;

            // 1. Tela coberta e preta; o rádio começa escondido.
            SetFaderAlpha(1f);
            if (viewRoot != null) viewRoot.SetActive(false);
            ShowFrame(-1);
            if (dialogueText != null) dialogueText.text = string.Empty;
            SetIndicator(0f);

            // 2. Jingle antes do rádio aparecer.
            //    Sem jingle não existe espera: seria silêncio em tela preta.
            if (_definition.jingle != null && jingleSource != null)
            {
                jingleSource.PlayOneShot(_definition.jingle);
                yield return WaitUnscaled(_definition.jingleDelaySeconds);
            }

            // 3. Rádio e Frame_01.
            if (viewRoot != null) viewRoot.SetActive(true);
            ShowFrame(0);
            yield return FadeFader(1f, 0f, _definition.revealSeconds);

            // 4. Linhas.
            for (int index = 0; index < _definition.lines.Length; index++)
                yield return PlayLine(index);

            // 5. Preto no fim.
            yield return FadeFader(0f, 1f, _definition.endFadeSeconds);

            _running = false;

            // 6. Retorno.
            Finish();
        }

        private IEnumerator PlayLine(int index)
        {
            RadioLine line = _definition.lines[index];

            ShowFrame(line.frameIndex);
            SetIndicator(0f);
            _advanceRequested = false;

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
        }

        private void Finish()
        {
            if (Session == null)
            {
                Debug.LogError("[RadioEventController] Sessão ausente: não há para onde voltar.", this);
                return;
            }
            if (!Session.TryFinishRadioEvent(out string error))
                Debug.LogError("[RadioEventController] Falha ao retornar do evento: " + error, this);
        }

        // ---------------- Avanço / indicador ----------------

        private bool ConsumeAdvance()
        {
            if (!_advanceRequested) return false;
            _advanceRequested = false;
            return true;
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

        // ---------------- Visual ----------------

        private void ShowFrame(int index)
        {
            if (frames == null) return;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] != null) frames[i].gameObject.SetActive(i == index);
        }

        private void SetFaderAlpha(float alpha)
        {
            if (faderGroup != null) faderGroup.alpha = alpha;
            if (fader != null)
            {
                Color color = fader.color;
                color.a = alpha;
                fader.color = color;
            }
        }

        private IEnumerator FadeFader(float from, float to, float duration)
        {
            if (duration <= 0f) { SetFaderAlpha(to); yield break; }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetFaderAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            SetFaderAlpha(to);
        }

        // ---------------- Áudio ----------------

        private void PlayTypewriterTick()
        {
            if (typewriterSource == null) return;
            AudioClip[] clips = _definition.typewriterClips;
            if (clips == null || clips.Length == 0) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip != null) typewriterSource.PlayOneShot(clip);
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
