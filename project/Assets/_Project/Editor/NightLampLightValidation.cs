#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Whispers;

namespace Whispers.EditorTests
{
    public static class NightLampLightValidation
    {
        [MenuItem("Whispers/Exhauster/Validar luz do lampião na Noite")]
        public static void Run()
        {
            const string path = "Assets/_Project/Scenes/night.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = false;
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    opened = true;
                }
                LanternEffect effect = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (LanternEffect candidate in root.GetComponentsInChildren<LanternEffect>(true))
                    {
                        if (effect != null) throw new InvalidOperationException("Mais de um LanternEffect na cena night.");
                        effect = candidate;
                    }
                if (effect == null) throw new InvalidOperationException("LanternEffect ausente na cena night.");
                SerializedObject data = new SerializedObject(effect);
                StairLampController lamp = data.FindProperty("stairLamp").objectReferenceValue as StairLampController;
                Material mat = data.FindProperty("darknessMaterial").objectReferenceValue as Material;
                if (!data.FindProperty("darknessEnabled").boolValue || data.FindProperty("targetCanvas").objectReferenceValue == null)
                    throw new InvalidOperationException("Escuridão ou Canvas da lanterna não configurados.");
                if (lamp == null || lamp.LightSource == null)
                    throw new InvalidOperationException("Fonte visual do lampião não configurada.");
                if (mat == null || mat.shader == null || mat.shader.name != "Whispers/UI/LanternDarkness" ||
                    !mat.HasProperty("_LampDarknessCenter") || !mat.HasProperty("_LampDarknessSize") ||
                    !mat.HasProperty("_LampLightIntensity"))
                    throw new InvalidOperationException("Material ou propriedades do shader da lanterna ausentes.");
                Debug.Log("[Luz do lampião] Referências estáticas OK. Valide projeção, brilho e interação em Play Mode e build.");
            }
            finally
            {
                if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
