using System.Collections.Generic;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Autoridade local do cursor de gameplay. Mantém o cursor padrão global e
    /// arbitra pedidos temporários de HotspotBase, evitando que a saída de um
    /// hotspot restaure o cursor enquanto outro ainda está sob o ponteiro.
    /// </summary>
    public sealed class GameplayCursorController : MonoBehaviour
    {
        private sealed class CursorClaim
        {
            public Texture2D texture;
            public Vector2 hotspot;
            public int sequence;
        }

        private readonly Dictionary<HotspotBase, CursorClaim> _claims =
            new Dictionary<HotspotBase, CursorClaim>();

        private GlobalHotspotSettings _settings;
        private int _sequence;

        /// <summary>Configura os fallbacks globais da cena atual e reaplica o cursor.</summary>
        public void Initialize(GlobalHotspotSettings settings)
        {
            _settings = settings;
            RefreshCursor();
        }

        /// <summary>
        /// Solicita uma skin temporária para um hotspot. A última solicitação ativa
        /// tem prioridade, o que cobre sobreposição visual de hotspots.
        /// </summary>
        public void RequestHotspotCursor(HotspotBase owner, Texture2D texture, Vector2 hotspot)
        {
            if (owner == null || texture == null)
            {
                ReleaseHotspotCursor(owner);
                return;
            }

            CursorClaim claim;
            if (!_claims.TryGetValue(owner, out claim))
            {
                claim = new CursorClaim();
                _claims.Add(owner, claim);
            }

            claim.texture = texture;
            claim.hotspot = hotspot;
            claim.sequence = ++_sequence;
            RefreshCursor();
        }

        /// <summary>Aplica o cursor bloqueado global, sem override por hotspot.</summary>
        public void RequestBlockedCursor(HotspotBase owner)
        {
            if (_settings == null || _settings.defaultBlockedCursor == null)
            {
                ReleaseHotspotCursor(owner);
                return;
            }

            RequestHotspotCursor(owner, _settings.defaultBlockedCursor, _settings.defaultBlockedCursorHotspot);
        }

        /// <summary>Remove somente a solicitação do hotspot informado.</summary>
        public void ReleaseHotspotCursor(HotspotBase owner)
        {
            if (owner == null) return;
            if (_claims.Remove(owner)) RefreshCursor();
        }

        private void RefreshCursor()
        {
            CursorClaim selected = null;
            HotspotBase staleOwner = null;
            bool hasStaleOwner = false;
            foreach (KeyValuePair<HotspotBase, CursorClaim> pair in _claims)
            {
                // Unity torna referências destruídas equivalentes a null. A limpeza
                // principal ocorre no OnDisable do hotspot; isto cobre casos extremos.
                if (pair.Key == null)
                {
                    staleOwner = pair.Key;
                    hasStaleOwner = true;
                    continue;
                }
                if (selected == null || pair.Value.sequence > selected.sequence)
                    selected = pair.Value;
            }
            if (hasStaleOwner) _claims.Remove(staleOwner);

            if (selected != null && selected.texture != null)
            {
                Cursor.SetCursor(selected.texture, selected.hotspot, CursorMode.Auto);
                return;
            }

            Texture2D fallback = _settings != null ? _settings.defaultCursor : null;
            Vector2 fallbackHotspot = _settings != null ? _settings.defaultCursorHotspot : Vector2.zero;
            Cursor.SetCursor(fallback, fallbackHotspot, CursorMode.Auto);
        }

        private void OnDisable()
        {
            _claims.Clear();
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
