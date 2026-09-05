using System;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Tipos de consequência que uma interação pode executar.
    /// O resultado DESCREVE a consequência; quem executa é o sistema responsável
    /// (inventário pelo GameSessionManager, flags pelo SceneRuntimeState,
    /// navegação pelo NavigationManager, modal pelo ModalUIController).
    /// </summary>
    public enum InteractionResultType
    {
        /// <summary>Entrega amount unidades do item itemId ao inventário de trabalho.</summary>
        AddItem,
        /// <summary>Remove amount unidades do item itemId do inventário de trabalho.</summary>
        RemoveItem,
        /// <summary>Registra itemId como coletado (impede reaparecimento no ciclo).</summary>
        MarkCollected,
        /// <summary>Liga uma flag temporária da cena (flagId).</summary>
        SetRuntimeFlag,
        /// <summary>Desliga uma flag temporária da cena (flagId).</summary>
        ClearRuntimeFlag,
        /// <summary>Registra um fato persistente na cópia de trabalho (factId).</summary>
        SetPersistentFact,
        /// <summary>Solicita navegação ao NavigationManager (destinationId).</summary>
        RequestNavigate,
        /// <summary>Abre o DocumentPanel com o documento informado.</summary>
        OpenDocument,
        /// <summary>
        /// Solicita o encerramento do período ao fluxo global.
        /// A execução efetiva (troca Dia ⇄ Noite) chega com o VS3 (cartão 16);
        /// até lá, o manager registra um warning de não implementado.
        /// </summary>
        RequestPeriodEnd
    }

    /// <summary>
    /// Uma consequência executável de uma interação. Os campos são compartilhados:
    /// cada tipo lê apenas os que lhe interessam (mantém o Inspector simples).
    /// </summary>
    [Serializable]
    public class InteractionResult
    {
        [Tooltip("Tipo de consequência executada pelo InteractionManager.")]
        public InteractionResultType type;

        [Tooltip("Item usado por AddItem, RemoveItem e MarkCollected.")]
        public string itemId;

        [Tooltip("Quantidade usada por AddItem e RemoveItem.")]
        public int amount = 1;

        [Tooltip("Flag usada por SetRuntimeFlag e ClearRuntimeFlag.")]
        public string flagId;

        [Tooltip("Fato persistente usado por SetPersistentFact.")]
        public string factId;

        [Tooltip("ViewNode de destino usado por RequestNavigate.")]
        public string destinationId;

        [Tooltip("Documento aberto por OpenDocument.")]
        public DocumentData document;
    }
}
