using System;
using System.Collections.Generic;
using System.Linq;
using DragonBound.Bootstrap;
using DragonBound.Combat;
using DragonBound.Core;
using DragonBound.Items;
using DragonBound.Recruitment;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Minimal development console for exercising Greybox gameplay and authored animations.
    /// It is never created in a non-development player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GreyboxGameplayAnimationTestConsole : MonoBehaviour
    {
        private static readonly string[] BasicUnitLabels = { "AXE", "BOW", "SPEAR", "BERSERKER" };
        private static readonly string[] BasicUnitIds =
        {
            "basic.axe_raider",
            "basic.longbow_hunter",
            "basic.spear_raider",
            "basic.twinaxe_berserker"
        };

        private static readonly string[] HeroLabels =
        {
            "Windclaw Ranger", "Ember Shaman", "Flame Drake Rider",
            "Runebolt Mage", "Stonebound Warlock", "Starfall Archmage",
            "Oathcrown Blademaster", "Frostcrown Hunter", "Thunderlord",
            "Nightfang Assassin", "Abyssal Harpooner", "Skyborne Valkyrie"
        };

        private static readonly string[] HeroIds =
        {
            DragonBoundHeroIds.WindclawRanger,
            DragonBoundHeroIds.EmberShaman,
            DragonBoundHeroIds.DragonRider,
            DragonBoundHeroIds.RuneboltMage,
            DragonBoundHeroIds.Stonebinder,
            DragonBoundHeroIds.StarfallArchmage,
            DragonBoundHeroIds.CrownSwordLeader,
            DragonBoundHeroIds.CrownHunterLeader,
            DragonBoundHeroIds.ThunderJarl,
            DragonBoundHeroIds.NightfangAssassin,
            DragonBoundHeroIds.LeviathanHunter,
            DragonBoundHeroIds.SkyhunterValkyrie
        };

        private static readonly string[] EnemyLabels = { "Enemy01", "Enemy02", "Enemy03", "Enemy04" };
        private static readonly string[] BossLabels = { "W6", "W12", "W16", "W20" };
        private static readonly int[] BossWaves = { 6, 12, 16, 20 };
        private static string[] pendingPlayerItemIds;
        private static string[] pendingAiItemIds;
        private static bool pendingMirrorItemsToAi;

        private DragonBoundBootstrap bootstrap;
        private TwentyWavePressureRuntime subscribedItemRuntime;
        private Rect windowRect = new Rect(12f, 12f, 580f, 600f);
        private Vector2 itemScroll;
        private bool visible = true;
        private int tabIndex;
        private string waveText = "1";
        private int sideIndex;
        private int basicUnitIndex;
        private int heroIndex;
        private int enemyIndex;
        private int bossIndex;
        private int itemSideIndex;
        private int runtimeActiveItemIndex;
        private int runtimeTargetIndex;
        private bool mirrorItemsToAi;
        private bool itemSelectionsInitialized;
        private string itemPointX = "0";
        private string itemPointY = "0";
        private readonly HashSet<string> playerItemIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> aiItemIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> itemStatusHistory = new Queue<string>();
        private string status = "Ready";

        public static GreyboxGameplayAnimationTestConsole Create(DragonBoundBootstrap bootstrap)
        {
            if (bootstrap == null ||
                (!Application.isEditor && !Debug.isDebugBuild) ||
                SceneManager.GetActiveScene().name != "Greybox_Main")
            {
                return null;
            }

            var existing = FindObjectOfType<GreyboxGameplayAnimationTestConsole>();
            if (existing != null)
            {
                existing.bootstrap = bootstrap;
                existing.BindItemRuntimeEvents();
                return existing;
            }

            var host = new GameObject("Greybox Gameplay Animation Test Console");
            var console = host.AddComponent<GreyboxGameplayAnimationTestConsole>();
            console.bootstrap = bootstrap;
            console.BindItemRuntimeEvents();
            return console;
        }

        private TeamSide SelectedSide => sideIndex == 0 ? TeamSide.Player : TeamSide.AI;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                visible = !visible;
            }
        }

        private void OnDestroy()
        {
            if (subscribedItemRuntime != null)
            {
                subscribedItemRuntime.ItemUseResolved -= HandleItemUseResolved;
            }
        }

        private void OnGUI()
        {
            if (!visible || bootstrap == null || !bootstrap.IsInitialized)
            {
                return;
            }

            windowRect.width = Mathf.Min(580f, Mathf.Max(320f, Screen.width - 24f));
            windowRect.height = Mathf.Min(tabIndex == 0 ? 600f : 760f, Mathf.Max(260f, Screen.height - 24f));
            windowRect.x = Mathf.Clamp(windowRect.x, 0f, Mathf.Max(0f, Screen.width - windowRect.width));
            windowRect.y = Mathf.Clamp(windowRect.y, 0f, Mathf.Max(0f, Screen.height - 40f));
            windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Greybox Gameplay + Animation Test (F8)");
        }

        private void DrawWindow(int windowId)
        {
            tabIndex = GUILayout.Toolbar(tabIndex, new[] { "Gameplay", "Items" });
            GUILayout.Space(6f);
            if (tabIndex == 0)
            {
                DrawGameplayTab();
            }
            else
            {
                DrawItemsTab();
            }

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
        }

        private void DrawGameplayTab()
        {
            GUILayout.Label("Current wave");
            GUILayout.BeginHorizontal();
            waveText = GUILayout.TextField(waveText, 3, GUILayout.Width(80f));
            if (GUILayout.Button("Set Wave", GUILayout.Width(120f)))
            {
                if (int.TryParse(waveText, out var wave))
                {
                    SetStatus(bootstrap.TryDebugSetCurrentWave(wave), "Wave " + wave);
                }
                else
                {
                    status = "Invalid wave";
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Side (units defend this side; enemies attack this side)");
            sideIndex = GUILayout.SelectionGrid(sideIndex, new[] { "Player", "AI" }, 2);

            GUILayout.Space(8f);
            GUILayout.Label("Basic unit");
            basicUnitIndex = GUILayout.SelectionGrid(basicUnitIndex, BasicUnitLabels, 4);
            if (GUILayout.Button("Spawn Basic Unit"))
            {
                SetStatus(
                    bootstrap.TryDebugSpawnBasicUnit(SelectedSide, BasicUnitIds[basicUnitIndex]),
                    BasicUnitLabels[basicUnitIndex]);
            }

            GUILayout.Space(8f);
            GUILayout.Label("Hero");
            heroIndex = GUILayout.SelectionGrid(heroIndex, HeroLabels, 3);
            if (GUILayout.Button("Spawn Hero"))
            {
                SetStatus(
                    bootstrap.TryDebugSpawnDragonRouteHeroDirect(SelectedSide, HeroIds[heroIndex]),
                    HeroLabels[heroIndex]);
            }

            GUILayout.Space(8f);
            GUILayout.Label("Enemy");
            enemyIndex = GUILayout.SelectionGrid(enemyIndex, EnemyLabels, 4);
            if (GUILayout.Button("Spawn Enemy"))
            {
                SetStatus(
                    bootstrap.TryDebugSpawnEnemy(SelectedSide, enemyIndex + 1),
                    EnemyLabels[enemyIndex]);
            }

            GUILayout.Space(8f);
            GUILayout.Label("Boss (spawns the complete boss wave for both sides)");
            bossIndex = GUILayout.SelectionGrid(bossIndex, BossLabels, 4);
            if (GUILayout.Button("Spawn Boss"))
            {
                SetStatus(bootstrap.TryDebugSpawnBoss(BossWaves[bossIndex]), "Boss " + BossLabels[bossIndex]);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Status: " + status);
        }

        private void DrawItemsTab()
        {
            EnsureItemSelectionsInitialized();
            itemScroll = GUILayout.BeginScrollView(itemScroll);

            GUILayout.Label("Configure side");
            itemSideIndex = GUILayout.SelectionGrid(itemSideIndex, new[] { "Player", "AI" }, 2);
            bool nextMirror = GUILayout.Toggle(mirrorItemsToAi, "Mirror Player loadout to AI");
            if (nextMirror != mirrorItemsToAi)
            {
                mirrorItemsToAi = nextMirror;
                AddItemStatus(mirrorItemsToAi ? "AI will mirror Player on reload" : "AI loadout can be edited separately");
            }

            HashSet<string> selected = itemSideIndex == 0 ? playerItemIds : aiItemIds;
            bool editingDisabled = itemSideIndex == 1 && mirrorItemsToAi;
            GUI.enabled = !editingDisabled;
            DrawItemCategory(selected, ItemCategory.Active, ItemLoadout.MaxActiveItems);
            DrawItemCategory(selected, ItemCategory.Passive, ItemLoadout.MaxPassiveItems);
            GUI.enabled = true;
            if (editingDisabled)
            {
                GUILayout.Label("AI uses the Player selection while mirroring is enabled.");
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Selected Side"))
            {
                selected.Clear();
                AddItemStatus((itemSideIndex == 0 ? "Player" : "AI") + " loadout cleared");
            }
            if (GUILayout.Button("Apply & Reload Greybox"))
            {
                ApplyItemLoadoutAndReload();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            DrawRuntimeItemControls();
            GUILayout.Space(8f);
            GUILayout.Label("Recent item actions");
            if (itemStatusHistory.Count == 0)
            {
                GUILayout.Label("No item actions yet.");
            }
            else
            {
                foreach (string entry in itemStatusHistory)
                {
                    GUILayout.Label(entry);
                }
            }

            GUILayout.EndScrollView();
        }

        private void DrawItemCategory(HashSet<string> selected, ItemCategory category, int maximum)
        {
            int selectedCount = CountSelected(selected, category);
            GUILayout.Space(8f);
            GUILayout.Label(category + " Items (" + selectedCount + "/" + maximum + ")");
            foreach (ItemDefinition definition in ItemCatalog.All)
            {
                if (definition.Category != category || !definition.IsFormalCandidate)
                {
                    continue;
                }

                bool wasSelected = selected.Contains(definition.ItemId);
                bool isSelected = GUILayout.Toggle(
                    wasSelected,
                    ItemCatalog.GetEnglishDisplayName(definition.ItemId) + "  [" + definition.Rarity + "]");
                if (isSelected == wasSelected)
                {
                    continue;
                }

                if (!isSelected)
                {
                    selected.Remove(definition.ItemId);
                    continue;
                }

                if (selectedCount >= maximum)
                {
                    AddItemStatus(category + " slots are full");
                    continue;
                }

                selected.Add(definition.ItemId);
                selectedCount++;
            }
        }

        private void DrawRuntimeItemControls()
        {
            GUILayout.Label("Runtime test (uses formal item command path)");
            TwentyWavePressureRuntime waveRuntime = bootstrap.TwentyWave;
            ItemRunRuntime itemRuntime = waveRuntime == null
                ? null
                : itemSideIndex == 0 ? waveRuntime.PlayerItems : waveRuntime.AiItems;
            if (itemRuntime == null || !itemRuntime.IsStarted)
            {
                GUILayout.Label("Runtime is not started. Apply a loadout or wait for the Run to start.");
                return;
            }

            IReadOnlyList<string> activeItems = itemRuntime.Snapshot.ActiveItems;
            GUILayout.Label(
                "Locked snapshot: Active=" + activeItems.Count +
                ", Passive=" + itemRuntime.Snapshot.PassiveItems.Count);
            if (activeItems.Count == 0)
            {
                GUILayout.Label("No Active Items equipped.");
                return;
            }

            runtimeActiveItemIndex = Mathf.Clamp(runtimeActiveItemIndex, 0, activeItems.Count - 1);
            string[] activeLabels = activeItems
                .Select(ItemCatalog.GetEnglishDisplayName)
                .ToArray();
            runtimeActiveItemIndex = GUILayout.SelectionGrid(runtimeActiveItemIndex, activeLabels, 2);
            string itemId = activeItems[runtimeActiveItemIndex];
            float remaining = Mathf.Max(
                itemRuntime.GetInitialCooldownRemainingSeconds(itemId),
                itemRuntime.GetCooldownRemainingSeconds(itemId));
            float duration = Mathf.Max(
                itemRuntime.GetInitialCooldownDurationSeconds(itemId),
                itemRuntime.GetCooldownDurationSeconds(itemId));
            GUILayout.Label("Cooldown: " + remaining.ToString("0.0") + " / " + duration.ToString("0.0") + "s");

            List<ItemCombatUnitState> targets = null;
            List<EnemyRuntime> enemyTargets = null;
            if (IsUnitTargetedItem(itemId))
            {
                targets = itemRuntime.UnitRegistry.Units
                    .Where(value => value != null && value.IsAlive)
                    .OrderBy(value => value.RuntimeId, StringComparer.Ordinal)
                    .ToList();
                if (targets.Count == 0)
                {
                    GUILayout.Label("Target: no alive unit. Spawn a Basic Unit or Hero first.");
                }
                else
                {
                    runtimeTargetIndex = Mathf.Clamp(runtimeTargetIndex, 0, targets.Count - 1);
                    string[] targetLabels = targets
                        .Select(value => value.Kind + " L" + value.Level + " " + value.RuntimeId)
                        .ToArray();
                    runtimeTargetIndex = GUILayout.SelectionGrid(runtimeTargetIndex, targetLabels, 1);
                }
            }
            else if (IsEnemyTargetedItem(itemId))
            {
                var targetSide = itemSideIndex == 0 ? TeamSide.Player : TeamSide.AI;
                var registry = targetSide == TeamSide.Player
                    ? waveRuntime.PlayerEnemyRegistry
                    : waveRuntime.AiEnemyRegistry;
                enemyTargets = registry.Enemies
                    .Where(value => value != null && value.Team == targetSide && value.IsAlive)
                    .OrderBy(value => value.RuntimeId, StringComparer.Ordinal)
                    .ToList();
                if (enemyTargets.Count == 0)
                {
                    GUILayout.Label("Target: no alive enemy.");
                }
                else
                {
                    runtimeTargetIndex = Mathf.Clamp(runtimeTargetIndex, 0, enemyTargets.Count - 1);
                    var targetLabels = enemyTargets
                        .Select(value => value.Archetype + " " + value.RuntimeId)
                        .ToArray();
                    runtimeTargetIndex = GUILayout.SelectionGrid(runtimeTargetIndex, targetLabels, 1);
                }
            }
            else if (itemId == ItemIds.RuneburstMine)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Point X", GUILayout.Width(55f));
                itemPointX = GUILayout.TextField(itemPointX, GUILayout.Width(70f));
                GUILayout.Label("Y", GUILayout.Width(20f));
                itemPointY = GUILayout.TextField(itemPointY, GUILayout.Width(70f));
                if (GUILayout.Button("Lead Enemy", GUILayout.Width(100f)))
                {
                    SelectLeadEnemyPoint(waveRuntime);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Selected Item"))
            {
                UseSelectedItem(waveRuntime, itemId, targets, enemyTargets);
            }
            if (GUILayout.Button("Advance Item Clock +120s"))
            {
                itemRuntime.Tick(120f);
                AddItemStatus(ItemCatalog.GetEnglishDisplayName(itemId) + " cooldown advanced");
            }
            GUILayout.EndHorizontal();
        }

        private void UseSelectedItem(
            TwentyWavePressureRuntime runtime,
            string itemId,
            IReadOnlyList<ItemCombatUnitState> targets,
            IReadOnlyList<EnemyRuntime> enemyTargets)
        {
            if (runtime == null || !runtime.IsGameplayRunning)
            {
                AddItemStatus(ItemCatalog.GetEnglishDisplayName(itemId) + " rejected: RunNotRunning");
                return;
            }

            TeamSide side = itemSideIndex == 0 ? TeamSide.Player : TeamSide.AI;
            if (IsUnitTargetedItem(itemId))
            {
                if (targets == null || targets.Count == 0)
                {
                    AddItemStatus(ItemCatalog.GetEnglishDisplayName(itemId) + " rejected: NoAliveTargets");
                    return;
                }

                runtimeTargetIndex = Mathf.Clamp(runtimeTargetIndex, 0, targets.Count - 1);
                runtime.TryUseItemOnUnit(side, itemId, targets[runtimeTargetIndex].RuntimeId, out _);
            }
            else if (IsEnemyTargetedItem(itemId))
            {
                if (enemyTargets == null || enemyTargets.Count == 0)
                {
                    AddItemStatus(ItemCatalog.GetEnglishDisplayName(itemId) + " rejected: NoAliveTargets");
                    return;
                }

                runtimeTargetIndex = Mathf.Clamp(runtimeTargetIndex, 0, enemyTargets.Count - 1);
                runtime.TryUseItemOnUnit(
                    side,
                    itemId,
                    enemyTargets[runtimeTargetIndex].RuntimeId,
                    out _);
            }
            else if (itemId == ItemIds.RuneburstMine)
            {
                if (!float.TryParse(itemPointX, out float x) || !float.TryParse(itemPointY, out float y))
                {
                    AddItemStatus(ItemCatalog.GetEnglishDisplayName(itemId) + " rejected: InvalidPoint");
                    return;
                }
                runtime.TryUseItemAtPoint(side, itemId, new CombatPoint(x, y), out _);
            }
            else
            {
                runtime.TryUseItem(side, itemId, out _);
            }

            // ItemRunRuntime publishes the authoritative result synchronously. The subscribed
            // handler records it with cooldown data so the console reflects the formal path.
        }

        private void ApplyItemLoadoutAndReload()
        {
            string[] player = GetSelectedIds(playerItemIds);
            string[] ai = mirrorItemsToAi ? player : GetSelectedIds(aiItemIds);
            var provider = new DevelopmentItemRunSnapshotProvider();
            if (!provider.TryConfigureSides(player, ai, out string reason))
            {
                AddItemStatus("Loadout rejected: " + (reason ?? "Unknown"));
                return;
            }

            pendingPlayerItemIds = player;
            pendingAiItemIds = ai;
            pendingMirrorItemsToAi = mirrorItemsToAi;
            DragonBoundBootstrap.ItemRunSnapshotProviderOverrideForTests = provider;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void SelectLeadEnemyPoint(TwentyWavePressureRuntime runtime)
        {
            EnemyRegistry registry = itemSideIndex == 0
                ? runtime.PlayerEnemyRegistry
                : runtime.AiEnemyRegistry;
            EnemyRuntime lead = registry.Enemies
                .Where(value => value != null && value.IsAlive)
                .OrderByDescending(value => value.PathProgress)
                .ThenBy(value => value.SpawnSequence)
                .FirstOrDefault();
            if (lead == null)
            {
                AddItemStatus("Runeburst Mine target rejected: NoAliveTargets");
                return;
            }

            itemPointX = lead.CombatPosition.X.ToString("0.###");
            itemPointY = lead.CombatPosition.Y.ToString("0.###");
            AddItemStatus("Runeburst Mine point selected from " + lead.RuntimeId);
        }

        private void EnsureItemSelectionsInitialized()
        {
            if (itemSelectionsInitialized)
            {
                return;
            }

            if (pendingPlayerItemIds != null)
            {
                CopyInto(playerItemIds, pendingPlayerItemIds);
                CopyInto(aiItemIds, pendingAiItemIds);
                mirrorItemsToAi = pendingMirrorItemsToAi;
                itemSelectionsInitialized = true;
                return;
            }

            if (bootstrap.TwentyWave?.PlayerItems == null || bootstrap.TwentyWave.AiItems == null)
            {
                return;
            }

            CopySnapshotInto(playerItemIds, bootstrap.TwentyWave.PlayerItems.Snapshot);
            CopySnapshotInto(aiItemIds, bootstrap.TwentyWave.AiItems.Snapshot);
            itemSelectionsInitialized = true;
        }

        private static void CopySnapshotInto(HashSet<string> target, ItemRunSnapshot snapshot)
        {
            target.Clear();
            if (snapshot == null)
            {
                return;
            }
            CopyInto(target, snapshot.ActiveItems);
            CopyInto(target, snapshot.PassiveItems);
        }

        private static void CopyInto(HashSet<string> target, IEnumerable<string> values)
        {
            if (values == null)
            {
                return;
            }
            foreach (string value in values)
            {
                target.Add(value);
            }
        }

        private static int CountSelected(HashSet<string> selected, ItemCategory category)
        {
            return selected.Count(itemId => ItemCatalog.Get(itemId)?.Category == category);
        }

        private static string[] GetSelectedIds(HashSet<string> selected)
        {
            return ItemCatalog.All
                .Where(definition => selected.Contains(definition.ItemId))
                .Select(definition => definition.ItemId)
                .ToArray();
        }

        private static bool IsUnitTargetedItem(string itemId)
        {
            return itemId == ItemIds.FrenzyRune ||
                   itemId == ItemIds.RuneOfTempering ||
                   itemId == ItemIds.WarforgeSigil;
        }

        private static bool IsEnemyTargetedItem(string itemId)
        {
            return itemId == ItemIds.WyrmfangSnare;
        }

        private void AddItemStatus(string message)
        {
            if (itemStatusHistory.Count >= 8)
            {
                itemStatusHistory.Dequeue();
            }
            itemStatusHistory.Enqueue("[" + Time.time.ToString("0.0") + "s] " + message);
            status = message;
        }

        private void BindItemRuntimeEvents()
        {
            if (subscribedItemRuntime != null)
            {
                subscribedItemRuntime.ItemUseResolved -= HandleItemUseResolved;
            }

            subscribedItemRuntime = bootstrap != null ? bootstrap.TwentyWave : null;
            if (subscribedItemRuntime != null)
            {
                subscribedItemRuntime.ItemUseResolved += HandleItemUseResolved;
            }
        }

        private void HandleItemUseResolved(TeamSide side, ItemUseResolvedEvent result)
        {
            string message = side + " " + ItemCatalog.GetEnglishDisplayName(result.ItemId) + " " +
                             (result.Accepted ? "accepted" : "rejected: " + (result.Reason ?? "Unknown"));
            if (result.CooldownDurationSeconds > 0.0001f)
            {
                message += " (cooldown " + result.CooldownRemainingSeconds.ToString("0.0") +
                           "/" + result.CooldownDurationSeconds.ToString("0.0") + "s)";
            }
            AddItemStatus(message);
        }

        private void SetStatus(bool success, string action)
        {
            status = success ? action + " succeeded" : action + " rejected";
        }
    }
}
