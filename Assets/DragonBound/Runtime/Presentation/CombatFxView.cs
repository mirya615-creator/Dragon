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
        private const string StarfallNormalSpritePath =
            "VFX/Starfall Archmage/road/微信图片_20260831170133_175_101";
        private const string StarfallExplosionControllerPath = "Animation/StarfallArchmageBoom";
        private const string NightfangExplosionControllerPath = "Animation/NightfangAssassinBoom";
        private const string FlameDrakeNormalFireballSpritePath = "VFX/Flame Drake Rider/road";
        private const string FlameDrakeSkillFireballSpritePath = "VFX/Flame Drake Rider/Sroad";
        private const string FlameDrakeNormalExplosionControllerPath = "Animation/Flame Drake Rider Boom";
        private const string FlameDrakeSkillExplosionControllerPath = "Animation/FlameDrakeRiderBoomS";
        private const string FlameDrakeBurningGroundControllerPath = "Animation/boomBoard";
        private const float FlameDrakeBurningGroundTileSize = 110f;
        private const string BowSwordProjectileSpritePath = "VFX/Unit/road";
        private const string SkyborneValkyrieArrowSpritePath = "VFX/Skyborne Valkyrie/road";
        private const string SkyborneValkyrieExplosionControllerPath = "Animation/SkyborneValkyrieBoom";
        private const string WindclawImpactSpritePath = "VFX/Windclaw Ranger/road";
        private const string WindclawNormalVfxControllerPath =
            "Animations/Hero/Windclaw Ranger/normal/VFX/Windclaw Ranger norVFX";
        private const string EmberShamanFireballSpritePath = "VFX/Ember Shaman/road";
        private const string RuneboltMageBoltControllerPath = "Animation/Runebolt MageBoom";
        private const string RuneboltMageNormalProjectileControllerPathV2 =
            "Animations/Hero/Runebolt Mage/normal/VFX/RM nor Projectile";
        private const string RuneboltMageNormalImpactControllerPathV2 =
            "Animations/Hero/Runebolt Mage/normal/VFX/RM nor Impact";

        private const string StoneboundWarlockNormalRockSpritePath =
            "VFX/Stonebound Warlock/rood";
        private const string StoneboundWarlockSkillRockSpritePath =
            "VFX/Stonebound Warlock/roodS";
        private const string ThunderlordMainChainSpritePath = "VFX/Thunderlord/road/Main";
        private const string ThunderlordFirstChainSpritePath = "VFX/Thunderlord/road/Froad";
        private const string ThunderlordSecondChainSpritePath = "VFX/Thunderlord/road/Sroad";
        private const string ThunderlordExplosionControllerPath = "Animation/ThunderlordBoom";
        private const string AbyssalHarpoonChainSpritePath = "VFX/Abyssal Harpooner/road";
        private const string AbyssalHarpoonHookSpritePath = "VFX/Abyssal Harpooner/boom";
        private const string AbyssalHarpoonPortalSpritePath = "VFX/Abyssal Harpooner/start";
        private const float AbyssalHarpoonVisualLength = 660f;
        private const float AbyssalHarpoonCellSize = 110f;
        private const float AbyssalHarpoonChainHeight = 16f;
        private const float AbyssalHarpoonOriginForwardOffset = 44f;
        private static readonly Vector2 AbyssalHarpoonHookSize = new Vector2(56f, 33.25f);
        private static readonly Vector2 AbyssalHarpoonPortalSize = new Vector2(72f, 110f);

        private Sprite windclawImpactSprite;
        private RuntimeAnimatorController windclawNormalVfxController;

        [SerializeField] private Image attackLineTemplate;
        [SerializeField] private Image bowProjectileTemplate;
        [SerializeField] private Image spearPierceTemplate;
        [SerializeField] private Image riderSweepTemplate;
        [SerializeField] private Image itemImpactTemplate;
        [SerializeField] private Image starfallWarningTemplate;
        [Header("ART_HeroCombat")]
        [SerializeField] private Image ART_EmberExplosiveFireball;
        [SerializeField] private Image ART_FlameDrakeRiderDive;
        [SerializeField, Min(0f)] private float flameDrakeRiderDiveOvershoot = 300f;
        [SerializeField, Min(0.05f)] private float flameDrakeFireballMinTravelDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float flameDrakeFireballMaxTravelDuration = 0.32f;
        [SerializeField, Min(0f)] private float flameDrakeFireballArcHeight = 42f;
        [SerializeField, Min(0.1f)] private float flameDrakeReleaseFallbackDelay = 0.42f;
        [SerializeField] private Vector2 flameDrakeNormalFireballSize = new Vector2(68f, 42f);
        [SerializeField] private Vector2 flameDrakeSkillFireballSize = new Vector2(92f, 58f);
        [SerializeField] private Vector2 flameDrakeNormalExplosionSize = new Vector2(105f, 105f);
        [SerializeField] private Vector2 flameDrakeSkillExplosionSize = new Vector2(145f, 145f);
        [SerializeField, Min(0.1f)] private float flameDrakeBurningGroundDuration = 3f;
        [SerializeField] private Vector2 bowSwordProjectileSize = new Vector2(48f, 41f);
        [SerializeField, Min(0f)] private float bowSwordProjectileArcHeight = 54f;
        [SerializeField] private float bowSwordProjectileRotationOffset = -135f;
        [SerializeField, Min(0.05f)] private float bowSwordMinTravelDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float bowSwordMaxTravelDuration = 0.30f;
        [SerializeField, Min(0.1f)] private float bowReleaseFallbackDelay = 0.45f;
        [SerializeField] private Vector2 skyborneValkyrieArrowSize = new Vector2(68f, 29f);
        [SerializeField, Min(0f)] private float skyborneValkyrieArrowArcHeight = 54f;
        // The authored arrow sprite points toward local -X. Rotate it half a turn so
        // its tip follows the sampled projectile tangent instead of facing backward.
        [SerializeField] private float skyborneValkyrieArrowRotationOffset = 180f;
        [SerializeField, Min(0.05f)] private float skyborneValkyrieMinTravelDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float skyborneValkyrieMaxTravelDuration = 0.30f;
        [SerializeField, Min(0.1f)] private float skyborneValkyrieReleaseFallbackDelay = 0.38f;
        [SerializeField] private Vector2 skyborneValkyrieExplosionSize = new Vector2(90f, 90f);
        [SerializeField] private Vector2 windclawImpactSize = new Vector2(110f, 110f);
        [SerializeField] private Vector2 windclawNormalVfxSize = new Vector2(110f, 110f);
        [SerializeField, Min(0.1f)] private float windclawReleaseFallbackDelay = 0.3f;
        [SerializeField] private Vector2 emberShamanFireballSize = new Vector2(72f, 32f);
        [SerializeField] private Vector2 emberShamanSplashFireballSize = new Vector2(54f, 24f);
        [SerializeField, Min(0f)] private float emberShamanFireballArcHeight = 42f;
        [SerializeField, Min(0.05f)] private float emberShamanFireballMinTravelDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float emberShamanFireballMaxTravelDuration = 0.30f;
        [SerializeField, Min(0.1f)] private float emberShamanReleaseFallbackDelay = 0.34f;
        // V1 art (1080x1080 canvas, stroke occupies only 2.2%-8.3% of the height) keeps the original value.  
        private const float RuneboltMagePathVisualHeightV1 = 300f;
        // V2 art (150x72 canvas, stroke occupies ~54%-61% of the height): 0.55 cell.  
        // 36 was too thin at small board scale; 60 keeps the beam readable without covering the row.  
        private const float RuneboltMagePathVisualHeightV2 = 100f;
        // V2 stroke never reaches the canvas edges: even the fullest frame ends at x142/150 and the  
        // last frame at x136/150 (~9.3% blank on each side). Divide the gameplay length by this  
        // ratio so the \*visible\* stroke actually lands on the enemy. Visual only.  
        private const float RuneboltMagePathVisualPaddingRatio = 0.9f;
        private const float RuneboltMageMaximumPathLength = 550f;
        [SerializeField, Min(0f)] private float runeboltMagePathVisualOvershoot = 55f;
        [SerializeField, Min(0f)] private float runeboltMagePathHoldDuration = 0.12f;
        [SerializeField] private Vector2 runeboltMageImpactSize = new Vector2(72f, 72f);
        [SerializeField, Min(0.01f)] private float runeboltMageImpactStateSpeed = 1f;
        [SerializeField, Min(0.01f)] private float runeboltMagePathFadeDuration = 0.08f;
        [SerializeField, Min(0.1f)] private float runeboltMageReleaseFallbackDelay = 0.38f;
        [SerializeField] private Vector2 stoneboundWarlockNormalRockSize = new Vector2(48f, 48f);
        [SerializeField] private Vector2 stoneboundWarlockSkillRockSize = new Vector2(88f, 88f);
        [SerializeField, Min(0f)] private float stoneboundWarlockNormalArcHeight = 48f;
        [SerializeField, Min(0f)] private float stoneboundWarlockSkillArcHeight = 72f;
        [SerializeField, Min(0.05f)] private float stoneboundWarlockMinTravelDuration = 0.20f;
        [SerializeField, Min(0.05f)] private float stoneboundWarlockMaxTravelDuration = 0.34f;
        [SerializeField, Min(0.1f)] private float stoneboundWarlockReleaseFallbackDelay = 0.36f;
        [SerializeField, Min(1f)] private float thunderlordMainChainHeight = 48f;
        [SerializeField, Min(1f)] private float thunderlordFirstChainHeight = 38f;
        [SerializeField, Min(1f)] private float thunderlordSecondChainHeight = 30f;
        [SerializeField, Min(0f)] private float thunderlordChainJumpDelay = 0.04f;
        [SerializeField, Min(0.01f)] private float thunderlordChainVisibleDuration = 0.16f;
        [SerializeField, Min(0.1f)] private float thunderlordReleaseFallbackDelay = 0.30f;
        [SerializeField] private Vector2 thunderlordExplosionSize = new Vector2(550f, 550f);
        [SerializeField, Min(0.1f)] private float thunderlordExplosionDuration = 0.9f;
        [SerializeField, Min(0.1f)] private float thunderlordSkillReleaseFallbackDelay = 0.30f;
        [SerializeField, Min(0.05f)] private float abyssalHarpoonExtendDuration = 0.22f;
        [SerializeField, Min(0f)] private float abyssalHarpoonHoldDuration = 0.08f;
        [SerializeField, Min(0.05f)] private float abyssalHarpoonRetractDuration = 0.16f;
        [SerializeField, Min(0.1f)] private float abyssalHarpoonReleaseFallbackDelay = 0.40f;
        [SerializeField, Min(0.1f)] private float abyssalHarpoonSkillReleaseFallbackDelay = 0.34f;
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
        [Header("ART_StarfallArchmage_Normal")]
        [SerializeField] private Sprite ART_StarfallNormalSprite;
        [SerializeField, Min(1f)] private float starfallGemSize = 18f;
        [SerializeField, Min(0f)] private float starfallGemArcHeight = 48f;
        // The authored gem points toward local -X, opposite to the trajectory helper.
        [SerializeField] private float starfallGemRotationOffset = 180f;
        [SerializeField, Min(0.05f)] private float starfallGemMinTravelDuration = 0.18f;
        [SerializeField, Min(0.05f)] private float starfallGemMaxTravelDuration = 0.32f;
        [SerializeField, Min(0.1f)] private float starfallGemReleaseFallbackDelay = 0.42f;
        [SerializeField] private Vector2 starfallSkillExplosionSize = new Vector2(145f, 145f);
        [SerializeField, Min(0.1f)] private float nightfangSkillReleaseFallbackDelay = 0.45f;
        [SerializeField] private Vector2 nightfangExplosionSize = new Vector2(100f, 100f);
        [SerializeField, Min(0.05f)] private float starfallNormalTravelDuration = 0.4f;
        [SerializeField, Min(1f)] private float starfallNormalHeadSize = 18f;
        [SerializeField, Min(0.01f)] private float starfallNormalTrailInterval = 0.035f;
        [SerializeField, Min(0.05f)] private float starfallNormalTrailLifetime = 0.28f;
        [SerializeField, Min(1f)] private float starfallNormalTrailSize = 6f;
        [SerializeField, Range(1, 16)] private int starfallNormalImpactParticleCount = 10;
        [SerializeField, Min(1f)] private float starfallNormalImpactRadius = 18f;
        [SerializeField] private Color starfallNormalHeadColor = Color.white;
        [SerializeField] private Color starfallNormalTrailColor = new Color(0.88f, 0.66f, 1f, 0.72f);
        [SerializeField, Min(1f)] private float starfallRibbonWidth = 10f;
        [SerializeField, Min(0.05f)] private float starfallRibbonLifetime = 0.24f;
        [SerializeField] private Color starfallRibbonHeadColor = new Color(0.96f, 0.86f, 1f, 0.82f);
        [SerializeField] private Color starfallRibbonTailColor = new Color(0.35f, 0.08f, 0.82f, 0f);
        [SerializeField] private Text damageNumberTemplate;
        [SerializeField] private Text suppliesGainTemplate;
        [SerializeField, Min(0.1f)] private float damageNumberDuration = 0.9f;
        [SerializeField] private float damageNumberVerticalOffsetPixels = 20f;

        private readonly List<ActiveFx> active = new List<ActiveFx>();
        private readonly List<StarfallUiParticleEffect> activeStarfallEffects =
            new List<StarfallUiParticleEffect>();
        private GreyboxLaneView lane;
        private GreyboxBoardView board;
        private IWaveRuntime runtime;
        private TeamSide side;
        private FixedBoardCanvasView fixedBoardCanvas;
        private readonly Dictionary<string, int> dragonRiderDiveFrameByAttacker =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> starfallNormalFrameByAttacker =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> facingFrameByAttacker =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingFlameDrakeCast> pendingFlameDrakeCasts =
            new Dictionary<string, PendingFlameDrakeCast>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingFlameDrakeCast> pendingFlameDrakeSkillCasts =
            new Dictionary<string, PendingFlameDrakeCast>(StringComparer.Ordinal);
        private readonly Dictionary<string, Queue<PendingBowCast>> pendingBowCasts =
            new Dictionary<string, Queue<PendingBowCast>>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingSkyborneValkyrieCast> pendingSkyborneValkyrieCasts =
            new Dictionary<string, PendingSkyborneValkyrieCast>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingStarfallCast> pendingStarfallNormalCasts =
            new Dictionary<string, PendingStarfallCast>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingStarfallCast> pendingStarfallSkillCasts =
            new Dictionary<string, PendingStarfallCast>(StringComparer.Ordinal);
        private readonly Dictionary<string, Queue<PendingNightfangSkillCast>> pendingNightfangSkillCasts =
            new Dictionary<string, Queue<PendingNightfangSkillCast>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Queue<PendingWindclawSkillCast>> pendingWindclawSkillCasts =
            new Dictionary<string, Queue<PendingWindclawSkillCast>>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingEmberShamanCast> pendingEmberShamanCasts =
            new Dictionary<string, PendingEmberShamanCast>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingRuneboltMageCast> pendingRuneboltMageCasts =
            new Dictionary<string, PendingRuneboltMageCast>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingStoneboundWarlockCast> pendingStoneboundWarlockCasts =
            new Dictionary<string, PendingStoneboundWarlockCast>(StringComparer.Ordinal);
        private readonly HashSet<string> frostcrownMarkedEnemyIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingThunderlordChainCast> pendingThunderlordChainCasts =
            new Dictionary<string, PendingThunderlordChainCast>(StringComparer.Ordinal);
        private readonly List<ActiveThunderlordChain> activeThunderlordChains =
            new List<ActiveThunderlordChain>();
        private readonly Dictionary<string, PendingThunderlordSkillCast> pendingThunderlordSkillCasts =
            new Dictionary<string, PendingThunderlordSkillCast>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingAbyssalHarpoonCast> pendingAbyssalHarpoonCasts =
            new Dictionary<string, PendingAbyssalHarpoonCast>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingAbyssalHarpoonSkillCast> pendingAbyssalHarpoonSkillCasts =
            new Dictionary<string, PendingAbyssalHarpoonSkillCast>(StringComparer.Ordinal);
        private readonly HashSet<string> abyssalHarpoonSkillReleaseLatches =
            new HashSet<string>(StringComparer.Ordinal);
        private GreyboxBoardView subscribedBoard;
        private Sprite flameDrakeNormalFireballSprite;
        private Sprite flameDrakeSkillFireballSprite;
        private RuntimeAnimatorController flameDrakeNormalExplosionController;
        private RuntimeAnimatorController flameDrakeSkillExplosionController;
        private RuntimeAnimatorController flameDrakeBurningGroundController;
        private Sprite bowSwordProjectileSprite;
        private Sprite skyborneValkyrieArrowSprite;
        private RuntimeAnimatorController skyborneValkyrieExplosionController;
        private RuntimeAnimatorController starfallExplosionController;
        private RuntimeAnimatorController nightfangExplosionController;
        private bool starfallSpriteWarningLogged;
        private bool flameDrakeResourceWarningLogged;
        private Sprite emberShamanFireballSprite;
        private RuntimeAnimatorController runeboltMageBoltController;
        private RuntimeAnimatorController runeboltMageNormalImpactController;
        private float runeboltMageBoltVisualHeight = -1f;
        private Sprite stoneboundWarlockNormalRockSprite;
        private Sprite stoneboundWarlockSkillRockSprite;
        private Sprite thunderlordMainChainSprite;
        private Sprite thunderlordFirstChainSprite;
        private Sprite thunderlordSecondChainSprite;
        private RuntimeAnimatorController thunderlordExplosionController;
        private Sprite abyssalHarpoonChainSprite;
        private Sprite abyssalHarpoonHookSprite;
        private Sprite abyssalHarpoonPortalSprite;
        private bool abyssalHarpoonResourceWarningLogged;

        public float DamageNumberDuration => damageNumberDuration;
        public float DamageNumberVerticalOffsetPixels => damageNumberVerticalOffsetPixels;
        public TeamSide Side => side;

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
            BindBoardReleaseEvents(boardView);
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
            BindBoardReleaseEvents(boardView ?? throw new ArgumentNullException(nameof(boardView)));
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
            MoveTemplateToLayer(itemImpactTemplate, canvasView.CombatFxLayer);
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
            lane?.SetFrostcrownMarkedEnemies(null);
            if (runtime != null)
            {
                runtime.CombatEmitted -= OnCombat;
            }
            if (subscribedBoard != null)
            {
                subscribedBoard.FlameDrakeFireballReleased -= HandleFlameDrakeFireballReleased;
                subscribedBoard.SkyborneValkyrieArrowReleased -= HandleSkyborneValkyrieArrowReleased;
                subscribedBoard.BowProjectileReleased -= HandleBowProjectileReleased;
                subscribedBoard.StarfallArchmageGemReleased -= HandleStarfallArchmageGemReleased;
                subscribedBoard.NightfangSkillAnimationCompleted -= HandleNightfangSkillAnimationCompleted;
                subscribedBoard.WindclawSkillReleased -= HandleWindclawSkillReleased;
                subscribedBoard.EmberShamanFireballReleased -= HandleEmberShamanFireballReleased;
                subscribedBoard.RuneboltMageBoltReleased -= HandleRuneboltMageBoltReleased;
                subscribedBoard.StoneboundWarlockRockReleased -= HandleStoneboundWarlockRockReleased;
                subscribedBoard.ThunderlordChainReleased -= HandleThunderlordChainReleased;
                subscribedBoard.ThunderlordSkillReleased -= HandleThunderlordSkillReleased;
                subscribedBoard.AbyssalHarpoonReleased -= HandleAbyssalHarpoonReleased;
                subscribedBoard.AbyssalHarpoonSkillReleased -= HandleAbyssalHarpoonSkillReleased;
            }
        }

        private void Update()
        {
            SyncFrostcrownHunterMarks();
            // Pending attacks must finish even when their killing blow ends the wave/match.
            // Otherwise held enemy health views can never be released or destroyed.
            TickPendingFlameDrakeCasts(pendingFlameDrakeCasts, Time.deltaTime);
            TickPendingFlameDrakeCasts(pendingFlameDrakeSkillCasts, Time.deltaTime);
            TickPendingBowCasts(Time.deltaTime);
            TickPendingSkyborneValkyrieCasts(Time.deltaTime);
            TickPendingStarfallCasts(pendingStarfallNormalCasts, Time.deltaTime);
            TickPendingStarfallCasts(pendingStarfallSkillCasts, Time.deltaTime);
            TickPendingNightfangSkillCasts(Time.deltaTime);
            TickPendingWindclawSkillCasts(Time.deltaTime);
            TickPendingEmberShamanCasts(Time.deltaTime);
            TickPendingRuneboltMageCasts(Time.deltaTime);
            TickPendingStoneboundWarlockCasts(Time.deltaTime);
            TickPendingThunderlordChainCasts(Time.deltaTime);
            TickActiveThunderlordChains(Time.deltaTime);
            TickPendingThunderlordSkillCasts(Time.deltaTime);
            TickPendingAbyssalHarpoonCasts(Time.deltaTime);
            TickPendingAbyssalHarpoonSkillCasts(Time.deltaTime);

            for (var index = activeStarfallEffects.Count - 1; index >= 0; index--)
            {
                var particleEffect = activeStarfallEffects[index];
                if (particleEffect == null)
                {
                    activeStarfallEffects.RemoveAt(index);
                    continue;
                }

                particleEffect.Tick(Time.deltaTime);
                if (!particleEffect.IsComplete)
                {
                    continue;
                }

                Destroy(particleEffect.gameObject);
                activeStarfallEffects.RemoveAt(index);
            }

            for (var index = active.Count - 1; index >= 0; index--)
            {
                var fx = active[index];
                if (fx.Root == null)
                {
                    fx.OnComplete?.Invoke();
                    active.RemoveAt(index);
                    continue;
                }

                fx.Elapsed += Time.deltaTime;
                var normalized = Mathf.Clamp01(fx.Elapsed / fx.Duration);
                if (fx.Projectile)
                {
                    var movement = fx.EaseIn ? normalized * normalized : normalized;
                    var end = fx.ResolveEnd != null ? fx.ResolveEnd() : fx.End;
                    fx.Root.position = EvaluateProjectilePosition(
                        fx.Start,
                        end,
                        movement,
                        fx.ArcHeight);
                    if (fx.OrientToTrajectory)
                    {
                        OrientToProjectileTrajectory(
                            fx.Root,
                            fx.Start,
                            end,
                            movement,
                            fx.ArcHeight,
                            fx.RotationOffset);
                    }
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
                    color.a = fx.StartAlpha * (1f - normalized);
                    fx.Image.color = color;
                }

                fx.OnProgress?.Invoke(normalized);

                if (fx.Elapsed < fx.Duration)
                {
                    continue;
                }

                fx.OnComplete?.Invoke();
                Destroy(fx.Root.gameObject);
                active.RemoveAt(index);
            }
        }

        private void SyncFrostcrownHunterMarks()
        {
            if (lane == null)
            {
                return;
            }

            frostcrownMarkedEnemyIds.Clear();
            board?.CollectFrostcrownMarkedEnemyIds(frostcrownMarkedEnemyIds);
            lane.SetFrostcrownMarkedEnemies(frostcrownMarkedEnemyIds);
        }

        private void OnCombat(CombatEvent combatEvent)
        {
            if (combatEvent.Team != side || combatEvent.Leaked)
            {
                return;
            }

            var target = Vector3.zero;
            var hasTargetPosition = lane != null &&
                                    lane.TryGetEnemyPosition(
                                        combatEvent.TargetRuntimeId,
                                        out target);
            var attacker = board != null && board.TryGetUnitPosition(combatEvent.AttackerRuntimeId, out var unitPosition)
                ? unitPosition
                : transform.position;

            if (hasTargetPosition)
            {
                FaceAttackerForCombatEvent(combatEvent, target);
            }

            if (combatEvent.DamageOwnerKind == CombatDamageOwnerKind.BasicUnit)
            {
                board?.PlayBasicUnitAttackAnimation(combatEvent.AttackerRuntimeId);
            }

            if (!hasTargetPosition)
            {
                return;
            }

            if (combatEvent.Killed)
            {
                // Runtime damage is already final at this point. Do not leave a logically
                // dead enemy frozen with its pre-impact health while the projectile catches up.
                // The projectile still uses the captured target position below.
                lane.ShowEnemyDeathVisual(combatEvent.TargetRuntimeId);
            }

            switch (combatEvent.Kind)
            {
                case AttackKind.StarfallTelegraph:
                    // ART_StarfallWarning is retained as an inactive authored template only.
                    // The runtime no longer creates ART_StarfallWarning(Clone).
                    return;
                case AttackKind.EmberExplosiveFireball:
                case AttackKind.EmberExplosiveSplash:
                    QueueEmberShamanCast(combatEvent, attacker, target);
                    return;
                case AttackKind.DragonRiderDive:
                    var firstSkillTarget = ShouldSpawnDragonRiderDive(combatEvent.AttackerRuntimeId);
                    QueueFlameDrakeCast(combatEvent, attacker, target, true);
                    if (firstSkillTarget)
                    {
                        board?.PlayHeroFormationAttackAnimation(combatEvent.AttackerRuntimeId, true);
                    }
                    return;
                case AttackKind.DragonRiderArea:
                    QueueFlameDrakeCast(combatEvent, attacker, target, false);
                    return;
                case AttackKind.NightfangExecutionSlash:
                    if (QueueNightfangSkillCast(combatEvent, target))
                    {
                        board?.PlayHeroFormationAttackAnimation(combatEvent.AttackerRuntimeId, true);
                    }
                    return;
                case AttackKind.NightfangStrike:
                    // Nightfang's basic attack intentionally has no projectile or impact VFX.
                    break;
                case AttackKind.AbyssHarpoonWarning:
                    BeginAbyssalHarpoonSkill(combatEvent, attacker, target);
                    return;
                case AttackKind.AbyssHarpoonStrike:
                    QueueAbyssalHarpoonSkillStrike(combatEvent, attacker, target);
                    return;
                case AttackKind.SkyhunterShot:
                    QueueSkyborneValkyrieCast(combatEvent, attacker, target, false);
                    return;
                case AttackKind.SkyhunterRadiancePrimary:
                case AttackKind.SkyhunterRadianceSecondary:
                    QueueSkyborneValkyrieCast(combatEvent, attacker, target, true);
                    return;
                case AttackKind.BowProjectile:
                    QueueBowCast(combatEvent, attacker, target);
                    return;
                case AttackKind.StarfallArea:
                    QueueStarfallCast(combatEvent, attacker, target, false);
                    return;
                case AttackKind.StarfallImpact:
                    if (QueueStarfallCast(combatEvent, attacker, target, true))
                    {
                        board?.PlayHeroFormationAttackAnimation(combatEvent.AttackerRuntimeId, true);
                    }
                    return;
                case AttackKind.SpearPierce:
                    // The legacy ART_SpearPierceLine presentation has been retired.
                    break;
                case AttackKind.LeviathanHarpoon:
                    QueueAbyssalHarpoonCast(combatEvent, attacker, target);
                    return;
                case AttackKind.RiderSweep:
                    // The legacy ART_RiderSweepCircle presentation has been retired.
                    // Damage and targeting are still resolved by the combat runtime.
                    break;
                case AttackKind.Item:
                    SpawnImage(itemImpactTemplate ?? attackLineTemplate, attacker, target, CombatFxPlacementMode.TemplateSizeAtTarget, true);
                    break;
                case AttackKind.Single:
                    // Basic-unit attacks intentionally have no projectile or line VFX.
                    break;
                case AttackKind.WindclawShot:
                    SpawnWindclawNormalVfx(combatEvent, target);
                    break;
                case AttackKind.WindclawPowerShot:
                    QueueWindclawSkillCast(combatEvent, target);
                    board?.PlayHeroFormationAttackAnimation(
                        combatEvent.AttackerRuntimeId,
                        true);
                    return;
                case AttackKind.RuneboltPierce:
                    QueueRuneboltMageCast(combatEvent, attacker, target);
                    return;
                case AttackKind.StonebinderShot:
                    QueueStoneboundWarlockCast(combatEvent, attacker, target);
                    return;
                case AttackKind.StoneBind:
                    UpgradeStoneboundWarlockCastToSkill(combatEvent.AttackerRuntimeId);
                    return;
                case AttackKind.ThunderJarlChain:
                    QueueThunderlordChainCast(combatEvent, attacker, target);
                    return;
                case AttackKind.ThunderDominion:
                    QueueThunderlordSkillCast(combatEvent, attacker, target);
                    return;

                default:
                    // Hero skills without authored presentation must remain visually silent.
                    // Falling back to ART_AttackLine produces an unrelated yellow route.
                    break;
            }

            if (combatEvent.Damage > 0f)
            {
                lane.PlayEnemyHitShake(combatEvent.TargetRuntimeId);
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

        private void BindBoardReleaseEvents(GreyboxBoardView value)
        {
            if (subscribedBoard != null)
            {
                subscribedBoard.FlameDrakeFireballReleased -= HandleFlameDrakeFireballReleased;
                subscribedBoard.SkyborneValkyrieArrowReleased -= HandleSkyborneValkyrieArrowReleased;
                subscribedBoard.BowProjectileReleased -= HandleBowProjectileReleased;
                subscribedBoard.StarfallArchmageGemReleased -= HandleStarfallArchmageGemReleased;
                subscribedBoard.NightfangSkillAnimationCompleted -= HandleNightfangSkillAnimationCompleted;
                subscribedBoard.WindclawSkillReleased -= HandleWindclawSkillReleased;
                subscribedBoard.EmberShamanFireballReleased -= HandleEmberShamanFireballReleased;
                subscribedBoard.RuneboltMageBoltReleased -= HandleRuneboltMageBoltReleased;
                subscribedBoard.StoneboundWarlockRockReleased -= HandleStoneboundWarlockRockReleased;
                subscribedBoard.ThunderlordChainReleased -= HandleThunderlordChainReleased;
                subscribedBoard.ThunderlordSkillReleased -= HandleThunderlordSkillReleased;
                subscribedBoard.AbyssalHarpoonReleased -= HandleAbyssalHarpoonReleased;
                subscribedBoard.AbyssalHarpoonSkillReleased -= HandleAbyssalHarpoonSkillReleased;
            }

            board = value;
            subscribedBoard = value;
            if (subscribedBoard != null)
            {
                subscribedBoard.FlameDrakeFireballReleased += HandleFlameDrakeFireballReleased;
                subscribedBoard.SkyborneValkyrieArrowReleased += HandleSkyborneValkyrieArrowReleased;
                subscribedBoard.BowProjectileReleased += HandleBowProjectileReleased;
                subscribedBoard.StarfallArchmageGemReleased += HandleStarfallArchmageGemReleased;
                subscribedBoard.NightfangSkillAnimationCompleted += HandleNightfangSkillAnimationCompleted;
                subscribedBoard.WindclawSkillReleased += HandleWindclawSkillReleased;
                subscribedBoard.EmberShamanFireballReleased += HandleEmberShamanFireballReleased;
                subscribedBoard.RuneboltMageBoltReleased += HandleRuneboltMageBoltReleased;
                subscribedBoard.StoneboundWarlockRockReleased += HandleStoneboundWarlockRockReleased;
                subscribedBoard.ThunderlordChainReleased += HandleThunderlordChainReleased;
                subscribedBoard.ThunderlordSkillReleased += HandleThunderlordSkillReleased;
                subscribedBoard.AbyssalHarpoonReleased += HandleAbyssalHarpoonReleased;
                subscribedBoard.AbyssalHarpoonSkillReleased += HandleAbyssalHarpoonSkillReleased;
            }
        }

        private void FaceAttackerForCombatEvent(
            CombatEvent combatEvent,
            Vector3 eventTargetPosition)
        {
            var attackerRuntimeId = combatEvent.AttackerRuntimeId ?? string.Empty;
            if (board == null || string.IsNullOrWhiteSpace(attackerRuntimeId) ||
                (facingFrameByAttacker.TryGetValue(attackerRuntimeId, out var facingFrame) &&
                 facingFrame == Time.frameCount))
            {
                return;
            }

            var facingTargetPosition = eventTargetPosition;
            if (combatEvent.DamageOwnerKind != CombatDamageOwnerKind.BasicUnit &&
                board.TryGetHeroCurrentTargetRuntimeId(
                    attackerRuntimeId,
                    out var primaryTargetRuntimeId) &&
                lane != null &&
                lane.TryGetEnemyPosition(primaryTargetRuntimeId, out var primaryTargetPosition))
            {
                // Area, splash and chain attacks can emit several CombatEvents together.
                // Always face the selected primary target instead of oscillating between hits.
                facingTargetPosition = primaryTargetPosition;
            }

            if (board.FaceAttackerTowardsTarget(
                    attackerRuntimeId,
                    facingTargetPosition.x))
            {
                facingFrameByAttacker[attackerRuntimeId] = Time.frameCount;
            }
        }

        private void QueueRuneboltMageCast(
            CombatEvent combatEvent,
            Vector3 attackerPosition,
            Vector3 targetPosition)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            if (!pendingRuneboltMageCasts.TryGetValue(attackerId, out var pending) ||
                pending.CreatedFrame != Time.frameCount)
            {
                if (pending != null)
                {
                    ReleaseRuneboltMageCast(attackerId);
                }

                pending = new PendingRuneboltMageCast(Time.frameCount, attackerPosition);
                pendingRuneboltMageCasts[attackerId] = pending;
            }

            if (pending.TryAdd(combatEvent, targetPosition))
            {
                lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
            }
        }

        private void HandleRuneboltMageBoltReleased(string attackerRuntimeId)
        {
            ReleaseRuneboltMageCast(attackerRuntimeId);
        }

        private void TickPendingRuneboltMageCasts(float deltaSeconds)
        {
            if (pendingRuneboltMageCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> fallbackReleases = null;
            foreach (var entry in pendingRuneboltMageCasts)
            {
                entry.Value.Elapsed += deltaSeconds;
                if (entry.Value.Elapsed < runeboltMageReleaseFallbackDelay)
                {
                    continue;
                }

                fallbackReleases ??= new List<string>();
                fallbackReleases.Add(entry.Key);
            }

            if (fallbackReleases == null)
            {
                return;
            }

            foreach (var attackerId in fallbackReleases)
            {
                ReleaseRuneboltMageCast(attackerId);
            }
        }

        private void ReleaseRuneboltMageCast(string attackerRuntimeId)
        {
            attackerRuntimeId = attackerRuntimeId ?? string.Empty;
            if (!pendingRuneboltMageCasts.TryGetValue(attackerRuntimeId, out var pending))
            {
                return;
            }

            pendingRuneboltMageCasts.Remove(attackerRuntimeId);
            var attackerPosition = pending.AttackerPosition;
            if (board != null &&
                board.TryGetRuneboltMageSpineAttackOrigin(attackerRuntimeId, out var spineAttackerPosition))
            {
                attackerPosition = spineAttackerPosition;
            }

            SpawnRuneboltMageBolt(pending, attackerPosition);
        }

        private RuntimeAnimatorController ResolveRuneboltMagePathController()
        {
            if (runeboltMageBoltController != null)
            {
                return runeboltMageBoltController;
            }

            // Probe silently: UiAssets.Load logs an error on a miss, and V1 has no V2 key, so an  
            // absent clip is an expected state rather than a failure. (UiAssetRegistry.Load<T> is  
            // the silent instance API; the static UiAssets.Load<T> logs.)  
            var registry = DragonBound.Presentation.UiAssets.Active;
            var projectile = registry != null
                ? registry.Load<RuntimeAnimatorController>(RuneboltMageNormalProjectileControllerPathV2)
                : null;
            if (projectile != null)
            {
                runeboltMageBoltController = projectile;
                runeboltMageBoltVisualHeight = RuneboltMagePathVisualHeightV2;
                return runeboltMageBoltController;
            }

            runeboltMageBoltController =
                DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(RuneboltMageBoltControllerPath);
            runeboltMageBoltVisualHeight = RuneboltMagePathVisualHeightV1;
            return runeboltMageBoltController;
        }

        private void RefreshRuneboltMageTargetPositions(PendingRuneboltMageCast pending)

        {

            if (lane == null)

            {

                return;

            }


            for (var i = 0; i < pending.Shots.Count; i++)

            {

                var shot = pending.Shots[i];

                if (shot.ImpactCompleted)

                {

                    continue;

                }

                // 敌人可能已死亡/离场：查不到就保留伤害结算时的快照，不动。

                if (lane.TryGetEnemyPosition(

                        shot.CombatEvent.TargetRuntimeId,

                        out var currentPosition))

                {

                    shot.SetTargetPosition(currentPosition);

                }

            }

        }

        private void SpawnRuneboltMageBolt(
            PendingRuneboltMageCast pending,
            Vector3 attackerPosition)
        {
            if (pending.Shots.Count == 0)
            {
                return;
            }

            var pathController = ResolveRuneboltMagePathController();
            if (pathController == null)
            {
                CompleteRuneboltMageCast(pending);
                return;
            }
            // Draw against where the enemies are RIGHT NOW: the FX is released by a fallback
            // timer, so the damage-frame snapshot can be a quarter-cell stale.
            // Draw against where the enemies are RIGHT NOW: the FX is released by a fallback

            // timer, so the damage-frame snapshot can be a quarter-cell stale.

            RefreshRuneboltMageTargetPositions(pending);

            pending.SortByDistance(attackerPosition);

            // Shots is sorted NEAREST-FIRST, so the last entry is the farthest target.

            // Aim the beam at it: everything in between lies on the same ray and is

            // therefore covered by the same stroke.

            var farthestShot = pending.Shots[pending.Shots.Count - 1];

            var direction = farthestShot.TargetPosition - attackerPosition;

            if (direction.sqrMagnitude <= 0.0001f)

            {

                CompleteRuneboltMageCast(pending);

                return;

            }

            direction.Normalize();



            var start = attackerPosition + (direction * 8f);

            // Real distance (NOT a dot projection): with \`direction\` pointing at the farthest

            // target this equals its projection, so no more cos-theta shrinkage when the

            // targets are spread apart.

            var travelDistance = Mathf.Clamp(

                Vector3.Distance(farthestShot.TargetPosition, start),

                1f,

                RuneboltMageMaximumPathLength);

            // Visual compensation for the V2 art's blank padding: overshoot the drawn box so

            // the visible stroke reaches the enemy. Damage/hit progress still uses

            // travelDistance, so the five-cell pierce (550) is untouched.

            var visualLength = travelDistance / RuneboltMagePathVisualPaddingRatio
  
                + runeboltMagePathVisualOvershoot;

            pending.ConfigurePath(start, direction, travelDistance);


            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                "Runebolt Mage Path",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.position = start;
            rect.pivot = new Vector2(0f, 0.5f);
            var visualHeight = runeboltMageBoltVisualHeight > 0f
                ? runeboltMageBoltVisualHeight
                : RuneboltMagePathVisualHeightV1;
            rect.sizeDelta = new Vector2(
               visualLength,            // ← 视觉长度；伤害判定仍是 travelDistance（5 格穿透不变）  
               visualHeight);
            rect.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            var image = root.GetComponent<Image>();
            // The authored sequence is intentionally stretched across the whole pierce line.
            // Keeping its square canvas aspect would make it read as a small flying arrow.
            image.preserveAspect = false;
            image.raycastTarget = false;
            var animator = root.GetComponent<Animator>();

            animator.runtimeAnimatorController = pathController;

            animator.Rebind();

            // Freeze on frame 2 (normalized 1/6 = 0.1667): the only frame whose stroke covers

            // ~92% of the canvas with just 4% blank on each side. Frame 1 covers only 56%

            // (39% blank on the left) and frame 6 ends at x136/150 (~9% blank on the right).

            animator.speed = 1f;

            animator.Play(0, 0, 1f / 6f);

            animator.Update(0f);

            var clipLength = pathController.animationClips.Length > 0

                ? pathController.animationClips[0].length

                : 0.1f;

            clipLength = Mathf.Max(0.01f, clipLength);
            var holdDuration = Mathf.Max(0f, runeboltMagePathHoldDuration);
            var fadeDuration = Mathf.Max(0.01f, runeboltMagePathFadeDuration);
            var duration = clipLength + holdDuration + fadeDuration;

            active.Add(new ActiveFx(
                rect,
                image,
                null,
                start,
                start,
                false,
                false,
                duration,
                false,
                () => CompleteRuneboltMageCast(pending),
                0f,
                false,
                0f,
                normalized =>
                {
                    var elapsed = normalized * duration;
                    var pathProgress = Mathf.Clamp01(elapsed / clipLength);
                    CompleteRuneboltMageHitsThrough(pending, pathProgress);

                    var fadeStart = clipLength + holdDuration;
                    var alpha = elapsed <= fadeStart
                        ? 1f
                        : 1f - Mathf.Clamp01((elapsed - fadeStart) / fadeDuration);
                    var color = image.color;
                    color.a = alpha;
                    image.color = color;
                }));
        }

        private void SpawnRuneboltMageImpact(Vector3 position)
        {
            if (runeboltMageNormalImpactController == null)
            {
                // Probe silently: V1 has no V2 key, so a miss is expected, not an error.  
                var registry = DragonBound.Presentation.UiAssets.Active;
                runeboltMageNormalImpactController = registry != null
                    ? registry.Load<RuntimeAnimatorController>(RuneboltMageNormalImpactControllerPathV2)
                    : null;
            }

            if (runeboltMageNormalImpactController == null)
            {
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;

            var root = new GameObject(
                "Runebolt Mage Impact",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator));

            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.position = position;
            rect.sizeDelta = runeboltMageImpactSize;

            var image = root.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = runeboltMageNormalImpactController;
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
            animator.speed = 1f;

            var duration = runeboltMageNormalImpactController.animationClips.Length > 0

                ? runeboltMageNormalImpactController.animationClips[0].length

                : 0.15f;

            duration = Mathf.Max(0.01f, duration / Mathf.Max(0.01f, runeboltMageImpactStateSpeed));

            active.Add(new ActiveFx(
                rect,
                image,
                null,
                position,
                position,
                false,
                false,
                duration));
        }

        private void CompleteRuneboltMageHitsThrough(
            PendingRuneboltMageCast pending,
            float normalizedPathProgress)
        {
            foreach (var shot in pending.Shots)
            {
                if (shot.ImpactCompleted || shot.PathProgress > normalizedPathProgress)
                {
                    continue;
                }

                CompleteRuneboltMageImpact(shot);
            }
        }

        private void CompleteRuneboltMageCast(PendingRuneboltMageCast pending)
        {
            CompleteRuneboltMageHitsThrough(pending, 1f);
        }

        private void CompleteRuneboltMageImpact(PendingRuneboltMageShot shot)
        {
            shot.ImpactCompleted = true;
            var combatEvent = shot.CombatEvent;
            lane?.ReleaseEnemyHealthVisual(combatEvent.TargetRuntimeId);
            var position = shot.TargetPosition;
            if (lane != null &&
                lane.TryGetEnemyPosition(combatEvent.TargetRuntimeId, out var currentPosition))
            {
                position = currentPosition;
            }
            // Reference shot: every pierced enemy gets a ~0.44 cell white burst, which is what  
            // visually "connects" the beam tip to the enemy.  
            SpawnRuneboltMageImpact(position);
            if (combatEvent.Damage > 0f)
            {
                lane?.PlayEnemyHitShake(combatEvent.TargetRuntimeId);
            }
            if (combatEvent.Damage > 0f && DamageNumberSettings.Visible)
            {
                SpawnLabel(
                    damageNumberTemplate,
                    position + (Vector3.up * damageNumberVerticalOffsetPixels),
                    $"-{combatEvent.Damage:0.##}",
                    damageNumberDuration);
            }
            if (combatEvent.Killed)
            {
                SpawnLabel(
                    suppliesGainTemplate,
                    position + (Vector3.up * 24f),
                    "+1 Supplies",
                    0.65f);
            }
        }

        private void QueueStoneboundWarlockCast(
            CombatEvent combatEvent,
            Vector3 attackerPosition,
            Vector3 targetPosition)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            if (pendingStoneboundWarlockCasts.ContainsKey(attackerId))
            {
                ReleaseStoneboundWarlockCast(attackerId);
            }

            pendingStoneboundWarlockCasts[attackerId] = new PendingStoneboundWarlockCast(
                Time.frameCount,
                combatEvent,
                attackerPosition,
                targetPosition);
            lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
        }

        private void UpgradeStoneboundWarlockCastToSkill(string attackerRuntimeId)
        {
            attackerRuntimeId ??= string.Empty;
            if (pendingStoneboundWarlockCasts.TryGetValue(attackerRuntimeId, out var pending) &&
                pending.CreatedFrame == Time.frameCount)
            {
                pending.IsSkill = true;
            }
        }

        private void HandleStoneboundWarlockRockReleased(string attackerRuntimeId)
        {
            ReleaseStoneboundWarlockCast(attackerRuntimeId);
        }

        private void TickPendingStoneboundWarlockCasts(float deltaSeconds)
        {
            if (pendingStoneboundWarlockCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> fallbackReleases = null;
            foreach (var entry in pendingStoneboundWarlockCasts)
            {
                entry.Value.Elapsed += deltaSeconds;
                if (entry.Value.Elapsed < stoneboundWarlockReleaseFallbackDelay)
                {
                    continue;
                }

                fallbackReleases ??= new List<string>();
                fallbackReleases.Add(entry.Key);
            }

            if (fallbackReleases == null)
            {
                return;
            }

            foreach (var attackerId in fallbackReleases)
            {
                ReleaseStoneboundWarlockCast(attackerId);
            }
        }

        private void ReleaseStoneboundWarlockCast(string attackerRuntimeId)
        {
            attackerRuntimeId ??= string.Empty;
            if (!pendingStoneboundWarlockCasts.TryGetValue(attackerRuntimeId, out var pending))
            {
                return;
            }

            pendingStoneboundWarlockCasts.Remove(attackerRuntimeId);
            SpawnStoneboundWarlockRock(pending);
        }

        private void SpawnStoneboundWarlockRock(PendingStoneboundWarlockCast pending)
        {
            var sprite = ResolveStoneboundWarlockRockSprite(pending.IsSkill);
            if (sprite == null)
            {
                CompleteStoneboundWarlockImpact(pending);
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                pending.IsSkill
                    ? "Stonebound Warlock Skill Rock"
                    : "Stonebound Warlock Normal Rock",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = SanitizeFxSize(
                pending.IsSkill
                    ? stoneboundWarlockSkillRockSize
                    : stoneboundWarlockNormalRockSize,
                pending.IsSkill ? new Vector2(88f, 88f) : new Vector2(48f, 48f));

            var targetPosition = pending.TargetPosition;
            if (lane != null &&
                lane.TryGetEnemyPosition(
                    pending.CombatEvent.TargetRuntimeId,
                    out var currentPosition))
            {
                targetPosition = currentPosition;
            }

            var direction = targetPosition - pending.AttackerPosition;
            var start = pending.AttackerPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                start += direction.normalized * 24f;
            }
            rect.position = start;

            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var arcHeight = pending.IsSkill
                ? stoneboundWarlockSkillArcHeight
                : stoneboundWarlockNormalArcHeight;
            var distance = Vector3.Distance(start, targetPosition);
            var duration = Mathf.Clamp(
                distance / 700f,
                stoneboundWarlockMinTravelDuration,
                Mathf.Max(
                    stoneboundWarlockMinTravelDuration,
                    stoneboundWarlockMaxTravelDuration));
            var spinDegrees = pending.IsSkill ? 540f : 360f;
            active.Add(new ActiveFx(
                rect,
                image,
                null,
                start,
                targetPosition,
                true,
                false,
                duration,
                false,
                () => CompleteStoneboundWarlockImpact(pending),
                arcHeight,
                false,
                0f,
                normalized =>
                    rect.localRotation = Quaternion.Euler(0f, 0f, normalized * spinDegrees)));
        }

        private Sprite ResolveStoneboundWarlockRockSprite(bool isSkill)
        {
            if (isSkill)
            {
                stoneboundWarlockSkillRockSprite ??=
                    DragonBound.Presentation.UiAssets.Load<Sprite>(StoneboundWarlockSkillRockSpritePath);
                return stoneboundWarlockSkillRockSprite;
            }

            stoneboundWarlockNormalRockSprite ??=
                DragonBound.Presentation.UiAssets.Load<Sprite>(StoneboundWarlockNormalRockSpritePath);
            return stoneboundWarlockNormalRockSprite;
        }

        private void CompleteStoneboundWarlockImpact(PendingStoneboundWarlockCast pending)
        {
            if (pending.ImpactCompleted)
            {
                return;
            }

            pending.ImpactCompleted = true;
            var combatEvent = pending.CombatEvent;
            lane?.ReleaseEnemyHealthVisual(combatEvent.TargetRuntimeId);
            var position = pending.TargetPosition;
            if (lane != null &&
                lane.TryGetEnemyPosition(combatEvent.TargetRuntimeId, out var currentPosition))
            {
                position = currentPosition;
            }
            if (combatEvent.Damage > 0f)
            {
                lane?.PlayEnemyHitShake(combatEvent.TargetRuntimeId);
            }
            if (combatEvent.Damage > 0f && DamageNumberSettings.Visible)
            {
                SpawnLabel(
                    damageNumberTemplate,
                    position + (Vector3.up * damageNumberVerticalOffsetPixels),
                    $"-{combatEvent.Damage:0.##}",
                    damageNumberDuration);
            }
            if (combatEvent.Killed)
            {
                SpawnLabel(
                    suppliesGainTemplate,
                    position + (Vector3.up * 24f),
                    "+1 Supplies",
                    0.65f);
            }
        }

        private void QueueThunderlordChainCast(
            CombatEvent combatEvent,
            Vector3 attackerPosition,
            Vector3 targetPosition)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            if (!pendingThunderlordChainCasts.TryGetValue(attackerId, out var pending) ||
                pending.CreatedFrame != Time.frameCount)
            {
                if (pending != null)
                {
                    ReleaseThunderlordChainCast(attackerId);
                }

                pending = new PendingThunderlordChainCast(
                    Time.frameCount,
                    attackerPosition);
                pendingThunderlordChainCasts[attackerId] = pending;
            }

            if (pending.TryAdd(combatEvent, targetPosition, 3))
            {
                lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
            }
        }

        private void HandleThunderlordChainReleased(string attackerRuntimeId)
        {
            ReleaseThunderlordChainCast(attackerRuntimeId);
        }

        private void TickPendingThunderlordChainCasts(float deltaSeconds)
        {
            if (pendingThunderlordChainCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> fallbackReleases = null;
            foreach (var entry in pendingThunderlordChainCasts)
            {
                entry.Value.Elapsed += deltaSeconds;
                if (entry.Value.Elapsed < thunderlordReleaseFallbackDelay)
                {
                    continue;
                }

                fallbackReleases ??= new List<string>();
                fallbackReleases.Add(entry.Key);
            }

            if (fallbackReleases == null)
            {
                return;
            }

            foreach (var attackerId in fallbackReleases)
            {
                ReleaseThunderlordChainCast(attackerId);
            }
        }

        private void ReleaseThunderlordChainCast(string attackerRuntimeId)
        {
            attackerRuntimeId ??= string.Empty;
            if (!pendingThunderlordChainCasts.TryGetValue(attackerRuntimeId, out var pending))
            {
                return;
            }

            pendingThunderlordChainCasts.Remove(attackerRuntimeId);
            if (pending.Shots.Count == 0)
            {
                return;
            }

            var activeChain = new ActiveThunderlordChain(pending);
            activeThunderlordChains.Add(activeChain);
            AdvanceThunderlordChain(activeChain, 0f);
            if (activeChain.NextSegmentIndex >= activeChain.Cast.Shots.Count)
            {
                activeThunderlordChains.Remove(activeChain);
            }
        }

        private void TickActiveThunderlordChains(float deltaSeconds)
        {
            for (var index = activeThunderlordChains.Count - 1; index >= 0; index--)
            {
                var chain = activeThunderlordChains[index];
                AdvanceThunderlordChain(chain, deltaSeconds);
                if (chain.NextSegmentIndex >= chain.Cast.Shots.Count)
                {
                    activeThunderlordChains.RemoveAt(index);
                }
            }
        }

        private void AdvanceThunderlordChain(ActiveThunderlordChain chain, float deltaSeconds)
        {
            chain.Elapsed += Mathf.Max(0f, deltaSeconds);
            var jumpDelay = Mathf.Max(0f, thunderlordChainJumpDelay);
            while (chain.NextSegmentIndex < chain.Cast.Shots.Count &&
                   chain.Elapsed + 0.0001f >= chain.NextSegmentIndex * jumpDelay)
            {
                SpawnThunderlordChainSegment(chain.Cast, chain.NextSegmentIndex);
                chain.NextSegmentIndex++;
            }
        }

        private void SpawnThunderlordChainSegment(
            PendingThunderlordChainCast pending,
            int segmentIndex)
        {
            var shot = pending.Shots[segmentIndex];
            var sprite = ResolveThunderlordChainSprite(segmentIndex);
            if (sprite == null)
            {
                CompleteThunderlordChainImpact(shot);
                return;
            }

            var start = segmentIndex == 0
                ? pending.AttackerPosition
                : ResolveThunderlordTargetPosition(pending.Shots[segmentIndex - 1]);
            var end = ResolveThunderlordTargetPosition(shot);
            var direction = end - start;
            var distance = direction.magnitude;
            if (distance <= 0.001f)
            {
                CompleteThunderlordChainImpact(shot);
                return;
            }

            direction /= distance;
            var startInset = segmentIndex == 0 ? 24f : 10f;
            var endInset = 10f;
            if (distance > startInset + endInset + 1f)
            {
                start += direction * startInset;
                end -= direction * endInset;
                distance = Vector3.Distance(start, end);
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                segmentIndex == 0
                    ? "Thunderlord Main Chain"
                    : segmentIndex == 1
                        ? "Thunderlord First Jump Chain"
                        : "Thunderlord Second Jump Chain",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.position = Vector3.Lerp(start, end, 0.5f);
            rect.sizeDelta = new Vector2(
                distance,
                ResolveThunderlordChainHeight(segmentIndex));
            rect.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = false;
            image.raycastTarget = false;

            CompleteThunderlordChainImpact(shot);
            active.Add(new ActiveFx(
                rect,
                image,
                null,
                rect.position,
                rect.position,
                false,
                true,
                Mathf.Max(0.01f, thunderlordChainVisibleDuration)));
        }

        private Vector3 ResolveThunderlordTargetPosition(PendingThunderlordChainShot shot)
        {
            if (lane != null &&
                lane.TryGetEnemyPosition(shot.CombatEvent.TargetRuntimeId, out var currentPosition))
            {
                return currentPosition;
            }

            return shot.TargetPosition;
        }

        private Sprite ResolveThunderlordChainSprite(int segmentIndex)
        {
            switch (segmentIndex)
            {
                case 0:
                    thunderlordMainChainSprite ??=
                        DragonBound.Presentation.UiAssets.Load<Sprite>(ThunderlordMainChainSpritePath);
                    return thunderlordMainChainSprite;
                case 1:
                    thunderlordFirstChainSprite ??=
                        DragonBound.Presentation.UiAssets.Load<Sprite>(ThunderlordFirstChainSpritePath);
                    return thunderlordFirstChainSprite;
                default:
                    thunderlordSecondChainSprite ??=
                        DragonBound.Presentation.UiAssets.Load<Sprite>(ThunderlordSecondChainSpritePath);
                    return thunderlordSecondChainSprite;
            }
        }

        private float ResolveThunderlordChainHeight(int segmentIndex)
        {
            switch (segmentIndex)
            {
                case 0:
                    return Mathf.Max(1f, thunderlordMainChainHeight);
                case 1:
                    return Mathf.Max(1f, thunderlordFirstChainHeight);
                default:
                    return Mathf.Max(1f, thunderlordSecondChainHeight);
            }
        }

        private void CompleteThunderlordChainImpact(PendingThunderlordChainShot shot)
        {
            if (shot.ImpactCompleted)
            {
                return;
            }

            shot.ImpactCompleted = true;
            var combatEvent = shot.CombatEvent;
            lane?.ReleaseEnemyHealthVisual(combatEvent.TargetRuntimeId);
            var position = ResolveThunderlordTargetPosition(shot);
            if (combatEvent.Damage > 0f)
            {
                lane?.PlayEnemyHitShake(combatEvent.TargetRuntimeId);
            }
            if (combatEvent.Damage > 0f && DamageNumberSettings.Visible)
            {
                SpawnLabel(
                    damageNumberTemplate,
                    position + (Vector3.up * damageNumberVerticalOffsetPixels),
                    $"-{combatEvent.Damage:0.##}",
                    damageNumberDuration);
            }
            if (combatEvent.Killed)
            {
                SpawnLabel(
                    suppliesGainTemplate,
                    position + (Vector3.up * 24f),
                    "+1 Supplies",
                    0.65f);
            }
        }

        private void QueueThunderlordSkillCast(
            CombatEvent combatEvent,
            Vector3 attackerPosition,
            Vector3 targetPosition)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            if (!pendingThunderlordSkillCasts.TryGetValue(attackerId, out var pending) ||
                pending.CreatedFrame != Time.frameCount)
            {
                if (pending != null)
                {
                    ReleaseThunderlordSkillCast(attackerId);
                }

                pending = new PendingThunderlordSkillCast(
                    Time.frameCount,
                    attackerPosition);
                pendingThunderlordSkillCasts[attackerId] = pending;
                board?.PlayHeroFormationAttackAnimation(attackerId, true);
            }

            if (pending.TryAdd(combatEvent, targetPosition))
            {
                lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
            }
        }

        private void HandleThunderlordSkillReleased(string attackerRuntimeId)
        {
            ReleaseThunderlordSkillCast(attackerRuntimeId);
        }

        private void TickPendingThunderlordSkillCasts(float deltaSeconds)
        {
            if (pendingThunderlordSkillCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> fallbackReleases = null;
            foreach (var entry in pendingThunderlordSkillCasts)
            {
                entry.Value.Elapsed += deltaSeconds;
                if (entry.Value.Elapsed < thunderlordSkillReleaseFallbackDelay)
                {
                    continue;
                }

                fallbackReleases ??= new List<string>();
                fallbackReleases.Add(entry.Key);
            }

            if (fallbackReleases == null)
            {
                return;
            }

            foreach (var attackerId in fallbackReleases)
            {
                ReleaseThunderlordSkillCast(attackerId);
            }
        }

        private void ReleaseThunderlordSkillCast(string attackerRuntimeId)
        {
            attackerRuntimeId ??= string.Empty;
            if (!pendingThunderlordSkillCasts.TryGetValue(attackerRuntimeId, out var pending))
            {
                return;
            }

            pendingThunderlordSkillCasts.Remove(attackerRuntimeId);
            if (pending.Shots.Count == 0)
            {
                return;
            }

            var center = pending.AttackerPosition;
            if (board != null && board.TryGetUnitPosition(attackerRuntimeId, out var currentPosition))
            {
                center = currentPosition;
            }

            SpawnThunderlordExplosion(center);
            foreach (var shot in pending.Shots)
            {
                CompleteThunderlordSkillImpact(shot);
            }
        }

        private void SpawnThunderlordExplosion(Vector3 center)
        {
            var controller = ResolveThunderlordExplosionController();
            if (controller == null)
            {
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                "Thunderlord Skill Boom",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.position = center;
            rect.sizeDelta = SanitizeFxSize(
                thunderlordExplosionSize,
                new Vector2(550f, 550f));
            rect.SetAsFirstSibling();

            var image = root.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            var duration = Mathf.Max(0.1f, thunderlordExplosionDuration);
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
            var clips = controller.animationClips;
            if (clips != null && clips.Length > 0 && clips[0] != null)
            {
                animator.speed = clips[0].length / duration;
            }

            active.Add(new ActiveFx(
                rect,
                image,
                null,
                center,
                center,
                false,
                false,
                duration));
        }

        private void CompleteThunderlordSkillImpact(PendingThunderlordSkillShot shot)
        {
            if (shot.ImpactCompleted)
            {
                return;
            }

            shot.ImpactCompleted = true;
            var combatEvent = shot.CombatEvent;
            lane?.ReleaseEnemyHealthVisual(combatEvent.TargetRuntimeId);
            var position = shot.TargetPosition;
            if (lane != null &&
                lane.TryGetEnemyPosition(combatEvent.TargetRuntimeId, out var currentPosition))
            {
                position = currentPosition;
            }

            if (combatEvent.Damage > 0f)
            {
                lane?.PlayEnemyHitShake(combatEvent.TargetRuntimeId);
            }
            if (combatEvent.Damage > 0f && DamageNumberSettings.Visible)
            {
                SpawnLabel(
                    damageNumberTemplate,
                    position + (Vector3.up * damageNumberVerticalOffsetPixels),
                    $"-{combatEvent.Damage:0.##}",
                    damageNumberDuration);
            }
            if (combatEvent.Killed)
            {
                SpawnLabel(
                    suppliesGainTemplate,
                    position + (Vector3.up * 24f),
                    "+1 Supplies",
                    0.65f);
            }
        }

        private RuntimeAnimatorController ResolveThunderlordExplosionController()
        {
            if (thunderlordExplosionController == null)
            {
                thunderlordExplosionController =
                    DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                        ThunderlordExplosionControllerPath);
            }

            if (thunderlordExplosionController == null)
            {
                Debug.LogWarning(
                    $"Thunderlord skill explosion is missing at Resources/{ThunderlordExplosionControllerPath}.",
                    this);
            }

            return thunderlordExplosionController;
        }

        private void BeginAbyssalHarpoonSkill(
            CombatEvent combatEvent,
            Vector3 attackerPosition,
            Vector3 targetPosition)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            if (pendingAbyssalHarpoonSkillCasts.TryGetValue(attackerId, out var previous))
            {
                CompleteAllAbyssalHarpoonImpacts(previous);
                DestroyPendingAbyssalHarpoonPortal(previous);
            }

            var pending = new PendingAbyssalHarpoonSkillCast(
                Time.frameCount,
                attackerPosition,
                combatEvent.TargetRuntimeId,
                targetPosition)
            {
                ReleaseRequested = abyssalHarpoonSkillReleaseLatches.Remove(attackerId)
            };
            pendingAbyssalHarpoonSkillCasts[attackerId] = pending;
            board?.PlayHeroFormationAttackAnimation(attackerId, true);
            CreateAbyssalHarpoonPortal(pending);
        }

        private void QueueAbyssalHarpoonSkillStrike(
            CombatEvent combatEvent,
            Vector3 attackerPosition,
            Vector3 targetPosition)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            if (!pendingAbyssalHarpoonSkillCasts.TryGetValue(attackerId, out var pending))
            {
                pending = new PendingAbyssalHarpoonSkillCast(
                    Time.frameCount,
                    attackerPosition,
                    combatEvent.TargetRuntimeId,
                    targetPosition)
                {
                    ReleaseRequested = abyssalHarpoonSkillReleaseLatches.Remove(attackerId)
                };
                pendingAbyssalHarpoonSkillCasts[attackerId] = pending;
                CreateAbyssalHarpoonPortal(pending);
            }

            if (pending.TryAdd(combatEvent, targetPosition, 6))
            {
                pending.LastAddedFrame = Time.frameCount;
                lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
            }
        }

        private void HandleAbyssalHarpoonSkillReleased(string attackerRuntimeId)
        {
            attackerRuntimeId ??= string.Empty;
            if (pendingAbyssalHarpoonSkillCasts.TryGetValue(attackerRuntimeId, out var pending))
            {
                pending.ReleaseRequested = true;
                return;
            }

            abyssalHarpoonSkillReleaseLatches.Add(attackerRuntimeId);
        }

        private void TickPendingAbyssalHarpoonSkillCasts(float deltaSeconds)
        {
            if (pendingAbyssalHarpoonSkillCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> releases = null;
            List<string> abandoned = null;
            foreach (var entry in pendingAbyssalHarpoonSkillCasts)
            {
                var pending = entry.Value;
                pending.Elapsed += deltaSeconds;
                if (pending.PortalRect != null)
                {
                    var opening = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pending.Elapsed / 0.18f));
                    pending.PortalRect.localScale = Vector3.one * opening;
                }

                var eventReady = pending.ReleaseRequested &&
                                 pending.Shots.Count > 0 &&
                                 Time.frameCount > pending.LastAddedFrame;
                var fallbackReady = pending.Shots.Count > 0 &&
                                    pending.Elapsed >= abyssalHarpoonSkillReleaseFallbackDelay;
                if (eventReady || fallbackReady)
                {
                    releases ??= new List<string>();
                    releases.Add(entry.Key);
                }
                else if (pending.Shots.Count == 0 && pending.Elapsed >= 1f)
                {
                    abandoned ??= new List<string>();
                    abandoned.Add(entry.Key);
                }
            }

            if (releases != null)
            {
                foreach (var attackerId in releases)
                {
                    ReleaseAbyssalHarpoonSkillCast(attackerId);
                }
            }

            if (abandoned != null)
            {
                foreach (var attackerId in abandoned)
                {
                    if (pendingAbyssalHarpoonSkillCasts.TryGetValue(attackerId, out var pending))
                    {
                        DestroyPendingAbyssalHarpoonPortal(pending);
                    }
                    pendingAbyssalHarpoonSkillCasts.Remove(attackerId);
                }
            }
        }

        private void CreateAbyssalHarpoonPortal(PendingAbyssalHarpoonSkillCast pending)
        {
            var portalSprite = ResolveAbyssalHarpoonPortalSprite();
            if (portalSprite == null)
            {
                return;
            }

            var portalPosition = pending.WarningTargetPosition;
            var portalDirection = Vector3.right;
            var route = new List<Vector3>();
            if (lane != null &&
                lane.TryGetEnemyBackwardPath(
                    pending.AnchorTargetRuntimeId,
                    AbyssalHarpoonVisualLength,
                    route))
            {
                portalPosition = route[0];
                portalDirection = route[1] - route[0];
            }
            else if (lane != null &&
                     lane.TryGetEnemyPathAxis(
                         pending.AnchorTargetRuntimeId,
                         out var pathAxis))
            {
                portalDirection = pathAxis;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? (Transform)fixedBoardCanvas.CombatFxLayer
                : transform;
            var portalObject = new GameObject(
                "Abyssal Harpooner Skill Portal",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var portalRect = portalObject.GetComponent<RectTransform>();
            portalRect.SetParent(parent, false);
            portalRect.position = portalPosition;
            portalRect.sizeDelta = AbyssalHarpoonPortalSize;
            portalRect.rotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(portalDirection.y, portalDirection.x) * Mathf.Rad2Deg);
            portalRect.localScale = Vector3.zero;
            var portalImage = portalObject.GetComponent<Image>();
            portalImage.sprite = portalSprite;
            portalImage.preserveAspect = true;
            portalImage.raycastTarget = false;
            pending.PortalRect = portalRect;
        }

        private void ReleaseAbyssalHarpoonSkillCast(string attackerRuntimeId)
        {
            attackerRuntimeId ??= string.Empty;
            if (!pendingAbyssalHarpoonSkillCasts.TryGetValue(attackerRuntimeId, out var pending))
            {
                return;
            }

            pendingAbyssalHarpoonSkillCasts.Remove(attackerRuntimeId);
            if (pending.Shots.Count == 0)
            {
                DestroyPendingAbyssalHarpoonPortal(pending);
                return;
            }

            var chainSprite = ResolveAbyssalHarpoonChainSprite();
            var hookSprite = ResolveAbyssalHarpoonHookSprite();
            if (chainSprite == null || hookSprite == null)
            {
                CompleteAllAbyssalHarpoonImpacts(pending);
                DestroyPendingAbyssalHarpoonPortal(pending);
                return;
            }

            var lockedShot = ResolveLockedAbyssalHarpoonShot(attackerRuntimeId, pending);
            var route = new List<Vector3>();
            if (lane == null ||
                !lane.TryGetBackwardPathFromPosition(
                    lockedShot.TargetPosition,
                    AbyssalHarpoonVisualLength,
                    route))
            {
                route.Add(pending.WarningTargetPosition);
                route.Add(ResolveAbyssalHarpoonTargetPosition(lockedShot));
            }

            foreach (var shot in pending.Shots)
            {
                shot.SkillRouteDistance = GetDistanceAlongPath(route, shot.TargetPosition);
                lane?.HoldEnemyPositionForPull(
                    shot.CombatEvent.TargetRuntimeId,
                    shot.TargetPosition);
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? (Transform)fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject("Abyssal Harpooner Skill Harpoon", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            // Route points are converted through this root, so its origin must match
            // the combat layer's pivot instead of the layer's bottom-left corner.
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = Vector2.zero;
            rootRect.localRotation = Quaternion.identity;
            rootRect.localScale = Vector3.one;

            if (pending.PortalRect != null)
            {
                pending.PortalRect.SetParent(rootRect, true);
                pending.PortalRect.position = route[0];
                pending.PortalRect.localScale = Vector3.one;
            }

            var segments = new List<AbyssalHarpoonPathSegment>();
            var totalLength = 0f;
            for (var index = 0; index < route.Count - 1; index++)
            {
                var localStart = (Vector2)rootRect.InverseTransformPoint(route[index]);
                var localEnd = (Vector2)rootRect.InverseTransformPoint(route[index + 1]);
                var delta = localEnd - localStart;
                var length = delta.magnitude;
                if (length <= 0.01f)
                {
                    continue;
                }

                var segmentObject = new GameObject(
                    $"Abyssal Harpooner Skill Chain {index + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                var rect = segmentObject.GetComponent<RectTransform>();
                rect.SetParent(rootRect, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = localStart;
                rect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                rect.sizeDelta = new Vector2(0f, AbyssalHarpoonChainHeight);
                var image = segmentObject.GetComponent<Image>();
                image.sprite = chainSprite;
                image.type = Image.Type.Tiled;
                image.preserveAspect = false;
                image.pixelsPerUnitMultiplier = Mathf.Max(
                    0.01f,
                    chainSprite.rect.height / AbyssalHarpoonChainHeight);
                image.raycastTarget = false;
                segments.Add(new AbyssalHarpoonPathSegment(
                    rect,
                    localStart,
                    localEnd,
                    totalLength,
                    length));
                totalLength += length;
            }

            var hookObject = new GameObject(
                "Abyssal Harpooner Skill Hook",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var hookRect = hookObject.GetComponent<RectTransform>();
            hookRect.SetParent(rootRect, false);
            hookRect.anchorMin = Vector2.zero;
            hookRect.anchorMax = Vector2.zero;
            hookRect.pivot = new Vector2(0.5f, 0.5f);
            hookRect.sizeDelta = AbyssalHarpoonHookSize;
            var hookImage = hookObject.GetComponent<Image>();
            hookImage.sprite = hookSprite;
            hookImage.preserveAspect = true;
            hookImage.raycastTarget = false;

            if (pending.PortalRect != null)
            {
                var firstDirection = segments.Count > 0
                    ? (segments[0].End - segments[0].Start).normalized
                    : Vector2.right;
                pending.PortalRect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Atan2(firstDirection.y, firstDirection.x) * Mathf.Rad2Deg);
                // Keep the chain and hook behind the opaque portal rim so their first
                // visible pixels appear to emerge from inside the opening.
                pending.PortalRect.SetAsLastSibling();
            }

            var extendDuration = Mathf.Max(0.05f, abyssalHarpoonExtendDuration);
            var holdDuration = Mathf.Max(0f, abyssalHarpoonHoldDuration);
            var retractDuration = Mathf.Max(0.05f, abyssalHarpoonRetractDuration);
            var totalDuration = extendDuration + holdDuration + retractDuration;
            active.Add(new ActiveFx(
                rootRect,
                null,
                null,
                rootRect.position,
                rootRect.position,
                false,
                false,
                totalDuration,
                false,
                () => CompleteUnstartedAbyssalHarpoonPulls(pending),
                0f,
                false,
                0f,
                normalized => UpdateAbyssalHarpoonSkillVisual(
                    pending,
                    segments,
                    hookRect,
                    totalLength,
                    normalized * totalDuration,
                    extendDuration,
                    holdDuration,
                    retractDuration)));
        }

        private void UpdateAbyssalHarpoonSkillVisual(
            PendingAbyssalHarpoonSkillCast pending,
            List<AbyssalHarpoonPathSegment> segments,
            RectTransform hookRect,
            float totalLength,
            float elapsed,
            float extendDuration,
            float holdDuration,
            float retractDuration)
        {
            float extension;
            if (elapsed < extendDuration)
            {
                extension = Mathf.Clamp01(elapsed / extendDuration);
            }
            else if (elapsed < extendDuration + holdDuration)
            {
                extension = 1f;
            }
            else
            {
                extension = 1f - Mathf.Clamp01(
                    (elapsed - extendDuration - holdDuration) / retractDuration);
            }

            var visibleLength = totalLength * extension;
            foreach (var segment in segments)
            {
                var length = Mathf.Clamp(
                    visibleLength - segment.StartDistance,
                    0f,
                    segment.Length);
                if (segment.Rect != null)
                {
                    segment.Rect.sizeDelta = new Vector2(length, AbyssalHarpoonChainHeight);
                }
            }

            if (hookRect != null && segments.Count > 0)
            {
                var segment = segments[segments.Count - 1];
                foreach (var candidate in segments)
                {
                    if (visibleLength <= candidate.StartDistance + candidate.Length + 0.01f)
                    {
                        segment = candidate;
                        break;
                    }
                }

                var localDistance = Mathf.Clamp(
                    visibleLength - segment.StartDistance,
                    0f,
                    segment.Length);
                var direction = (segment.End - segment.Start).normalized;
                hookRect.anchoredPosition = segment.Start + (direction * localDistance);
                hookRect.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f);
                hookRect.gameObject.SetActive(visibleLength > 1f);
            }

            if (elapsed >= extendDuration + holdDuration)
            {
                foreach (var shot in pending.Shots)
                {
                    if (!shot.PullStarted &&
                        visibleLength <= shot.SkillRouteDistance + 0.01f)
                    {
                        StartAbyssalHarpoonTargetPull(
                            shot,
                            totalLength,
                            retractDuration);
                    }
                }
            }

            if (pending.PortalRect != null && elapsed >= extendDuration + holdDuration)
            {
                pending.PortalRect.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, extension);
            }
        }

        private void StartAbyssalHarpoonTargetPull(
            PendingAbyssalHarpoonShot shot,
            float totalLength,
            float retractDuration)
        {
            if (shot.PullStarted)
            {
                return;
            }

            shot.PullStarted = true;
            var pullDistance = Mathf.Max(0f, shot.CombatEvent.PathDisplacementDistance) *
                               AbyssalHarpoonCellSize;
            if (pullDistance <= 0.01f || lane == null ||
                !lane.TryGetBackwardPositionFromPosition(
                    shot.TargetPosition,
                    pullDistance,
                    out var endPosition))
            {
                CompleteAbyssalHarpoonImpact(shot);
                return;
            }

            var remainingFraction = totalLength > 0.01f
                ? Mathf.Clamp01(shot.SkillRouteDistance / totalLength)
                : 1f;
            var pullDuration = Mathf.Clamp(
                retractDuration * remainingFraction,
                0.08f,
                retractDuration);
            lane.PlayEnemyPathPullVisual(
                shot.CombatEvent.TargetRuntimeId,
                shot.TargetPosition,
                endPosition,
                pullDuration,
                () => CompleteAbyssalHarpoonImpact(shot));
        }

        private void CompleteUnstartedAbyssalHarpoonPulls(
            PendingAbyssalHarpoonSkillCast pending)
        {
            foreach (var shot in pending.Shots)
            {
                if (!shot.PullStarted)
                {
                    StartAbyssalHarpoonTargetPull(shot, 1f, 0.08f);
                }
            }
        }

        private static float GetDistanceAlongPath(
            IReadOnlyList<Vector3> path,
            Vector3 position)
        {
            if (path == null || path.Count < 2)
            {
                return 0f;
            }

            var bestDistanceSquared = float.PositiveInfinity;
            var bestPathDistance = 0f;
            var cumulativeDistance = 0f;
            for (var index = 0; index < path.Count - 1; index++)
            {
                var start = path[index];
                var end = path[index + 1];
                var delta = end - start;
                var length = delta.magnitude;
                if (length <= 0.001f)
                {
                    continue;
                }

                var progress = Mathf.Clamp01(
                    Vector3.Dot(position - start, delta) / delta.sqrMagnitude);
                var closest = start + (delta * progress);
                var distanceSquared = (position - closest).sqrMagnitude;
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestPathDistance = cumulativeDistance + (length * progress);
                }

                cumulativeDistance += length;
            }

            return bestPathDistance;
        }

        private void DestroyPendingAbyssalHarpoonPortal(PendingAbyssalHarpoonSkillCast pending)
        {
            if (pending?.PortalRect != null)
            {
                Destroy(pending.PortalRect.gameObject);
                pending.PortalRect = null;
            }
        }

        private void QueueAbyssalHarpoonCast(
            CombatEvent combatEvent,
            Vector3 attackerPosition,
            Vector3 targetPosition)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            if (!pendingAbyssalHarpoonCasts.TryGetValue(attackerId, out var pending) ||
                pending.CreatedFrame != Time.frameCount)
            {
                if (pending != null)
                {
                    ReleaseAbyssalHarpoonCast(attackerId);
                }

                pending = new PendingAbyssalHarpoonCast(
                    Time.frameCount,
                    attackerPosition);
                pendingAbyssalHarpoonCasts[attackerId] = pending;
            }

            if (pending.TryAdd(combatEvent, targetPosition, 6))
            {
                lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
            }
        }

        private void HandleAbyssalHarpoonReleased(string attackerRuntimeId)
        {
            ReleaseAbyssalHarpoonCast(attackerRuntimeId);
        }

        private void TickPendingAbyssalHarpoonCasts(float deltaSeconds)
        {
            if (pendingAbyssalHarpoonCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> fallbackReleases = null;
            foreach (var entry in pendingAbyssalHarpoonCasts)
            {
                entry.Value.Elapsed += deltaSeconds;
                if (entry.Value.Elapsed < abyssalHarpoonReleaseFallbackDelay)
                {
                    continue;
                }

                fallbackReleases ??= new List<string>();
                fallbackReleases.Add(entry.Key);
            }

            if (fallbackReleases == null)
            {
                return;
            }

            foreach (var attackerId in fallbackReleases)
            {
                ReleaseAbyssalHarpoonCast(attackerId);
            }
        }

        private void ReleaseAbyssalHarpoonCast(string attackerRuntimeId)
        {
            attackerRuntimeId ??= string.Empty;
            if (!pendingAbyssalHarpoonCasts.TryGetValue(attackerRuntimeId, out var pending))
            {
                return;
            }

            pendingAbyssalHarpoonCasts.Remove(attackerRuntimeId);
            if (pending.Shots.Count == 0)
            {
                return;
            }

            var chainSprite = ResolveAbyssalHarpoonChainSprite();
            var hookSprite = ResolveAbyssalHarpoonHookSprite();
            if (chainSprite == null || hookSprite == null)
            {
                CompleteAllAbyssalHarpoonImpacts(pending);
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? (Transform)fixedBoardCanvas.CombatFxLayer
                : transform;
            var lockedShot = ResolveLockedAbyssalHarpoonShot(attackerRuntimeId, pending);
            var firstTargetPosition = ResolveAbyssalHarpoonTargetPosition(lockedShot);
            var startPosition = pending.AttackerPosition;
            if (board != null &&
                board.TryGetHeroAttackOrigin(attackerRuntimeId, out var currentAttacker))
            {
                startPosition = currentAttacker;
            }

            var localStart = parent.InverseTransformPoint(startPosition);
            var localTarget = parent.InverseTransformPoint(firstTargetPosition);
            var localDirection = (Vector2)(localTarget - localStart);
            if (localDirection.sqrMagnitude <= 0.0001f)
            {
                localDirection = Vector2.right;
            }
            localDirection.Normalize();
            var targetDistance = Vector2.Distance((Vector2)localStart, (Vector2)localTarget);
            var forwardOffset = Mathf.Min(
                AbyssalHarpoonOriginForwardOffset,
                Mathf.Max(0f, targetDistance - 1f));
            localStart += (Vector3)(localDirection * forwardOffset);
            var visualLength = Mathf.Min(
                AbyssalHarpoonVisualLength,
                Vector2.Distance((Vector2)localStart, (Vector2)localTarget));
            visualLength = Mathf.Max(1f, visualLength);

            foreach (var shot in pending.Shots)
            {
                var localShotPosition = parent.InverseTransformPoint(
                    ResolveAbyssalHarpoonTargetPosition(shot));
                var distance = Vector2.Dot(
                    (Vector2)(localShotPosition - localStart),
                    localDirection);
                shot.ReleaseProgress = Mathf.Clamp01(
                    distance / visualLength);
            }
            pending.Shots.Sort((left, right) =>
                left.ReleaseProgress.CompareTo(right.ReleaseProgress));

            var root = new GameObject("Abyssal Harpooner Normal Harpoon", typeof(RectTransform));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.localPosition = new Vector3(localStart.x, localStart.y, 0f);
            rootRect.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg);

            var chainObject = new GameObject(
                "Abyssal Harpooner Chain",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var chainRect = chainObject.GetComponent<RectTransform>();
            chainRect.SetParent(rootRect, false);
            chainRect.anchorMin = new Vector2(0f, 0.5f);
            chainRect.anchorMax = new Vector2(0f, 0.5f);
            chainRect.pivot = new Vector2(0f, 0.5f);
            chainRect.anchoredPosition = Vector2.zero;
            chainRect.sizeDelta = new Vector2(0f, AbyssalHarpoonChainHeight);
            var chainImage = chainObject.GetComponent<Image>();
            chainImage.sprite = chainSprite;
            chainImage.type = Image.Type.Tiled;
            chainImage.preserveAspect = false;
            chainImage.pixelsPerUnitMultiplier = Mathf.Max(
                0.01f,
                chainSprite.rect.height / AbyssalHarpoonChainHeight);
            chainImage.raycastTarget = false;

            var hookObject = new GameObject(
                "Abyssal Harpooner Hook",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var hookRect = hookObject.GetComponent<RectTransform>();
            hookRect.SetParent(rootRect, false);
            hookRect.anchorMin = new Vector2(0f, 0.5f);
            hookRect.anchorMax = new Vector2(0f, 0.5f);
            hookRect.pivot = new Vector2(0.5f, 0.5f);
            hookRect.anchoredPosition = Vector2.zero;
            hookRect.sizeDelta = SanitizeFxSize(
                AbyssalHarpoonHookSize,
                new Vector2(56f, 33.25f));
            hookRect.localRotation = Quaternion.Euler(0f, 0f, 180f);
            var hookImage = hookObject.GetComponent<Image>();
            hookImage.sprite = hookSprite;
            hookImage.preserveAspect = true;
            hookImage.raycastTarget = false;

            var extendDuration = Mathf.Max(0.05f, abyssalHarpoonExtendDuration);
            var holdDuration = Mathf.Max(0f, abyssalHarpoonHoldDuration);
            var retractDuration = Mathf.Max(0.05f, abyssalHarpoonRetractDuration);
            var totalDuration = extendDuration + holdDuration + retractDuration;
            active.Add(new ActiveFx(
                rootRect,
                null,
                null,
                rootRect.position,
                rootRect.position,
                false,
                false,
                totalDuration,
                false,
                () => CompleteAllAbyssalHarpoonImpacts(pending),
                0f,
                false,
                0f,
                normalized => UpdateAbyssalHarpoonVisual(
                    pending,
                    chainRect,
                    hookRect,
                    visualLength,
                    normalized * totalDuration,
                    extendDuration,
                    holdDuration,
                    retractDuration)));
        }

        private void UpdateAbyssalHarpoonVisual(
            PendingAbyssalHarpoonCast pending,
            RectTransform chainRect,
            RectTransform hookRect,
            float visualLength,
            float elapsed,
            float extendDuration,
            float holdDuration,
            float retractDuration)
        {
            float extension;
            if (elapsed < extendDuration)
            {
                extension = Mathf.Clamp01(elapsed / extendDuration);
            }
            else if (elapsed < extendDuration + holdDuration)
            {
                extension = 1f;
            }
            else
            {
                extension = 1f - Mathf.Clamp01(
                    (elapsed - extendDuration - holdDuration) / retractDuration);
            }

            var visibleLength = Mathf.Min(AbyssalHarpoonVisualLength, visualLength) * extension;
            if (chainRect != null)
            {
                chainRect.sizeDelta = new Vector2(
                    Mathf.Max(0.01f, visibleLength),
                    AbyssalHarpoonChainHeight);
            }
            if (hookRect != null)
            {
                hookRect.anchoredPosition = new Vector2(visibleLength, 0f);
                hookRect.gameObject.SetActive(visibleLength > 1f);
            }

            if (elapsed <= extendDuration)
            {
                foreach (var shot in pending.Shots)
                {
                    if (!shot.ImpactCompleted && shot.ReleaseProgress <= extension + 0.0001f)
                    {
                        CompleteAbyssalHarpoonImpact(shot);
                    }
                }
            }
            else
            {
                CompleteAllAbyssalHarpoonImpacts(pending);
            }
        }

        private void CompleteAllAbyssalHarpoonImpacts(PendingAbyssalHarpoonCast pending)
        {
            foreach (var shot in pending.Shots)
            {
                CompleteAbyssalHarpoonImpact(shot);
            }
        }

        private void CompleteAllAbyssalHarpoonImpacts(PendingAbyssalHarpoonSkillCast pending)
        {
            foreach (var shot in pending.Shots)
            {
                CompleteAbyssalHarpoonImpact(shot);
            }
        }

        private void CompleteAbyssalHarpoonImpact(PendingAbyssalHarpoonShot shot)
        {
            if (shot.ImpactCompleted)
            {
                return;
            }

            shot.ImpactCompleted = true;
            var combatEvent = shot.CombatEvent;
            lane?.ReleaseEnemyHealthVisual(combatEvent.TargetRuntimeId);
            var position = ResolveAbyssalHarpoonTargetPosition(shot);
            if (combatEvent.Damage > 0f)
            {
                lane?.PlayEnemyHitShake(combatEvent.TargetRuntimeId);
            }
            if (combatEvent.Damage > 0f && DamageNumberSettings.Visible)
            {
                SpawnLabel(
                    damageNumberTemplate,
                    position + (Vector3.up * damageNumberVerticalOffsetPixels),
                    $"-{combatEvent.Damage:0.##}",
                    damageNumberDuration);
            }
            if (combatEvent.Killed)
            {
                SpawnLabel(
                    suppliesGainTemplate,
                    position + (Vector3.up * 24f),
                    "+1 Supplies",
                    0.65f);
            }
        }

        private Vector3 ResolveAbyssalHarpoonTargetPosition(PendingAbyssalHarpoonShot shot)
        {
            if (lane != null &&
                lane.TryGetEnemyPosition(shot.CombatEvent.TargetRuntimeId, out var currentPosition))
            {
                return currentPosition;
            }

            return shot.TargetPosition;
        }

        private PendingAbyssalHarpoonShot ResolveLockedAbyssalHarpoonShot(
            string attackerRuntimeId,
            PendingAbyssalHarpoonCast pending)
        {
            if (board != null &&
                board.TryGetHeroCurrentTargetRuntimeId(attackerRuntimeId, out var targetRuntimeId))
            {
                foreach (var shot in pending.Shots)
                {
                    if (string.Equals(
                            shot.CombatEvent.TargetRuntimeId,
                            targetRuntimeId,
                            StringComparison.Ordinal))
                    {
                        return shot;
                    }
                }
            }

            return pending.Shots[0];
        }

        private PendingAbyssalHarpoonShot ResolveLockedAbyssalHarpoonShot(
            string attackerRuntimeId,
            PendingAbyssalHarpoonSkillCast pending)
        {
            if (board != null &&
                board.TryGetHeroCurrentTargetRuntimeId(attackerRuntimeId, out var targetRuntimeId))
            {
                foreach (var shot in pending.Shots)
                {
                    if (string.Equals(
                            shot.CombatEvent.TargetRuntimeId,
                            targetRuntimeId,
                            StringComparison.Ordinal))
                    {
                        return shot;
                    }
                }
            }

            foreach (var shot in pending.Shots)
            {
                if (string.Equals(
                        shot.CombatEvent.TargetRuntimeId,
                        pending.AnchorTargetRuntimeId,
                        StringComparison.Ordinal))
                {
                    return shot;
                }
            }

            return pending.Shots[0];
        }

        private Sprite ResolveAbyssalHarpoonChainSprite()
        {
            abyssalHarpoonChainSprite ??= DragonBound.Presentation.UiAssets.Load<Sprite>(AbyssalHarpoonChainSpritePath);
            abyssalHarpoonHookSprite ??= DragonBound.Presentation.UiAssets.Load<Sprite>(AbyssalHarpoonHookSpritePath);
            abyssalHarpoonPortalSprite ??= DragonBound.Presentation.UiAssets.Load<Sprite>(AbyssalHarpoonPortalSpritePath);
            LogMissingAbyssalHarpoonResources();
            return abyssalHarpoonChainSprite;
        }

        private Sprite ResolveAbyssalHarpoonHookSprite()
        {
            abyssalHarpoonChainSprite ??= DragonBound.Presentation.UiAssets.Load<Sprite>(AbyssalHarpoonChainSpritePath);
            abyssalHarpoonHookSprite ??= DragonBound.Presentation.UiAssets.Load<Sprite>(AbyssalHarpoonHookSpritePath);
            abyssalHarpoonPortalSprite ??= DragonBound.Presentation.UiAssets.Load<Sprite>(AbyssalHarpoonPortalSpritePath);
            LogMissingAbyssalHarpoonResources();
            return abyssalHarpoonHookSprite;
        }

        private Sprite ResolveAbyssalHarpoonPortalSprite()
        {
            abyssalHarpoonChainSprite ??= DragonBound.Presentation.UiAssets.Load<Sprite>(AbyssalHarpoonChainSpritePath);
            abyssalHarpoonHookSprite ??= DragonBound.Presentation.UiAssets.Load<Sprite>(AbyssalHarpoonHookSpritePath);
            abyssalHarpoonPortalSprite ??= DragonBound.Presentation.UiAssets.Load<Sprite>(AbyssalHarpoonPortalSpritePath);
            LogMissingAbyssalHarpoonResources();
            return abyssalHarpoonPortalSprite;
        }

        private void LogMissingAbyssalHarpoonResources()
        {
            if (abyssalHarpoonResourceWarningLogged ||
                (abyssalHarpoonChainSprite != null &&
                 abyssalHarpoonHookSprite != null &&
                 abyssalHarpoonPortalSprite != null))
            {
                return;
            }

            abyssalHarpoonResourceWarningLogged = true;
            Debug.LogWarning(
                "Abyssal Harpooner presentation requires Resources/VFX/Abyssal Harpooner/road, boom and start sprites.",
                this);
        }

        private void QueueEmberShamanCast(
            CombatEvent combatEvent,
            Vector3 attackerPosition,
            Vector3 targetPosition)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            if (!pendingEmberShamanCasts.TryGetValue(attackerId, out var pending) ||
                pending.CreatedFrame != Time.frameCount)
            {
                if (pending != null)
                {
                    ReleaseEmberShamanCast(attackerId);
                }

                pending = new PendingEmberShamanCast(
                    Time.frameCount,
                    attackerPosition);
                pendingEmberShamanCasts[attackerId] = pending;
            }

            if (pending.TryAdd(combatEvent, targetPosition, 5))
            {
                lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
            }
        }

        private void HandleEmberShamanFireballReleased(string attackerRuntimeId)
        {
            ReleaseEmberShamanCast(attackerRuntimeId);
        }

        private void TickPendingEmberShamanCasts(float deltaSeconds)
        {
            if (pendingEmberShamanCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> fallbackReleases = null;
            foreach (var entry in pendingEmberShamanCasts)
            {
                entry.Value.Elapsed += deltaSeconds;
                if (entry.Value.Elapsed < emberShamanReleaseFallbackDelay)
                {
                    continue;
                }

                fallbackReleases ??= new List<string>();
                fallbackReleases.Add(entry.Key);
            }

            if (fallbackReleases == null)
            {
                return;
            }

            foreach (var attackerId in fallbackReleases)
            {
                ReleaseEmberShamanCast(attackerId);
            }
        }

        private void ReleaseEmberShamanCast(string attackerRuntimeId)
        {
            attackerRuntimeId = attackerRuntimeId ?? string.Empty;
            if (!pendingEmberShamanCasts.TryGetValue(attackerRuntimeId, out var pending))
            {
                return;
            }

            pendingEmberShamanCasts.Remove(attackerRuntimeId);
            if (emberShamanFireballSprite == null)
            {
                emberShamanFireballSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(EmberShamanFireballSpritePath);
            }

            foreach (var shot in pending.Shots)
            {
                SpawnEmberShamanFireball(pending.AttackerPosition, shot);
            }
        }

        private void SpawnEmberShamanFireball(
            Vector3 attackerPosition,
            PendingEmberShamanShot shot)
        {
            if (emberShamanFireballSprite == null)
            {
                CompleteEmberShamanImpact(shot);
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                "Ember Shaman Fireball",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = shot.IsPrimary
                ? SanitizeFxSize(emberShamanFireballSize, new Vector2(72f, 32f))
                : SanitizeFxSize(emberShamanSplashFireballSize, new Vector2(54f, 24f));
            var direction = shot.TargetPosition - attackerPosition;
            var start = attackerPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                start += direction.normalized * 24f;
            }
            rect.position = start;

            var image = root.GetComponent<Image>();
            image.sprite = emberShamanFireballSprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var distance = Vector3.Distance(start, shot.TargetPosition);
            var duration = Mathf.Clamp(
                distance / 720f,
                emberShamanFireballMinTravelDuration,
                Mathf.Max(emberShamanFireballMinTravelDuration, emberShamanFireballMaxTravelDuration));
            active.Add(new ActiveFx(
                rect,
                image,
                null,
                start,
                shot.TargetPosition,
                true,
                false,
                duration,
                false,
                () => CompleteEmberShamanImpact(shot),
                emberShamanFireballArcHeight,
                true,
                180f));
        }

        private void CompleteEmberShamanImpact(PendingEmberShamanShot shot)
        {
            if (shot.ImpactCompleted)
            {
                return;
            }

            shot.ImpactCompleted = true;
            var combatEvent = shot.CombatEvent;
            lane?.ReleaseEnemyHealthVisual(combatEvent.TargetRuntimeId);
            var position = shot.TargetPosition;
            if (lane != null &&
                lane.TryGetEnemyPosition(combatEvent.TargetRuntimeId, out var currentPosition))
            {
                position = currentPosition;
            }
            if (combatEvent.Damage > 0f && DamageNumberSettings.Visible)
            {
                SpawnLabel(
                    damageNumberTemplate,
                    position + (Vector3.up * damageNumberVerticalOffsetPixels),
                    $"-{combatEvent.Damage:0.##}",
                    damageNumberDuration);
            }
            if (combatEvent.Killed)
            {
                SpawnLabel(
                    suppliesGainTemplate,
                    position + (Vector3.up * 24f),
                    "+1 Supplies",
                    0.65f);
            }
        }

        private void QueueBowCast(
            CombatEvent combatEvent,
            Vector3 attackerPosition,
            Vector3 targetPosition)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            if (!pendingBowCasts.TryGetValue(attackerId, out var queue))
            {
                queue = new Queue<PendingBowCast>();
                pendingBowCasts[attackerId] = queue;
            }

            queue.Enqueue(new PendingBowCast(combatEvent, attackerPosition, targetPosition));
            lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
        }

        private void HandleWindclawSkillReleased(string attackerRuntimeId)
        {
            ReleaseNextWindclawSkillCast(attackerRuntimeId);
        }

        private void TickPendingWindclawSkillCasts(float deltaSeconds)
        {
            if (pendingWindclawSkillCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> fallbackReleases = null;
            foreach (var entry in pendingWindclawSkillCasts)
            {
                if (entry.Value.Count == 0)
                {
                    continue;
                }

                entry.Value.Peek().Elapsed += deltaSeconds;
                if (entry.Value.Peek().Elapsed < windclawReleaseFallbackDelay)
                {
                    continue;
                }

                fallbackReleases = fallbackReleases ?? new List<string>();
                fallbackReleases.Add(entry.Key);
            }

            if (fallbackReleases == null)
            {
                return;
            }

            foreach (var attackerId in fallbackReleases)
            {
                ReleaseNextWindclawSkillCast(attackerId);
            }
        }

        private void ReleaseNextWindclawSkillCast(string attackerRuntimeId)
        {
            attackerRuntimeId = attackerRuntimeId ?? string.Empty;
            if (!pendingWindclawSkillCasts.TryGetValue(
                    attackerRuntimeId,
                    out var queue) ||
                queue.Count == 0)
            {
                return;
            }

            var pending = queue.Dequeue();
            if (queue.Count == 0)
            {
                pendingWindclawSkillCasts.Remove(attackerRuntimeId);
            }

            SpawnWindclawImpact(pending);
        }

        private void SpawnWindclawImpact(PendingWindclawSkillCast pending)
        {
            var sprite = ResolveWindclawImpactSprite();

            if (sprite == null)
            {
                CompleteWindclawImpact(pending);
                return;
            }

            var position = pending.TargetPosition;

            // 敌人在动画期间可能继续移动，所以优先读取最新位置。
            if (lane != null &&
                lane.TryGetEnemyPosition(
                    pending.CombatEvent.TargetRuntimeId,
                    out var currentPosition))
            {
                position = currentPosition;
            }

            var parent =
                fixedBoardCanvas != null &&
                fixedBoardCanvas.CombatFxLayer != null
                    ? fixedBoardCanvas.CombatFxLayer
                    : transform;

            var root = new GameObject(
                "Windclaw Ranger Impact",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.position = position;
            rect.sizeDelta = windclawImpactSize;

            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            // road.png 出现的这一帧同步血条和伤害数字。
            CompleteWindclawImpact(pending);

            active.Add(new ActiveFx(
                rect,
                image,
                null,
                position,
                position,
                false,
                true,
                0.22f));
        }

        private void SpawnWindclawNormalVfx(CombatEvent combatEvent, Vector3 targetPosition)
        {
            if (windclawNormalVfxController == null)
            {
                // Probe silently: UiAssets.Load logs an error on a miss, and V1 has no such  
                // key, so an absent clip is an expected state rather than a failure.  
                var registry = DragonBound.Presentation.UiAssets.Active;
                windclawNormalVfxController = registry != null
                    ? registry.Load<RuntimeAnimatorController>(WindclawNormalVfxControllerPath)
                    : null;
            }

            if (windclawNormalVfxController == null)
            {
                return;
            }

            var position = targetPosition;

            // 敌人在动画期间可能继续移动，所以优先读取最新位置。  
            if (lane != null &&
                lane.TryGetEnemyPosition(
                    combatEvent.TargetRuntimeId,
                    out var currentPosition))
            {
                position = currentPosition;
            }

            var parent =
                fixedBoardCanvas != null &&
                fixedBoardCanvas.CombatFxLayer != null
                    ? fixedBoardCanvas.CombatFxLayer
                    : transform;

            var root = new GameObject(
                "Windclaw Ranger Normal VFX",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator));

            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.position = position;
            rect.sizeDelta = windclawNormalVfxSize;

            var image = root.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = windclawNormalVfxController;
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
            animator.speed = 1f;

            var duration = windclawNormalVfxController.animationClips.Length > 0
                ? windclawNormalVfxController.animationClips[0].length
                : 0.15f;
            duration = Mathf.Max(0.01f, duration);

            active.Add(new ActiveFx(
                rect,
                image,
                null,
                position,
                position,
                false,
                false,
                duration));
        }

        private void CompleteWindclawImpact(PendingWindclawSkillCast pending)
        {
            if (pending.Completed)
            {
                return;
            }

            pending.Completed = true;

            lane?.ReleaseEnemyHealthVisual(
                pending.CombatEvent.TargetRuntimeId);

            var position = pending.TargetPosition;

            if (lane != null &&
                lane.TryGetEnemyPosition(
                    pending.CombatEvent.TargetRuntimeId,
                    out var currentPosition))
            {
                position = currentPosition;
            }

            if (pending.CombatEvent.Damage > 0f &&
                DamageNumberSettings.Visible)
            {
                SpawnLabel(
                    damageNumberTemplate,
                    position + Vector3.up * damageNumberVerticalOffsetPixels,
                    $"-{pending.CombatEvent.Damage:0.##}",
                    damageNumberDuration);
            }

            if (pending.CombatEvent.Killed)
            {
                SpawnLabel(
                    suppliesGainTemplate,
                    position + Vector3.up * 24f,
                    "+1 Supplies",
                    0.65f);
            }
        }
        private void HandleBowProjectileReleased(string attackerRuntimeId)
        {
            ReleaseNextBowCast(attackerRuntimeId);
        }

        private void TickPendingBowCasts(float deltaSeconds)
        {
            if (pendingBowCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> fallbackReleases = null;
            foreach (var entry in pendingBowCasts)
            {
                foreach (var pending in entry.Value)
                {
                    pending.Elapsed += deltaSeconds;
                }

                if (entry.Value.Count == 0 || entry.Value.Peek().Elapsed < bowReleaseFallbackDelay)
                {
                    continue;
                }

                fallbackReleases ??= new List<string>();
                fallbackReleases.Add(entry.Key);
            }

            if (fallbackReleases == null)
            {
                return;
            }

            foreach (var attackerId in fallbackReleases)
            {
                ReleaseNextBowCast(attackerId);
            }
        }

        private void ReleaseNextBowCast(string attackerRuntimeId)
        {
            attackerRuntimeId ??= string.Empty;
            if (!pendingBowCasts.TryGetValue(attackerRuntimeId, out var queue) || queue.Count == 0)
            {
                return;
            }

            var pending = queue.Dequeue();
            if (queue.Count == 0)
            {
                pendingBowCasts.Remove(attackerRuntimeId);
            }
            SpawnBowSwordProjectile(pending);
        }

        private void SpawnBowSwordProjectile(PendingBowCast pending)
        {
            var sprite = ResolveBowSwordProjectileSprite();
            if (sprite == null)
            {
                CompleteBowImpact(pending);
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                "Bow Sword Projectile",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = SanitizeFxSize(bowSwordProjectileSize, new Vector2(48f, 41f));

            var direction = pending.TargetPosition - pending.AttackerPosition;
            var start = pending.AttackerPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                start += direction.normalized * 20f;
            }
            rect.position = start;
            OrientToProjectileTrajectory(
                rect,
                start,
                pending.TargetPosition,
                0f,
                bowSwordProjectileArcHeight,
                bowSwordProjectileRotationOffset);

            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var distance = Vector3.Distance(start, pending.TargetPosition);
            var duration = Mathf.Clamp(
                distance / 760f,
                bowSwordMinTravelDuration,
                Mathf.Max(bowSwordMinTravelDuration, bowSwordMaxTravelDuration));
            active.Add(new ActiveFx(
                rect,
                image,
                null,
                start,
                pending.TargetPosition,
                true,
                false,
                duration,
                false,
                () => CompleteBowImpact(pending),
                bowSwordProjectileArcHeight,
                true,
                bowSwordProjectileRotationOffset));
        }

        private static Vector3 EvaluateProjectilePosition(
            Vector3 start,
            Vector3 end,
            float normalized,
            float arcHeight)
        {
            normalized = Mathf.Clamp01(normalized);
            var arcOffset = 4f * Mathf.Max(0f, arcHeight) * normalized * (1f - normalized);
            return Vector3.Lerp(start, end, normalized) + (Vector3.up * arcOffset);
        }

        private static void OrientToProjectileTrajectory(
            RectTransform root,
            Vector3 start,
            Vector3 end,
            float normalized,
            float arcHeight,
            float rotationOffset)
        {
            if (root == null)
            {
                return;
            }

            const float sampleDistance = 0.01f;
            var sampleStart = Mathf.Clamp01(normalized - sampleDistance);
            var sampleEnd = Mathf.Clamp01(normalized + sampleDistance);
            var tangent = EvaluateProjectilePosition(start, end, sampleEnd, arcHeight) -
                          EvaluateProjectilePosition(start, end, sampleStart, arcHeight);
            if (tangent.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            root.localRotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
        }

        private void CompleteBowImpact(PendingBowCast pending)
        {
            if (pending.ImpactCompleted)
            {
                return;
            }

            pending.ImpactCompleted = true;
            lane?.ReleaseEnemyHealthVisual(pending.CombatEvent.TargetRuntimeId);
            var position = pending.TargetPosition;
            if (lane != null &&
                lane.TryGetEnemyPosition(pending.CombatEvent.TargetRuntimeId, out var currentPosition))
            {
                position = currentPosition;
            }

            if (pending.CombatEvent.Damage > 0f && DamageNumberSettings.Visible)
            {
                SpawnLabel(
                    damageNumberTemplate,
                    position + (Vector3.up * damageNumberVerticalOffsetPixels),
                    $"-{pending.CombatEvent.Damage:0.##}",
                    damageNumberDuration);
            }
            if (pending.CombatEvent.Killed)
            {
                SpawnLabel(suppliesGainTemplate, position + (Vector3.up * 24f), "+1 Supplies", 0.65f);
            }
        }

        private Sprite ResolveBowSwordProjectileSprite()
        {
            if (bowSwordProjectileSprite == null)
            {
                bowSwordProjectileSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(BowSwordProjectileSpritePath);
            }
            if (bowSwordProjectileSprite == null)
            {
                Debug.LogWarning(
                    "Bow sword projectile sprite is missing at Resources/VFX/Unit/road.",
                    this);
            }
            return bowSwordProjectileSprite;
        }

        private Sprite ResolveWindclawImpactSprite()
        {
            if (windclawImpactSprite == null)
            {
                windclawImpactSprite =
                    DragonBound.Presentation.UiAssets.Load<Sprite>(WindclawImpactSpritePath);
            }

            if (windclawImpactSprite == null)
            {
                Debug.LogWarning(
                    $"Windclaw Ranger impact sprite is missing at " +
                    $"Resources/{WindclawImpactSpritePath}.",
                    this);
            }

            return windclawImpactSprite;
        }
        private void QueueWindclawSkillCast(CombatEvent combatEvent, Vector3 targetPosition)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;

            if (!pendingWindclawSkillCasts.TryGetValue(
                    attackerId,
                    out var queue))
            {
                queue = new Queue<PendingWindclawSkillCast>();
                pendingWindclawSkillCasts[attackerId] = queue;
            }

            queue.Enqueue(
                new PendingWindclawSkillCast(combatEvent, targetPosition));

            // 游戏逻辑已经扣血，但画面暂时保持旧血量。
            lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
        }

        private void QueueSkyborneValkyrieCast(
            CombatEvent combatEvent,
            Vector3 attackerPosition,
            Vector3 targetPosition,
            bool isSkill)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            if (!pendingSkyborneValkyrieCasts.TryGetValue(attackerId, out var pending) ||
                pending.CreatedFrame != Time.frameCount ||
                pending.IsSkill != isSkill)
            {
                if (pending != null)
                {
                    ReleaseSkyborneValkyrieCast(attackerId);
                }

                pending = new PendingSkyborneValkyrieCast(
                    Time.frameCount,
                    attackerPosition,
                    isSkill);
                pendingSkyborneValkyrieCasts[attackerId] = pending;
            }

            var maximumArrows = isSkill ? 3 : 1;
            if (pending.TryAdd(combatEvent, targetPosition, maximumArrows))
            {
                lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
            }
        }

        private void HandleSkyborneValkyrieArrowReleased(string attackerRuntimeId)
        {
            ReleaseSkyborneValkyrieCast(attackerRuntimeId);
        }

        private void TickPendingSkyborneValkyrieCasts(float deltaSeconds)
        {
            if (pendingSkyborneValkyrieCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> fallbackReleases = null;
            foreach (var entry in pendingSkyborneValkyrieCasts)
            {
                entry.Value.Elapsed += deltaSeconds;
                if (entry.Value.Elapsed < skyborneValkyrieReleaseFallbackDelay)
                {
                    continue;
                }

                fallbackReleases ??= new List<string>();
                fallbackReleases.Add(entry.Key);
            }

            if (fallbackReleases == null)
            {
                return;
            }

            foreach (var attackerId in fallbackReleases)
            {
                ReleaseSkyborneValkyrieCast(attackerId);
            }
        }

        private void ReleaseSkyborneValkyrieCast(string attackerRuntimeId)
        {
            attackerRuntimeId ??= string.Empty;
            if (!pendingSkyborneValkyrieCasts.TryGetValue(attackerRuntimeId, out var pending))
            {
                return;
            }

            pendingSkyborneValkyrieCasts.Remove(attackerRuntimeId);
            foreach (var shot in pending.Shots)
            {
                SpawnSkyborneValkyrieArrow(pending.AttackerPosition, shot);
            }
        }

        private void SpawnSkyborneValkyrieArrow(
            Vector3 attackerPosition,
            PendingSkyborneValkyrieShot shot)
        {
            var sprite = ResolveSkyborneValkyrieArrowSprite();
            if (sprite == null)
            {
                CompleteSkyborneValkyrieImpact(shot);
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                "Skyborne Valkyrie Arrow",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = SanitizeFxSize(
                skyborneValkyrieArrowSize,
                new Vector2(68f, 29f));

            var direction = shot.TargetPosition - attackerPosition;
            var start = attackerPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                start += direction.normalized * 24f;
            }
            rect.position = start;
            OrientToProjectileTrajectory(
                rect,
                start,
                shot.TargetPosition,
                0f,
                skyborneValkyrieArrowArcHeight,
                skyborneValkyrieArrowRotationOffset);

            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var distance = Vector3.Distance(start, shot.TargetPosition);
            var duration = Mathf.Clamp(
                distance / 760f,
                skyborneValkyrieMinTravelDuration,
                Mathf.Max(
                    skyborneValkyrieMinTravelDuration,
                    skyborneValkyrieMaxTravelDuration));
            active.Add(new ActiveFx(
                rect,
                image,
                null,
                start,
                shot.TargetPosition,
                true,
                false,
                duration,
                false,
                () => SpawnSkyborneValkyrieExplosion(shot),
                skyborneValkyrieArrowArcHeight,
                true,
                skyborneValkyrieArrowRotationOffset));
        }

        private void SpawnSkyborneValkyrieExplosion(PendingSkyborneValkyrieShot shot)
        {
            var controller = ResolveSkyborneValkyrieExplosionController();
            if (controller == null)
            {
                CompleteSkyborneValkyrieImpact(shot);
                return;
            }

            var position = shot.TargetPosition;
            if (lane != null &&
                lane.TryGetEnemyPosition(shot.CombatEvent.TargetRuntimeId, out var currentPosition))
            {
                position = currentPosition;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                "Skyborne Valkyrie Boom",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator),
                typeof(SkyborneValkyrieExplosionEventRelay));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.position = position;
            rect.sizeDelta = SanitizeFxSize(
                skyborneValkyrieExplosionSize,
                new Vector2(90f, 90f));

            var image = root.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            root.GetComponent<SkyborneValkyrieExplosionEventRelay>().Bind(
                () => CompleteSkyborneValkyrieImpact(shot),
                () => Destroy(root));
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
        }

        private void CompleteSkyborneValkyrieImpact(PendingSkyborneValkyrieShot shot)
        {
            if (shot.ImpactCompleted)
            {
                return;
            }

            shot.ImpactCompleted = true;
            lane?.ReleaseEnemyHealthVisual(shot.CombatEvent.TargetRuntimeId);
            var position = shot.TargetPosition;
            if (lane != null &&
                lane.TryGetEnemyPosition(shot.CombatEvent.TargetRuntimeId, out var currentPosition))
            {
                position = currentPosition;
            }

            if (shot.CombatEvent.Damage > 0f && DamageNumberSettings.Visible)
            {
                SpawnLabel(
                    damageNumberTemplate,
                    position + (Vector3.up * damageNumberVerticalOffsetPixels),
                    $"-{shot.CombatEvent.Damage:0.##}",
                    damageNumberDuration);
            }
            if (shot.CombatEvent.Killed)
            {
                SpawnLabel(suppliesGainTemplate, position + (Vector3.up * 24f), "+1 Supplies", 0.65f);
            }
        }

        private Sprite ResolveSkyborneValkyrieArrowSprite()
        {
            if (skyborneValkyrieArrowSprite == null)
            {
                skyborneValkyrieArrowSprite =
                    DragonBound.Presentation.UiAssets.Load<Sprite>(SkyborneValkyrieArrowSpritePath);
            }
            if (skyborneValkyrieArrowSprite == null)
            {
                Debug.LogWarning(
                    $"Skyborne Valkyrie arrow sprite is missing at Resources/{SkyborneValkyrieArrowSpritePath}.",
                    this);
            }
            return skyborneValkyrieArrowSprite;
        }

        private RuntimeAnimatorController ResolveSkyborneValkyrieExplosionController()
        {
            if (skyborneValkyrieExplosionController == null)
            {
                skyborneValkyrieExplosionController =
                    DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(
                        SkyborneValkyrieExplosionControllerPath);
            }
            if (skyborneValkyrieExplosionController == null)
            {
                Debug.LogWarning(
                    $"Skyborne Valkyrie explosion is missing at Resources/{SkyborneValkyrieExplosionControllerPath}.",
                    this);
            }
            return skyborneValkyrieExplosionController;
        }

        private void QueueFlameDrakeCast(
            CombatEvent combatEvent,
            Vector3 attackerPosition,
            Vector3 targetPosition,
            bool isSkill)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            var pendingCasts = isSkill ? pendingFlameDrakeSkillCasts : pendingFlameDrakeCasts;
            if (!pendingCasts.TryGetValue(attackerId, out var pending) ||
                pending.CreatedFrame != Time.frameCount)
            {
                if (pending != null)
                {
                    ReleaseFlameDrakeCast(pendingCasts, attackerId);
                }

                pending = new PendingFlameDrakeCast(
                    Time.frameCount,
                    attackerPosition,
                    targetPosition,
                    combatEvent.TargetRuntimeId,
                    isSkill);
                pendingCasts[attackerId] = pending;
            }

            if (pending.Add(combatEvent))
            {
                lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
            }
        }

        private void HandleFlameDrakeFireballReleased(string attackerRuntimeId)
        {
            ReleaseFlameDrakeCast(pendingFlameDrakeSkillCasts, attackerRuntimeId);
            ReleaseFlameDrakeCast(pendingFlameDrakeCasts, attackerRuntimeId);
        }

        private void TickPendingFlameDrakeCasts(
            Dictionary<string, PendingFlameDrakeCast> pendingCasts,
            float deltaSeconds)
        {
            if (pendingCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> fallbackReleases = null;
            foreach (var entry in pendingCasts)
            {
                entry.Value.Elapsed += deltaSeconds;
                if (entry.Value.Elapsed < flameDrakeReleaseFallbackDelay)
                {
                    continue;
                }

                if (fallbackReleases == null)
                {
                    fallbackReleases = new List<string>();
                }
                fallbackReleases.Add(entry.Key);
            }

            if (fallbackReleases == null)
            {
                return;
            }

            foreach (var attackerId in fallbackReleases)
            {
                ReleaseFlameDrakeCast(pendingCasts, attackerId);
            }
        }

        private void ReleaseFlameDrakeCast(
            Dictionary<string, PendingFlameDrakeCast> pendingCasts,
            string attackerRuntimeId)
        {
            attackerRuntimeId = attackerRuntimeId ?? string.Empty;
            if (!pendingCasts.TryGetValue(attackerRuntimeId, out var pending))
            {
                return;
            }

            pendingCasts.Remove(attackerRuntimeId);
            SpawnFlameDrakeFireball(pending);
        }

        private void SpawnFlameDrakeFireball(PendingFlameDrakeCast pending)
        {
            var sprite = ResolveFlameDrakeFireballSprite(pending.IsSkill);
            if (sprite == null)
            {
                CompleteFlameDrakeImpact(pending);
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                "Flame Drake Rider Fireball",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = SanitizeFxSize(
                pending.IsSkill ? flameDrakeSkillFireballSize : flameDrakeNormalFireballSize,
                pending.IsSkill ? new Vector2(92f, 58f) : new Vector2(68f, 42f));
            var direction = pending.TargetPosition - pending.AttackerPosition;
            var start = pending.AttackerPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                start += direction.normalized * 28f;
                if (pending.IsSkill)
                {
                    OrientToProjectileTrajectory(
                        rect,
                        start,
                        pending.TargetPosition,
                        0f,
                        flameDrakeFireballArcHeight,
                        0f);
                }
                else
                {
                    rect.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                }
            }
            rect.position = start;

            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var distance = Vector3.Distance(start, pending.TargetPosition);
            var duration = Mathf.Clamp(
                distance / 720f,
                flameDrakeFireballMinTravelDuration,
                Mathf.Max(flameDrakeFireballMinTravelDuration, flameDrakeFireballMaxTravelDuration));
            active.Add(new ActiveFx(
                rect,
                image,
                null,
                start,
                pending.TargetPosition,
                true,
                false,
                duration,
                false,
                () => SpawnFlameDrakeExplosion(pending),
                flameDrakeFireballArcHeight,
                pending.IsSkill,
                0f,
                null,
                () => ResolveFlameDrakeImpactPosition(pending)));
        }

        private void SpawnFlameDrakeExplosion(PendingFlameDrakeCast pending)
        {
            pending.MarkFireballArrived();
            if (pending.IsSkill && lane != null &&
                lane.TryGetEnemyPathTileIndex(
                    pending.PrimaryTargetRuntimeId,
                    out var impactPathTileIndex))
            {
                pending.SetImpactPathTileIndex(impactPathTileIndex);
            }

            var controller = ResolveFlameDrakeExplosionController(pending.IsSkill);
            if (controller == null)
            {
                CompleteFlameDrakeImpact(pending);
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                pending.IsSkill ? "Flame Drake Rider Skill Boom" : "Flame Drake Rider Boom",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator),
                typeof(FlameDrakeExplosionEventRelay));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.position = ResolveFlameDrakeImpactPosition(pending);
            rect.sizeDelta = SanitizeFxSize(
                pending.IsSkill ? flameDrakeSkillExplosionSize : flameDrakeNormalExplosionSize,
                pending.IsSkill ? new Vector2(145f, 145f) : new Vector2(105f, 105f));
            var image = root.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            var relay = root.GetComponent<FlameDrakeExplosionEventRelay>();
            relay.Bind(
                () => CompleteFlameDrakeImpact(pending),
                () =>
                {
                    pending.MarkExplosionCompleted();
                    TrySpawnFlameDrakeBurningGround(pending);
                    Destroy(root);
                });
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
        }

        private void TrySpawnFlameDrakeBurningGround(PendingFlameDrakeCast pending)
        {
            if (!pending.IsSkill ||
                !pending.FireballArrived ||
                !pending.ExplosionCompleted ||
                pending.BurningGroundSpawned ||
                pending.ImpactPathTileIndex < 1)
            {
                return;
            }

            pending.MarkBurningGroundSpawned();
            var controller = ResolveFlameDrakeBurningGroundController();
            if (controller == null)
            {
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var tilePositions = new List<Vector3>(3);
            if (lane == null ||
                !lane.TryGetBurningGroundPathTiles(
                    pending.ImpactPathTileIndex,
                    tilePositions))
            {
                return;
            }

            for (var index = 0; index < tilePositions.Count; index++)
            {
                var position = tilePositions[index];
                var root = new GameObject(
                    $"Flame Drake Rider Burning Ground {index + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Animator));
                var rect = root.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.position = position;
                rect.sizeDelta = new Vector2(
                    FlameDrakeBurningGroundTileSize,
                    FlameDrakeBurningGroundTileSize);
                rect.SetAsFirstSibling();

                var image = root.GetComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;

                var animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.Rebind();
                animator.Play(0, 0, 0f);
                animator.Update(0f);

                active.Add(new ActiveFx(
                    rect,
                    image,
                    null,
                    position,
                    position,
                    false,
                    false,
                    flameDrakeBurningGroundDuration));
            }
        }

        private void CompleteFlameDrakeImpact(PendingFlameDrakeCast pending)
        {
            if (pending.ImpactCompleted)
            {
                return;
            }

            pending.ImpactCompleted = true;
            foreach (var combatEvent in pending.Events)
            {
                lane?.ReleaseEnemyHealthVisual(combatEvent.TargetRuntimeId);
                var position = pending.TargetPosition;
                if (lane != null &&
                    lane.TryGetEnemyVisualImpactPosition(
                        combatEvent.TargetRuntimeId,
                        out var currentPosition))
                {
                    position = currentPosition;
                }
                if (combatEvent.Damage > 0f && DamageNumberSettings.Visible)
                {
                    SpawnLabel(
                        damageNumberTemplate,
                        position + (Vector3.up * damageNumberVerticalOffsetPixels),
                        $"-{combatEvent.Damage:0.##}",
                        damageNumberDuration);
                }
                if (combatEvent.Killed)
                {
                    SpawnLabel(
                        suppliesGainTemplate,
                        position + (Vector3.up * 24f),
                        "+1 Supplies",
                        0.65f);
                }
            }
        }

        private Vector3 ResolveFlameDrakeImpactPosition(PendingFlameDrakeCast pending)
        {
            if (lane != null &&
                lane.TryGetEnemyVisualImpactPosition(
                    pending.PrimaryTargetRuntimeId,
                    out var currentPosition))
            {
                return currentPosition;
            }

            return pending.TargetPosition;
        }

        private Sprite ResolveFlameDrakeFireballSprite(bool isSkill)
        {
            if (isSkill)
            {
                if (flameDrakeSkillFireballSprite == null)
                {
                    flameDrakeSkillFireballSprite =
                        DragonBound.Presentation.UiAssets.Load<Sprite>(FlameDrakeSkillFireballSpritePath);
                }
                if (flameDrakeSkillFireballSprite == null)
                {
                    WarnIfFlameDrakeResourcesMissing();
                }
                return flameDrakeSkillFireballSprite;
            }

            if (flameDrakeNormalFireballSprite == null)
            {
                flameDrakeNormalFireballSprite =
                    DragonBound.Presentation.UiAssets.Load<Sprite>(FlameDrakeNormalFireballSpritePath);
            }
            if (flameDrakeNormalFireballSprite == null)
            {
                WarnIfFlameDrakeResourcesMissing();
            }
            return flameDrakeNormalFireballSprite;
        }

        private RuntimeAnimatorController ResolveFlameDrakeExplosionController(bool isSkill)
        {
            if (isSkill)
            {
                if (flameDrakeSkillExplosionController == null)
                {
                    flameDrakeSkillExplosionController =
                        DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(FlameDrakeSkillExplosionControllerPath);
                }
                if (flameDrakeSkillExplosionController == null)
                {
                    WarnIfFlameDrakeResourcesMissing();
                }
                return flameDrakeSkillExplosionController;
            }

            if (flameDrakeNormalExplosionController == null)
            {
                flameDrakeNormalExplosionController =
                    DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(FlameDrakeNormalExplosionControllerPath);
            }
            if (flameDrakeNormalExplosionController == null)
            {
                WarnIfFlameDrakeResourcesMissing();
            }
            return flameDrakeNormalExplosionController;
        }

        private RuntimeAnimatorController ResolveFlameDrakeBurningGroundController()
        {
            if (flameDrakeBurningGroundController == null)
            {
                flameDrakeBurningGroundController =
                    DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(FlameDrakeBurningGroundControllerPath);
            }
            if (flameDrakeBurningGroundController == null)
            {
                WarnIfFlameDrakeResourcesMissing();
            }
            return flameDrakeBurningGroundController;
        }

        private void WarnIfFlameDrakeResourcesMissing()
        {
            if (flameDrakeResourceWarningLogged ||
                (flameDrakeNormalFireballSprite != null &&
                 flameDrakeSkillFireballSprite != null &&
                 flameDrakeNormalExplosionController != null &&
                 flameDrakeSkillExplosionController != null &&
                 flameDrakeBurningGroundController != null))
            {
                return;
            }

            flameDrakeResourceWarningLogged = true;
            Debug.LogWarning(
                "Flame Drake Rider presentation requires Resources/VFX/Flame Drake Rider/road, " +
                "Resources/VFX/Flame Drake Rider/Sroad, Resources/Animation/Flame Drake Rider Boom, " +
                "Resources/Animation/FlameDrakeRiderBoomS and Resources/Animation/boomBoard.",
                this);
        }

        private static Vector2 SanitizeFxSize(Vector2 configured, Vector2 fallback)
        {
            return new Vector2(
                configured.x > 0f ? configured.x : fallback.x,
                configured.y > 0f ? configured.y : fallback.y);
        }

        private void SpawnImage(
            Image template,
            Vector3 start,
            Vector3 end,
            CombatFxPlacementMode fallbackPlacement,
            bool fallbackFade,
            float? durationOverride = null)
        {
            if (template == null)
            {
                return;
            }

            var image = Instantiate(template, template.transform.parent);
            image.gameObject.SetActive(true);
            var rect = image.rectTransform;
            var distance = Vector3.Distance(start, end);
            var authoring = template.GetComponent<CombatFxAuthoring>();
            var placement = authoring != null ? authoring.Placement : fallbackPlacement;
            var offset = authoring != null ? (Vector3)authoring.PositionOffset : Vector3.zero;
            var angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            var authoredRotation = rect.localRotation;

            if (placement == CombatFxPlacementMode.Projectile)
            {
                rect.position = start + offset;
            }
            else if (placement == CombatFxPlacementMode.StretchBetweenPoints)
            {
                rect.position = ((start + end) * 0.5f) + offset;
                var lengthScale = authoring != null ? authoring.LengthScale : 1f;
                rect.sizeDelta = new Vector2(Mathf.Max(8f, distance * lengthScale), rect.sizeDelta.y);
                rect.localRotation = Quaternion.Euler(0f, 0f, angle) * authoredRotation;
            }
            else
            {
                rect.position = end + offset;
            }

            if (authoring != null && authoring.OrientToAttackDirection && placement != CombatFxPlacementMode.StretchBetweenPoints)
            {
                rect.localRotation = Quaternion.Euler(0f, 0f, angle) * authoredRotation;
            }

            var projectile = placement == CombatFxPlacementMode.Projectile;
            var fade = authoring != null ? authoring.Fade : fallbackFade;
            var duration = durationOverride ?? (authoring != null ? authoring.Duration : 0.28f);
            active.Add(new ActiveFx(rect, image, null, start + offset, end + offset, projectile, fade, duration));
        }

        private bool QueueNightfangSkillCast(CombatEvent combatEvent, Vector3 targetPosition)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            if (!pendingNightfangSkillCasts.TryGetValue(attackerId, out var queue))
            {
                queue = new Queue<PendingNightfangSkillCast>();
                pendingNightfangSkillCasts[attackerId] = queue;
            }

            var shouldStartAnimation = queue.Count == 0;
            queue.Enqueue(new PendingNightfangSkillCast(combatEvent, targetPosition));
            lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
            return shouldStartAnimation;
        }

        private void HandleNightfangSkillAnimationCompleted(string attackerRuntimeId)
        {
            if (ReleaseNextNightfangSkillCast(attackerRuntimeId))
            {
                board?.PlayHeroFormationAttackAnimation(attackerRuntimeId, true);
            }
        }

        private void TickPendingNightfangSkillCasts(float deltaSeconds)
        {
            if (pendingNightfangSkillCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> fallbackReleases = null;
            foreach (var entry in pendingNightfangSkillCasts)
            {
                if (entry.Value.Count == 0)
                {
                    continue;
                }
                entry.Value.Peek().Elapsed += deltaSeconds;
                if (entry.Value.Peek().Elapsed < nightfangSkillReleaseFallbackDelay)
                {
                    continue;
                }
                fallbackReleases = fallbackReleases ?? new List<string>();
                fallbackReleases.Add(entry.Key);
            }

            if (fallbackReleases == null)
            {
                return;
            }
            foreach (var attackerId in fallbackReleases)
            {
                if (ReleaseNextNightfangSkillCast(attackerId))
                {
                    board?.PlayHeroFormationAttackAnimation(attackerId, true);
                }
            }
        }

        private bool ReleaseNextNightfangSkillCast(string attackerRuntimeId)
        {
            attackerRuntimeId = attackerRuntimeId ?? string.Empty;
            if (!pendingNightfangSkillCasts.TryGetValue(attackerRuntimeId, out var queue) ||
                queue.Count == 0)
            {
                return false;
            }

            var pending = queue.Dequeue();
            if (queue.Count == 0)
            {
                pendingNightfangSkillCasts.Remove(attackerRuntimeId);
            }
            else
            {
                queue.Peek().Elapsed = 0f;
            }
            SpawnNightfangExplosion(pending);
            return queue.Count > 0;
        }

        private void SpawnNightfangExplosion(PendingNightfangSkillCast pending)
        {
            var controller = ResolveNightfangExplosionController();
            if (controller == null)
            {
                CompleteNightfangSkillImpact(pending);
                return;
            }

            var position = pending.TargetPosition;
            if (lane != null &&
                lane.TryGetEnemyPosition(pending.CombatEvent.TargetRuntimeId, out var currentPosition))
            {
                position = currentPosition;
            }
            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                "Nightfang Assassin Boom",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator),
                typeof(NightfangExplosionEventRelay));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.position = position;
            rect.sizeDelta = SanitizeFxSize(nightfangExplosionSize, new Vector2(100f, 100f));
            var image = root.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            root.GetComponent<NightfangExplosionEventRelay>().Bind(
                () => CompleteNightfangSkillImpact(pending),
                () => Destroy(root));
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
        }

        private void CompleteNightfangSkillImpact(PendingNightfangSkillCast pending)
        {
            if (pending.ImpactCompleted)
            {
                return;
            }
            pending.ImpactCompleted = true;
            lane?.ReleaseEnemyHealthVisual(pending.CombatEvent.TargetRuntimeId);
            var position = pending.TargetPosition;
            if (lane != null &&
                lane.TryGetEnemyPosition(pending.CombatEvent.TargetRuntimeId, out var currentPosition))
            {
                position = currentPosition;
            }
            if (pending.CombatEvent.Damage > 0f && DamageNumberSettings.Visible)
            {
                SpawnLabel(
                    damageNumberTemplate,
                    position + (Vector3.up * damageNumberVerticalOffsetPixels),
                    $"-{pending.CombatEvent.Damage:0.##}",
                    damageNumberDuration);
            }
            if (pending.CombatEvent.Killed)
            {
                SpawnLabel(
                    suppliesGainTemplate,
                    position + (Vector3.up * 24f),
                    "+1 Supplies",
                    0.65f);
            }
        }

        private RuntimeAnimatorController ResolveNightfangExplosionController()
        {
            if (nightfangExplosionController == null)
            {
                nightfangExplosionController =
                    DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(NightfangExplosionControllerPath);
            }
            if (nightfangExplosionController == null)
            {
                Debug.LogWarning(
                    $"Nightfang Assassin explosion is missing at Resources/{NightfangExplosionControllerPath}.",
                    this);
            }
            return nightfangExplosionController;
        }

        private bool QueueStarfallCast(
            CombatEvent combatEvent,
            Vector3 attackerPosition,
            Vector3 targetPosition,
            bool isSkill)
        {
            var attackerId = combatEvent.AttackerRuntimeId ?? string.Empty;
            var pendingCasts = isSkill ? pendingStarfallSkillCasts : pendingStarfallNormalCasts;
            var created = false;
            if (!pendingCasts.TryGetValue(attackerId, out var pending) ||
                pending.CreatedFrame != Time.frameCount)
            {
                if (pending != null)
                {
                    ReleaseStarfallCast(pendingCasts, attackerId);
                }

                pending = new PendingStarfallCast(
                    Time.frameCount,
                    attackerPosition,
                    targetPosition,
                    isSkill);
                pendingCasts[attackerId] = pending;
                created = true;
            }

            if (pending.Add(combatEvent))
            {
                lane?.HoldEnemyHealthVisual(combatEvent.TargetRuntimeId);
            }
            return created;
        }

        private void HandleStarfallArchmageGemReleased(string attackerRuntimeId)
        {
            // Skill is checked first because its explicit startup animation owns this release event.
            if (ReleaseStarfallCast(pendingStarfallSkillCasts, attackerRuntimeId))
            {
                return;
            }
            ReleaseStarfallCast(pendingStarfallNormalCasts, attackerRuntimeId);
        }

        private void TickPendingStarfallCasts(
            Dictionary<string, PendingStarfallCast> pendingCasts,
            float deltaSeconds)
        {
            if (pendingCasts.Count == 0 || deltaSeconds <= 0f)
            {
                return;
            }

            List<string> fallbackReleases = null;
            foreach (var entry in pendingCasts)
            {
                entry.Value.Elapsed += deltaSeconds;
                if (entry.Value.Elapsed < starfallGemReleaseFallbackDelay)
                {
                    continue;
                }

                fallbackReleases = fallbackReleases ?? new List<string>();
                fallbackReleases.Add(entry.Key);
            }

            if (fallbackReleases == null)
            {
                return;
            }
            foreach (var attackerId in fallbackReleases)
            {
                ReleaseStarfallCast(pendingCasts, attackerId);
            }
        }

        private bool ReleaseStarfallCast(
            Dictionary<string, PendingStarfallCast> pendingCasts,
            string attackerRuntimeId)
        {
            attackerRuntimeId = attackerRuntimeId ?? string.Empty;
            if (!pendingCasts.TryGetValue(attackerRuntimeId, out var pending))
            {
                return false;
            }

            pendingCasts.Remove(attackerRuntimeId);
            SpawnStarfallGem(pending);
            return true;
        }

        private void SpawnStarfallGem(PendingStarfallCast pending)
        {
            var sprite = ResolveStarfallNormalSprite();
            if (sprite == null)
            {
                CompleteStarfallImpact(pending);
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                "Starfall Archmage Gem",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var gemHeight = Mathf.Max(1f, starfallGemSize);
            var gemAspect = sprite.rect.height > 0f
                ? sprite.rect.width / sprite.rect.height
                : 1f;
            rect.sizeDelta = new Vector2(gemHeight * gemAspect, gemHeight);
            var direction = pending.TargetPosition - pending.AttackerPosition;
            var start = pending.AttackerPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                start += direction.normalized * 24f;
            }
            rect.position = start;
            OrientToProjectileTrajectory(
                rect,
                start,
                pending.TargetPosition,
                0f,
                starfallGemArcHeight,
                starfallGemRotationOffset);

            var image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            CreateStarfallGemRibbon(rect, parent, gemHeight);

            var distance = Vector3.Distance(start, pending.TargetPosition);
            var duration = Mathf.Clamp(
                distance / 720f,
                starfallGemMinTravelDuration,
                Mathf.Max(starfallGemMinTravelDuration, starfallGemMaxTravelDuration));
            active.Add(new ActiveFx(
                rect,
                image,
                null,
                start,
                pending.TargetPosition,
                true,
                false,
                duration,
                false,
                () =>
                {
                    if (pending.IsSkill)
                    {
                        SpawnStarfallSkillExplosion(pending);
                    }
                    else
                    {
                        CompleteStarfallImpact(pending);
                    }
                },
                starfallGemArcHeight,
                true,
                starfallGemRotationOffset));
        }

        private void CreateStarfallGemRibbon(
            RectTransform projectile,
            Transform parent,
            float gemSize)
        {
            var ribbonObject = new GameObject(
                "Starfall Archmage Gem Ribbon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(StarfallRibbonTrailGraphic),
                typeof(StarfallGemRibbonController));
            var ribbonRect = ribbonObject.GetComponent<RectTransform>();
            StretchToParent(ribbonRect, parent);
            ribbonRect.SetSiblingIndex(Mathf.Max(0, projectile.GetSiblingIndex()));
            projectile.SetAsLastSibling();

            var ribbon = ribbonObject.GetComponent<StarfallRibbonTrailGraphic>();
            ribbon.Configure(
                Mathf.Min(starfallRibbonWidth, Mathf.Max(2f, gemSize * 0.32f)),
                starfallRibbonLifetime,
                starfallRibbonHeadColor,
                starfallRibbonTailColor);
            ribbonObject.GetComponent<StarfallGemRibbonController>().Bind(
                projectile,
                ribbon,
                starfallRibbonLifetime);
        }

        private void SpawnStarfallSkillExplosion(PendingStarfallCast pending)
        {
            var controller = ResolveStarfallExplosionController();
            if (controller == null)
            {
                CompleteStarfallImpact(pending);
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            var root = new GameObject(
                "Starfall Archmage Boom",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator),
                typeof(StarfallExplosionEventRelay));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.position = pending.TargetPosition;
            rect.sizeDelta = SanitizeFxSize(starfallSkillExplosionSize, new Vector2(145f, 145f));
            var image = root.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            root.GetComponent<StarfallExplosionEventRelay>().Bind(
                () => CompleteStarfallImpact(pending),
                () => Destroy(root));
            var animator = root.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
        }

        private void CompleteStarfallImpact(PendingStarfallCast pending)
        {
            if (pending.ImpactCompleted)
            {
                return;
            }

            pending.ImpactCompleted = true;
            foreach (var combatEvent in pending.Events)
            {
                lane?.ReleaseEnemyHealthVisual(combatEvent.TargetRuntimeId);
                var position = pending.TargetPosition;
                if (lane != null &&
                    lane.TryGetEnemyPosition(combatEvent.TargetRuntimeId, out var currentPosition))
                {
                    position = currentPosition;
                }
                if (combatEvent.Damage > 0f && DamageNumberSettings.Visible)
                {
                    SpawnLabel(
                        damageNumberTemplate,
                        position + (Vector3.up * damageNumberVerticalOffsetPixels),
                        $"-{combatEvent.Damage:0.##}",
                        damageNumberDuration);
                }
                if (combatEvent.Killed)
                {
                    SpawnLabel(
                        suppliesGainTemplate,
                        position + (Vector3.up * 24f),
                        "+1 Supplies",
                        0.65f);
                }
            }
        }

        private RuntimeAnimatorController ResolveStarfallExplosionController()
        {
            if (starfallExplosionController == null)
            {
                starfallExplosionController =
                    DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>(StarfallExplosionControllerPath);
            }
            if (starfallExplosionController == null && !starfallSpriteWarningLogged)
            {
                starfallSpriteWarningLogged = true;
                Debug.LogWarning(
                    $"Starfall Archmage explosion is missing at Resources/{StarfallExplosionControllerPath}.",
                    this);
            }
            return starfallExplosionController;
        }

        private bool ShouldSpawnStarfallNormal(string attackerRuntimeId)
        {
            if (string.IsNullOrWhiteSpace(attackerRuntimeId))
            {
                return true;
            }

            if (starfallNormalFrameByAttacker.TryGetValue(attackerRuntimeId, out var frame) &&
                frame == Time.frameCount)
            {
                return false;
            }

            starfallNormalFrameByAttacker[attackerRuntimeId] = Time.frameCount;
            return true;
        }

        private void SpawnStarfallNormalProjectile(Vector3 start, Vector3 end)
        {
            var sprite = ResolveStarfallNormalSprite();
            if (sprite == null)
            {
                return;
            }

            var effect = CreateStarfallParticleEffect("ART_StarfallNormalProjectile(Clone)", true);
            var effectRect = effect.rectTransform;

            var localStart = (Vector2)effectRect.InverseTransformPoint(start);
            var localEnd = (Vector2)effectRect.InverseTransformPoint(end);
            effect.Configure(
                sprite,
                localStart,
                localEnd,
                starfallNormalTravelDuration,
                starfallNormalHeadSize,
                starfallNormalTrailInterval,
                starfallNormalTrailLifetime,
                starfallNormalTrailSize,
                starfallNormalImpactParticleCount,
                starfallNormalImpactRadius,
                starfallNormalHeadColor,
                starfallNormalTrailColor,
                starfallRibbonWidth,
                starfallRibbonLifetime,
                starfallRibbonHeadColor,
                starfallRibbonTailColor);
            activeStarfallEffects.Add(effect);
        }

        private void SpawnStarfallImpact(Vector3 target)
        {
            var sprite = ResolveStarfallNormalSprite();
            if (sprite == null)
            {
                return;
            }

            var effect = CreateStarfallParticleEffect("ART_StarfallImpact(Clone)", false);
            var localTarget = (Vector2)effect.rectTransform.InverseTransformPoint(target);
            effect.ConfigureImpact(
                sprite,
                localTarget,
                starfallNormalHeadSize * 0.85f,
                starfallNormalImpactParticleCount,
                starfallNormalImpactRadius,
                starfallNormalHeadColor,
                starfallNormalTrailColor);
            activeStarfallEffects.Add(effect);
        }

        private StarfallUiParticleEffect CreateStarfallParticleEffect(string objectName, bool includeRibbon)
        {
            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : transform;
            StarfallRibbonTrailGraphic ribbon = null;
            if (includeRibbon)
            {
                var ribbonObject = new GameObject(
                    "ART_StarfallRibbonTrail(Clone)",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(StarfallRibbonTrailGraphic));
                var ribbonRect = ribbonObject.GetComponent<RectTransform>();
                StretchToParent(ribbonRect, parent);
                ribbon = ribbonObject.GetComponent<StarfallRibbonTrailGraphic>();
            }

            var effectObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(ParticleSystem),
                typeof(StarfallUiParticleEffect));
            var effectRect = effectObject.GetComponent<RectTransform>();
            StretchToParent(effectRect, parent);
            var effect = effectObject.GetComponent<StarfallUiParticleEffect>();
            effect.AttachRibbon(ribbon);
            return effect;
        }

        private static void StretchToParent(RectTransform rect, Transform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private Sprite ResolveStarfallNormalSprite()
        {
            if (ART_StarfallNormalSprite != null)
            {
                return ART_StarfallNormalSprite;
            }

            ART_StarfallNormalSprite = DragonBound.Presentation.UiAssets.Load<Sprite>(StarfallNormalSpritePath);
            if (ART_StarfallNormalSprite == null && !starfallSpriteWarningLogged)
            {
                starfallSpriteWarningLogged = true;
                Debug.LogWarning(
                    $"Starfall Archmage normal VFX sprite is missing at Resources/{StarfallNormalSpritePath}.",
                    this);
            }
            return ART_StarfallNormalSprite;
        }

        private bool ShouldSpawnDragonRiderDive(string attackerRuntimeId)
        {
            if (string.IsNullOrWhiteSpace(attackerRuntimeId))
            {
                return false;
            }

            if (dragonRiderDiveFrameByAttacker.TryGetValue(attackerRuntimeId, out var frame) &&
                frame == Time.frameCount)
            {
                return false;
            }

            dragonRiderDiveFrameByAttacker[attackerRuntimeId] = Time.frameCount;
            return true;
        }

        private void SpawnDragonRiderDive(Vector3 start, Vector3 end, string attackerRuntimeId)
        {
            var template = ResolveDragonRiderDiveTemplate();
            if (template == null)
            {
                return;
            }

            var parent = fixedBoardCanvas != null && fixedBoardCanvas.CombatFxLayer != null
                ? fixedBoardCanvas.CombatFxLayer
                : template.rectTransform.parent;
            var image = Instantiate(template, parent);
            image.gameObject.name = "Flame Drake Rider S";
            image.gameObject.SetActive(true);
            image.raycastTarget = false;
            image.preserveAspect = true;

            var rect = image.rectTransform;
            rect.position = start;
            var diveEnd = ExtendDiveEndPastTarget(parent, start, end, flameDrakeRiderDiveOvershoot);
            var direction = diveEnd - start;
            if (direction.sqrMagnitude > 0.0001f)
            {
                var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle - 180f);
            }

            var animator = image.GetComponent<Animator>();
            var duration = 0.8f;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var clips = animator.runtimeAnimatorController.animationClips;
                if (clips != null && clips.Length > 0 && clips[0] != null)
                {
                    duration = Mathf.Max(0.1f, clips[0].length);
                }

                animator.enabled = true;
                animator.speed = 1f;
                animator.Rebind();
                animator.Play(0, 0, 0f);
                animator.Update(0f);
            }

            board?.SetHeroFormationArtVisible(attackerRuntimeId, false);
            active.Add(new ActiveFx(
                rect,
                image,
                null,
                start,
                diveEnd,
                true,
                false,
                duration,
                true,
                () => board?.SetHeroFormationArtVisible(attackerRuntimeId, true)));
        }

        private static Vector3 ExtendDiveEndPastTarget(
            Transform movementParent,
            Vector3 start,
            Vector3 target,
            float overshoot)
        {
            if (overshoot <= 0f)
            {
                return target;
            }

            if (movementParent is RectTransform parentRect)
            {
                var localStart = parentRect.InverseTransformPoint(start);
                var localTarget = parentRect.InverseTransformPoint(target);
                var localDirection = localTarget - localStart;
                if (localDirection.sqrMagnitude > 0.0001f)
                {
                    localTarget += localDirection.normalized * overshoot;
                    return parentRect.TransformPoint(localTarget);
                }
            }

            var worldDirection = target - start;
            return worldDirection.sqrMagnitude > 0.0001f
                ? target + (worldDirection.normalized * overshoot)
                : target;
        }

        private Image ResolveDragonRiderDiveTemplate()
        {
            if (ART_FlameDrakeRiderDive != null)
            {
                ART_FlameDrakeRiderDive.raycastTarget = false;
                ART_FlameDrakeRiderDive.gameObject.SetActive(false);
                return ART_FlameDrakeRiderDive;
            }

            var controller = DragonBound.Presentation.UiAssets.Load<RuntimeAnimatorController>("Animation/Flame Drake Rider S");
            if (controller == null)
            {
                Debug.LogWarning("Flame Drake Rider dive animation is missing at Resources/Animation/Flame Drake Rider S.");
                return null;
            }

            var root = new GameObject(
                "Flame Drake Rider S",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Animator));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(fixedBoardCanvas != null ? fixedBoardCanvas.CombatFxLayer : transform, false);
            rect.sizeDelta = new Vector2(145f, 145f);
            var image = root.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            root.GetComponent<Animator>().runtimeAnimatorController = controller;
            root.SetActive(false);
            ART_FlameDrakeRiderDive = image;
            return image;
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
            var authoring = template.GetComponent<CombatFxAuthoring>();
            var cellSize = fixedBoardCanvas != null
                ? Mathf.Min(fixedBoardCanvas.CellSize.x, fixedBoardCanvas.CellSize.y)
                : 64f;
            var radiusScale = authoring != null ? authoring.RadiusScale : 1f;
            var diameter = Mathf.Max(20f, radiusCells * cellSize * 2f * radiusScale);
            var useGameplayRadius = authoring == null || authoring.Placement == CombatFxPlacementMode.GameplayRadius;
            warning.rectTransform.position = position + (authoring != null ? (Vector3)authoring.PositionOffset : Vector3.zero);
            if (useGameplayRadius)
            {
                warning.rectTransform.sizeDelta = Vector2.one * diameter;
            }
            active.Add(new ActiveFx(
                warning.rectTransform,
                warning,
                null,
                position,
                position,
                false,
                authoring != null ? authoring.Fade : true,
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
            if (itemImpactTemplate != null) itemImpactTemplate.gameObject.SetActive(false);
            if (starfallWarningTemplate != null) starfallWarningTemplate.gameObject.SetActive(false);
            if (ART_EmberExplosiveFireball != null) ART_EmberExplosiveFireball.gameObject.SetActive(false);
            if (ART_FlameDrakeRiderDive != null) ART_FlameDrakeRiderDive.gameObject.SetActive(false);
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
                float duration,
                bool easeIn = false,
                Action onComplete = null,
                float arcHeight = 0f,
                bool orientToTrajectory = false,
                float rotationOffset = 0f,
                Action<float> onProgress = null,
                Func<Vector3> resolveEnd = null)
            {
                Root = root;
                Image = image;
                Label = label;
                Start = start;
                End = end;
                Projectile = projectile;
                Fade = fade;
                Duration = Mathf.Max(0.01f, duration);
                StartAlpha = image != null ? image.color.a : 1f;
                EaseIn = easeIn;
                OnComplete = onComplete;
                ArcHeight = Mathf.Max(0f, arcHeight);
                OrientToTrajectory = orientToTrajectory;
                RotationOffset = rotationOffset;
                OnProgress = onProgress;
                ResolveEnd = resolveEnd;
            }

            public RectTransform Root { get; }
            public Image Image { get; }
            public Text Label { get; }
            public Vector3 Start { get; }
            public Vector3 End { get; }
            public bool Projectile { get; }
            public bool Fade { get; }
            public float Duration { get; }
            public float StartAlpha { get; }
            public bool EaseIn { get; }
            public Action OnComplete { get; }
            public float ArcHeight { get; }
            public bool OrientToTrajectory { get; }
            public float RotationOffset { get; }
            public Action<float> OnProgress { get; }
            public Func<Vector3> ResolveEnd { get; }
            public float Elapsed { get; set; }
        }

        private sealed class PendingFlameDrakeCast
        {
            private readonly HashSet<string> targetIds =
                new HashSet<string>(StringComparer.Ordinal);

            public PendingFlameDrakeCast(
                int createdFrame,
                Vector3 attackerPosition,
                Vector3 targetPosition,
                string primaryTargetRuntimeId,
                bool isSkill)
            {
                CreatedFrame = createdFrame;
                AttackerPosition = attackerPosition;
                TargetPosition = targetPosition;
                PrimaryTargetRuntimeId = primaryTargetRuntimeId ?? string.Empty;
                IsSkill = isSkill;
            }

            public int CreatedFrame { get; }
            public Vector3 AttackerPosition { get; }
            public Vector3 TargetPosition { get; }
            public string PrimaryTargetRuntimeId { get; }
            public bool IsSkill { get; }
            public List<CombatEvent> Events { get; } = new List<CombatEvent>();
            public float Elapsed { get; set; }
            public bool ImpactCompleted { get; set; }
            public bool FireballArrived { get; private set; }
            public bool ExplosionCompleted { get; private set; }
            public bool BurningGroundSpawned { get; private set; }
            public int ImpactPathTileIndex { get; private set; } = -1;

            public void MarkFireballArrived()
            {
                FireballArrived = true;
            }

            public void SetImpactPathTileIndex(int tileIndex)
            {
                ImpactPathTileIndex = tileIndex;
            }

            public void MarkExplosionCompleted()
            {
                ExplosionCompleted = true;
            }

            public void MarkBurningGroundSpawned()
            {
                BurningGroundSpawned = true;
            }

            public bool Add(CombatEvent combatEvent)
            {
                var targetId = combatEvent.TargetRuntimeId ?? string.Empty;
                if (!targetIds.Add(targetId))
                {
                    return false;
                }

                Events.Add(combatEvent);
                return true;
            }
        }

        private sealed class PendingEmberShamanCast
        {
            private readonly HashSet<string> targetIds =
                new HashSet<string>(StringComparer.Ordinal);

            public PendingEmberShamanCast(
                int createdFrame,
                Vector3 attackerPosition)
            {
                CreatedFrame = createdFrame;
                AttackerPosition = attackerPosition;
            }

            public int CreatedFrame { get; }
            public Vector3 AttackerPosition { get; }
            public List<PendingEmberShamanShot> Shots { get; } =
                new List<PendingEmberShamanShot>(5);
            public float Elapsed { get; set; }

            public bool TryAdd(
                CombatEvent combatEvent,
                Vector3 targetPosition,
                int maximumFireballs)
            {
                var targetId = combatEvent.TargetRuntimeId ?? string.Empty;
                if (Shots.Count >= maximumFireballs || !targetIds.Add(targetId))
                {
                    return false;
                }

                Shots.Add(new PendingEmberShamanShot(
                    combatEvent,
                    targetPosition,
                    combatEvent.Kind == AttackKind.EmberExplosiveFireball));
                return true;
            }
        }

        private sealed class PendingRuneboltMageCast
        {
            private readonly HashSet<string> targetIds =
                new HashSet<string>(StringComparer.Ordinal);

            public PendingRuneboltMageCast(int createdFrame, Vector3 attackerPosition)
            {
                CreatedFrame = createdFrame;
                AttackerPosition = attackerPosition;
            }

            public int CreatedFrame { get; }
            public Vector3 AttackerPosition { get; }
            public List<PendingRuneboltMageShot> Shots { get; } =
                new List<PendingRuneboltMageShot>();
            public float Elapsed { get; set; }

            public bool TryAdd(CombatEvent combatEvent, Vector3 targetPosition)
            {
                var targetId = combatEvent.TargetRuntimeId ?? string.Empty;
                if (!targetIds.Add(targetId))
                {
                    return false;
                }

                Shots.Add(new PendingRuneboltMageShot(combatEvent, targetPosition));
                return true;
            }

            public void SortByDistance(Vector3 attackerPosition)
            {
                Shots.Sort((left, right) =>
                    Vector3.SqrMagnitude(left.TargetPosition - attackerPosition)
                        .CompareTo(Vector3.SqrMagnitude(right.TargetPosition - attackerPosition)));
            }

            public void ConfigurePath(Vector3 start, Vector3 direction, float travelDistance)
            {
                foreach (var shot in Shots)
                {
                    var distance = Vector3.Dot(shot.TargetPosition - start, direction);
                    shot.PathProgress = Mathf.Clamp01(distance / Mathf.Max(1f, travelDistance));
                }
            }
        }

        private sealed class PendingRuneboltMageShot
        {
            public PendingRuneboltMageShot(CombatEvent combatEvent, Vector3 targetPosition)
            {
                CombatEvent = combatEvent;
                TargetPosition = targetPosition;
            }

            public CombatEvent CombatEvent { get; }
            public Vector3 TargetPosition { get; private set; }
            public void SetTargetPosition(Vector3 position) { TargetPosition = position; }
            public float PathProgress { get; set; }
            public bool ImpactCompleted { get; set; }
        }

        private sealed class PendingStoneboundWarlockCast
        {
            public PendingStoneboundWarlockCast(
                int createdFrame,
                CombatEvent combatEvent,
                Vector3 attackerPosition,
                Vector3 targetPosition)
            {
                CreatedFrame = createdFrame;
                CombatEvent = combatEvent;
                AttackerPosition = attackerPosition;
                TargetPosition = targetPosition;
            }

            public int CreatedFrame { get; }
            public CombatEvent CombatEvent { get; }
            public Vector3 AttackerPosition { get; }
            public Vector3 TargetPosition { get; }
            public bool IsSkill { get; set; }
            public bool ImpactCompleted { get; set; }
            public float Elapsed { get; set; }
        }

        private sealed class PendingThunderlordChainCast
        {
            private readonly HashSet<string> targetIds =
                new HashSet<string>(StringComparer.Ordinal);

            public PendingThunderlordChainCast(int createdFrame, Vector3 attackerPosition)
            {
                CreatedFrame = createdFrame;
                AttackerPosition = attackerPosition;
            }

            public int CreatedFrame { get; }
            public Vector3 AttackerPosition { get; }
            public List<PendingThunderlordChainShot> Shots { get; } =
                new List<PendingThunderlordChainShot>();
            public float Elapsed { get; set; }

            public bool TryAdd(
                CombatEvent combatEvent,
                Vector3 targetPosition,
                int maximumTargets)
            {
                var targetId = combatEvent.TargetRuntimeId ?? string.Empty;
                if (Shots.Count >= maximumTargets || !targetIds.Add(targetId))
                {
                    return false;
                }

                Shots.Add(new PendingThunderlordChainShot(combatEvent, targetPosition));
                return true;
            }
        }

        private sealed class PendingThunderlordChainShot
        {
            public PendingThunderlordChainShot(
                CombatEvent combatEvent,
                Vector3 targetPosition)
            {
                CombatEvent = combatEvent;
                TargetPosition = targetPosition;
            }

            public CombatEvent CombatEvent { get; }
            public Vector3 TargetPosition { get; }
            public bool ImpactCompleted { get; set; }
        }

        private sealed class ActiveThunderlordChain
        {
            public ActiveThunderlordChain(PendingThunderlordChainCast cast)
            {
                Cast = cast;
            }

            public PendingThunderlordChainCast Cast { get; }
            public float Elapsed { get; set; }
            public int NextSegmentIndex { get; set; }
        }

        private sealed class PendingThunderlordSkillCast
        {
            private readonly HashSet<string> targetIds =
                new HashSet<string>(StringComparer.Ordinal);

            public PendingThunderlordSkillCast(int createdFrame, Vector3 attackerPosition)
            {
                CreatedFrame = createdFrame;
                AttackerPosition = attackerPosition;
            }

            public int CreatedFrame { get; }
            public Vector3 AttackerPosition { get; }
            public List<PendingThunderlordSkillShot> Shots { get; } =
                new List<PendingThunderlordSkillShot>();
            public float Elapsed { get; set; }

            public bool TryAdd(CombatEvent combatEvent, Vector3 targetPosition)
            {
                var targetId = combatEvent.TargetRuntimeId ?? string.Empty;
                if (!targetIds.Add(targetId))
                {
                    return false;
                }

                Shots.Add(new PendingThunderlordSkillShot(combatEvent, targetPosition));
                return true;
            }
        }

        private sealed class PendingThunderlordSkillShot
        {
            public PendingThunderlordSkillShot(
                CombatEvent combatEvent,
                Vector3 targetPosition)
            {
                CombatEvent = combatEvent;
                TargetPosition = targetPosition;
            }

            public CombatEvent CombatEvent { get; }
            public Vector3 TargetPosition { get; }
            public bool ImpactCompleted { get; set; }
        }

        private sealed class PendingAbyssalHarpoonSkillCast
        {
            private readonly HashSet<string> targetIds =
                new HashSet<string>(StringComparer.Ordinal);

            public PendingAbyssalHarpoonSkillCast(
                int createdFrame,
                Vector3 attackerPosition,
                string anchorTargetRuntimeId,
                Vector3 warningTargetPosition)
            {
                CreatedFrame = createdFrame;
                AttackerPosition = attackerPosition;
                AnchorTargetRuntimeId = anchorTargetRuntimeId ?? string.Empty;
                WarningTargetPosition = warningTargetPosition;
                LastAddedFrame = createdFrame;
            }

            public int CreatedFrame { get; }
            public Vector3 AttackerPosition { get; }
            public string AnchorTargetRuntimeId { get; }
            public Vector3 WarningTargetPosition { get; }
            public RectTransform PortalRect { get; set; }
            public List<PendingAbyssalHarpoonShot> Shots { get; } =
                new List<PendingAbyssalHarpoonShot>(6);
            public int LastAddedFrame { get; set; }
            public float Elapsed { get; set; }
            public bool ReleaseRequested { get; set; }
            public bool PullStarted { get; set; }

            public bool TryAdd(
                CombatEvent combatEvent,
                Vector3 targetPosition,
                int maximumTargets)
            {
                var targetId = combatEvent.TargetRuntimeId ?? string.Empty;
                if (Shots.Count >= maximumTargets || !targetIds.Add(targetId))
                {
                    return false;
                }

                Shots.Add(new PendingAbyssalHarpoonShot(combatEvent, targetPosition));
                return true;
            }
        }

        private sealed class AbyssalHarpoonPathSegment
        {
            public AbyssalHarpoonPathSegment(
                RectTransform rect,
                Vector2 start,
                Vector2 end,
                float startDistance,
                float length)
            {
                Rect = rect;
                Start = start;
                End = end;
                StartDistance = startDistance;
                Length = length;
            }

            public RectTransform Rect { get; }
            public Vector2 Start { get; }
            public Vector2 End { get; }
            public float StartDistance { get; }
            public float Length { get; }
        }

        private sealed class PendingAbyssalHarpoonCast
        {
            private readonly HashSet<string> targetIds =
                new HashSet<string>(StringComparer.Ordinal);

            public PendingAbyssalHarpoonCast(int createdFrame, Vector3 attackerPosition)
            {
                CreatedFrame = createdFrame;
                AttackerPosition = attackerPosition;
            }

            public int CreatedFrame { get; }
            public Vector3 AttackerPosition { get; }
            public List<PendingAbyssalHarpoonShot> Shots { get; } =
                new List<PendingAbyssalHarpoonShot>(6);
            public float Elapsed { get; set; }

            public bool TryAdd(
                CombatEvent combatEvent,
                Vector3 targetPosition,
                int maximumTargets)
            {
                var targetId = combatEvent.TargetRuntimeId ?? string.Empty;
                if (Shots.Count >= maximumTargets || !targetIds.Add(targetId))
                {
                    return false;
                }

                Shots.Add(new PendingAbyssalHarpoonShot(combatEvent, targetPosition));
                return true;
            }
        }

        private sealed class PendingAbyssalHarpoonShot
        {
            public PendingAbyssalHarpoonShot(
                CombatEvent combatEvent,
                Vector3 targetPosition)
            {
                CombatEvent = combatEvent;
                TargetPosition = targetPosition;
            }

            public CombatEvent CombatEvent { get; }
            public Vector3 TargetPosition { get; }
            public float ReleaseProgress { get; set; }
            public float SkillRouteDistance { get; set; }
            public bool PullStarted { get; set; }
            public bool ImpactCompleted { get; set; }
        }

        private sealed class PendingEmberShamanShot
        {
            public PendingEmberShamanShot(
                CombatEvent combatEvent,
                Vector3 targetPosition,
                bool isPrimary)
            {
                CombatEvent = combatEvent;
                TargetPosition = targetPosition;
                IsPrimary = isPrimary;
            }

            public CombatEvent CombatEvent { get; }
            public Vector3 TargetPosition { get; }
            public bool IsPrimary { get; }
            public bool ImpactCompleted { get; set; }
        }

        private sealed class PendingNightfangSkillCast
        {
            public PendingNightfangSkillCast(CombatEvent combatEvent, Vector3 targetPosition)
            {
                CombatEvent = combatEvent;
                TargetPosition = targetPosition;
            }

            public CombatEvent CombatEvent { get; }
            public Vector3 TargetPosition { get; }
            public float Elapsed { get; set; }
            public bool ImpactCompleted { get; set; }
        }

        private sealed class PendingStarfallCast
        {
            private readonly HashSet<string> targetIds =
                new HashSet<string>(StringComparer.Ordinal);

            public PendingStarfallCast(
                int createdFrame,
                Vector3 attackerPosition,
                Vector3 targetPosition,
                bool isSkill)
            {
                CreatedFrame = createdFrame;
                AttackerPosition = attackerPosition;
                TargetPosition = targetPosition;
                IsSkill = isSkill;
            }

            public int CreatedFrame { get; }
            public Vector3 AttackerPosition { get; }
            public Vector3 TargetPosition { get; }
            public bool IsSkill { get; }
            public List<CombatEvent> Events { get; } = new List<CombatEvent>();
            public float Elapsed { get; set; }
            public bool ImpactCompleted { get; set; }

            public bool Add(CombatEvent combatEvent)
            {
                var targetId = combatEvent.TargetRuntimeId ?? string.Empty;
                if (!targetIds.Add(targetId))
                {
                    return false;
                }
                Events.Add(combatEvent);
                return true;
            }
        }
        private sealed class PendingWindclawSkillCast
        {
            public PendingWindclawSkillCast(
                CombatEvent combatEvent,
                Vector3 targetPosition)
            {
                CombatEvent = combatEvent;
                TargetPosition = targetPosition;
            }

            public CombatEvent CombatEvent { get; }
            public Vector3 TargetPosition { get; }
            public float Elapsed { get; set; }
            public bool Completed { get; set; }
        }

        private sealed class PendingBowCast
        {
            public PendingBowCast(
                CombatEvent combatEvent,
                Vector3 attackerPosition,
                Vector3 targetPosition)
            {
                CombatEvent = combatEvent;
                AttackerPosition = attackerPosition;
                TargetPosition = targetPosition;
            }

            public CombatEvent CombatEvent { get; }
            public Vector3 AttackerPosition { get; }
            public Vector3 TargetPosition { get; }
            public float Elapsed { get; set; }
            public bool ImpactCompleted { get; set; }
        }

        private sealed class PendingSkyborneValkyrieCast
        {
            private readonly HashSet<string> targetIds =
                new HashSet<string>(StringComparer.Ordinal);

            public PendingSkyborneValkyrieCast(
                int createdFrame,
                Vector3 attackerPosition,
                bool isSkill)
            {
                CreatedFrame = createdFrame;
                AttackerPosition = attackerPosition;
                IsSkill = isSkill;
            }

            public int CreatedFrame { get; }
            public Vector3 AttackerPosition { get; }
            public bool IsSkill { get; }
            public List<PendingSkyborneValkyrieShot> Shots { get; } =
                new List<PendingSkyborneValkyrieShot>(3);
            public float Elapsed { get; set; }

            public bool TryAdd(
                CombatEvent combatEvent,
                Vector3 targetPosition,
                int maximumArrows)
            {
                var targetId = combatEvent.TargetRuntimeId ?? string.Empty;
                if (Shots.Count >= maximumArrows || !targetIds.Add(targetId))
                {
                    return false;
                }

                Shots.Add(new PendingSkyborneValkyrieShot(combatEvent, targetPosition));
                return true;
            }
        }

        private sealed class PendingSkyborneValkyrieShot
        {
            public PendingSkyborneValkyrieShot(
                CombatEvent combatEvent,
                Vector3 targetPosition)
            {
                CombatEvent = combatEvent;
                TargetPosition = targetPosition;
            }

            public CombatEvent CombatEvent { get; }
            public Vector3 TargetPosition { get; }
            public bool ImpactCompleted { get; set; }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class SkyborneValkyrieExplosionEventRelay : MonoBehaviour
    {
        private Action damageCallback;
        private Action endCallback;
        private bool damageInvoked;

        public void Bind(Action onDamage, Action onEnd)
        {
            damageCallback = onDamage;
            endCallback = onEnd;
        }

        public void OnDamageFrame()
        {
            if (damageInvoked)
            {
                return;
            }

            damageInvoked = true;
            damageCallback?.Invoke();
        }

        public void OnAnimationEnd()
        {
            if (!damageInvoked)
            {
                OnDamageFrame();
            }
            endCallback?.Invoke();
        }
    }

    [DisallowMultipleComponent]
    internal sealed class FlameDrakeExplosionEventRelay : MonoBehaviour
    {
        private Action damageCallback;
        private Action endCallback;
        private bool damageInvoked;

        public void Bind(Action onDamage, Action onEnd)
        {
            damageCallback = onDamage;
            endCallback = onEnd;
        }

        public void OnDamageFrame()
        {
            if (damageInvoked)
            {
                return;
            }

            damageInvoked = true;
            damageCallback?.Invoke();
        }

        public void OnAnimationEnd()
        {
            if (!damageInvoked)
            {
                OnDamageFrame();
            }
            endCallback?.Invoke();
        }
    }

    [DisallowMultipleComponent]
    internal sealed class NightfangExplosionEventRelay : MonoBehaviour
    {
        private Action damageCallback;
        private Action endCallback;
        private bool damageInvoked;

        public void Bind(Action onDamage, Action onEnd)
        {
            damageCallback = onDamage;
            endCallback = onEnd;
        }

        public void OnDamageFrame()
        {
            if (damageInvoked)
            {
                return;
            }
            damageInvoked = true;
            damageCallback?.Invoke();
        }

        public void OnAnimationEnd()
        {
            if (!damageInvoked)
            {
                OnDamageFrame();
            }
            endCallback?.Invoke();
        }
    }

    [DisallowMultipleComponent]
    internal sealed class StarfallExplosionEventRelay : MonoBehaviour
    {
        private Action damageCallback;
        private Action endCallback;
        private bool damageInvoked;

        public void Bind(Action onDamage, Action onEnd)
        {
            damageCallback = onDamage;
            endCallback = onEnd;
        }

        public void OnDamageFrame()
        {
            if (damageInvoked)
            {
                return;
            }
            damageInvoked = true;
            damageCallback?.Invoke();
        }

        public void OnAnimationEnd()
        {
            if (!damageInvoked)
            {
                OnDamageFrame();
            }
            endCallback?.Invoke();
        }
    }

    [DisallowMultipleComponent]
    internal sealed class StarfallGemRibbonController : MonoBehaviour
    {
        private RectTransform target;
        private StarfallRibbonTrailGraphic ribbon;
        private float lifetime;
        private float detachedElapsed;

        public void Bind(
            RectTransform projectile,
            StarfallRibbonTrailGraphic ribbonGraphic,
            float trailLifetime)
        {
            target = projectile;
            ribbon = ribbonGraphic;
            lifetime = Mathf.Max(0.05f, trailLifetime);
            AddCurrentPoint();
        }

        private void LateUpdate()
        {
            if (ribbon == null)
            {
                Destroy(gameObject);
                return;
            }

            ribbon.Tick(Time.deltaTime);
            if (target != null)
            {
                AddCurrentPoint();
                return;
            }

            detachedElapsed += Time.deltaTime;
            if (detachedElapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void AddCurrentPoint()
        {
            if (target != null && ribbon != null)
            {
                ribbon.AddPoint(ribbon.rectTransform.InverseTransformPoint(target.position));
            }
        }
    }

    [RequireComponent(typeof(CanvasRenderer), typeof(ParticleSystem))]
    internal sealed class StarfallUiParticleEffect : MaskableGraphic
    {
        private ParticleSystem source;
        private ParticleSystem.Particle[] particles = Array.Empty<ParticleSystem.Particle>();
        private Sprite particleSprite;
        private Vector2 start;
        private Vector2 end;
        private float travelDuration;
        private float headSize;
        private float trailInterval;
        private float trailLifetime;
        private float trailSize;
        private int impactParticleCount;
        private float impactRadius;
        private Color headColor;
        private Color trailColor;
        private StarfallRibbonTrailGraphic ribbon;
        private float elapsed;
        private float nextTrailTime;
        private int trailOrdinal;
        private bool impactEmitted;

        public bool IsComplete { get; private set; }

        public void AttachRibbon(StarfallRibbonTrailGraphic value)
        {
            ribbon = value;
        }

        public override Texture mainTexture => particleSprite != null
            ? particleSprite.texture
            : Texture2D.whiteTexture;

        public void Configure(
            Sprite sprite,
            Vector2 startPosition,
            Vector2 endPosition,
            float projectileDuration,
            float projectileHeadSize,
            float particleTrailInterval,
            float particleTrailLifetime,
            float particleTrailSize,
            int burstParticleCount,
            float burstRadius,
            Color projectileColor,
            Color particleColor,
            float ribbonWidth = 0f,
            float ribbonLifetime = 0.24f,
            Color ribbonHeadColor = default,
            Color ribbonTailColor = default)
        {
            particleSprite = sprite ?? throw new ArgumentNullException(nameof(sprite));
            start = startPosition;
            end = endPosition;
            travelDuration = Mathf.Max(0.05f, projectileDuration);
            headSize = Mathf.Max(1f, projectileHeadSize);
            trailInterval = Mathf.Max(0.01f, particleTrailInterval);
            trailLifetime = Mathf.Max(0.05f, particleTrailLifetime);
            trailSize = Mathf.Max(1f, particleTrailSize);
            impactParticleCount = Mathf.Clamp(burstParticleCount, 1, 32);
            impactRadius = Mathf.Max(1f, burstRadius);
            headColor = projectileColor;
            trailColor = particleColor;
            color = Color.white;
            raycastTarget = false;

            source = GetComponent<ParticleSystem>();
            var renderer = source.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            var main = source.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 256;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.stopAction = ParticleSystemStopAction.None;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = source.emission;
            emission.enabled = false;
            var shape = source.shape;
            shape.enabled = false;
            var colorOverLifetime = source.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateFadeGradient());
            var sizeOverLifetime = source.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, CreateSizeCurve());
            source.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            EmitParticle(start, Vector2.zero, headSize, 0.07f, headColor, 0f);
            EmitParticle(start, Vector2.zero, trailSize, trailLifetime, trailColor, 0f);
            if (ribbon != null && ribbonWidth > 0f)
            {
                ribbon.Configure(ribbonWidth, ribbonLifetime, ribbonHeadColor, ribbonTailColor);
                ribbon.AddPoint(start);
            }
            nextTrailTime = trailInterval;
            SetAllDirty();
        }

        public void ConfigureImpact(
            Sprite sprite,
            Vector2 impactPosition,
            float impactHeadSize,
            int burstParticleCount,
            float burstRadius,
            Color impactHeadColor,
            Color impactParticleColor)
        {
            Configure(
                sprite,
                impactPosition,
                impactPosition,
                0.05f,
                impactHeadSize,
                1f,
                0.28f,
                Mathf.Max(4f, impactHeadSize * 0.32f),
                burstParticleCount,
                burstRadius,
                impactHeadColor,
                impactParticleColor);
            elapsed = travelDuration;
            EmitImpact();
            SetVerticesDirty();
        }

        public void Tick(float deltaSeconds)
        {
            if (IsComplete || source == null || deltaSeconds <= 0f)
            {
                return;
            }

            source.Simulate(deltaSeconds, false, false, false);
            ribbon?.Tick(deltaSeconds);
            elapsed += deltaSeconds;
            if (!impactEmitted)
            {
                var normalized = Mathf.Clamp01(elapsed / travelDuration);
                var movement = normalized * normalized;
                var position = Vector2.Lerp(start, end, movement);
                ribbon?.AddPoint(position);
                EmitParticle(
                    position,
                    Vector2.zero,
                    headSize * (1f + Mathf.Sin(normalized * Mathf.PI) * 0.18f),
                    Mathf.Max(0.055f, deltaSeconds * 1.5f),
                    headColor,
                    normalized * 110f);

                while (nextTrailTime <= elapsed + 0.0001f && nextTrailTime <= travelDuration)
                {
                    var trailNormalized = Mathf.Clamp01(nextTrailTime / travelDuration);
                    var trailPosition = Vector2.Lerp(start, end, trailNormalized * trailNormalized);
                    EmitParticle(
                        trailPosition,
                        Vector2.zero,
                        trailSize,
                        trailLifetime,
                        trailColor,
                        trailOrdinal * 137.5f);
                    trailOrdinal++;
                    nextTrailTime += trailInterval;
                }

                if (elapsed + 0.0001f >= travelDuration)
                {
                    EmitImpact();
                }
            }

            SetVerticesDirty();
            if (impactEmitted &&
                elapsed >= travelDuration + Mathf.Max(0.45f, trailLifetime + 0.1f) &&
                source.particleCount == 0)
            {
                IsComplete = true;
            }
        }

        protected override void OnDestroy()
        {
            if (ribbon != null)
            {
                Destroy(ribbon.gameObject);
            }

            base.OnDestroy();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (source == null)
            {
                source = GetComponent<ParticleSystem>();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (source == null || particleSprite == null)
            {
                return;
            }

            var requiredCapacity = Mathf.Max(1, source.main.maxParticles);
            if (particles.Length < requiredCapacity)
            {
                particles = new ParticleSystem.Particle[requiredCapacity];
            }

            var count = source.GetParticles(particles);
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(particleSprite);
            for (var index = 0; index < count; index++)
            {
                AppendParticleQuad(vertexHelper, particles[index], uv);
            }
        }

        private void EmitImpact()
        {
            impactEmitted = true;
            EmitParticle(end, Vector2.zero, headSize * 1.35f, 0.2f, headColor, 0f);
            const float burstLifetime = 0.28f;
            for (var index = 0; index < impactParticleCount; index++)
            {
                var angle = (Mathf.PI * 2f * index / impactParticleCount) +
                            (Mathf.PI / impactParticleCount);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                EmitParticle(
                    end,
                    direction * (impactRadius / burstLifetime),
                    trailSize * 0.8f,
                    burstLifetime,
                    trailColor,
                    angle * Mathf.Rad2Deg);
            }
        }

        private void EmitParticle(
            Vector2 position,
            Vector2 velocity,
            float size,
            float lifetime,
            Color particleColor,
            float rotation)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = Mathf.Max(0.01f, lifetime),
                startColor = particleColor,
                rotation = rotation,
                applyShapeToPosition = false
            };
            source.Emit(emit, 1);
        }

        private void AppendParticleQuad(
            VertexHelper vertexHelper,
            ParticleSystem.Particle particle,
            Vector4 uv)
        {
            var size = particle.GetCurrentSize(source);
            var halfSize = size * 0.5f;
            var radians = particle.rotation * Mathf.Deg2Rad;
            var right = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * halfSize;
            var up = new Vector2(-right.y, right.x);
            var center = (Vector2)particle.position;
            var particleColor = particle.GetCurrentColor(source) * color;
            var startIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(center - right - up, particleColor, new Vector2(uv.x, uv.y));
            vertexHelper.AddVert(center - right + up, particleColor, new Vector2(uv.x, uv.w));
            vertexHelper.AddVert(center + right + up, particleColor, new Vector2(uv.z, uv.w));
            vertexHelper.AddVert(center + right - up, particleColor, new Vector2(uv.z, uv.y));
            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }

        private static Gradient CreateFadeGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.78f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static AnimationCurve CreateSizeCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.65f, 0.82f),
                new Keyframe(1f, 0.2f));
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class StarfallRibbonTrailGraphic : MaskableGraphic
    {
        private readonly List<TrailPoint> points = new List<TrailPoint>();
        private float width = 10f;
        private float lifetime = 0.24f;
        private Color headColor = new Color(0.96f, 0.86f, 1f, 0.82f);
        private Color tailColor = new Color(0.35f, 0.08f, 0.82f, 0f);

        public override Texture mainTexture => Texture2D.whiteTexture;

        public void Configure(
            float trailWidth,
            float trailLifetime,
            Color trailHeadColor,
            Color trailTailColor)
        {
            width = Mathf.Max(1f, trailWidth);
            lifetime = Mathf.Max(0.05f, trailLifetime);
            headColor = trailHeadColor;
            tailColor = trailTailColor;
            color = Color.white;
            raycastTarget = false;
            points.Clear();
            SetAllDirty();
        }

        public void AddPoint(Vector2 position)
        {
            if (points.Count > 0)
            {
                var lastIndex = points.Count - 1;
                if ((points[lastIndex].Position - position).sqrMagnitude < 0.64f)
                {
                    var point = points[lastIndex];
                    point.Position = position;
                    point.Age = 0f;
                    points[lastIndex] = point;
                    SetVerticesDirty();
                    return;
                }
            }

            points.Add(new TrailPoint(position));
            SetVerticesDirty();
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || points.Count == 0)
            {
                return;
            }

            for (var index = 0; index < points.Count; index++)
            {
                var point = points[index];
                point.Age += deltaSeconds;
                points[index] = point;
            }

            while (points.Count > 0 && points[0].Age >= lifetime)
            {
                points.RemoveAt(0);
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (points.Count < 2)
            {
                return;
            }

            for (var index = 0; index < points.Count; index++)
            {
                var previous = points[Mathf.Max(0, index - 1)].Position;
                var next = points[Mathf.Min(points.Count - 1, index + 1)].Position;
                var tangent = next - previous;
                if (tangent.sqrMagnitude < 0.0001f)
                {
                    tangent = Vector2.right;
                }
                else
                {
                    tangent.Normalize();
                }

                var life = Mathf.Clamp01(1f - (points[index].Age / lifetime));
                var normal = new Vector2(-tangent.y, tangent.x) * (width * 0.5f * life);
                var vertexColor = Color.Lerp(tailColor, headColor, life) * color;
                vertexColor.a *= life;
                var vertexIndex = vertexHelper.currentVertCount;
                vertexHelper.AddVert(points[index].Position - normal, vertexColor, new Vector2(0f, life));
                vertexHelper.AddVert(points[index].Position + normal, vertexColor, new Vector2(1f, life));

                if (index == 0)
                {
                    continue;
                }

                vertexHelper.AddTriangle(vertexIndex - 2, vertexIndex - 1, vertexIndex);
                vertexHelper.AddTriangle(vertexIndex, vertexIndex - 1, vertexIndex + 1);
            }
        }

        private struct TrailPoint
        {
            public TrailPoint(Vector2 position)
            {
                Position = position;
                Age = 0f;
            }

            public Vector2 Position;
            public float Age;
        }
    }
}
