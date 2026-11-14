using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>Abre e fecha os créditos autorados no menu principal.</summary>
    [DisallowMultipleComponent]
    public sealed class CreditsPanelController : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject overwriteModal;
        [SerializeField] private MainMenuIntroSequence intro;
        [SerializeField] private LevelManager levelManager;

        private bool IsBlocked => (optionsPanel != null && optionsPanel.activeInHierarchy) ||
                                  (overwriteModal != null && overwriteModal.activeInHierarchy) ||
                                  (intro != null && intro.IsPlaying) ||
                                  (levelManager != null && levelManager.IsLoading);

        private void Awake() { if (panel != null) panel.SetActive(false); }
        private void OnEnable()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }
        private void OnDisable()
        {
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (panel != null) panel.SetActive(false);
        }
        private void Update()
        {
            if (openButton != null) openButton.interactable = !IsBlocked && (panel == null || !panel.activeSelf);
            if (panel != null && panel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
        }
        public void Open()
        {
            if (panel == null || IsBlocked) return;
            panel.SetActive(true);
            if (EventSystem.current != null && closeButton != null)
                EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            StartCoroutine(ScrollToTop());
        }
        public void Close()
        {
            if (panel == null || !panel.activeSelf) return;
            panel.SetActive(false);
            if (EventSystem.current != null && openButton != null && !IsBlocked)
                EventSystem.current.SetSelectedGameObject(openButton.gameObject);
        }
        private IEnumerator ScrollToTop()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (scroll != null && panel != null && panel.activeSelf) scroll.verticalNormalizedPosition = 1f;
        }
    }
}
