#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Whispers.EditorTools
{
    /// <summary>
    /// Detector de referências quebradas em cenas editadas à mão.
    ///
    /// O Unity só reclama de um PPtr quebrado quando abre a cena, e às vezes só de um por
    /// vez. Este menu varre o YAML de todas as cenas do projeto e reporta tudo de uma vez:
    ///
    ///  1. ids de documento duplicados;
    ///  2. `m_Component` apontando para documento inexistente;
    ///  3. `m_GameObject` de componente apontando para GameObject inexistente;
    ///  4. `m_Father` apontando para Transform/RectTransform inexistente (órfão);
    ///  5. `m_Children` apontando para filho inexistente;
    ///  6. filho que não lista o pai em `m_Father` (assimetria).
    ///
    /// O caso 4 é o mais traiçoeiro: apagar um objeto com filhos deixa os filhos órfãos, e
    /// como ninguém os lista em `m_Children`, uma verificação só de ida não os encontra.
    /// </summary>
    public static class SceneYamlValidator
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";

        private sealed class Doc
        {
            public string ClassId;
            public string FileId;
            public string Text;
        }

        [MenuItem("Whispers/Cenas/Verificar integridade das cenas", priority = 10)]
        public static void VerifyAllScenes()
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder });
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Sem cenas", "Nenhuma cena em " + ScenesFolder + ".", "OK");
                return;
            }

            StringBuilder report = new StringBuilder();
            int totalProblems = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                List<string> problems = Validate(path);
                totalProblems += problems.Count;

                report.AppendLine(Path.GetFileName(path) + ": " + problems.Count + " problema(s)");
                for (int index = 0; index < problems.Count && index < 12; index++)
                    report.AppendLine("   • " + problems[index]);
                if (problems.Count > 12)
                    report.AppendLine("   • ... e mais " + (problems.Count - 12));
            }

            report.AppendLine();
            report.AppendLine(totalProblems == 0
                ? "Nenhuma referência quebrada."
                : "Corrija antes de abrir as cenas no Editor.");

            if (totalProblems == 0) Debug.Log("[SceneYamlValidator]\n" + report);
            else Debug.LogError("[SceneYamlValidator]\n" + report);

            EditorUtility.DisplayDialog("Integridade das cenas", report.ToString(), "OK");
        }

        /// <summary>Valida uma cena e devolve a lista de problemas encontrados.</summary>
        public static List<string> Validate(string path)
        {
            List<string> problems = new List<string>();
            if (!File.Exists(path)) { problems.Add("arquivo não encontrado"); return problems; }

            string source = File.ReadAllText(path);
            List<Doc> docs = Parse(source);
            Dictionary<string, Doc> byId = new Dictionary<string, Doc>();
            HashSet<string> duplicated = new HashSet<string>();

            foreach (Doc doc in docs)
            {
                if (byId.ContainsKey(doc.FileId)) duplicated.Add(doc.FileId);
                else byId[doc.FileId] = doc;
            }

            foreach (string id in duplicated)
                problems.Add("id duplicado: " + id);

            foreach (Doc doc in docs)
            {
                if (doc.ClassId == "1")
                {
                    foreach (Match match in Regex.Matches(doc.Text, @"- component: \{fileID: (-?\d+)\}"))
                        if (!byId.ContainsKey(match.Groups[1].Value))
                            problems.Add("componente inexistente " + match.Groups[1].Value + " no GameObject " + doc.FileId);
                }

                if (doc.ClassId == "4" || doc.ClassId == "222" || doc.ClassId == "224" ||
                    doc.ClassId == "225" || doc.ClassId == "114")
                {
                    Match gameObject = Regex.Match(doc.Text, @"m_GameObject: \{fileID: (-?\d+)\}");
                    if (gameObject.Success && !byId.ContainsKey(gameObject.Groups[1].Value))
                        problems.Add("m_GameObject inexistente " + gameObject.Groups[1].Value + " no documento " + doc.FileId);
                }

                if (doc.ClassId != "4" && doc.ClassId != "224") continue;

                Match father = Regex.Match(doc.Text, @"m_Father: \{fileID: (-?\d+)\}");
                if (father.Success && father.Groups[1].Value != "0" && !byId.ContainsKey(father.Groups[1].Value))
                    problems.Add("órfão: m_Father inexistente " + father.Groups[1].Value + " no documento " + doc.FileId);

                int childrenStart = doc.Text.IndexOf("m_Children:", System.StringComparison.Ordinal);
                int fatherStart = doc.Text.IndexOf("m_Father:", System.StringComparison.Ordinal);
                if (childrenStart < 0 || fatherStart < childrenStart) continue;

                string childrenBlock = doc.Text.Substring(childrenStart, fatherStart - childrenStart);
                foreach (Match match in Regex.Matches(childrenBlock, @"\{fileID: (-?\d+)\}"))
                {
                    string childId = match.Groups[1].Value;
                    if (!byId.ContainsKey(childId))
                    {
                        problems.Add("m_Children inexistente " + childId + " no documento " + doc.FileId);
                        continue;
                    }

                    Match childFather = Regex.Match(byId[childId].Text, @"m_Father: \{fileID: (-?\d+)\}");
                    if (!childFather.Success || childFather.Groups[1].Value != doc.FileId)
                        problems.Add("assimetria: " + doc.FileId + " lista " + childId + ", que não aponta de volta");
                }
            }

            return problems;
        }

        private static List<Doc> Parse(string source)
        {
            List<Doc> docs = new List<Doc>();
            MatchCollection headers = Regex.Matches(source, @"^--- !u!(\d+) &(-?\d+).*$", RegexOptions.Multiline);
            for (int index = 0; index < headers.Count; index++)
            {
                int start = headers[index].Index + headers[index].Length;
                int end = index + 1 < headers.Count ? headers[index + 1].Index : source.Length;
                docs.Add(new Doc
                {
                    ClassId = headers[index].Groups[1].Value,
                    FileId = headers[index].Groups[2].Value,
                    Text = source.Substring(start, end - start)
                });
            }
            return docs;
        }
    }
}
#endif
