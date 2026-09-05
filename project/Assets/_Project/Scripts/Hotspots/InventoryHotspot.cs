using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Hotspot fixo de UI que ALTERNA o inventário (ex.: ícone da mochila no canto
    /// da tela): abre quando fechado e fecha quando aberto, por hover. Vive no
    /// Canvas de gameplay, FORA dos ViewNodes — por isso marque "Always Presented"
    /// e "Active During Modal" no componente (o Start avisa se faltar).
    /// A exceção ao bloqueio Modal é a UI autorizada da seção 5.3/16 da arquitetura:
    /// enquanto o inventário está aberto, os hotspots do CENÁRIO ficam bloqueados,
    /// mas este hotspot continua ativo. Para fechar por hover é preciso sair do
    /// ícone e entrar de novo (reentrada). Solicita ao ModalUIController; não
    /// executa nada. A tecla I alterna da mesma forma.
    /// IMPORTANTE (layout): este objeto deve ser o ÚLTIMO filho do Canvas (renderiza
    /// por cima de painéis e do overlay de escurecimento) e não pode ser coberto
    /// pela área do painel do inventário — senão o raycast nunca o alcança.
    /// Modo recomendado: Hover Immediate + Repeatable.
    /// </summary>
    public class InventoryHotspot : HotspotBase
    {
        private void Start()
        {
            // Diagnóstico das causas mais comuns de "toggle por hover não funciona".
            if (!AlwaysPresentedFlag)
                Debug.LogWarning($"[InventoryHotspot] '{name}' sem 'Always Presented': nunca ficará apresentado. Marque a flag no Inspector.", this);
            if (!ActiveDuringModalFlag)
                Debug.LogWarning($"[InventoryHotspot] '{name}' sem 'Active During Modal': o hover não conseguirá FECHAR o inventário (bloqueio Modal). Marque a flag no Inspector.", this);
            if (ActivationModeFlag != HotspotActivationMode.HoverImmediate)
                Debug.LogWarning($"[InventoryHotspot] '{name}': o modo recomendado para a mochila é Hover Immediate.", this);
        }

        protected override bool OnActivated()
        {
            ModalUIController modal = Scene != null ? Scene.ModalUI : null;
            if (modal == null)
            {
                Debug.LogWarning("[InventoryHotspot] ModalUIController indisponível no cenário.", this);
                return false;
            }

            modal.ToggleInventory();
            return true;
        }
    }
}
