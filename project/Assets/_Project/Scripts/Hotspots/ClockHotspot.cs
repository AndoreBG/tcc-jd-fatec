using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Relógio do gameplay.
    ///
    /// Duas funções separadas:
    ///  1. TEXTO — ao passar o mouse, mostra a hora atual como texto ("01PM" no Dia,
    ///     "12AM" a "06AM" na Noite). O texto usa o HotspotTooltipUI e acompanha o ponteiro.
    ///  2. ARTE — o sprite da imagem do relógio troca sozinho a cada hora, sempre, com ou
    ///     sem mouse em cima. No Dia existe um sprite só (01PM). Na Noite existem 7.
    ///
    /// Regras (plano §7.2, com a correção de 2026-11-11):
    ///  1. `activationMode = HoverImmediate` e `repeatPolicy = Repeatable`.
    ///  2. `OnActivated` devolve verdadeiro e não executa ação. O hotspot só apresenta.
    ///  3. A hora vem de `EntityDirector.DisplayHourIndex` (faixa 0 a 6), lida por frame.
    ///  4. O cursor do sistema não é escondido. O relógio é arte de cena, não cursor.
    /// </summary>
    public class ClockHotspot : HotspotBase
    {
        [Header("Arte do relógio")]
        [Tooltip("Image do relógio. O sprite troca sozinho a cada hora. Sem referência, só o texto da hora funciona.")]
        [SerializeField] private Image clockImage;

        [Tooltip("Relógio do Dia. O Dia tem uma hora só: 01PM.")]
        [SerializeField] private Sprite daySprite;

        [Tooltip("Sete sprites da Noite. Índice 0 = 12AM, 1 = 01AM, ..., 6 = 06AM.")]
        [SerializeField] private Sprite[] nightHourSprites = new Sprite[7];

        [Tooltip("Sprite usado quando a hora não tem arte configurada.")]
        [SerializeField] private Sprite unavailableSprite;

        private const string DayLabel = "01PM";
        private static readonly string[] HourLabels = { "12AM", "01AM", "02AM", "03AM", "04AM", "05AM", "06AM" };

        private int _shownHourIndex = -1;
        private bool _warnedMissingSprite;

        /// <summary>O hotspot de relógio não executa ação. Ele só apresenta a hora.</summary>
        protected override bool OnActivated() => true;

        protected override void OnHoverEnter()
        {
            HotspotTooltipUI tooltip = Scene != null ? Scene.Tooltip : null;
            if (tooltip != null) tooltip.Show(FormatHourLabel());
        }

        protected override void OnHoverExit()
        {
            HotspotTooltipUI tooltip = Scene != null ? Scene.Tooltip : null;
            if (tooltip != null) tooltip.Hide();
        }

        protected override void Update()
        {
            // A base trata bloqueio, reentrada e a abertura/fechamento da UI de hover.
            base.Update();
            RefreshClockSprite();
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

        // ---------------- Arte do relógio ----------------

        /// <summary>
        /// Troca o sprite da imagem só quando a hora muda.
        /// No Dia o relógio fica parado em 01PM: aplica uma vez e não lê mais a hora.
        /// </summary>
        private void RefreshClockSprite()
        {
            if (clockImage == null) return;
            if (IsDayPeriod() && _shownHourIndex >= 0) return;

            int hourIndex = CurrentHourIndex();
            if (hourIndex == _shownHourIndex) return;

            Sprite sprite = ResolveSprite(hourIndex);
            clockImage.sprite = sprite;
            _shownHourIndex = hourIndex;

            if (sprite != null || _warnedMissingSprite) return;
            _warnedMissingSprite = true;
            Debug.LogWarning("[ClockHotspot] Sem sprite de relógio para a hora " +
                             FormatHourLabel() + ". A arte do relógio não aparece.", this);
        }

        private Sprite ResolveSprite(int hourIndex)
        {
            if (IsDayPeriod())
                return daySprite != null ? daySprite : unavailableSprite;

            if (nightHourSprites != null && hourIndex >= 0 && hourIndex < nightHourSprites.Length &&
                nightHourSprites[hourIndex] != null)
                return nightHourSprites[hourIndex];

            return unavailableSprite;
        }

        // ---------------- Texto da hora ----------------

        private string FormatHourLabel()
        {
            if (IsDayPeriod()) return DayLabel;
            return HourLabels[Mathf.Clamp(CurrentHourIndex(), 0, 6)];
        }

        private bool IsDayPeriod()
        {
            GameplaySceneController scene = Scene;
            return scene == null || scene.SceneDefinition == null || scene.SceneDefinition.period == GamePeriod.Day;
        }

        private int CurrentHourIndex()
        {
            if (IsDayPeriod()) return 0;

            GameplaySceneController scene = Scene;
            EntityDirector entities = scene != null ? scene.Entities : null;
            return entities != null ? Mathf.Clamp(entities.DisplayHourIndex, 0, 6) : 0;
        }
    }
}
