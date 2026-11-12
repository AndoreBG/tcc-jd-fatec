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
        private readonly Dictionary<object, CursorClaim> _externalClaims =
            new Dictionary<object, CursorClaim>();
        private readonly HashSet<object> _hiddenRequests = new HashSet<object>();

        private GlobalHotspotSettings _settings;
        private GamePeriod _period = GamePeriod.Day;
        private int _sequence;

        /// <summary>Configura os fallbacks globais da cena atual e reaplica o cursor.</summary>
        public void Initialize(GlobalHotspotSettings settings)
        {
            _settings = settings;
            RefreshCursor();
        }

        /// <summary>Define o período atual para seleção automática do cursor padrão da noite.</summary>
        public void SetPeriod(GamePeriod period)
        {
            _period = period;
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

        /// <summary>
        /// Pedido de cursor que não vem de um hotspot. A última solicitação, de
        /// qualquer origem, continua vencendo.
        /// </summary>
        public void RequestCursor(object owner, Texture2D texture, Vector2 hotspot)
        {
            if (owner == null || texture == null)
            {
                ReleaseCursor(owner);
                return;
            }

            CursorClaim claim;
            if (!_externalClaims.TryGetValue(owner, out claim))
            {
                claim = new CursorClaim();
                _externalClaims.Add(owner, claim);
            }

            claim.texture = texture;
            claim.hotspot = hotspot;
            claim.sequence = ++_sequence;
            RefreshCursor();
        }

        /// <summary>Remove somente a solicitação do dono informado.</summary>
        public void ReleaseCursor(object owner)
        {
            if (owner == null) return;
            if (_externalClaims.Remove(owner)) RefreshCursor();
        }

        /// <summary>
        /// Pede o cursor do sistema oculto. Usado quando outro elemento substitui o
        /// ponteiro (ex.: relógio no cursor). Vários donos podem pedir ao mesmo tempo;
        /// o cursor só volta quando o último dono liberar.
        /// </summary>
        public void RequestHiddenCursor(object owner)
        {
            if (owner == null) return;
            if (_hiddenRequests.Add(owner)) RefreshCursor();
        }

        /// <summary>Libera o pedido de cursor oculto do dono informado.</summary>
        public void ReleaseHiddenCursor(object owner)
        {
            if (owner == null) return;
            if (_hiddenRequests.Remove(owner)) RefreshCursor();
        }

        private void RefreshCursor()
        {
            PruneHiddenRequests();
            if (_hiddenRequests.Count > 0)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                return;
            }

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

            object staleExternal = null;
            bool hasStaleExternal = false;
            foreach (KeyValuePair<object, CursorClaim> pair in _externalClaims)
            {
                UnityEngine.Object unityOwner = pair.Key as UnityEngine.Object;
                if (unityOwner == null && pair.Key is UnityEngine.Object)
                {
                    staleExternal = pair.Key;
                    hasStaleExternal = true;
                    continue;
                }
                if (selected == null || pair.Value.sequence > selected.sequence)
                    selected = pair.Value;
            }
            if (hasStaleExternal) _externalClaims.Remove(staleExternal);

            if (selected != null && selected.texture != null)
            {
                Cursor.SetCursor(selected.texture, selected.hotspot, CursorMode.Auto);
                return;
            }

            Texture2D fallback;
            Vector2 fallbackHotspot;

            if (_period == GamePeriod.Night && _settings != null && _settings.defaultNightCursor != null)
            {
                fallback = _settings.defaultNightCursor;
                fallbackHotspot = _settings.defaultNightCursorHotspot;
            }
            else
            {
                fallback = _settings != null ? _settings.defaultCursor : null;
                fallbackHotspot = _settings != null ? _settings.defaultCursorHotspot : Vector2.zero;
            }

            Cursor.SetCursor(fallback, fallbackHotspot, CursorMode.Auto);
        }

        /// <summary>Descarta pedidos de donos que já foram destruídos.</summary>
        private void PruneHiddenRequests()
        {
            if (_hiddenRequests.Count == 0) return;

            List<object> stale = null;
            foreach (object owner in _hiddenRequests)
            {
                UnityEngine.Object unityOwner = owner as UnityEngine.Object;
                if (unityOwner == null && owner is UnityEngine.Object)
                {
                    if (stale == null) stale = new List<object>();
                    stale.Add(owner);
                }
            }

            if (stale == null) return;
            foreach (object owner in stale) _hiddenRequests.Remove(owner);
        }

        private void OnDisable()
        {
            _claims.Clear();
            _externalClaims.Clear();
            _hiddenRequests.Clear();
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
