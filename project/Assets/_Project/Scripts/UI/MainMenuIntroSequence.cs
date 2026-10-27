using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Whispers
{
    /// <summary>
    /// Animação de entrada do menu principal: cada elemento da lista aparece sozinho,
    /// um de cada vez, com cintilação de luz (ver <see cref="FlickerProfile"/>).
    ///
    /// Regras:
    /// - Roda sempre que o menu carrega (inclusive no retorno do gameplay), em tempo não escalado.
    /// - Não decide regra de menu: quem manda no Continue, nos painéis e no clique continua sendo
    ///   o <see cref="MainMenuController"/>, que chama <see cref="Play"/> e escuta <see cref="Finished"/>.
    /// - Só anima elementos ATIVOS no momento da entrada. Um elemento desativado pelo controller
    ///   (ex.: o botão Continue sem save) nunca é "ressuscitado" pela animação.
    /// - Com <see cref="waitForSceneReveal"/> ligado, espera a cortina do <see cref="LevelManager"/>
    ///   terminar de revelar a cena para não gastar a animação atrás do preto.
    /// - O alpha original de cada elemento é restaurado ao final, então outros sistemas podem
    ///   ligar/desligar os mesmos objetos sem herdar um alpha intermediário.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuIntroSequence : MonoBehaviour
    {
        [Header("Elementos (ordem da sequência)")]
        [Tooltip("Arraste os elementos na ordem em que devem aparecer. Apenas os que estiverem ativos entram na sequência.")]
        [SerializeField] private List<GameObject> elements = new List<GameObject>();

        [Header("Ritmo (tempo não escalado)")]
        [Tooltip("Espera antes do primeiro elemento, contada depois da revelação da cena.")]
        [Min(0f)][SerializeField] private float initialDelay = 0.1f;

        [Tooltip("Intervalo entre um elemento e o próximo. Cada elemento ocupa um 'slot' deste tamanho: " +
                 "o flicker consome o tempo que precisar e o restante do slot é espera silenciosa.")]
        [Min(0f)][SerializeField] private float slotDuration = 0.5f;

        [Header("Cintilação")]
        [SerializeField] private FlickerProfile flicker = new FlickerProfile();

        [Header("Sincronização com a cortina")]
        [Tooltip("Quando ligado, a sequência só começa após o LevelManager terminar de revelar a cena. " +
                 "Sem isso o primeiro segundo da animação fica atrás da cortina preta.")]
        [SerializeField] private bool waitForSceneReveal = true;

        [Tooltip("Limite de segurança da espera pela revelação, em segundos. Evita menu travado se o sinal não chegar.")]
        [Min(1f)][SerializeField] private float revealTimeout = 8f;

        [Header("Aleatoriedade")]
        [Tooltip("Semente fixa para reproduzir exatamente o mesmo flicker (útil em debug).")]
        [SerializeField] private bool useFixedSeed = false;

        [SerializeField] private int seed = 20261027;

        /// <summary>Disparado quando a sequência termina (ou é abortada).</summary>
        public event Action Finished;

        /// <summary>Verdadeiro enquanto a sequência está rodando.</summary>
        public bool IsPlaying { get; private set; }

        private Random _rng;
        private readonly List<CanvasGroup> _touchedGroups = new List<CanvasGroup>();
        private readonly List<float> _originalAlphas = new List<float>();

        private void Awake()
        {
            _rng = useFixedSeed ? new Random(seed) : new Random();
        }

        private void OnDisable()
        {
            // Cena descarregada / menu desligado: encerra sem deixar elementos com alpha intermediário.
            if (!IsPlaying) return;
            StopAllCoroutines();
            RestoreAlphas();
            IsPlaying = false;
            SafeRaiseFinished();
        }

        /// <summary>
        /// Inicia a sequência. Chamadas repetidas enquanto ela roda são ignoradas.
        /// Elementos já ficam invisíveis imediatamente, antes mesmo da espera pela cortina.
        /// </summary>
        public void Play()
        {
            if (IsPlaying) return;

            CollectTargets();
            if (_touchedGroups.Count == 0)
            {
                SafeRaiseFinished();
                return;
            }

            IsPlaying = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // Tudo apagado desde o primeiro frame: nada "pula" na tela.
            for (int i = 0; i < _touchedGroups.Count; i++)
            {
                CanvasGroup group = _touchedGroups[i];
                if (group != null) group.alpha = flicker.offAlpha;
            }

            if (waitForSceneReveal)
            {
                yield return WaitForReveal();
                if (initialDelay > 0f) yield return FlickerProfile.Wait(initialDelay);
            }

            for (int i = 0; i < _touchedGroups.Count; i++)
            {
                CanvasGroup group = _touchedGroups[i];
                if (group == null) continue;

                // Pode ter sido desativado pelo controller entre o Play e este slot.
                if (!group.gameObject.activeInHierarchy)
                {
                    group.alpha = _originalAlphas[i];
                    continue;
                }

                float startedAt = Time.realtimeSinceStartup;
                yield return flicker.Reveal(group, _rng);

                float remaining = slotDuration - (Time.realtimeSinceStartup - startedAt);
                if (remaining > 0f) yield return FlickerProfile.Wait(remaining);
            }

            RestoreAlphas();
            IsPlaying = false;
            SafeRaiseFinished();
        }

        /// <summary>
        /// Espera o <see cref="LevelManager"/> terminar a revelação da cena.
        ///
        /// O contrato do LevelManager já dá o sinal necessário sem API nova: ele limpa
        /// <see cref="LevelManager.IsLoading"/> somente DEPOIS de executar
        /// AnimateTransitionOut(), ou seja, quando o preto terminou de sair. Enquanto isso,
        /// os elementos ficam apagados (aplicado no início da corrotina), então nada aparece
        /// por baixo da cortina. Sem LevelManager (Play direto na cena do menu), segue imediatamente.
        /// </summary>
        private IEnumerator WaitForReveal()
        {
            LevelManager loader = LevelManager.Instance;
            if (loader == null || !loader.IsLoading) yield break;

            float elapsed = 0f;
            while (loader != null && loader.IsLoading && elapsed < revealTimeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (loader != null && loader.IsLoading)
            {
                Debug.LogWarning("[MainMenuIntroSequence] A revelação da cena não terminou em " + revealTimeout +
                                 "s. A sequência de entrada começou mesmo assim.", this);
            }
        }

        /// <summary>
        /// Monta a lista de alvos reais: apenas elementos ativos, com CanvasGroup garantido.
        /// Guarda o alpha original de cada um para restaurar no fim.
        /// </summary>
        private void CollectTargets()
        {
            _touchedGroups.Clear();
            _originalAlphas.Clear();

            for (int i = 0; i < elements.Count; i++)
            {
                GameObject element = elements[i];
                if (element == null || !element.activeInHierarchy) continue;

                CanvasGroup group = element.GetComponent<CanvasGroup>();
                if (group == null) group = element.AddComponent<CanvasGroup>();

                _touchedGroups.Add(group);
                _originalAlphas.Add(group.alpha);
            }
        }

        private void RestoreAlphas()
        {
            for (int i = 0; i < _touchedGroups.Count; i++)
            {
                CanvasGroup group = _touchedGroups[i];
                if (group == null) continue;
                group.alpha = i < _originalAlphas.Count ? _originalAlphas[i] : 1f;
            }
        }

        private void SafeRaiseFinished()
        {
            Action handler = Finished;
            if (handler == null) return;

            // Uma falha de apresentação não pode impedir o menu de voltar a aceitar input.
            foreach (Action listener in handler.GetInvocationList())
            {
                try { ((Action)listener)(); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
