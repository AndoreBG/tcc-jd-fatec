using System.Collections;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Classe base para efeitos visuais de transição entre cenas.
    /// Opera em tempo não escalado para garantir previsibilidade e evitar travamentos com Time.timeScale = 0.
    /// </summary>
    public abstract class SceneTransition : MonoBehaviour
    {
        public abstract IEnumerator AnimateTransitionIn();
        public abstract IEnumerator AnimateTransitionOut();
    }
}
