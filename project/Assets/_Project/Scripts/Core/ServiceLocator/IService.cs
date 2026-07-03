namespace Whispers.Core.ServiceLocator
{
    /// <summary>
    /// Interface base para qualquer serviço que possa ser registrado no Service Locator.
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// Chamado quando o serviço é registrado ou quando o jogo é iniciado.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Chamado para limpeza de memória e cancelamento de eventos ao encerrar.
        /// </summary>
        void Dispose();
    }
}