using System;
using System.Collections.Generic;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Tuning autorado de entidades para uma cena/Noite específica. Não contém
    /// progresso de runtime e nunca é modificado pelo EntityDirector.
    /// </summary>
    [CreateAssetMenu(fileName = "NIGHT_Entities", menuName = "Whispers/Entities/Night Entity Profile")]
    public sealed class NightEntityProfile : ScriptableObject
    {
        [Tooltip("ID de autoria e diagnóstico do perfil noturno.")]
        public string profileId;

        [Tooltip("Ordem estável de avaliação das entidades em cada tick global.")]
        public EntityNightEntry[] entries = Array.Empty<EntityNightEntry>();

        public EntityNightEntry FindEntry(string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityId) || entries == null) return null;
            foreach (EntityNightEntry entry in entries)
            {
                if (entry == null || entry.entityDefinition == null) continue;
                if (string.Equals(entry.entityDefinition.entityId, entityId, StringComparison.Ordinal)) return entry;
            }
            return null;
        }

        /// <summary>Validação pura de dados usada pelo boot, sem alterar o asset.</summary>
        public void CollectValidation(ISet<string> sceneViewNodeIds, List<string> errors, List<string> warnings)
        {
            if (errors == null) return;
            if (string.IsNullOrWhiteSpace(profileId)) errors.Add("NightEntityProfile.profileId está vazio.");
            if (entries == null || entries.Length == 0)
            {
                errors.Add("NightEntityProfile não possui entradas de entidade.");
                return;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> globalAnchorOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            int predatorCount = 0;
            int voyeurCount = 0;
            foreach (EntityNightEntry entry in entries)
            {
                if (entry == null)
                {
                    errors.Add("NightEntityProfile contém uma entrada nula.");
                    continue;
                }

                EntityDefinition definition = entry.entityDefinition;
                if (definition == null)
                {
                    errors.Add("NightEntityProfile possui entrada sem EntityDefinition.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(definition.entityId))
                {
                    errors.Add("EntityDefinition sem entityId no NightEntityProfile.");
                    continue;
                }
                if (!ids.Add(definition.entityId))
                    errors.Add("entityId duplicado no NightEntityProfile: '" + definition.entityId + "'.");

                if (definition is PredatorDefinition)
                {
                    predatorCount++;
                    if (definition.allowedAnchorCategory != AudioAnchorCategory.Door)
                        errors.Add("PredatorDefinition '" + definition.entityId + "' deve fixar a categoria Door.");
                }
                else if (definition is VoyeurDefinition)
                {
                    voyeurCount++;
                    if (definition.allowedAnchorCategory != AudioAnchorCategory.Window)
                        errors.Add("VoyeurDefinition '" + definition.entityId + "' deve fixar a categoria Window.");
                }
                else errors.Add("O primeiro NightEntityProfile aceita somente PredatorDefinition e VoyeurDefinition.");

                entry.CollectValidation(sceneViewNodeIds, errors, warnings);
                CollectGlobalAnchorIdValidation(entry, definition, globalAnchorOwners, errors);
            }

            // O VS5 tem exatamente as duas entidades aprovadas; expansões futuras
            // poderão flexibilizar esta regra junto de novos cards de conteúdo.
            if (predatorCount != 1)
                errors.Add("NightEntityProfile exige exatamente uma PredatorDefinition no primeiro slice (encontradas: " + predatorCount + ").");
            if (voyeurCount != 1)
                errors.Add("NightEntityProfile exige exatamente uma VoyeurDefinition no primeiro slice (encontradas: " + voyeurCount + ").");
        }

        /// <summary>
        /// A reserva do EntityDirector é global e indexada pelo id do anchor. Logo,
        /// assets diferentes com o mesmo id também conflitam e precisam ser recusados
        /// no boot, inclusive quando pertencem a categorias distintas.
        /// </summary>
        private static void CollectGlobalAnchorIdValidation(EntityNightEntry entry, EntityDefinition definition,
            Dictionary<string, string> globalAnchorOwners, List<string> errors)
        {
            if (entry == null || definition == null || entry.allowedAnchors == null) return;
            foreach (AudioAnchorDefinition anchor in entry.allowedAnchors)
            {
                if (anchor == null || string.IsNullOrWhiteSpace(anchor.id)) continue;
                string previousOwner;
                if (globalAnchorOwners.TryGetValue(anchor.id, out previousOwner))
                {
                    if (!string.Equals(previousOwner, definition.entityId, StringComparison.Ordinal))
                    {
                        errors.Add("Anchor com id global duplicado: '" + anchor.id + "' é usado por '" +
                                   previousOwner + "' e '" + definition.entityId + "'.");
                    }
                    continue;
                }
                globalAnchorOwners.Add(anchor.id, definition.entityId);
            }
        }
    }

    [Serializable]
    public sealed class EntityNightEntry
    {
        public EntityDefinition entityDefinition;

        [Tooltip("Anchors elegíveis para ciclos de ataque desta entidade nesta Noite.")]
        public AudioAnchorDefinition[] allowedAnchors = Array.Empty<AudioAnchorDefinition>();

        [Tooltip("AI Level para 12 AM, 1 AM, 2 AM, 3 AM, 4 AM e 5 AM. Valores válidos: 0..20.")]
        public int[] aiLevelByHour = new int[6];

        [Header("Timers comuns (segundos reais)")]
        [Min(0f)] public float nearToCriticalSeconds = 8f;
        [Min(0f)] public float criticalToTerminalSeconds = 10f;
        [Min(0f)] public float resolvedCooldownSeconds = 30f;
        [Min(0f)] public float terminalCountdownSeconds = 5f;

        [Header("Predator (usado somente por PredatorDefinition)")]
        [Tooltip("Segundos para começar a segurar a maçaneta depois que a luta começa.")]
        [Min(0f)] public float predatorResolveStartWindowSeconds = 2f;
        [Tooltip("Tempo de vitória acumulado só enquanto o botão esquerdo está na maçaneta.")]
        [Min(0f)] public float predatorResolveHoldSeconds = 4f;
        [Tooltip("Fechamento alvo da luta. 100 = porta encostada.")]
        [Range(0f, 100f)] public float predatorStruggleClosedPercent = 75f;
        [Tooltip("Oscilação em torno do alvo, em pontos percentuais de fechamento.")]
        [Min(0f)] public float predatorStruggleVariationPercent = 10f;
        [Tooltip("Abaixo deste fechamento, segurar de novo fecha a porta de volta ao alvo.")]
        [Range(0f, 100f)] public float predatorRecoverBelowClosedPercent = 65f;
        [Tooltip("Morte se o fechamento ficar estritamente abaixo deste valor.")]
        [Range(0f, 100f)] public float predatorDeathBelowClosedPercent = 25f;
        [Tooltip("Pontos de fechamento por segundo em direção ao alvo, enquanto segura e a porta ainda está mais fechada.")]
        [Min(0f)] public float predatorHoldOpenSpeed = 8f;
        [Tooltip("Pontos de fechamento por segundo que a porta abre com a mão fora.")]
        [Min(0f)] public float predatorReleaseOpenSpeed = 22f;
        [Tooltip("Pontos de fechamento por segundo ao voltar ao alvo depois de passar do limite de recuperação.")]
        [Min(0f)] public float predatorRecoverCloseSpeed = 18f;
        [Tooltip("Frequência da oscilação em torno do alvo, em Hz.")]
        [Min(0f)] public float predatorWobbleSpeed = 0.35f;
        [Tooltip("Intervalo entre batidas enquanto o jogador segura. 0 dispara uma batida por frame.")]
        [Min(0f)] public float predatorBangIntervalSeconds = 2.5f;
        [Tooltip("Distância extra, em pixels, além da borda da maçaneta.")]
        [Min(0f)] public float predatorBangExtraPixels = 80f;
        [Tooltip("Duração do empurrão do cursor. 0 é teleporte.")]
        [Min(0f)] public float predatorBangShoveSeconds = 0.12f;
        [Tooltip("Intensidade repassada a ViewCameraController.Shake.")]
        [Min(0f)] public float predatorBangShakeIntensity = 0.08f;

        [Header("Voyeur (usado somente por VoyeurDefinition)")]
        [Min(0f)] public float voyeurLightContactRequiredSeconds = 5f;

        public PredatorStruggleConfig ToStruggleConfig()
        {
            return new PredatorStruggleConfig
            {
                startWindowSeconds = predatorResolveStartWindowSeconds,
                holdDurationSeconds = predatorResolveHoldSeconds,
                struggleClosedPercent = predatorStruggleClosedPercent,
                variationPercent = predatorStruggleVariationPercent,
                recoverBelowClosedPercent = predatorRecoverBelowClosedPercent,
                deathBelowClosedPercent = predatorDeathBelowClosedPercent,
                holdOpenSpeed = predatorHoldOpenSpeed,
                releaseOpenSpeed = predatorReleaseOpenSpeed,
                recoverCloseSpeed = predatorRecoverCloseSpeed,
                wobbleSpeed = predatorWobbleSpeed,
                bangIntervalSeconds = predatorBangIntervalSeconds
            };
        }

        private void CollectPredatorStruggleValidation(List<string> errors)
        {
            if (predatorStruggleVariationPercent < 0f || predatorHoldOpenSpeed < 0f || predatorReleaseOpenSpeed < 0f ||
                predatorRecoverCloseSpeed < 0f || predatorWobbleSpeed < 0f || predatorBangIntervalSeconds < 0f ||
                predatorBangShoveSeconds < 0f || predatorBangExtraPixels < 0f || predatorBangShakeIntensity < 0f)
            {
                errors.Add("Tuning de luta do Predator '" + entityDefinition.entityId + "' contém valor negativo.");
            }
            if (predatorDeathBelowClosedPercent < 0f ||
                predatorDeathBelowClosedPercent >= predatorRecoverBelowClosedPercent)
            {
                errors.Add("Limite de morte do Predator '" + entityDefinition.entityId +
                           "' deve ser menor que o limite de recuperação.");
            }
            if (predatorRecoverBelowClosedPercent >= predatorStruggleClosedPercent)
            {
                errors.Add("Recuperação do Predator '" + entityDefinition.entityId +
                           "' deve ficar abaixo do alvo da luta.");
            }
            if (predatorStruggleClosedPercent - predatorStruggleVariationPercent < 0f ||
                predatorStruggleClosedPercent + predatorStruggleVariationPercent > 100f)
            {
                errors.Add("Faixa de oscilação do Predator '" + entityDefinition.entityId + "' sai de 0..100.");
            }
        }

        public int GetAiLevel(int hourIndex)
        {
            if (aiLevelByHour == null || aiLevelByHour.Length != 6) return 0;
            return Mathf.Clamp(aiLevelByHour[Mathf.Clamp(hourIndex, 0, 5)], 0, 20);
        }

        public void CollectValidation(ISet<string> sceneViewNodeIds, List<string> errors, List<string> warnings)
        {
            if (entityDefinition == null) return;
            if (aiLevelByHour == null || aiLevelByHour.Length != 6)
            {
                errors.Add("Entidade '" + entityDefinition.entityId + "' precisa de exatamente seis AI Levels.");
            }
            else
            {
                for (int index = 0; index < aiLevelByHour.Length; index++)
                {
                    if (aiLevelByHour[index] < 0 || aiLevelByHour[index] > 20)
                        errors.Add("AI Level inválido para '" + entityDefinition.entityId + "' na hora " + index + ": " + aiLevelByHour[index] + ".");
                }
            }

            if (allowedAnchors == null || allowedAnchors.Length == 0)
            {
                errors.Add("Entidade '" + entityDefinition.entityId + "' não possui anchors permitidos.");
            }
            else
            {
                HashSet<string> anchorIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (AudioAnchorDefinition anchor in allowedAnchors)
                {
                    if (anchor == null)
                    {
                        errors.Add("Entidade '" + entityDefinition.entityId + "' possui anchor nulo.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(anchor.id))
                    {
                        errors.Add("Entidade '" + entityDefinition.entityId + "' possui anchor com id vazio.");
                        continue;
                    }
                    if (!anchorIds.Add(anchor.id))
                        errors.Add("Anchor duplicado para '" + entityDefinition.entityId + "': '" + anchor.id + "'.");
                    if (anchor.category != entityDefinition.allowedAnchorCategory)
                        errors.Add("Anchor '" + anchor.id + "' possui categoria " + anchor.category +
                                   ", incompatível com '" + entityDefinition.entityId + "'.");
                    if (string.IsNullOrWhiteSpace(anchor.encounterViewNodeId))
                        errors.Add("Anchor '" + anchor.id + "' não possui encounterViewNodeId.");
                    else if (sceneViewNodeIds == null || !sceneViewNodeIds.Contains(anchor.encounterViewNodeId))
                        errors.Add("Anchor '" + anchor.id + "' aponta para ViewNode inexistente: '" + anchor.encounterViewNodeId + "'.");
                }
            }

            if (nearToCriticalSeconds < 0f || criticalToTerminalSeconds < 0f ||
                resolvedCooldownSeconds < 0f || terminalCountdownSeconds < 0f)
            {
                errors.Add("Timers comuns negativos para '" + entityDefinition.entityId + "'.");
            }
            if (entityDefinition is PredatorDefinition &&
                (predatorResolveStartWindowSeconds < 0f || predatorResolveHoldSeconds < 0f))
            {
                errors.Add("Timers de resolução do Predator inválidos.");
            }
            if (entityDefinition is PredatorDefinition)
                CollectPredatorStruggleValidation(errors);
            if (entityDefinition is VoyeurDefinition && voyeurLightContactRequiredSeconds < 0f)
                errors.Add("Timer de resolução do Voyeur inválido.");

            if (entityDefinition.stateAudio == null || entityDefinition.stateAudio.Length == 0)
                warnings?.Add("Entidade '" + entityDefinition.entityId + "' ainda não possui apresentação de áudio autorada.");
            if (entityDefinition.jumpscare == null ||
                (entityDefinition.jumpscare.fullscreenSprite == null && entityDefinition.jumpscare.jumpscareSfx == null))
            {
                warnings?.Add("Entidade '" + entityDefinition.entityId + "' ainda não possui mídia de jumpscare autorada.");
            }
        }
    }
}
