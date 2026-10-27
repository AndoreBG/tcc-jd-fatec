using UnityEngine;

namespace Whispers
{
    public enum PredatorHingeSide
    {
        Left = 0,
        Right = 1
    }

    /// <summary>
    /// Gira só a folha no Y. O pivô vai para a borda escolhida e a pose fechada
    /// não salta: essa borda fica parada enquanto o outro lado abre.
    /// </summary>
    public static class PredatorDoorYaw
    {
        public struct Binding
        {
            public Vector2 pivot;
            public Vector2 anchoredPosition;
            public Vector3 localScale;
            public Vector2 size;
            public bool valid;

            public static Binding Capture(RectTransform door)
            {
                if (door == null) return default;
                return new Binding
                {
                    pivot = door.pivot,
                    anchoredPosition = door.anchoredPosition,
                    localScale = door.localScale,
                    size = door.sizeDelta,
                    valid = true
                };
            }
        }

        public static Vector2 AnchoredForPivot(Binding rest, float pivotX)
        {
            float delta = pivotX - rest.pivot.x;
            return rest.anchoredPosition + new Vector2(delta * rest.size.x * rest.localScale.x, 0f);
        }

        public static void PlacePivot(RectTransform door, Binding rest, PredatorHingeSide side)
        {
            if (door == null || !rest.valid) return;
            float pivotX = side == PredatorHingeSide.Right ? 1f : 0f;
            door.pivot = new Vector2(pivotX, rest.pivot.y);
            door.anchoredPosition = AnchoredForPivot(rest, pivotX);
            door.localScale = rest.localScale;
            door.localRotation = Quaternion.identity;
        }

        public static void Apply(RectTransform door, float closedPercent, float maxAngle, float openSign)
        {
            if (door == null) return;
            float open = 1f - Mathf.Clamp(closedPercent, 0f, 100f) / 100f;
            float sign = openSign < 0f ? -1f : 1f;
            door.localRotation = Quaternion.Euler(0f, open * Mathf.Max(0f, maxAngle) * sign, 0f);
        }

        public static void Restore(RectTransform door, Binding rest)
        {
            if (door == null || !rest.valid) return;
            door.pivot = rest.pivot;
            door.anchoredPosition = rest.anchoredPosition;
            door.localScale = rest.localScale;
            door.localRotation = Quaternion.identity;
        }
    }
}
