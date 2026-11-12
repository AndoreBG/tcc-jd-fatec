using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Relógio do gameplay. Substitui o HUD de tempo: quando o ponteiro está sobre o
    /// hotspot, o cursor do sistema é escondido e o sprite da hora atual aparece no
    /// lugar do ponteiro.
    ///
    /// Regras (plano §7.2):
    ///  1. `activationMode = HoverImmediate` e `repeatPolicy = Repeatable`.
    ///  2. `OnActivated` devolve verdadeiro e não executa ação. O hotspot só apresenta.
    ///  3. Enquanto `IsCursorOver` e `IsAvailable`, pede o cursor oculto e mostra o sprite.
    ///  4. Ao sair, ao bloquear, ao navegar ou no GameOver, libera o cursor e esconde o sprite.
    ///  5. A hora é lida por frame de `EntityDirector.DisplayHourIndex` (faixa 0 a 6).
    /// </summary>
    public class ClockHotspot : HotspotBase
    {
        [Header("Sprites")]
        [Tooltip("Relógio do Dia. O Dia tem uma hora só: 01PM.")]
        [SerializeField] private Sprite daySprite;

        [Tooltip("Sete sprites da Noite. Índice 0 = 12AM, 1 = 01AM, ..., 6 = 06AM.")]
        [SerializeField] private Sprite[] nightHourSprites = new Sprite[7];

        [Tooltip("Sprite opcional para hora sem arte configurada.")]
        [SerializeField] private Sprite unavailableSprite;

        [Tooltip("Esconde o cursor do sistema enquanto o relógio está sob o ponteiro.")]
        [SerializeField] private bool hideCursor = true;

        private static readonly string[] HourLabels = { "12AM", "01AM", "02AM", "03AM", "04AM", "05AM", "06AM" };

        private bool _showing;
        private int _shownHourIndex = -1;
        private bool _warnedMissingSprite;

        /// <summary>O hotspot de relógio não executa ação. Ele somente apresenta a hora.</summary>
        protected override bool OnActivated() => true;

        protected override void OnHoverEnter() => Present(force: true);

        protected override void OnHoverExit() => StopPresentation();

        protected override void Update()
        {
            // A base trata bloqueio, reentrada e a abertura/fechamento da UI de hover.
            base.Update();

            if (!_showing) return;

            // Trava os casos que não passam pelo PointerExit: bloqueio, navegação, GameOver.
            if (!IsPresented || !IsCursorOver || !IsAvailable || IsGameplayBlocked)
            {
                StopPresentation();
                return;
            }

            Present(force: false);
        }

        /// <summary>Verdadeiro quando existem sprites para o período informado.</summary>
        public bool HasSpritesForPeriod(GamePeriod period)
        {
            if (period == GamePeriod.Day) return daySprite != null;
            if (nightHourSprites == null || nightHourSprites.Length < 7) return false;
            foreach (Sprite sprite in nightHourSprites)
                if (sprite == null) return false;
            return true;
        }

        // ---------------- Apresentação ----------------

        private void Present(bool force)
        {
            GameplaySceneController scene = Scene;
            if (scene == null) return;

            ClockCursorPresenter presenter = scene.ClockPresenter;
            if (presenter == null) return;

            int hourIndex = CurrentHourIndex();
            if (!force && _showing && hourIndex == _shownHourIndex) return;

            Sprite sprite = ResolveSprite(hourIndex);
            if (sprite == null)
            {
                StopPresentation();
                if (!_warnedMissingSprite)
                {
                    _warnedMissingSprite = true;
                    Debug.LogWarning("[ClockHotspot] Sem sprite de relógio para a hora " +
                                     HourLabels[Mathf.Clamp(hourIndex, 0, 6)] + ".", this);
                }
                return;
            }

            if (hideCursor && scene.Cursor != null) scene.Cursor.RequestHiddenCursor(this);
            presenter.Show(sprite);
            _showing = true;
            _shownHourIndex = hourIndex;
        }

        private void StopPresentation()
        {
            if (!_showing) return;
            _showing = false;
            _shownHourIndex = -1;

            GameplaySceneController scene = Scene;
            if (scene == null) return;
            if (scene.Cursor != null) scene.Cursor.ReleaseHiddenCursor(this);
            if (scene.ClockPresenter != null) scene.ClockPresenter.Hide();
        }

        protected override void OnDisable()
        {
            StopPresentation();
            base.OnDisable();
        }

        // ---------------- Dados ----------------

        private int CurrentHourIndex()
        {
            GameplaySceneController scene = Scene;
            if (scene == null || scene.SceneDefinition == null) return 0;
            if (scene.SceneDefinition.period == GamePeriod.Day) return 0;

            EntityDirector entities = scene.Entities;
            return entities != null ? Mathf.Clamp(entities.DisplayHourIndex, 0, 6) : 0;
        }

        private Sprite ResolveSprite(int hourIndex)
        {
            GameplaySceneController scene = Scene;
            bool isDay = scene != null && scene.SceneDefinition != null && scene.SceneDefinition.period == GamePeriod.Day;

            if (isDay)
                return daySprite != null ? daySprite : unavailableSprite;

            if (nightHourSprites != null && hourIndex >= 0 && hourIndex < nightHourSprites.Length &&
                nightHourSprites[hourIndex] != null)
                return nightHourSprites[hourIndex];

            return unavailableSprite;
        }
    }
}
