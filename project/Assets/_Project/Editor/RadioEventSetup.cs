#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Whispers.EditorTools
{
    /// <summary>
    /// Autoria do evento de rádio (plano §8).
    ///
    /// Menu 1: cria a cena `Scenes/radio_event.unity` com a hierarquia completa e registra
    ///         a cena no Build Profile.
    /// Menu 2: cria o `Manager_DayFlow` na cena `day` e aponta o evento do Dia.
    ///
    /// O texto das linhas e o áudio são autoria do usuário: o asset nasce sem linhas.
    /// </summary>
    public static class RadioEventSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/radio_event.unity";
        private const string FontPath = "Assets/_Project/Art/Fonts/SpecialElite-Regular SDF.asset";
        private const string DefinitionPath = "Assets/_Project/SO/Radio/RADIO_EventoDia.asset";
        private const string DaySceneName = "day";
        private const string DayFlowName = "Manager_DayFlow";
        private const string AnchorId = "radio_corredorfront";
        private const string DayRadioFolder = "Assets/_Project/SO/Radio";
        private static readonly string[] DayRadioAssetNames = { "RADIO_Dia1", "RADIO_Dia2", "RADIO_Dia3", "RADIO_Dia4" };

        [MenuItem("Whispers/Rádio/Criar cena do rádio", priority = 70)]
        public static void CreateRadioScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- Câmera + AudioListener (PlayOneShot precisa de listener) ----
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;

            // ---- Canvas ----
            GameObject canvasObject = new GameObject("Canvas_Radio",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // ---- RadioViewRoot (inativo no início) ----
            GameObject viewRoot = CreateUiObject(canvasObject.transform, "RadioViewRoot");
            Stretch(viewRoot.GetComponent<RectTransform>());
            viewRoot.SetActive(false);

            Image[] frames = new Image[3];
            for (int index = 0; index < frames.Length; index++)
            {
                GameObject frameObject = CreateUiObject(viewRoot.transform, "Frame_0" + (index + 1));
                Stretch(frameObject.GetComponent<RectTransform>());
                Image image = frameObject.AddComponent<Image>();
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
                frames[index] = image;
                frameObject.SetActive(false);
            }

            // Faixa preta inferior (28% da altura).
            GameObject dialogueFadeObject = CreateUiObject(viewRoot.transform, "DialogueFade");
            RectTransform dialogueFadeRect = dialogueFadeObject.GetComponent<RectTransform>();
            dialogueFadeRect.anchorMin = new Vector2(0f, 0f);
            dialogueFadeRect.anchorMax = new Vector2(1f, 0.28f);
            dialogueFadeRect.offsetMin = Vector2.zero;
            dialogueFadeRect.offsetMax = Vector2.zero;
            Image dialogueFade = dialogueFadeObject.AddComponent<Image>();
            dialogueFade.color = new Color(0f, 0f, 0f, 0.85f);
            dialogueFade.raycastTarget = false;

            // Texto do diálogo.
            GameObject textObject = CreateUiObject(viewRoot.transform, "DialogueText");
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.06f, 0.03f);
            textRect.anchorMax = new Vector2(0.94f, 0.27f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            label.fontSize = 34f;
            label.color = new Color(0.94f, 0.91f, 0.84f, 1f);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.text = string.Empty;

            // Indicador de avanço (canto inferior direito).
            GameObject indicatorObject = CreateUiObject(viewRoot.transform, "AdvanceIndicator");
            RectTransform indicatorRect = indicatorObject.GetComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(1f, 0f);
            indicatorRect.anchorMax = new Vector2(1f, 0f);
            indicatorRect.pivot = new Vector2(1f, 0f);
            indicatorRect.sizeDelta = new Vector2(28f, 28f);
            indicatorRect.anchoredPosition = new Vector2(-48f, 48f);
            Image indicatorImage = indicatorObject.AddComponent<Image>();
            indicatorImage.color = new Color(0.94f, 0.91f, 0.84f, 1f);
            indicatorImage.raycastTarget = false;
            CanvasGroup indicatorGroup = indicatorObject.AddComponent<CanvasGroup>();
            indicatorGroup.alpha = 0f;
            indicatorGroup.blocksRaycasts = false;

            // ---- Fader por cima de tudo ----
            GameObject faderObject = CreateUiObject(canvasObject.transform, "Fader");
            Stretch(faderObject.GetComponent<RectTransform>());
            Image faderImage = faderObject.AddComponent<Image>();
            faderImage.color = new Color(0f, 0f, 0f, 1f);
            faderImage.raycastTarget = false;
            CanvasGroup faderGroup = faderObject.AddComponent<CanvasGroup>();
            faderGroup.alpha = 1f;
            faderGroup.blocksRaycasts = false;

            // ---- Controlador ----
            GameObject controllerObject = new GameObject("RadioEventController");
            AudioSource jingleSource = controllerObject.AddComponent<AudioSource>();
            ConfigureSource(jingleSource);
            AudioSource typewriterSource = controllerObject.AddComponent<AudioSource>();
            ConfigureSource(typewriterSource);

            RadioEventController controller = controllerObject.AddComponent<RadioEventController>();
            SerializedObject so = new SerializedObject(controller);
            // Fallback serve só para testar a cena do rádio sozinha (Play direto, sem sessão).
            RadioEventDefinition fallback = AssetDatabase.LoadAssetAtPath<RadioEventDefinition>(
                DayRadioFolder + "/" + DayRadioAssetNames[0] + ".asset");
            if (fallback == null)
                fallback = AssetDatabase.LoadAssetAtPath<RadioEventDefinition>(DefinitionPath);
            so.FindProperty("fallbackDefinition").objectReferenceValue = fallback;
            so.FindProperty("viewRoot").objectReferenceValue = viewRoot;
            SetArray(so.FindProperty("frames"), frames);
            so.FindProperty("dialogueText").objectReferenceValue = label;
            so.FindProperty("indicatorGroup").objectReferenceValue = indicatorGroup;
            so.FindProperty("fader").objectReferenceValue = faderImage;
            so.FindProperty("faderGroup").objectReferenceValue = faderGroup;
            so.FindProperty("jingleSource").objectReferenceValue = jingleSource;
            so.FindProperty("typewriterSource").objectReferenceValue = typewriterSource;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuild(ScenePath);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("Cena do rádio",
                "Criada: " + ScenePath + "\n" +
                "Registrada no Build Profile: sim\n\n" +
                "Pendências de autoria:\n" +
                "• Preencher as linhas em RADIO_EventoDia (texto, frame e som).\n" +
                "• Soltar os sprites nos Frame_01..03.\n" +
                "• Configurar jingle e clips de typewriter.", "OK");
        }

        [MenuItem("Whispers/Rádio/Aplicar fluxo do Dia", priority = 71)]
        public static void ApplyDayFlow()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != DaySceneName)
            {
                EditorUtility.DisplayDialog("Cena diferente de 'day'",
                    "Abra a cena 'day' e rode de novo. Cena atual: '" +
                    (scene.IsValid() ? scene.name : "nenhuma") + "'.", "OK");
                return;
            }

            GameplaySceneController controller = Object.FindFirstObjectByType<GameplaySceneController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Controller ausente", "GameplaySceneController não encontrado na cena.", "OK");
                return;
            }

            RadioEventDefinition[] dayDefinitions = LoadDayDefinitions();
            int found = 0;
            foreach (RadioEventDefinition candidate in dayDefinitions)
                if (candidate != null) found++;

            if (found == 0)
            {
                EditorUtility.DisplayDialog("Assets ausentes",
                    "Não encontrei nenhum RADIO_Dia1..4 em '" + DayRadioFolder +
                    "'. Deixe o Unity importar e rode de novo.", "OK");
                return;
            }

            GameObject flowObject = null;
            Transform child = controller.transform.Find(DayFlowName);
            if (child != null) flowObject = child.gameObject;
            if (flowObject == null)
            {
                flowObject = new GameObject(DayFlowName);
                Undo.RegisterCreatedObjectUndo(flowObject, "Fluxo do Dia");
                flowObject.transform.SetParent(controller.transform, false);
            }

            DayFlowController flow = flowObject.GetComponent<DayFlowController>();
            if (flow == null) flow = Undo.AddComponent<DayFlowController>(flowObject);

            SerializedObject flowSo = new SerializedObject(flow);
            SerializedProperty events = flowSo.FindProperty("dayRadioEvents");
            int nights = 4;
            SerializedProperty cycleProperty = new SerializedObject(controller).FindProperty("cycleDefinition");
            GameCycleDefinition cycle = cycleProperty != null ? cycleProperty.objectReferenceValue as GameCycleDefinition : null;
            if (cycle != null) nights = Mathf.Max(1, cycle.EffectiveNightCount);

            int slots = Mathf.Max(nights, dayDefinitions.Length);
            events.arraySize = slots;
            for (int index = 0; index < slots; index++)
                events.GetArrayElementAtIndex(index).objectReferenceValue =
                    index < dayDefinitions.Length ? dayDefinitions[index] : null;
            flowSo.FindProperty("requiredNavigationsBeforeRadio").intValue = 3;

            // Boletim de abertura: no Dia 1 o rádio não depende de navegação.
            SerializedProperty bootRadio = flowSo.FindProperty("playFirstDayOnBoot");
            if (bootRadio != null) bootRadio.boolValue = true;
            SerializedProperty bootDelay = flowSo.FindProperty("firstDayBootDelaySeconds");
            if (bootDelay != null) bootDelay.floatValue = 1.5f;
            flowSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(flow);

            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("dayFlow").objectReferenceValue = flow;
            controllerSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            StringBuilder report = new StringBuilder();
            report.AppendLine("• " + DayFlowName + " criado/reaproveitado em " + controller.name + ".");
            report.AppendLine("• Navegações antes do rádio: 3 (Dias 2 a 4).");
            report.AppendLine("• Dia 1: boletim toca sozinho no boot, 1.5s depois da cortina.");
            for (int index = 0; index < slots; index++)
            {
                RadioEventDefinition candidate = index < dayDefinitions.Length ? dayDefinitions[index] : null;
                string state = candidate == null ? "vazio (sem rádio)" : candidate.name;
                report.AppendLine("   Dia " + (index + 1) + ": " + state);
            }
            report.AppendLine("• Um Dia sem linhas não dispara nada.");
            EditorUtility.DisplayDialog("Fluxo do Dia", report.ToString(), "OK");
            Debug.Log("[RadioEventSetup]\n" + report);
        }

        [MenuItem("Whispers/Rádio/Aplicar anchor nos perfis de áudio", priority = 72)]
        public static void ApplyRadioAnchor()
        {
            string[] guids = AssetDatabase.FindAssets("t:ViewAudioProfile",
                new[] { "Assets/_Project/SO/Audio/ViewAudioProfiles" });
            int updated = 0;

            foreach (string guid in guids)
            {
                ViewAudioProfile profile = AssetDatabase.LoadAssetAtPath<ViewAudioProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile == null) continue;

                SerializedObject profileSo = new SerializedObject(profile);
                SerializedProperty points = profileSo.FindProperty("pointPerspectives");
                if (points == null) continue;

                int index = -1;
                for (int i = 0; i < points.arraySize; i++)
                {
                    SerializedProperty candidate = points.GetArrayElementAtIndex(i);
                    if (candidate.FindPropertyRelative("anchorId").stringValue == AnchorId) { index = i; break; }
                }
                if (index < 0)
                {
                    points.arraySize++;
                    index = points.arraySize - 1;
                }

                SerializedProperty entry = points.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("anchorId").stringValue = AnchorId;
                entry.FindPropertyRelative("isAudible").boolValue = true;
                entry.FindPropertyRelative("distance").enumValueIndex = (int)AudioDistance.Medium;
                entry.FindPropertyRelative("pan").floatValue = 0f;
                entry.FindPropertyRelative("occlusion").floatValue = 0.2f;

                profileSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(profile);
                updated++;
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Anchor do rádio",
                "Perfis de áudio atualizados: " + updated + "\n" +
                "Anchor: " + AnchorId + " (pan 0, Medium, oclusão 0.2).\n\n" +
                "O anchor não toca nada sozinho: só o evento de rádio usa este ponto.", "OK");
        }

        private static RadioEventDefinition[] LoadDayDefinitions()
        {
            RadioEventDefinition[] definitions = new RadioEventDefinition[DayRadioAssetNames.Length];
            for (int index = 0; index < definitions.Length; index++)
            {
                string path = DayRadioFolder + "/" + DayRadioAssetNames[index] + ".asset";
                definitions[index] = AssetDatabase.LoadAssetAtPath<RadioEventDefinition>(path);
            }
            return definitions;
        }

        // ---------------- Helpers ----------------

        private static void ConfigureSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        private static GameObject CreateUiObject(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : Object
        {
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void EnsureSceneInBuild(string path)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene scene in scenes)
                if (scene.path == path) return;

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
