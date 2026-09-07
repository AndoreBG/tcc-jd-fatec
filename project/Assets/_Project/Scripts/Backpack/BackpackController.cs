using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Backpack — inventário do Dia. Modal inferior aberto SOMENTE por tecla (padrão B;
    /// ESC também fecha) e apenas durante o período Dia. Slots travados por tipo de item
    /// (BackpackSlotDefinition). Uso de ferramenta: pressionar o item e ARRASTAR para
    /// fora do modal (cursor além do topo + exitThreshold do slot) — o modal desce com
    /// animação, a ferramenta fica "na mão" (ghost no cursor + motivo ToolDrag; hotspots
    /// param de reagir) e o DROP sobre um ToolHotspot solicita o uso pelo
    /// InteractionManager (acceptedTools, condições e consumesOnUse preservados).
    ///
    /// Política acordada ("generosa"): após QUALQUER tentativa — uso bem-sucedido,
    /// ferramenta incompatível, hotspot com condição bloqueada ou drop fora de alvo —
    /// a ferramenta VOLTA para a Backpack (não é perdida). Soltar dentro do modal
    /// cancela o arraste e mantém o modal aberto.
    /// </summary>
    public class BackpackController : MonoBehaviour
    {
        [Header("Teclas (Input Manager legado)")]
        [Tooltip("Tecla que abre/fecha a Backpack (somente durante o Dia).")]
        [SerializeField] private KeyCode toggleKey = KeyCode.B;

        [Tooltip("Fecha a Backpack e cancela um arraste em andamento (devolve a ferramenta).")]
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;

        [Header("Slots (6 no design atual)")]
        [Tooltip("Definições dos slots, na ordem da esquerda para a direita.")]
        [SerializeField] private BackpackSlotDefinition[] slotDefinitions = new BackpackSlotDefinition[0];

        [Tooltip("Prefab de UI de slot (BackpackSlotUI).")]
        [SerializeField] private BackpackSlotUI slotUIPrefab;

        [Tooltip("Container onde os slots são instanciados.")]
        [SerializeField] private RectTransform slotsRoot;

        [Tooltip("Espaço horizontal entre slots (unidades de canvas).")]
        [SerializeField] private float slotSpacing = 16f;

        [Header("Painel e animação")]
        [Tooltip("Painel que desliza (anchoredPosition.y entre openY e closedY).")]
        [SerializeField] private RectTransform panel;

        [Tooltip("anchoredPosition.y com a Backpack ABERTA (recomendado pivot/anchor na base).")]
        [SerializeField] private float openY = 0f;

        [Tooltip("anchoredPosition.y FECHADA (abaixo da tela, fora de vista).")]
        [SerializeField] private float closedY = -420f;

        [Tooltip("Duração de cada direção da animação (tempo NÃO escalado — §15).")]
        [SerializeField] private float animationDuration = 0.25f;

        private enum BpState { Closed, Opening, Open, Closing, DragInside, DragOutside }

        private BpState _state = BpState.Closed;
        private readonly List<BackpackSlotUI> _slotUIs = new List<BackpackSlotUI>();
        private BackpackSlotDefinition _dragSlot;
        private RectTransform _ghost;
        private Image _ghostImage;
        private Coroutine _animRoutine;
        private bool _sessionSubscribed;
        private bool _modalAdded;
        private bool _toolDragAdded;

        private GameSessionManager Session => GameSessionManager.Instance;
        private GameplaySceneController Scene => GameplaySceneController.Instance;
        private InputBlocker Blocker => Scene != null ? Scene.Blocker : null;
        private Canvas HostCanvas => GetComponentInParent<Canvas>();

        private bool IsDragging => _state == BpState.DragInside || _state == BpState.DragOutside;

        private void Start()
        {
            if (panel != null)
                panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, closedY);
            BuildSlots();
            RefreshSlots();
        }

        protected void OnEnable()
        {
            EnsureSessionSubscription();
        }

        protected void OnDisable()
        {
            if (_sessionSubscribed && Session != null)
            {
                Session.SessionStateChanged -= RefreshSlots;
                _sessionSubscribed = false;
            }
            if (IsDragging) CleanupDrag();
            if (_animRoutine != null) { StopCoroutine(_animRoutine); _animRoutine = null; }
            RemoveModalReason();    // remove SOMENTE motivos adicionados por este controller
            RemoveToolDragReason();
            _state = BpState.Closed;
        }

        private void Update()
        {
            EnsureSessionSubscription();

            if (Input.GetKeyDown(toggleKey))
            {
                if (_state == BpState.Closed) TryOpen();
                else if (_state == BpState.Open) Close();
            }
            if (Input.GetKeyDown(closeKey))
            {
                if (_state == BpState.Open || _state == BpState.Opening) Close();
                else if (IsDragging) CancelDrag(); // devolve a ferramenta ao slot
            }

            if (IsDragging)
            {
                UpdateGhostPosition();

                // Ferramenta saiu da mochila: cursor além do topo do painel + threshold do slot.
                if (_state == BpState.DragInside && IsCursorBeyondExitThreshold())
                    ExitBackpackWithDrag();

                // Fim do arraste: botão esquerdo solto em qualquer lugar.
                if (Input.GetMouseButtonUp(0))
                    EndDrag();
            }
        }

        // ---------------- Abrir / fechar ----------------

        private void TryOpen()
        {
            if (Session == null || Scene == null || Blocker == null) return;

            if (Session.period != GamePeriod.Day)
            {
                Debug.Log("[Backpack] A Backpack só pode ser aberta durante o Dia.");
                return;
            }
            if (Blocker.HasReason(InputBlockReason.Transition)) return;

            AddModalReason(); // todos os hotspots do cenário param de reagir
            _state = BpState.Opening;
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(MovePanel(openY, () => _state = BpState.Open));
        }

        private void Close()
        {
            if (_state != BpState.Open && _state != BpState.Opening) return;
            _state = BpState.Closing;
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(MovePanel(closedY, () =>
            {
                RemoveModalReason(); // bloqueio cobre toda a animação (arquitetura §5.3)
                _state = BpState.Closed;
            }));
        }

        private IEnumerator MovePanel(float targetY, System.Action onDone)
        {
            if (panel == null || animationDuration <= 0f)
            {
                if (panel != null) panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, targetY);
                _animRoutine = null;
                onDone?.Invoke();
                yield break;
            }

            float startY = panel.anchoredPosition.y;
            float t = 0f;
            while (t < animationDuration)
            {
                t += Time.unscaledDeltaTime;
                float y = Mathf.Lerp(startY, targetY, Mathf.Clamp01(t / animationDuration));
                panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, y);
                yield return null;
            }
            panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, targetY);
            _animRoutine = null;
            onDone?.Invoke();
        }

        // ---------------- Arraste ----------------

        /// <summary>Iniciado pelo BackpackSlotUI ao pressionar um slot com item presente.</summary>
        public void BeginDragFromSlot(BackpackSlotDefinition slotDefinition)
        {
            if (_state != BpState.Open || slotDefinition == null || !slotDefinition.dragEnabled) return;

            ItemDefinition item = slotDefinition.acceptedItem;
            if (item == null || Session == null || Session.GetQuantity(item.id) <= 0) return;

            _dragSlot = slotDefinition;
            Session.SetSelectedTool(item.id); // API de seleção em uso: ferramenta "na mão"
            AddToolDragReason();
            CreateGhost(item, slotDefinition);
            _state = BpState.DragInside;
        }

        private void CreateGhost(ItemDefinition item, BackpackSlotDefinition slotDefinition)
        {
            Canvas host = HostCanvas;
            if (host == null) return;

            GameObject go = new GameObject("DraggedTool");
            go.transform.SetParent(host.transform, false);
            _ghost = go.AddComponent<RectTransform>();
            _ghost.sizeDelta = slotDefinition.slotSize;
            _ghostImage = go.AddComponent<Image>();
            _ghostImage.sprite = item.icon;
            _ghostImage.raycastTarget = false; // o ghost nunca intercepta o raycast do drop
            UpdateGhostPosition();
        }

        private void UpdateGhostPosition()
        {
            if (_ghost == null) return;
            Canvas host = HostCanvas;
            if (host == null) return;

            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)host.transform, Input.mousePosition, host.worldCamera, out Vector3 world);
            _ghost.position = world;
        }

        /// <summary>Cursor além do topo do painel + exitThreshold do slot arrastado.</summary>
        private bool IsCursorBeyondExitThreshold()
        {
            if (panel == null || _dragSlot == null) return false;
            Canvas host = HostCanvas;
            if (host == null) return false;

            Vector3[] corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            float topY = corners[1].y; // topo do painel (world space do canvas)

            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)host.transform, Input.mousePosition, host.worldCamera, out Vector3 world);
            return world.y > topY + _dragSlot.exitThreshold;
        }

        /// <summary>Ferramenta saiu da mochila: modal desce animado; o arraste continua.</summary>
        private void ExitBackpackWithDrag()
        {
            _state = BpState.DragOutside;
            if (_animRoutine != null) StopCoroutine(_animRoutine);
            _animRoutine = StartCoroutine(MovePanel(closedY, RemoveModalReason)); // ToolDrag permanece
        }

        /// <summary>Botão solto: drop sobre ToolHotspot ou cancelamento (dentro do modal).</summary>
        private void EndDrag()
        {
            if (_state == BpState.DragInside)
            {
                // Soltou dentro da mochila: item volta ao slot, modal permanece aberto.
                CancelDrag();
                return;
            }

            ToolHotspot target = FindToolHotspotUnderMouse();
            if (target != null)
            {
                // Conta como interação em qualquer desfecho (sucesso, ferramenta
                // incompatível ou condição bloqueada). Política generosa: a
                // ferramenta volta para a Backpack em CleanupDrag.
                target.AttemptUseFromDrop();
            }
            CleanupDrag();
        }

        /// <summary>Cancela o arraste e devolve a ferramenta ao slot.</summary>
        private void CancelDrag()
        {
            bool wasInside = _state == BpState.DragInside;
            CleanupDrag();
            if (wasInside) _state = BpState.Open; // modal segue aberto
            // (fora do modal: o painel já saiu/saindo — estado final é Closed)
        }

        /// <summary>Remove o ghost, devolve a seleção e libera o motivo ToolDrag.</summary>
        private void CleanupDrag()
        {
            if (_ghost != null)
            {
                Destroy(_ghost.gameObject);
                _ghost = null;
                _ghostImage = null;
            }
            _dragSlot = null;
            if (Session != null) Session.ClearSelectedTool();
            RemoveToolDragReason();
            _state = BpState.Closed;
        }

        /// <summary>Raycast manual: ToolHotspot apresentado sob o cursor no momento do drop.</summary>
        private ToolHotspot FindToolHotspotUnderMouse()
        {
            if (EventSystem.current == null) return null;

            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (RaycastResult result in results)
            {
                ToolHotspot hotspot = result.gameObject != null
                    ? result.gameObject.GetComponentInParent<ToolHotspot>()
                    : null;
                if (hotspot != null && hotspot.IsPresented)
                    return hotspot;
            }
            return null;
        }

        // ---------------- Slots ----------------

        private void BuildSlots()
        {
            if (slotsRoot == null || slotUIPrefab == null)
            {
                Debug.LogWarning("[Backpack] Referências ausentes (slotsRoot/slotUIPrefab); slots não construídos.", this);
                return;
            }

            for (int i = 0; i < slotDefinitions.Length; i++)
            {
                BackpackSlotDefinition def = slotDefinitions[i];
                BackpackSlotUI ui = Instantiate(slotUIPrefab, slotsRoot);

                RectTransform rt = (RectTransform)ui.transform;
                float width = def != null ? def.slotSize.x : 90f;
                float x = (i - (slotDefinitions.Length - 1) * 0.5f) * (width + slotSpacing);
                rt.anchoredPosition = new Vector2(x, 0f);

                ui.Setup(def);
                ui.PointerPressed += OnSlotPointerPressed;
                _slotUIs.Add(ui);
            }
        }

        private void OnSlotPointerPressed(BackpackSlotUI slotUI)
        {
            BeginDragFromSlot(slotUI.Definition);
        }

        private void RefreshSlots()
        {
            if (Session == null) return;

            bool overLimit = false;
            foreach (BackpackSlotUI ui in _slotUIs)
            {
                BackpackSlotDefinition def = ui.Definition;
                if (def == null || def.acceptedItem == null) continue;

                int quantity = Session.GetQuantity(def.acceptedItem.id);
                bool found = quantity > 0 || Session.WasCollected(def.acceptedItem.id);
                if (quantity > def.maxQuantity) overLimit = true;

                ui.Refresh(def.acceptedItem, Mathf.Min(quantity, def.maxQuantity), found);
            }

            if (overLimit)
                Debug.LogWarning("[Backpack] Item acima do maxQuantity do slot (exibição limitada). Capacidade rígida pertence ao sistema de coleta (futuro).");
        }

        // ---------------- Sessão e bloqueios ----------------

        private void EnsureSessionSubscription()
        {
            if (_sessionSubscribed || Session == null) return;
            Session.SessionStateChanged += RefreshSlots;
            _sessionSubscribed = true;
            RefreshSlots();
        }

        private void AddModalReason()
        {
            if (Blocker != null && !_modalAdded)
            {
                Blocker.AddReason(InputBlockReason.Modal);
                _modalAdded = true;
            }
        }

        private void RemoveModalReason()
        {
            if (_modalAdded)
            {
                Blocker?.RemoveReason(InputBlockReason.Modal);
                _modalAdded = false;
            }
        }

        private void AddToolDragReason()
        {
            if (Blocker != null && !_toolDragAdded)
            {
                Blocker.AddReason(InputBlockReason.ToolDrag);
                _toolDragAdded = true;
            }
        }

        private void RemoveToolDragReason()
        {
            if (_toolDragAdded)
            {
                Blocker?.RemoveReason(InputBlockReason.ToolDrag);
                _toolDragAdded = false;
            }
        }
    }
}
