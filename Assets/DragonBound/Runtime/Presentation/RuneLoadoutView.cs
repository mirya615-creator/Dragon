using System;
using System.Collections.Generic;
using DragonBound.Recruitment;
using DragonBound.Runes;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Development greybox for the permanent Rune profile. It intentionally renders content keys
    /// and rarity swatches rather than baking final art into runtime code.
    /// </summary>
    public sealed class RuneLoadoutView : MonoBehaviour
    {
        [SerializeField] private Button closeButton;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text gateLabel;
        [SerializeField] private Text selectedHeroLabel;
        [SerializeField] private Text selectedRuneLabel;
        [SerializeField] private Text validationLabel;
        [SerializeField] private Button unequipButton;
        [SerializeField] private Button craftButton;
        [SerializeField] private Button allFilterButton;
        [SerializeField] private Button commonFilterButton;
        [SerializeField] private Button excellentFilterButton;
        [SerializeField] private Button epicFilterButton;
        [SerializeField] private Button legendaryFilterButton;
        [SerializeField] private Transform heroGrid;
        [SerializeField] private Transform runeGrid;
        [SerializeField] private RuneLoadoutEntryView heroEntryTemplate;
        [SerializeField] private RuneLoadoutEntryView runeEntryTemplate;

        private readonly Dictionary<string, RuneLoadoutEntryView> heroEntries =
            new Dictionary<string, RuneLoadoutEntryView>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuneLoadoutEntryView> runeEntries =
            new Dictionary<string, RuneLoadoutEntryView>(StringComparer.Ordinal);
        private readonly RunePresentationCatalog presentation = new RunePresentationCatalog();

        private RuneLoadoutService service;
        private Func<bool> canEdit;
        private string selectedHeroId;
        private string selectedRuneId;
        private RuneRarity? activeFilter;
        private bool initialized;

        public bool IsOpen => gameObject.activeSelf;
        public bool IsFeatureLocked => service != null && !service.Gate.IsUnlocked;
        public int HeroEntryCount => heroEntries.Count;
        public int RuneEntryCount => runeEntries.Count;

        public void Configure(
            Button close,
            Text title,
            Text gate,
            Text hero,
            Text rune,
            Text validation,
            Button unequip,
            Button craft,
            Button allFilter,
            Button commonFilter,
            Button excellentFilter,
            Button epicFilter,
            Button legendaryFilter,
            Transform heroes,
            Transform runes,
            RuneLoadoutEntryView heroTemplate,
            RuneLoadoutEntryView runeTemplate)
        {
            closeButton = close;
            titleLabel = title;
            gateLabel = gate;
            selectedHeroLabel = hero;
            selectedRuneLabel = rune;
            validationLabel = validation;
            unequipButton = unequip;
            craftButton = craft;
            allFilterButton = allFilter;
            commonFilterButton = commonFilter;
            excellentFilterButton = excellentFilter;
            epicFilterButton = epicFilter;
            legendaryFilterButton = legendaryFilter;
            heroGrid = heroes;
            runeGrid = runes;
            heroEntryTemplate = heroTemplate;
            runeEntryTemplate = runeTemplate;
        }

        public void Initialize(RuneLoadoutService value, Func<bool> canEditLoadout)
        {
            if (initialized)
            {
                return;
            }

            service = value ?? throw new ArgumentNullException(nameof(value));
            canEdit = canEditLoadout ?? (() => true);
            selectedHeroId = FirstHeroId();
            BuildEntries();
            BindButtons();
            initialized = true;
            gameObject.SetActive(false);
            Refresh();
        }

        public void Open()
        {
            if (!initialized)
            {
                return;
            }

            gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (!initialized)
            {
                return;
            }

            var unlocked = service.Gate.IsUnlocked;
            var editable = unlocked && !service.Loadout.IsLocked && canEdit();
            if (titleLabel != null) titleLabel.text = "RUNE LOADOUT";
            if (gateLabel != null)
            {
                gateLabel.text = unlocked
                    ? (editable ? "DAY " + service.Gate.AccountDay + " | LOADOUT EDITING" : "RUN STARTED | LOADOUT LOCKED")
                    : "LOCKED | UNLOCKS ON DAY 3";
            }

            RefreshHeroEntries(editable);
            RefreshRuneEntries(editable);
            RefreshSelection(editable);
        }

        private void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (unequipButton != null) unequipButton.onClick.RemoveListener(UnequipSelectedHero);
            if (craftButton != null) craftButton.onClick.RemoveListener(CraftSelectedRune);
            RemoveFilterListener(allFilterButton, ShowAll);
            RemoveFilterListener(commonFilterButton, ShowCommon);
            RemoveFilterListener(excellentFilterButton, ShowExcellent);
            RemoveFilterListener(epicFilterButton, ShowEpic);
            RemoveFilterListener(legendaryFilterButton, ShowLegendary);
        }

        private void BuildEntries()
        {
            if (heroEntryTemplate == null || runeEntryTemplate == null || heroGrid == null || runeGrid == null)
            {
                throw new InvalidOperationException("Rune loadout entry templates must be assigned on the editable prefab.");
            }

            foreach (var hero in HeroDefinitionCatalog.Definitions)
            {
                var entry = Instantiate(heroEntryTemplate, heroGrid);
                entry.name = "ART_RuneHero_" + hero.Id;
                entry.gameObject.SetActive(true);
                heroEntries.Add(hero.Id, entry);
            }

            foreach (var rune in RuneCatalog.All)
            {
                var entry = Instantiate(runeEntryTemplate, runeGrid);
                entry.name = "ART_RuneEntry_" + rune.RuneId;
                entry.gameObject.SetActive(true);
                runeEntries.Add(rune.RuneId, entry);
            }
        }

        private void BindButtons()
        {
            closeButton.onClick.AddListener(Close);
            unequipButton.onClick.AddListener(UnequipSelectedHero);
            craftButton.onClick.AddListener(CraftSelectedRune);
            allFilterButton.onClick.AddListener(ShowAll);
            commonFilterButton.onClick.AddListener(ShowCommon);
            excellentFilterButton.onClick.AddListener(ShowExcellent);
            epicFilterButton.onClick.AddListener(ShowEpic);
            legendaryFilterButton.onClick.AddListener(ShowLegendary);
        }

        private void RefreshHeroEntries(bool editable)
        {
            foreach (var hero in HeroDefinitionCatalog.Definitions)
            {
                var runeId = service.Loadout.GetRune(hero.Id);
                var detail = string.IsNullOrEmpty(runeId) ? "EMPTY" : "EQUIPPED: " + runeId;
                heroEntries[hero.Id].SetData(
                    hero.Id,
                    hero.DisplayNameEn,
                    detail,
                    "HeroPortrait." + hero.Id,
                    new Color(0.22f, 0.32f, 0.36f, 0.98f),
                    hero.Id == selectedHeroId,
                    editable,
                    SelectHero);
            }
        }

        private void RefreshRuneEntries(bool editable)
        {
            foreach (var rune in RuneCatalog.All)
            {
                var entry = runeEntries[rune.RuneId];
                var visible = !activeFilter.HasValue || rune.Rarity == activeFilter.Value;
                entry.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var owned = service.Inventory.OwnedCount(rune.RuneId);
                var fragments = service.Inventory.FragmentCount(rune.RuneId);
                var equippedHero = FindEquippedHero(rune.RuneId);
                var detail = "OWNED " + owned + " | FRAG " + fragments +
                    (string.IsNullOrEmpty(equippedHero) ? string.Empty : " | " + equippedHero);
                var data = presentation.Get(rune.RuneId);
                entry.SetData(
                    rune.RuneId,
                    rune.RuneId + " | " + rune.Rarity,
                    detail,
                    data != null ? data.ArtAssetKey : string.Empty,
                    ColorForRarity(rune.Rarity),
                    rune.RuneId == selectedRuneId,
                    editable,
                    SelectRuneAndEquip);
            }
        }

        private void RefreshSelection(bool editable)
        {
            var equippedRune = service.Loadout.GetRune(selectedHeroId);
            if (selectedHeroLabel != null)
            {
                selectedHeroLabel.text = "HERO: " + selectedHeroId + " | " +
                    (string.IsNullOrEmpty(equippedRune) ? "NO RUNE" : equippedRune);
            }

            if (selectedRuneLabel != null)
            {
                selectedRuneLabel.text = string.IsNullOrEmpty(selectedRuneId)
                    ? "SELECT A RUNE TO EQUIP"
                    : "RUNE: " + selectedRuneId;
            }

            if (unequipButton != null) unequipButton.interactable = editable && !string.IsNullOrEmpty(equippedRune);
            if (craftButton != null)
            {
                craftButton.interactable = editable && !string.IsNullOrEmpty(selectedRuneId) &&
                                         service.Inventory.CanCraftRune(selectedRuneId);
            }
        }

        private void SelectHero(string heroId)
        {
            selectedHeroId = heroId;
            SetValidation(string.Empty);
            Refresh();
        }

        private void SelectRuneAndEquip(string runeId)
        {
            selectedRuneId = runeId;
            if (!service.TryEquip(selectedHeroId, runeId, out var reason))
            {
                SetValidation(reason);
            }
            else
            {
                SetValidation("EQUIPPED");
            }

            Refresh();
        }

        private void UnequipSelectedHero()
        {
            SetValidation(service.TryUnequip(selectedHeroId, out var reason) ? "UNEQUIPPED" : reason);
            Refresh();
        }

        private void CraftSelectedRune()
        {
            SetValidation(service.TryCraft(selectedRuneId, out var reason) ? "CRAFTED" : reason);
            Refresh();
        }

        private void ShowAll() { activeFilter = null; Refresh(); }
        private void ShowCommon() { activeFilter = RuneRarity.Common; Refresh(); }
        private void ShowExcellent() { activeFilter = RuneRarity.Excellent; Refresh(); }
        private void ShowEpic() { activeFilter = RuneRarity.Epic; Refresh(); }
        private void ShowLegendary() { activeFilter = RuneRarity.Legendary; Refresh(); }

        private void SetValidation(string value)
        {
            if (validationLabel != null) validationLabel.text = value ?? string.Empty;
        }

        private string FindEquippedHero(string runeId)
        {
            foreach (var assignment in service.Loadout.Assignments)
            {
                if (string.Equals(assignment.Value, runeId, StringComparison.Ordinal))
                {
                    return assignment.Key;
                }
            }

            return string.Empty;
        }

        private static string FirstHeroId()
        {
            foreach (var hero in HeroDefinitionCatalog.Definitions)
            {
                return hero.Id;
            }

            return string.Empty;
        }

        private static Color ColorForRarity(RuneRarity rarity)
        {
            switch (rarity)
            {
                case RuneRarity.Common: return new Color(0.22f, 0.52f, 0.30f, 0.98f);
                case RuneRarity.Excellent: return new Color(0.22f, 0.40f, 0.72f, 0.98f);
                case RuneRarity.Epic: return new Color(0.50f, 0.28f, 0.68f, 0.98f);
                default: return new Color(0.70f, 0.54f, 0.19f, 0.98f);
            }
        }

        private static void RemoveFilterListener(Button button, UnityEngine.Events.UnityAction listener)
        {
            if (button != null) button.onClick.RemoveListener(listener);
        }
    }
}
