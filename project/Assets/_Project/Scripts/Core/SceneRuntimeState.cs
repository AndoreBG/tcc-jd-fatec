using System;
using System.Collections.Generic;

namespace Whispers
{
    /// <summary>
    /// Estado temporário e local da cena: flags e alterações que não precisam
    /// sobreviver à troca de cena. É o único dono de decisões locais.
    /// Dispara <see cref="Changed"/> quando algo muda, para a apresentação atualizar.
    /// </summary>
    public class SceneRuntimeState
    {
        private readonly HashSet<string> _flags = new HashSet<string>();

        public event Action Changed;

        public bool GetFlag(string id) => _flags.Contains(id);

        public void SetFlag(string id, bool on)
        {
            if (on)
            {
                if (_flags.Add(id)) Changed?.Invoke();
            }
            else
            {
                if (_flags.Remove(id)) Changed?.Invoke();
            }
        }
    }
}
