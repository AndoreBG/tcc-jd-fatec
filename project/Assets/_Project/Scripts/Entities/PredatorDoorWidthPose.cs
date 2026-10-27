using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Abertura da porta como perda de largura. O eixo autorado continua fixo:
    /// a borda que já encosta nele não se move, e o lado oposto é que abre.
    /// </summary>
    public static class PredatorDoorWidthPose
    {
        public struct Rest
        {
            public Vector2 anchoredPosition;
            public Vector3 localScale;
            public bool valid;

            public static Rest Capture(RectTransform target)
            {
                if (target == null) return default;
                return new Rest
                {
                    anchoredPosition = target.anchoredPosition,
                    localScale = target.localScale,
                    valid = true
                };
            }
        }

        /// <summary>
        /// 100 mantém a pose de repouso. 0 some na largura, sempre em direção ao
        /// pai. O pai é o eixo; a borda que já está nele permanece no lugar.
        /// </summary>
        public static void Apply(RectTransform target, Rest rest, float closedPercent)
        {
            if (target == null || !rest.valid) return;
            Evaluate(rest.anchoredPosition, rest.localScale, closedPercent, out Vector2 anchored, out Vector3 scale);
            target.localRotation = Quaternion.identity;
            target.localScale = scale;
            target.anchoredPosition = anchored;
        }

        public static void Evaluate(Vector2 restAnchored, Vector3 restScale, float closedPercent,
            out Vector2 anchored, out Vector3 scale)
        {
            float width = Mathf.Clamp(closedPercent, 0f, 100f) / 100f;
            scale = new Vector3(restScale.x * width, restScale.y, restScale.z);
            anchored = new Vector2(restAnchored.x * width, restAnchored.y);
        }
    }
}
