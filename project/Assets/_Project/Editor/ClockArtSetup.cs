#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Whispers.EditorTools
{
    /// <summary>
    /// Arte do relógio de parede (plano §7.2).
    ///
    /// Os sprites (`daySprite` e `nightHourSprites`) já vêm preenchidos nos assets de cena.
    /// O que este menu resolve é a `Image` que recebe o sprite: cria um filho `Clock` em cada
    /// `ClockHotspot`, esticado ao retângulo do hotspot.
    ///
    /// A regra de posicionamento é: o hotspot é quem manda. Ao arrastar/redimensionar o hotspot
    /// sobre o relógio da parede, a arte acompanha. Nada é posicionado às cegas.
    /// </summary>
    public static class ClockArtSetup
    {
        private const string ChildName = "Clock";
        private const string LegacyPresenterName = "ClockCursorPresenter";

        [MenuItem("Whispers/UI/Criar arte do relógio", priority = 53)]
        public static void CreateClockArt()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Cena inválida", "Abra a cena 'day' ou 'night' e rode de novo.", "OK");
                return;
            }

            ClockHotspot[] hotspots = Object.FindObjectsByType<ClockHotspot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (hotspots.Length == 0)
            {
                EditorUtility.DisplayDialog("Sem relógio",
                    "Não encontrei nenhum ClockHotspot na cena '" + scene.name + "'.", "OK");
                return;
            }

            int created = 0;
            int reused = 0;
            int alreadySet = 0;
            int removedLegacy = RemoveLegacyPresenters();

            foreach (ClockHotspot hotspot in hotspots)
            {
                SerializedObject serialized = new SerializedObject(hotspot);
                SerializedProperty imageProperty = serialized.FindProperty("clockImage");
                if (imageProperty == null) continue;

                if (imageProperty.objectReferenceValue is Image existing)
                {
                    if (existing.raycastTarget)
                    {
                        existing.raycastTarget = false;
                        EditorUtility.SetDirty(existing);
                    }
                    alreadySet++;
                    continue;
                }

                bool wasCreated;
                Image image = FindOrCreateChild(hotspot, serialized, out wasCreated);
                if (image == null) continue;

                if (wasCreated) created++; else reused++;

                serialized.FindProperty("clockImage").objectReferenceValue = image;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(hotspot);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            StringBuilder report = new StringBuilder();
            report.AppendLine("Cena: " + scene.name);
            report.AppendLine("Hotspots: " + hotspots.Length);
            report.AppendLine("• Image criada: " + created);
            report.AppendLine("• Image reaproveitada: " + reused);
            report.AppendLine("• Já configurados: " + alreadySet);
            report.AppendLine("• Objetos ClockCursorPresenter removidos: " + removedLegacy);
            report.AppendLine();
            report.AppendLine("Ajuste fino: o filho 'Clock' é esticado ao retângulo do hotspot.");
            report.AppendLine("Mova/redimensione o hotspot sobre o relógio da parede em cada ViewNode.");
            report.AppendLine("Depois de conferir nas duas cenas, o script ClockCursorPresenter.cs pode ser apagado.");
            EditorUtility.DisplayDialog("Arte do relógio", report.ToString(), "OK");
            Debug.Log("[ClockArtSetup]\n" + report);
        }

        private static Image FindOrCreateChild(ClockHotspot hotspot, SerializedObject serialized, out bool wasCreated)
        {
            wasCreated = false;

            Transform child = hotspot.transform.Find(ChildName);
            if (child != null)
            {
                Image found = child.GetComponent<Image>();
                if (found != null) return found;
            }

            GameObject childObject = new GameObject(ChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(childObject, "Criar arte do relógio");
            childObject.transform.SetParent(hotspot.transform, false);

            RectTransform rect = childObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;

            Image image = childObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = Color.white;
            image.sprite = FirstSprite(serialized);

            wasCreated = true;
            return image;
        }

        private static Sprite FirstSprite(SerializedObject serialized)
        {
            SerializedProperty day = serialized.FindProperty("daySprite");
            if (day != null && day.objectReferenceValue is Sprite daySprite) return daySprite;

            SerializedProperty night = serialized.FindProperty("nightHourSprites");
            if (night != null && night.arraySize > 0)
            {
                SerializedProperty first = night.GetArrayElementAtIndex(0);
                if (first.objectReferenceValue is Sprite nightSprite) return nightSprite;
            }

            return null;
        }

        private static int RemoveLegacyPresenters()
        {
            GameObject[] all = Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int removed = 0;
            foreach (GameObject candidate in all)
            {
                if (candidate.name != LegacyPresenterName) continue;
                Undo.DestroyObjectImmediate(candidate);
                removed++;
            }
            return removed;
        }
    }
}
#endif
