using System;
using System.Collections.Generic;
using DragonBound.Core;
using GameShared.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    // Uses authored UI templates so combat feedback remains editable by the frontend team.
    public sealed class CombatFxView : MonoBehaviour
    {
        [SerializeField] private Image attackLineTemplate;
        [SerializeField] private Image bowProjectileTemplate;
        [SerializeField] private Image spearPierceTemplate;
        [SerializeField] private Image riderSweepTemplate;
        [SerializeField] private Image starfallWarningTemplate;
        [Header("ART_HeroCombat")]
        [SerializeField] private Image ART_EmberExplosiveFireball;
        [SerializeField] private Image ART_ShadowExecution;
        [SerializeField] private Image ART_ShadowAfterimage;
        [SerializeField] private Image ART_ExecutionSlash;
        [SerializeField] private Image ART_AbyssHarpoonWarning;
        [SerializeField] private Image ART_AbyssHarpoon;
        [SerializeField] private Image ART_HarpoonChain;
        [SerializeField] private Image ART_HarpoonPull;
        [SerializeField] private Image ART_ValkyrieWingGlow;
        [SerializeField] private Image ART_ValkyrieBodyGlow;
        [SerializeField] private Image ART_ValkyriePrimaryArrow;
        [SerializeField] private Image ART_ValkyrieSecondaryArrow;
        [SerializeField] private Image ART_ValkyrieLightFeather;
        [SerializeField] private Text damageNumberTemplate;
        [SerializeField] private Text suppliesGainTemplate;
        [SerializeField, Min(0.1f)] private float damageNumberDuration = 0.9f;
        [SerializeField] private float damageNumberVerticalOffsetPixels = 20f;

        private readonly List<ActiveFx> active = new List<ActiveFx>();
        private GreyboxLaneView lane;
        private GreyboxBoardView board;
        private IWaveRuntime runtime;
        private TeamSide side;
        private FixedBoardCanvasView fixedBoardCanvas;

        public float DamageNumberDuration => damageNumberDuration;
        public float DamageNumberVerticalOffsetPixels => damageNumberVerticalOffsetPixels;

        public void Configure(
            GreyboxLaneView laneView,
            GreyboxBoardView boardView,
            Image attackLine,
            Image bowProjectile,
            Image spearPierce,
            Image riderSweep,
            Text damageNumber,
            Text suppliesGain,
            Image starfallWarning = null)
        {
            lane = laneView;
            board = boardView;
            attackLineTemplate = attackLine;
            bowProjectileTemplate = bowProjectile;
            spearPierceTemplate = spearPierce;
            riderSweepTemplate = riderSweep;
            starfallWarningTemplate = starfallWarning;
            damageNumberTemplate = damageNumber;
            suppliesGainTemplate = suppliesGain;
            DisableTemplates();
        }

        public void ConfigureStarfallWarning(Image warningTemplate)
        {
            starfallWarningTemplate = warningTemplate;
            if (starfallWarningTemplate != null)
            {
                starfallWarningTemplate.raycastTarget = false;
                starfallWarningTemplate.gameObject.SetActive(false);
            }
        }

        public void BindPresentationSources(
            GreyboxLaneView laneView,
            GreyboxBoardView boardView)
        {
            lane = laneView ?? throw new ArgumentNullException(nameof(laneView));
            board = boardView ?? throw new ArgumentNullException(nameof(boardView));
        }

        public void Initialize(TeamSide teamSide)
        {
            side = teamSide;
        }

        public void ConfigureFixedBoardCanvas(FixedBoardCanvasView canvasView)
        {
            if (canvasView == null || canvasView.CombatFxLayer == null)
            {
                return;
            }

            fixedBoardCanvas = canvasView;

            MoveTemplateToLayer(attackLineTemplate, canvasView.CombatFxLayer);
            MoveTemplateToLayer(bowProjectileTemplate, canvasView.CombatFxLayer);
            MoveTemplateToLayer(spearPierceTemplate, canvasView.CombatFxLayer);
            MoveTemplateToLayer(riderSweepTemplate, canvasView.CombatFxLayer);
            MoveTemplateToLayer(starfallWarningTemplate, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_EmberExplosiveFireball, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_ShadowExecution, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_ShadowAfterimage, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_ExecutionSlash, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_AbyssHarpoonWarning, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_AbyssHarpoon, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_HarpoonChain, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_HarpoonPull, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_ValkyrieWingGlow, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_ValkyrieBodyGlow, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_ValkyriePrimaryArrow, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_ValkyrieSecondaryArrow, canvasView.CombatFxLayer);
            MoveTemplateToLayer(ART_ValkyrieLightFeather, canvasView.CombatFxLayer);
            MoveTemplateToLayer(damageNumberTemplate, canvasView.CombatFxLayer);
            MoveTemplateToLayer(suppliesGainTemplate, canvasView.CombatFxLayer);
        }

        public void Bind(IWaveRuntime value)
        {
            if (runtime != null)
            {
                runtime.CombatEmitted -= OnCombat;
            }

            runtime = value ?? throw new ArgumentNullException(nameof(value));
            runtime.CombatEmitted += OnCombat;
        }

        private void OnDestroy()
        {
            if (runtime != null)
            {
                runtime.CombatEmitted -= OnCombat;
            }
        }

        private void Update()
        {
            if (runtime != null && !runtime.IsGameplayRunning)
            {
                return;
            }

            for (var index = active.Count - 1; index >= 0; index--)
            {
                var fx = active[index];
                if (fx.Root == null)
                {
                    active.RemoveAt(index);
                    continue;
                }

                fx.Elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(fx.Elapsed / fx.Duration);
                if (fx.Projectile)
                {
                    fx.Root.position = Vector3.Lerp(fx.Start, fx.End, normalized);
                }

                if (fx.Label != null)
                {
                    fx.Root.position = fx.Start + (Vector3.up * (normalized * 24f));
                    var color = fx.Label.color;
                    color.a = 1f - normalized;
                    fx.Label.color = color;
                }

                if (fx.Image != null && fx.Fade)
                {
                    var color = fx.Image.color;
                    color.a = 1f - normalized;
                    fx.Image.color = color;
                }

                if (fx.Elapsed < fx.Duration)
                {
                    continue;
                }

                Destroy(fx.Root.gameObject);
                active.RemoveAt(index);
            }
        }

        private void OnCombat(CombatEvent combatEvent)
        {
            if (combatEvent.Team != side || combatEvent.Leaked)
            {
                return;
            }

            if (lane == null ||
                !lane.TryGetEnemyPosition(combatEvent.TargetRuntimeId, out var target))
            {
                return;
            }
            var attacker = board != null && board.TryGetUnitPosition(combatEvent.AttackerRuntimeId, out var unitPosition)
                ? unitPosition
                : transform.position;

            switch (combatEvent.Kind)
            {
                case AttackKind.StarfallTelegraph:
                    SpawnWarning(
                        starfallWarningTemplate,
                        target,
                        combatEvent.EffectRadius,
                        combatEvent.EffectDuration);
                    return;
                case AttackKind.EmberExplosiveFireball:
                case AttackKind.EmberExplosiveSplash:
                    SpawnImage(ART_EmberExplosiveFireball ?? riderSweepTemplate, attacker, target, false, true);
                    break;
                case AttackKind.NightfangExecutionSlash:
                    SpawnImage(ART_ExecutionSlash ?? ART_ShadowExecution ?? attackLineTemplate, attacker, target, false, true, 0.16f);
                    break;
                case AttackKind.AbyssHarpoonWarning:
                    SpawnImage(
                        ART_AbyssHarpoonWarning ?? spearPierceTemplate,
                        attacker,
                        target,
                        false,
                        true,
                        Mathf.Max(0.1f, combatEvent.EffectDuration));
                    break;
                case AttackKind.AbyssHarpoonStrike:
                    SpawnImage(ART_HarpoonChain ?? ART_AbyssHarpoon ?? spearPierceTemplate, attacker, target, false, true);
                    break;
                case AttackKind.SkyhunterRadiancePrimary:
                    SpawnImage(ART_ValkyriePrimaryArrow ?? bowProjectileTemplate, attacker, target, true, false);
                    break;
                case AttackKind.SkyhunterRadianceSecondary:
                    SpawnImage(ART_ValkyrieSecondaryArrow ?? bowProjectileTemplate, attacker, target, true, false);
                    break;
                case AttackKind.BowProjectile:
                    SpawnImage(bowProjectileTemplate, attacker, target, true, false);
                    break;
                case AttackKind.SpearPierce:
                case AttackKind.LeviathanHarpoon:
                    SpawnImage(spearPierceTemplate, attacker, target, false, true);
                    break;
                case AttackKind.RiderSweep:
                    SpawnImage(riderSweepTemplate, target, target, false, true);
                    break;
                default:
                    SpawnImage(attackLineTemplate, attacker, target, false, true);
                    break;
            }

            if (combatEvent.Damage > 0 && DamageNumberSettings.Visible)
            {
                SpawnLabel(
                    damageNumberTemplate,
                    target + (Vector3.up * damageNumberVerticalOffsetPixels),
                    $"-{combatEvent.Damage:0.##}",
                    damageNumberDuration);
            }

            if (combatEvent.Killed)
            {
                SpawnLabel(suppliesGainTemplate, target + (Vector3.up * 24f), "+1 Supplies", 0.65f);
            }
        }

        private void SpawnImage(
            Image template,
            Vector3 start,
            Vector3 end,
            bool projectile,
            bool fade,
            float duration = 0.28f)
        {
            if (template == null)
            {
                return;
            }

            var image = Instantiate(template, template.transform.parent);
            image.gameObject.SetActive(true);
            var rect = image.rectTransform;
            var distance = Vector3.Distance(start, end);
            if (projectile)
            {
                rect.position = start;
                rect.sizeDelta = Vector2.one * 22f;
            }
            else if (template == riderSweepTemplate)
            {
                rect.position = end;
                rect.sizeDelta = Vector2.one * 132f;
            }
            else
            {
                rect.position = (start + end) * 0.5f;
                rect.sizeDelta = new Vector2(Mathf.Max(8f, distance), 7f);
                var angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            active.Add(new ActiveFx(rect, image, null, start, end, projectile, fade, duration));
        }

        private void SpawnWarning(Image template, Vector3 position, float radiusCells, float duration)
        {
            if (template == null)
            {
                return;
            }

            var warning = Instantiate(template, template.transform.parent);
            warning.gameObject.SetActive(true);
            warning.raycastTarget = false;
            var cellSize = fixedBoardCanvas != null
                ? Mathf.Min(fixedBoardCanvas.CellSize.x, fixedBoardCanvas.CellSize.y)
                : 64f;
            var diameter = Mathf.Max(20f, radiusCells * cellSize * 2f);
            warning.rectTransform.position = position;
            warning.rectTransform.sizeDelta = Vector2.one * diameter;
            active.Add(new ActiveFx(
                warning.rectTransform,
                warning,
                null,
                position,
                position,
                false,
                true,
                Mathf.Max(0.1f, duration)));
        }

        private void SpawnLabel(Text template, Vector3 position, string value, float duration)
        {
            if (template == null)
            {
                return;
            }

            var label = Instantiate(template, template.transform.parent);
            label.gameObject.SetActive(true);
            label.text = value;
            label.rectTransform.position = position;
            active.Add(new ActiveFx(label.rectTransform, null, label, position, position, false, false, duration));
        }

        private void DisableTemplates()
        {
            if (attackLineTemplate != null) attackLineTemplate.gameObject.SetActive(false);
            if (bowProjectileTemplate != null) bowProjectileTemplate.gameObject.SetActive(false);
            if (spearPierceTemplate != null) spearPierceTemplate.gameObject.SetActive(false);
            if (riderSweepTemplate != null) riderSweepTemplate.gameObject.SetActive(false);
            if (starfallWarningTemplate != null) starfallWarningTemplate.gameObject.SetActive(false);
            if (ART_EmberExplosiveFireball != null) ART_EmberExplosiveFireball.gameObject.SetActive(false);
            if (ART_ShadowExecution != null) ART_ShadowExecution.gameObject.SetActive(false);
            if (ART_ShadowAfterimage != null) ART_ShadowAfterimage.gameObject.SetActive(false);
            if (ART_ExecutionSlash != null) ART_ExecutionSlash.gameObject.SetActive(false);
            if (ART_AbyssHarpoonWarning != null) ART_AbyssHarpoonWarning.gameObject.SetActive(false);
            if (ART_AbyssHarpoon != null) ART_AbyssHarpoon.gameObject.SetActive(false);
            if (ART_HarpoonChain != null) ART_HarpoonChain.gameObject.SetActive(false);
            if (ART_HarpoonPull != null) ART_HarpoonPull.gameObject.SetActive(false);
            if (ART_ValkyrieWingGlow != null) ART_ValkyrieWingGlow.gameObject.SetActive(false);
            if (ART_ValkyrieBodyGlow != null) ART_ValkyrieBodyGlow.gameObject.SetActive(false);
            if (ART_ValkyriePrimaryArrow != null) ART_ValkyriePrimaryArrow.gameObject.SetActive(false);
            if (ART_ValkyrieSecondaryArrow != null) ART_ValkyrieSecondaryArrow.gameObject.SetActive(false);
            if (ART_ValkyrieLightFeather != null) ART_ValkyrieLightFeather.gameObject.SetActive(false);
            if (damageNumberTemplate != null) damageNumberTemplate.gameObject.SetActive(false);
            if (suppliesGainTemplate != null) suppliesGainTemplate.gameObject.SetActive(false);
        }

        private static void MoveTemplateToLayer(Graphic template, RectTransform layer)
        {
            if (template != null && layer != null && template.transform.parent != layer)
            {
                template.transform.SetParent(layer, false);
            }
        }

        private sealed class ActiveFx
        {
            public ActiveFx(
                RectTransform root,
                Image image,
                Text label,
                Vector3 start,
                Vector3 end,
                bool projectile,
                bool fade,
                float duration)
            {
                Root = root;
                Image = image;
                Label = label;
                Start = start;
                End = end;
                Projectile = projectile;
                Fade = fade;
                Duration = duration;
            }

            public RectTransform Root { get; }
            public Image Image { get; }
            public Text Label { get; }
            public Vector3 Start { get; }
            public Vector3 End { get; }
            public bool Projectile { get; }
            public bool Fade { get; }
            public float Duration { get; }
            public float Elapsed { get; set; }
        }
    }
}
