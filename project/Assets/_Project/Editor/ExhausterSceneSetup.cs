#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Whispers.EditorTools
{
    /// <summary>
    /// Autoria automatizada do Exhauster na cena `night`, seguindo
    /// `Whispers_Of_Unknown_Entidades_Exhauster_Autoria_Cena.md`.
    ///
    /// O script NÃO cria arte: sprites, clips e jumpscare continuam como pendência e
    /// aparecem no log final e no item de menu "Verificar pendências do lampião".
    /// Nenhum placeholder é criado (regra do projeto).
    ///
    /// Seguro para rodar mais de uma vez: objetos são localizados por nome e
    /// reaproveitados, referências são regravadas sem duplicar e listas existentes
    /// (`dimTargets`, `relightHotspots`, `conditions`) são preservadas.
    /// Todas as alterações entram no histórico em um único passo de Undo.
    /// </summary>
    public static class ExhausterSceneSetup
    {
        // ---- assets já presentes no projeto ----
        private const string DefinitionPath = "Assets/_Project/SO/Entities/ENT_Exhauster.asset";
        private const string LampBrokenConditionPath = "Assets/_Project/SO/Conditions/LAMP_LampiaoIntacto.asset";
        private const string HasOilConditionPath = "Assets/_Project/SO/Conditions/HASITEM_PossuiOleo.asset";
        private const string CursorPath = "Assets/_Project/Art/Cursor/14_oil_can.png";
        private const string InteractionFolder = "Assets/_Project/SO/Interactions";
        private const string InteractionPath = InteractionFolder + "/INT_ReabastecerLampiao.asset";
        private const string InteractionId = "int_reabastecerlampiao";

        // ---- nomes na cena ----
        private const string SceneName = "night";
        private const string EntityId = "exhauster";
        private const string ManagersName = "Manager_Entities";
        private const string ViewNodeName = "__VN_Sotao";
        private const string ContentName = "Content";
        private const string BackgroundLayerName = "Layer_Background";
        private const string NavLayerName = "Layer_Nav";
        private const string LampLayerName = "Layer_Lamp";
        private const string LampName = "Lamp_Sotao";
        private const string FlameName = "Flame";
        private const string DimName = "DimOverlay_Sotao";
        private const string HotspotName = "Hotspot_ReabastecerLampiao";
        private const string AudioName = "SFX_Lamp";

        private const string UndoLabel = "Exhauster — autoria do lampião";

        // =====================================================================
        // Menu
        // =====================================================================

        [MenuItem("Whispers/Exhauster/Aplicar autoria do lampião na cena night", priority = 20)]
        public static void Apply()
        {
            if (!ValidateEditorState()) return;

            Scene scene = SceneManager.GetActiveScene();
            if (scene.name != SceneName &&
                !EditorUtility.DisplayDialog("Cena diferente de '" + SceneName + "'",
                    "A cena aberta é '" + scene.name + "'. O passo a passo foi escrito para '" + SceneName +
                    "'.\n\nContinuar mesmo assim?", "Continuar", "Cancelar"))
                return;

            // ---- assets obrigatórios ----
            ExhausterDefinition definition = AssetDatabase.LoadAssetAtPath<ExhausterDefinition>(DefinitionPath);
            if (definition == null)
            {
                Fail("ExhausterDefinition não encontrado em " + DefinitionPath + ".");
                return;
            }

            HotspotConditionSO lampBrokenCondition =
                AssetDatabase.LoadAssetAtPath<HotspotConditionSO>(LampBrokenConditionPath);
            HotspotConditionSO hasOilCondition =
                AssetDatabase.LoadAssetAtPath<HotspotConditionSO>(HasOilConditionPath);
            if (lampBrokenCondition == null || hasOilCondition == null)
            {
                Fail("Condições não encontradas. Esperado: " + LampBrokenConditionPath +
                     " e " + HasOilConditionPath + ".");
                return;
            }

            Texture2D cursorSkin = AssetDatabase.LoadAssetAtPath<Texture2D>(CursorPath);
            if (cursorSkin == null)
                Debug.LogWarning("[ExhausterSetup] Cursor não encontrado em " + CursorPath +
                                 "; o hotspot usará o cursor global.");

            // ---- âncoras da cena ----
            EntityDirector director = Object.FindFirstObjectByType<EntityDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                Fail("EntityDirector não encontrado. Abra a cena '" + SceneName + "' e tente novamente.");
                return;
            }

            Transform managers = director.transform;
            while (managers.parent != null && managers.name != ManagersName) managers = managers.parent;
            if (managers.name != ManagersName)
            {
                Fail("Não encontrei '" + ManagersName + "' acima do EntityDirector.");
                return;
            }

            Transform viewNode = FindInScene(scene, ViewNodeName);
            Transform content = viewNode != null ? viewNode.Find(ContentName) : null;
            Transform backgroundLayer = content != null ? content.Find(BackgroundLayerName) : null;
            Transform navLayer = content != null ? content.Find(NavLayerName) : null;
            if (content == null || backgroundLayer == null || navLayer == null)
            {
                Fail("Não encontrei '" + ViewNodeName + "/" + ContentName + "/{" + BackgroundLayerName + "," +
                     NavLayerName + "}'. Confira os nomes da cena.");
                return;
            }

            // ---- Undo: um único passo ----
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoLabel);

            // 1) Controllers no objeto sempre ativo
            ExhausterController exhausterController = managers.GetComponent<ExhausterController>();
            if (exhausterController == null) exhausterController = Undo.AddComponent<ExhausterController>(managers.gameObject);

            StairLampController stairLamp = managers.GetComponent<StairLampController>();
            if (stairLamp == null) stairLamp = Undo.AddComponent<StairLampController>(managers.gameObject);

            new Binder(exhausterController, "ExhausterController").ApplyText("entityId", EntityId);

            // 2) Camada do lampião (criada ANTES, para o lampião nascer no lugar certo)
            Transform lampLayer = content.Find(LampLayerName);
            if (lampLayer == null)
            {
                GameObject layerGo = EnsureUIObject(content, LampLayerName, out RectTransform layerRect);
                Stretch(layerRect);
                lampLayer = layerGo.transform;
            }
            // Ordem de renderização: fundo (com overlay) -> lampião -> navegação
            lampLayer.SetSiblingIndex(Mathf.Max(0, navLayer.GetSiblingIndex()));

            // 3) Visual do lampião
            GameObject lamp = EnsureUIObject(lampLayer, LampName, out RectTransform lampRect);
            lampRect.anchorMin = lampRect.anchorMax = new Vector2(0.5f, 0.5f);
            lampRect.pivot = new Vector2(0.5f, 0.5f);
            lampRect.sizeDelta = new Vector2(200f, 260f);
            lampRect.anchoredPosition = new Vector2(0f, 40f);

            Image lampImage = lamp.GetComponent<Image>();
            if (lampImage == null) lampImage = Undo.AddComponent<Image>(lamp);
            lampImage.raycastTarget = false;
            // Uma Image sem sprite desenha um retângulo branco: só é desligada enquanto
            // não houver arte, para não apagar autoria já feita em execuções anteriores.
            if (lampImage.sprite == null) lampImage.enabled = false;

            GameObject flame = EnsureUIObject(lamp.transform, FlameName, out RectTransform flameRect);
            flameRect.anchorMin = flameRect.anchorMax = new Vector2(0.5f, 0.18f);
            flameRect.pivot = new Vector2(0.5f, 0.5f);
            flameRect.sizeDelta = new Vector2(48f, 72f);
            flameRect.anchoredPosition = Vector2.zero;

            Image flameImage = flame.GetComponent<Image>();
            if (flameImage == null) flameImage = Undo.AddComponent<Image>(flame);
            flameImage.raycastTarget = false;
            if (flameImage.sprite == null)
            {
                flameImage.enabled = false;
                flame.SetActive(false); // aceso/apagado é do StairLampController
            }

            // 4) Escurecimento: último filho da camada de fundo
            GameObject dim = EnsureUIObject(backgroundLayer, DimName, out RectTransform dimRect);
            Stretch(dimRect);
            Image dimImage = dim.GetComponent<Image>();
            if (dimImage == null) dimImage = Undo.AddComponent<Image>(dim);
            dimImage.raycastTarget = false;
            if (dimImage.sprite == null) dimImage.color = new Color(0.02f, 0.02f, 0.03f, 0f);
            dim.transform.SetAsLastSibling();

            // 5) Áudio do lampião
            Transform audioTransform = managers.Find(AudioName);
            if (audioTransform == null)
            {
                GameObject audioGo = new GameObject(AudioName);
                Undo.RegisterCreatedObjectUndo(audioGo, UndoLabel);
                audioGo.transform.SetParent(managers, false);
                audioTransform = audioGo.transform;
            }
            AudioSource lampAudio = audioTransform.GetComponent<AudioSource>();
            if (lampAudio == null) lampAudio = Undo.AddComponent<AudioSource>(audioTransform.gameObject);
            lampAudio.playOnAwake = false;
            lampAudio.loop = true;
            lampAudio.spatialBlend = 0f;

            // 6) Hotspot
            GameObject hotspotGo = EnsureUIObject(navLayer, HotspotName, out RectTransform hotspotRect);
            hotspotRect.anchorMin = hotspotRect.anchorMax = new Vector2(0.5f, 0.5f);
            hotspotRect.pivot = new Vector2(0.5f, 0.5f);
            hotspotRect.sizeDelta = new Vector2(260f, 320f);
            hotspotRect.anchoredPosition = new Vector2(0f, 40f);
            hotspotGo.transform.SetAsLastSibling();

            Image hotspotImage = hotspotGo.GetComponent<Image>();
            if (hotspotImage == null) hotspotImage = Undo.AddComponent<Image>(hotspotGo);
            hotspotImage.raycastTarget = true;
            // Cor de rascunho só enquanto não houver arte de hotspot (0.25 torna a área
            // visível para posicionar sem parecer bug; alpha 0 continua clicável).
            if (hotspotImage.sprite == null) hotspotImage.color = new Color(1f, 1f, 1f, 0.25f);

            CanvasGroup hotspotGroup = hotspotGo.GetComponent<CanvasGroup>();
            if (hotspotGroup == null) hotspotGroup = Undo.AddComponent<CanvasGroup>(hotspotGo);

            InteractionHotspot hotspot = hotspotGo.GetComponent<InteractionHotspot>();
            if (hotspot == null) hotspot = Undo.AddComponent<InteractionHotspot>(hotspotGo);

            // 7) InteractionDefinition sem resultados
            InteractionDefinition interaction = EnsureInteractionDefinition();

            Binder hotspotBinder = new Binder(hotspot, "InteractionHotspot");
            hotspotBinder.SetBool("enabledForGameplay", true);
            hotspotBinder.SetInt("activationMode", (int)HotspotActivationMode.Click);
            hotspotBinder.SetInt("repeatPolicy", (int)HotspotRepeatPolicy.Repeatable);
            hotspotBinder.SetInt("conditionPolicy", (int)HotspotConditionPolicy.All);
            hotspotBinder.SetInt("unavailableMode", (int)HotspotUnavailableMode.Hidden);
            hotspotBinder.SetObject("visualGroup", hotspotGroup);
            hotspotBinder.SetObject("definition", interaction);
            hotspotBinder.SetObject("cursorSkin", cursorSkin);
            hotspotBinder.SetVector2("cursorSkinHotspot", new Vector2(16f, 16f));
            hotspotBinder.EnsureArray("conditions", hasOilCondition);
            hotspotBinder.EnsureArray("conditions", lampBrokenCondition);
            hotspotBinder.Apply();

            // 8) onActivated -> StairLampController.RelightFromInteraction()
            if (!HasRelightListener(hotspot, stairLamp))
            {
                Undo.RecordObject(hotspot, UndoLabel);
                UnityEventTools.AddVoidPersistentListener(
                    hotspot.onActivated, new UnityAction(stairLamp.RelightFromInteraction));
                EditorUtility.SetDirty(hotspot);
            }

            // 9) Referências de apresentação do lampião
            Binder lampBinder = new Binder(stairLamp, "StairLampController");
            lampBinder.SetText("debugName", "Lampião do sótão");
            lampBinder.SetText("requiredItemId", definition.requiredRelightItemId);
            lampBinder.SetObject("flameRoot", flame);
            lampBinder.SetObject("lampImage", lampImage);
            lampBinder.SetObject("lampAudioSource", lampAudio);
            lampBinder.EnsureArray("dimTargets", dimImage);
            lampBinder.EnsureArray("relightHotspots", hotspot);
            lampBinder.Apply();

            // 10) EntityDirector -> exhausterController
            Binder directorBinder = new Binder(director, "EntityDirector");
            directorBinder.SetObject("exhausterController", exhausterController);
            directorBinder.Apply();

            // 11) Finalização
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = hotspotGo;
            EditorGUIUtility.PingObject(hotspotGo);

            Debug.Log("[ExhausterSetup] Autoria aplicada em '" + scene.name + "'.\n" + BuildPendingSummary(), director);
        }

        [MenuItem("Whispers/Exhauster/Verificar pendências do lampião", priority = 21)]
        public static void Verify()
        {
            Debug.Log("[ExhausterSetup] Pendências de autoria do lampião:\n" + BuildPendingSummary());
        }

        // =====================================================================
        // Assets
        // =====================================================================

        private static InteractionDefinition EnsureInteractionDefinition()
        {
            InteractionDefinition existing = AssetDatabase.LoadAssetAtPath<InteractionDefinition>(InteractionPath);
            if (existing != null)
            {
                if (string.IsNullOrWhiteSpace(existing.id)) existing.id = InteractionId;
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(InteractionFolder))
                AssetDatabase.CreateFolder("Assets/_Project/SO", "Interactions");

            InteractionDefinition created = ScriptableObject.CreateInstance<InteractionDefinition>();
            created.id = InteractionId;
            created.debugName = "Reabastecer lampião (zero resultados)";
            // `results` fica vazio de propósito: reabastecer não altera inventário,
            // flags, fatos nem save. A consequência é o onActivated do hotspot.
            AssetDatabase.CreateAsset(created, InteractionPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ExhausterSetup] InteractionDefinition criada em " + InteractionPath + " (zero resultados).");
            return created;
        }

        // =====================================================================
        // Utilidades
        // =====================================================================

        private static bool ValidateEditorState()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Fail("Saia do Play Mode antes de aplicar a autoria.");
                return false;
            }
            return true;
        }

        private static void Fail(string message)
        {
            Debug.LogError("[ExhausterSetup] " + message);
            EditorUtility.DisplayDialog("Exhauster — autoria", message, "OK");
        }

        private static Transform FindInScene(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                    if (candidate.name == objectName) return candidate;
            return null;
        }

        /// <summary>Garante um GameObject de UI (com RectTransform) sob o pai informado.</summary>
        private static GameObject EnsureUIObject(Transform parent, string objectName, out RectTransform rect)
        {
            Transform existing = parent.Find(objectName);
            if (existing == null)
            {
                GameObject created = new GameObject(objectName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(created, UndoLabel);
                created.transform.SetParent(parent, false);
                existing = created.transform;
            }

            rect = existing.GetComponent<RectTransform>();
            if (rect == null) rect = existing.gameObject.AddComponent<RectTransform>();
            return existing.gameObject;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static bool HasRelightListener(InteractionHotspot hotspot, StairLampController target)
        {
            if (hotspot == null || target == null) return false;
            int count = hotspot.onActivated.GetPersistentEventCount();
            for (int index = 0; index < count; index++)
            {
                if (hotspot.onActivated.GetPersistentTarget(index) == target &&
                    hotspot.onActivated.GetPersistentMethodName(index) ==
                    nameof(StairLampController.RelightFromInteraction))
                    return true;
            }
            return false;
        }

        private static string BuildPendingSummary()
        {
            StairLampController lamp =
                Object.FindFirstObjectByType<StairLampController>(FindObjectsInactive.Include);
            StringBuilder text = new StringBuilder();
            if (lamp == null)
            {
                text.AppendLine("• StairLampController ausente: rode 'Aplicar autoria' primeiro.");
                return text.ToString();
            }

            Binder binder = new Binder(lamp, "StairLampController", false);
            if (binder.ReadObject("litSprite") == null) text.AppendLine("• FALTA sprite ACESO (Lit Sprite).");
            if (binder.ReadObject("outSprite") == null) text.AppendLine("• FALTA sprite APAGADO (Out Sprite).");
            if (binder.ReadObject("brokenSprite") == null)
                text.AppendLine("• FALTA sprite QUEBRADO (Broken Sprite) — sem ele a quebra usa o apagado.");
            if (binder.ReadObject("burnLoopClip") == null) text.AppendLine("• FALTA clip de queima (Burn Loop Clip).");

            Image lampImage = binder.ReadObject("lampImage") as Image;
            if (lampImage != null && !lampImage.enabled)
                text.AppendLine("• A Image do lampião está desligada: atribua o sprite e ligue o componente.");

            GameObject flameRoot = binder.ReadObject("flameRoot") as GameObject;
            Image flameImage = flameRoot != null ? flameRoot.GetComponent<Image>() : null;
            if (flameImage != null && !flameImage.enabled)
                text.AppendLine("• A Image da chama está desligada: atribua o sprite da chama e ligue o componente.");

            ExhausterDefinition definition = AssetDatabase.LoadAssetAtPath<ExhausterDefinition>(DefinitionPath);
            if (definition != null)
            {
                if (definition.lampOutSfx == null) text.AppendLine("• FALTA ENT_Exhauster.lampOutSfx.");
                if (definition.relightSfx == null) text.AppendLine("• FALTA ENT_Exhauster.relightSfx.");
                if (definition.lampBrokenSfx == null) text.AppendLine("• FALTA ENT_Exhauster.lampBrokenSfx (opcional).");
                if (definition.jumpscare == null || definition.jumpscare.fullscreenSprite == null)
                    text.AppendLine("• FALTA ENT_Exhauster.jumpscare.fullscreenSprite.");
                if (definition.jumpscare == null || definition.jumpscare.jumpscareSfx == null)
                    text.AppendLine("• FALTA ENT_Exhauster.jumpscare.jumpscareSfx.");
            }

            text.AppendLine("• CONTEÚDO: item_oleo ainda não é obtível no Dia (INT_PegarOleo não está em nenhuma cena).");
            return text.ToString();
        }

        // =====================================================================
        // Binder: campos [SerializeField] private via SerializedObject
        // =====================================================================

        private sealed class Binder
        {
            private readonly SerializedObject _serialized;
            private readonly Object _target;
            private readonly string _owner;
            private readonly bool _reportMissing;

            public Binder(Object target, string owner, bool reportMissing = true)
            {
                _target = target;
                _serialized = new SerializedObject(target);
                _owner = owner;
                _reportMissing = reportMissing;
            }

            private SerializedProperty Find(string field)
            {
                SerializedProperty property = _serialized.FindProperty(field);
                if (property == null && _reportMissing)
                    Debug.LogWarning("[ExhausterSetup] Campo '" + field + "' não existe em " + _owner + ".");
                return property;
            }

            public void SetObject(string field, Object value)
            {
                SerializedProperty property = Find(field);
                if (property != null) property.objectReferenceValue = value;
            }

            public Object ReadObject(string field)
            {
                SerializedProperty property = Find(field);
                return property != null ? property.objectReferenceValue : null;
            }

            public void SetText(string field, string value)
            {
                SerializedProperty property = Find(field);
                if (property != null) property.stringValue = value ?? string.Empty;
            }

            public void SetBool(string field, bool value)
            {
                SerializedProperty property = Find(field);
                if (property != null) property.boolValue = value;
            }

            public void SetInt(string field, int value)
            {
                SerializedProperty property = Find(field);
                if (property != null) property.intValue = value;
            }

            public void SetVector2(string field, Vector2 value)
            {
                SerializedProperty property = Find(field);
                if (property != null) property.vector2Value = value;
            }

            /// <summary>Acrescenta a referência ao array preservando entradas já autoradas.</summary>
            public void EnsureArray(string field, Object value)
            {
                SerializedProperty property = Find(field);
                if (property == null || value == null) return;

                List<Object> values = new List<Object>();
                for (int index = 0; index < property.arraySize; index++)
                {
                    Object current = property.GetArrayElementAtIndex(index).objectReferenceValue;
                    if (current != null && !values.Contains(current)) values.Add(current);
                }
                if (!values.Contains(value)) values.Add(value);

                property.arraySize = values.Count;
                for (int index = 0; index < values.Count; index++)
                    property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }

            public void ApplyText(string field, string value)
            {
                SetText(field, value);
                Apply();
            }

            public void Apply()
            {
                Undo.RecordObject(_target, UndoLabel);
                _serialized.ApplyModifiedProperties();
            }
        }
    }
}
#endif
