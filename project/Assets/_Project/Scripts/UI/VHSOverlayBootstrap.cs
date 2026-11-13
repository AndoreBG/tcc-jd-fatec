using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    // Sobreposição leve, sem amostrar a imagem ou interceptar entrada.
    public static class VHSOverlayBootstrap
    {
        private static GameObject instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (instance != null) return;
            Material material = Resources.Load<Material>("VHSOverlay");
            if (material == null || material.shader == null || !material.shader.isSupported)
            {
                Debug.LogWarning("[VHS] Material ou shader indisponível; efeito desativado.");
                return;
            }
            instance = new GameObject("VHS Overlay", typeof(Canvas));
            Object.DontDestroyOnLoad(instance);
            var canvas = instance.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            var imageObject = new GameObject("VHS", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(instance.transform, false);
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = imageObject.GetComponent<Image>();
            image.material = material;
            image.raycastTarget = false;
        }
    }
}
