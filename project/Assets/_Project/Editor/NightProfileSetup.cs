#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Whispers.EditorTools
{
    /// <summary>
    /// Autoria automatizada dos perfis noturnos, seguindo
    /// `Whispers_Of_Unknown_Plano_Ciclo_Eventos_UI_v1.md` §2.4.
    ///
    /// O script faz três coisas e nada além disso:
    ///  1. grava `profileId` único por asset (`night_entities_N`);
    ///  2. desliga (`active = false`) as entidades que não pertencem àquela Noite;
    ///  3. relata o estado de cada perfil (entidades ativas e Exhauster).
    ///
    /// NÃO cria arte, NÃO cria clip e NÃO atribui ExhausterDefinition. O Exhauster de uma
    /// Noite sem lampião continua com `definition` vazia, o que agora significa desligado.
    ///
    /// Seguro para rodar mais de uma vez.
    /// </summary>
    public static class NightProfileSetup
    {
        private const string Folder = "Assets/_Project/SO/NightEntities";
        private const string UndoLabel = "Perfis noturnos — entidades ativas e profileId";

        /// <summary>
        /// Entidades ativas por Noite do ciclo novo (1 Dia -> 1 Noite, 4 pares).
        /// Ajuste esta tabela quando o desenho de dificuldade mudar.
        /// </summary>
        private static readonly Dictionary<int, NightComposition> Composition = new Dictionary<int, NightComposition>
        {
            { 1, new NightComposition(predator: false, voyeur: true,  exhauster: false) },
            { 2, new NightComposition(predator: true,  voyeur: true,  exhauster: false) },
            { 3, new NightComposition(predator: true,  voyeur: true,  exhauster: true) },
            { 4, new NightComposition(predator: true,  voyeur: true,  exhauster: true) }
        };

        private readonly struct NightComposition
        {
            public readonly bool predator;
            public readonly bool voyeur;
            public readonly bool exhauster;

            public NightComposition(bool predator, bool voyeur, bool exhauster)
            {
                this.predator = predator;
                this.voyeur = voyeur;
                this.exhauster = exhauster;
            }
        }

        // =====================================================================
        // Menu
        // =====================================================================

        [MenuItem("Whispers/Perfis/Aplicar entidades ativas e profileId", priority = 30)]
        public static void Apply()
        {
            StringBuilder report = new StringBuilder();
            int changedAssets = 0;

            foreach (KeyValuePair<int, NightComposition> pair in Composition)
            {
                int night = pair.Key;
                string path = Folder + "/NIGHT_Entities_" + night + ".asset";
                NightEntityProfile profile = AssetDatabase.LoadAssetAtPath<NightEntityProfile>(path);
                if (profile == null)
                {
                    report.AppendLine("• Noite " + night + ": asset ausente em " + path + ".");
                    continue;
                }

                Undo.RecordObject(profile, UndoLabel);
                SerializedObject so = new SerializedObject(profile);

                // 1. profileId único por Noite.
                SerializedProperty profileId = so.FindProperty("profileId");
                string expectedId = "night_entities_" + night;
                if (profileId != null && profileId.stringValue != expectedId)
                    profileId.stringValue = expectedId;

                // 2. Entidades ativas conforme a tabela de composição.
                SerializedProperty entries = so.FindProperty("entries");
                if (entries != null && entries.isArray)
                {
                    for (int i = 0; i < entries.arraySize; i++)
                    {
                        SerializedProperty element = entries.GetArrayElementAtIndex(i);
                        SerializedProperty definitionProperty = element.FindPropertyRelative("entityDefinition");
                        SerializedProperty activeProperty = element.FindPropertyRelative("active");
                        if (definitionProperty == null || activeProperty == null) continue;

                        EntityDefinition definition = definitionProperty.objectReferenceValue as EntityDefinition;
                        if (definition == null) continue;

                        bool shouldBeActive =
                            definition is PredatorDefinition ? pair.Value.predator :
                            definition is VoyeurDefinition ? pair.Value.voyeur :
                            activeProperty.boolValue;   // tipo desconhecido: não opinamos

                        if (activeProperty.boolValue != shouldBeActive)
                            activeProperty.boolValue = shouldBeActive;
                    }
                }

                // 3. Exhauster: 'active' ligado/desligado. A definição continua como está.
                SerializedProperty exhauster = so.FindProperty("exhauster");
                if (exhauster != null)
                {
                    SerializedProperty exhausterActive = exhauster.FindPropertyRelative("active");
                    SerializedProperty exhausterDefinition = exhauster.FindPropertyRelative("definition");
                    if (exhausterActive != null)
                        exhausterActive.boolValue = pair.Value.exhauster;

                    bool hasDefinition = exhausterDefinition != null && exhausterDefinition.objectReferenceValue != null;
                    if (pair.Value.exhauster && !hasDefinition)
                        report.AppendLine("• Noite " + night + ": Exhauster deveria rodar, mas está sem ExhausterDefinition.");
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(profile);
                changedAssets++;
                report.AppendLine("• " + profile.name + " → " + Describe(profile, night));
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[NightProfileSetup] " + changedAssets + " perfis processados.\n" + report);
            EditorUtility.DisplayDialog("Perfis noturnos", report.ToString(), "OK");
        }

        [MenuItem("Whispers/Perfis/Verificar perfis noturnos", priority = 31)]
        public static void Verify()
        {
            StringBuilder report = new StringBuilder();
            foreach (KeyValuePair<int, NightComposition> pair in Composition)
            {
                string path = Folder + "/NIGHT_Entities_" + pair.Key + ".asset";
                NightEntityProfile profile = AssetDatabase.LoadAssetAtPath<NightEntityProfile>(path);
                if (profile == null) { report.AppendLine("• Noite " + pair.Key + ": ausente."); continue; }
                report.AppendLine("• Noite " + pair.Key + " → " + Describe(profile, pair.Key));
            }

            Debug.Log("[NightProfileSetup] Verificação:\n" + report);
            EditorUtility.DisplayDialog("Perfis noturnos", report.ToString(), "OK");
        }

        // =====================================================================
        // Diagnóstico
        // =====================================================================

        private static string Describe(NightEntityProfile profile, int night)
        {
            List<string> parts = new List<string>();
            if (profile.entries != null)
            {
                foreach (EntityNightEntry entry in profile.entries)
                {
                    if (entry == null) continue;
                    string id = entry.entityDefinition != null ? entry.entityDefinition.entityId : "(sem definição)";
                    parts.Add(id + (entry.active ? " [ATIVA]" : " [inativa]"));
                }
            }

            ExhausterNightEntry exhauster = profile.Exhauster;
            string exhausterLabel;
            if (exhauster == null) exhausterLabel = "Exhauster: ausente";
            else if (exhauster.definition == null) exhausterLabel = "Exhauster: desligado (sem definição)";
            else exhausterLabel = "Exhauster: " + (exhauster.active ? "ATIVO" : "inativo") +
                                  " ('" + exhauster.definition.entityId + "')";

            return "profileId='" + profile.profileId + "'"
                   + (profile.profileId == "night_entities_" + night ? "" : " [DIVERGENTE]")
                   + " | " + string.Join(", ", parts) + " | " + exhausterLabel;
        }
    }
}
#endif
