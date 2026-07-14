namespace Whispers.Core.ServiceLocator
{
    /// <summary>
    /// Contrato base para serviços controlados pelo ServiceLocator.
    /// O ciclo de vida do serviço pertence ao Composition Root que o registra.
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// Informa se o serviço concluiu sua inicialização.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Inicializa recursos, inscrições e dependências do serviço.
        /// Deve ser seguro chamar este método mais de uma vez.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Libera recursos e remove inscrições.
        /// Deve ser seguro chamar este método mais de uma vez.
        /// </summary>
        void Dispose();
    }
}