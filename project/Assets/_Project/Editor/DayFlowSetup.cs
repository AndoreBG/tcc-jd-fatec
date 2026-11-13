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
    /// Autorias do M9 (plano §10): tabela dos Dias, banner de objetivo e travas de
    /// navegação por Dia.
    ///
    /// Menu 1: cria o asset `DayFlow_Gameplay` com os quatro Dias e liga no
    ///         `DayFlowController` da cena `day`.
    /// Menu 2: cria o banner de objetivo na cena `day` (canvas próprio, canto superior
    ///         esquerdo) e liga no `DayFlowController`.
    /// Menu 3: cria as condições de Dia e aplica nos hotspots de navegação por destino.
    /// Menu 4: valida a tabela do Dia e lista o que está faltando.
    /// Menu 5: cria os objetivos como assets (`OBJ_Dia1..4`) e liga na tabela.
    ///
    /// Nenhum texto é escrito por aqui: título e corpo dos objetivos nascem vazios, para
    /// o usuário autorar, como nos documentos e no rádio.
    /// </summary>
    public static class DayFlowSetup
    {
        private const string DaySceneName = "day";
        private const string FontPath = "Assets/_Project/Art/Fonts/SpecialElite-Regular SDF.asset";
        private const string DayFolder = "Assets/_Project/SO/Day";
        private const string DayFlowPath = DayFolder + "/DayFlow_Gameplay.asset";
        private const string ConditionFolder = "Assets/_Project/SO/Conditions";
        private const string CondDay2Path = ConditionFolder + "/COND_Dia2.asset";
        private const string CondDay3Path = ConditionFolder + "/COND_Dia3.asset";
        private const string RadioFolder = "Assets/_Project/SO/Radio";
        private const string ObjectivesFolder = "Assets/_Project/SO/Objectives";
        private const string ObjectivePrefix = "OBJ_Dia";

        /// <summary>
        /// Título inicial de cada objetivo, tirado da tabela do plano (§10.1). É só ponto de
        /// partida: o texto no asset manda, e o menu nunca sobrescreve o que já está escrito.
        /// O Dia 4 nasce vazio porque o plano não pede objetivo nele.
        /// </summary>
        private static readonly string[] DefaultObjectiveTitles =
        {
            "Encontre a lanterna",
            "Vá até as portas",
            "Encontre o lampião e o óleo",
            string.Empty,
        };
        private const string BannerCanvasName = "Canvas_Objetivo";

        /// <summary>
        /// Destino de navegação e o Dia mínimo em que ele abre. O hotspot é encontrado pelo
        /// `DestinationId`, não pelo nome: existem vários hotspots para o mesmo destino e
        /// todos precisam da mesma trava.
        /// </summary>
        private static readonly (string destination, int minDay)[] NavigationLocks =
        {
            ("vn_portasala", 2),
            ("vn_portacozinha", 2),
            ("vn_sotao", 3),
        };

        [MenuItem("Whispers/Dia/Criar tabela do Dia", priority = 30)]
        public static void CreateDayFlow()
        {
            if (AssetDatabase.LoadAssetAtPath<DayFlowDefinition>(DayFlowPath) != null)
            {
                EditorUtility.DisplayDialog("Já existe",
                    DayFlowPath + " já está no projeto. Nada foi criado.\n" +
                    "Use Whispers > Dia > Validar tabela do Dia para conferir.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(DayFolder))
                AssetDatabase.CreateFolder("Assets/_Project/SO", "Day");

            DayFlowDefinition definition = ScriptableObject.CreateInstance<DayFlowDefinition>();
            definition.stageId = "gameplay";

            string[] radioNames = { "RADIO_Dia1", "RADIO_Dia2", "RADIO_Dia3", "RADIO_Dia4" };
            DayFlowEntry[] entries = new DayFlowEntry[radioNames.Length];
            ObjectiveDefinition[] objectives = EnsureObjectives(radioNames.Length);

            for (int index = 0; index < entries.Length; index++)
            {
                string radioPath = RadioFolder + "/" + radioNames[index] + ".asset";
                entries[index] = new DayFlowEntry
                {
                    day = index + 1,
                    objective = objectives[index],
                    requiredNavigationsBeforeRadio = 3,
                    radioEvent = AssetDatabase.LoadAssetAtPath<RadioEventDefinition>(radioPath),
                };
            }

            definition.entries = entries;
            AssetDatabase.CreateAsset(definition, DayFlowPath);
            AssetDatabase.SaveAssets();

            bool linked = LinkToDayScene(definition);

            EditorUtility.DisplayDialog("Tabela do Dia",
                "Criada: " + DayFlowPath + "\n" +
                "• Quatro entradas, Dia 1 a Dia 4, rádio ligado em cada uma.\n" +
                "• Navegações exigidas: 3 (o Dia 1 ignora, toca no boot).\n" +
                "• Objetivos: " + ObjectivesFolder + "/OBJ_Dia1..4 (título e corpo vazios).\n" +
                (linked
                    ? "• Ligada no DayFlowController da cena day aberta."
                    : "• A cena day não estava aberta: ligue a tabela à mão no DayFlowController."),
                "OK");
        }

        [MenuItem("Whispers/Dia/Criar banner de objetivo", priority = 31)]
        public static void CreateObjectiveBanner()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != DaySceneName)
            {
                EditorUtility.DisplayDialog("Cena diferente de 'day'",
                    "Abra a cena 'day' e rode de novo. Cena atual: '" +
                    (scene.IsValid() ? scene.name : "nenhuma") + "'.", "OK");
                return;
            }

            DayFlowController flow = Object.FindFirstObjectByType<DayFlowController>(FindObjectsInactive.Include);
            if (flow == null)
            {
                EditorUtility.DisplayDialog("Fluxo do Dia ausente",
                    "A cena não tem DayFlowController. Rode antes: Whispers > Rádio > Aplicar fluxo do Dia.", "OK");
                return;
            }

            if (Object.FindFirstObjectByType<ObjectiveBanner>(FindObjectsInactive.Include) != null)
            {
                EditorUtility.DisplayDialog("Já existe",
                    "A cena já tem um ObjectiveBanner. Nada foi criado.", "OK");
                return;
            }

            // Canvas próprio, abaixo da VN do rádio (200) e acima do HUD.
            GameObject canvasObject = new GameObject(BannerCanvasName,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Criar banner de objetivo");

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject labelObject = new GameObject("ObjectiveText", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(880f, 140f);
            rect.anchoredPosition = new Vector2(64f, -64f);

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            label.fontSize = 32f;
            label.color = new Color(0.94f, 0.91f, 0.84f, 1f);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            label.text = string.Empty;

            CanvasGroup group = canvasObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            ObjectiveBanner banner = canvasObject.AddComponent<ObjectiveBanner>();
            SerializedObject serialized = new SerializedObject(banner);
            serialized.FindProperty("group").objectReferenceValue = group;
            serialized.FindProperty("label").objectReferenceValue = label;
            serialized.ApplyModifiedProperties();

            SerializedObject flowSo = new SerializedObject(flow);
            flowSo.FindProperty("objectiveBanner").objectReferenceValue = banner;
            flowSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(flow);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog("Banner de objetivo",
                "Criado na cena 'day':\n" +
                "• " + BannerCanvasName + " (sorting order 150, canto superior esquerdo).\n" +
                "• Ligado no DayFlowController.\n\n" +
                "O texto vem do asset de objetivo ligado no Dia (OBJ_Dia1..4).\n" +
                "Dia sem objetivo, ou objetivo sem texto, não abre banner.", "OK");
        }

        [MenuItem("Whispers/Dia/Aplicar travas de navegação", priority = 32)]
        public static void ApplyNavigationLocks()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != DaySceneName)
            {
                EditorUtility.DisplayDialog("Cena diferente de 'day'",
                    "Abra a cena 'day' e rode de novo. Cena atual: '" +
                    (scene.IsValid() ? scene.name : "nenhuma") + "'.", "OK");
                return;
            }

            bool cond2 = EnsureCondition(CondDay2Path, 2);
            bool cond3 = EnsureCondition(CondDay3Path, 3);
            if (!cond2 || !cond3)
            {
                EditorUtility.DisplayDialog("Condições não criadas",
                    "Não consegui criar os assets em " + ConditionFolder + ".", "OK");
                return;
            }

            DayAtLeastCondition day2 = AssetDatabase.LoadAssetAtPath<DayAtLeastCondition>(CondDay2Path);
            DayAtLeastCondition day3 = AssetDatabase.LoadAssetAtPath<DayAtLeastCondition>(CondDay3Path);

            NavigationHotspot[] hotspots = Object.FindObjectsByType<NavigationHotspot>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int touched = 0;
            List<string> skipped = new List<string>();

            foreach (NavigationHotspot hotspot in hotspots)
            {
                string destination = hotspot.DestinationId;
                int minDay = 0;
                foreach ((string candidate, int day) in NavigationLocks)
                {
                    if (candidate == destination) { minDay = day; break; }
                }

                if (minDay == 0)
                {
                    if (!string.IsNullOrEmpty(destination) && !skipped.Contains(destination))
                        skipped.Add(destination);
                    continue;
                }

                DayAtLeastCondition condition = minDay >= 3 ? day3 : day2;
                if (ApplyCondition(hotspot, condition)) touched++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            StringBuilder report = new StringBuilder();
            report.AppendLine("Hotspots travados: " + touched + ".");
            report.AppendLine("• Portas (vn_portasala, vn_portacozinha): a partir do Dia 2.");
            report.AppendLine("• Sótão (vn_sotao): a partir do Dia 3.");
            report.AppendLine("• Modo quando indisponível: Blocked (aparece, não clica).");
            if (touched == 0) report.AppendLine("NENHUM hotspot encontrado pelos destinos acima.");
            if (skipped.Count > 0)
                report.AppendLine("Destinos sem trava: " + string.Join(", ", skipped));
            EditorUtility.DisplayDialog("Travas de navegação", report.ToString(), "OK");
            Debug.Log("[DayFlowSetup]\n" + report);
        }

        [MenuItem("Whispers/Dia/Criar objetivos dos Dias", priority = 34)]
        public static void CreateObjectives()
        {
            ObjectiveDefinition[] objectives = EnsureObjectives(4);

            int created = 0;
            foreach (ObjectiveDefinition objective in objectives)
                if (objective != null) created++;

            DayFlowDefinition definition = AssetDatabase.LoadAssetAtPath<DayFlowDefinition>(DayFlowPath);
            int linked = 0;

            if (definition != null)
            {
                SerializedObject serialized = new SerializedObject(definition);
                SerializedProperty entries = serialized.FindProperty("entries");

                for (int index = 0; index < entries.arraySize; index++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    SerializedProperty dayProperty = entry.FindPropertyRelative("day");
                    if (dayProperty == null) continue;

                    int day = dayProperty.intValue;
                    if (day < 1 || day > objectives.Length) continue;

                    SerializedProperty objectiveProperty = entry.FindPropertyRelative("objective");
                    if (objectiveProperty == null) continue;

                    if (objectiveProperty.objectReferenceValue == null && objectives[day - 1] != null)
                    {
                        objectiveProperty.objectReferenceValue = objectives[day - 1];
                        linked++;
                    }
                }

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
            }

            EditorUtility.DisplayDialog("Objetivos dos Dias",
                "• Assets em " + ObjectivesFolder + ": " + created + " (OBJ_Dia1..4).\n" +
                "• IDs: obj_dia_1 a obj_dia_4.\n" +
                "• Títulos do plano §10.1 já preenchidos, editáveis no Inspector.\n" +
                "• Corpo vazio: o complemento é autoria sua.\n" +
                (definition == null
                    ? "• A tabela do Dia ainda não existe: rode 'Criar tabela do Dia' antes."
                    : "• Ligados na tabela agora: " + linked + " (entradas já preenchidas foram mantidas)."),
                "OK");
        }

        [MenuItem("Whispers/Dia/Validar tabela do Dia", priority = 33)]
        public static void ValidateDayFlow()
        {
            DayFlowDefinition definition = AssetDatabase.LoadAssetAtPath<DayFlowDefinition>(DayFlowPath);
            if (definition == null)
            {
                EditorUtility.DisplayDialog("Tabela ausente",
                    "Não existe " + DayFlowPath + ". Rode Whispers > Dia > Criar tabela do Dia.", "OK");
                return;
            }

            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            definition.CollectValidation(errors, warnings);

            StringBuilder report = new StringBuilder();
            report.AppendLine("Tabela: " + DayFlowPath);
            report.AppendLine("Entradas: " + definition.entries.Length);
            report.AppendLine("Erros: " + errors.Count + " | Avisos: " + warnings.Count);
            foreach (string error in errors) report.AppendLine("ERRO: " + error);
            foreach (string warning in warnings) report.AppendLine("aviso: " + warning);
            if (errors.Count == 0 && warnings.Count == 0) report.AppendLine("Tudo autorado.");

            EditorUtility.DisplayDialog("Tabela do Dia", report.ToString(), "OK");
            Debug.Log("[DayFlowSetup]\n" + report);
        }

        // ---------------- Helpers ----------------

        private static bool LinkToDayScene(DayFlowDefinition definition)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene open = SceneManager.GetSceneAt(index);
                if (!open.IsValid() || open.name != DaySceneName) continue;

                DayFlowController flow = Object.FindFirstObjectByType<DayFlowController>(FindObjectsInactive.Include);
                if (flow == null) return false;

                SerializedObject serialized = new SerializedObject(flow);
                serialized.FindProperty("definition").objectReferenceValue = definition;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(flow);
                EditorSceneManager.MarkSceneDirty(open);
                EditorSceneManager.SaveScene(open);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Garante um objetivo por Dia em `SO/Objectives`, reaproveitando os que já existem.
        /// Idempotente: rodar de novo não sobrescreve texto autorado.
        /// </summary>
        private static ObjectiveDefinition[] EnsureObjectives(int days)
        {
            if (!AssetDatabase.IsValidFolder(ObjectivesFolder))
                AssetDatabase.CreateFolder("Assets/_Project/SO", "Objectives");

            ObjectiveDefinition[] objectives = new ObjectiveDefinition[days];

            for (int index = 0; index < days; index++)
            {
                int day = index + 1;
                string path = ObjectivesFolder + "/" + ObjectivePrefix + day + ".asset";

                ObjectiveDefinition existing = AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(path);
                if (existing != null)
                {
                    objectives[index] = existing;
                    continue;
                }

                ObjectiveDefinition objective = ScriptableObject.CreateInstance<ObjectiveDefinition>();
                objective.objectiveId = "obj_dia_" + day;
                objective.title = index < DefaultObjectiveTitles.Length ? DefaultObjectiveTitles[index] : string.Empty;
                objective.body = string.Empty;

                AssetDatabase.CreateAsset(objective, path);
                objectives[index] = AssetDatabase.LoadAssetAtPath<ObjectiveDefinition>(path);
            }

            AssetDatabase.SaveAssets();
            return objectives;
        }

        private static bool EnsureCondition(string path, int minDay)
        {
            DayAtLeastCondition existing = AssetDatabase.LoadAssetAtPath<DayAtLeastCondition>(path);
            if (existing != null) return true;

            if (!AssetDatabase.IsValidFolder(ConditionFolder))
                AssetDatabase.CreateFolder("Assets/_Project/SO", "Conditions");

            DayAtLeastCondition condition = ScriptableObject.CreateInstance<DayAtLeastCondition>();
            condition.minDay = minDay;
            condition.maxDay = 99;

            AssetDatabase.CreateAsset(condition, path);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<DayAtLeastCondition>(path) != null;
        }

        /// <summary>
        /// Acrescenta a condição ao hotspot, se ela ainda não estiver lá, e garante o modo
        /// Blocked. Idempotente: rodar duas vezes não duplica condição.
        /// </summary>
        private static bool ApplyCondition(NavigationHotspot hotspot, HotspotConditionSO condition)
        {
            SerializedObject serialized = new SerializedObject(hotspot);

            SerializedProperty conditions = serialized.FindProperty("conditions");
            if (conditions == null) return false;

            for (int index = 0; index < conditions.arraySize; index++)
            {
                if (conditions.GetArrayElementAtIndex(index).objectReferenceValue == condition)
                {
                    SerializedProperty mode = serialized.FindProperty("unavailableMode");
                    if (mode != null) mode.enumValueIndex = (int)HotspotUnavailableMode.Blocked;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(hotspot);
                    return false; // já estava lá
                }
            }

            conditions.arraySize++;
            conditions.GetArrayElementAtIndex(conditions.arraySize - 1).objectReferenceValue = condition;

            SerializedProperty policy = serialized.FindProperty("conditionPolicy");
            if (policy != null) policy.enumValueIndex = (int)HotspotConditionPolicy.All;

            SerializedProperty available = serialized.FindProperty("unavailableMode");
            if (available != null) available.enumValueIndex = (int)HotspotUnavailableMode.Blocked;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(hotspot);
            return true;
        }
    }
}
#endif
