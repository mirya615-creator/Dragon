using DragonBound.Combat;
using DragonBound.Recruitment;
using DragonBound.Runes;
using TMPro;
using UnityEngine;

namespace DragonBound.Presentation
{
    [DisallowMultipleComponent]
    public sealed class UnitInformController : MonoBehaviour
    {
        private const float BasicHeight = 115f;
        private const float HeroHeight = 165f;
        private const float HeroWithRuneHeight = 325f;

        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text maxLevelLabel;
        [SerializeField] private TMP_Text attackLabel;
        [SerializeField] private TMP_Text experienceLabel;
        [SerializeField] private GameObject divider;
        [SerializeField] private TMP_Text runeLabel;
        [SerializeField] private TMP_Text runeAttackLabel;
        [SerializeField] private TMP_Text attackRangeLabel;

        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = transform as RectTransform;
            ResolveReferences();
            NormalizeLayout();
            gameObject.SetActive(false);
        }

        public void ShowBasic(RecruitCard card)
        {
            if (card == null || card.Kind != RecruitItemKind.BasicUnit)
            {
                Hide();
                return;
            }

            var stats = BasicUnitCatalog.GetStats(card.ConfigId, card.Level);
            SetText(nameLabel, $"{BasicUnitCatalog.GetDisplayName(card.ConfigId)} Lv{card.Level} (Unit)");
            SetText(maxLevelLabel, $"Max Lv{BasicUnitCatalog.MaxLevel}");
            SetText(attackLabel, $"ATK : {stats.Attack:F2}      ATK Speed : {stats.AttackSpeed:F2}");
            SetSectionVisible(experienceLabel, false);
            SetRuneSectionVisible(false);
            SetHeight(BasicHeight);
            Show();
        }

        public void ShowHero(HeroPairCombatProxy combat)
        {
            if (combat == null)
            {
                Hide();
                return;
            }

            var definition = combat.Definition;
            SetText(nameLabel, $"{definition.DisplayName} Lv{combat.Level} (Unit)");
            SetText(maxLevelLabel, $"Max Lv{definition.MaxLevel}");
            SetText(attackLabel, $"ATK : {combat.Attack:F2}      ATK Speed : {combat.AttackSpeed:F2}");
            SetSectionVisible(experienceLabel, true);
            SetText(
                experienceLabel,
                combat.Level >= definition.MaxLevel
                    ? "EXP : (Max)"
                    : $"EXP : ({FormatNumber(combat.Experience)} / {FormatNumber(definition.GetLevelStats(combat.Level + 1).RequiredExperience)})");

            var rune = string.IsNullOrWhiteSpace(combat.RuneId)
                ? null
                : RuneCatalog.Get(combat.RuneId);
            if (rune == null)
            {
                SetRuneSectionVisible(false);
                SetHeight(HeroHeight);
            }
            else
            {
                SetRuneSectionVisible(true);
                SetText(runeLabel, $"Rune : {rune.RuneId}");
                var levelStats = definition.GetLevelStats(combat.Level);
                var attackWithoutRune = definition.BaseAttack * levelStats.AttackMultiplier;
                var runeAttack = Mathf.Max(0f, combat.Attack - attackWithoutRune);
                var runeRange = Mathf.Max(0f, combat.RangeCells - definition.RangeCells);
                SetText(runeAttackLabel, $"Rune ATK : {FormatNumber(runeAttack)}");
                SetText(attackRangeLabel, $"Attack Range + {FormatNumber(runeRange)}");
                SetHeight(HeroWithRuneHeight);
            }

            Show();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void ResolveReferences()
        {
            nameLabel = Resolve(nameLabel, "Name");
            maxLevelLabel = Resolve(maxLevelLabel, "MaxLv");
            attackLabel = Resolve(attackLabel, "ATK");
            experienceLabel = Resolve(experienceLabel, "EXP");
            divider = divider != null ? divider : transform.Find("device")?.gameObject;
            runeLabel = Resolve(runeLabel, "Rune");
            runeAttackLabel = Resolve(runeAttackLabel, "Rune ATK");
            attackRangeLabel = Resolve(attackRangeLabel, "ATKRange");
        }

        private TMP_Text Resolve(TMP_Text current, string childName)
        {
            return current != null ? current : transform.Find(childName)?.GetComponent<TMP_Text>();
        }

        private void NormalizeLayout()
        {
            if (rectTransform == null)
            {
                return;
            }

            // Keep the authored top edge fixed while the panel changes height.
            var topEdge = rectTransform.anchoredPosition.y +
                          ((1f - rectTransform.pivot.y) * rectTransform.rect.height);
            rectTransform.pivot = new Vector2(rectTransform.pivot.x, 1f);
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, topEdge);

            PlaceAtTop(nameLabel, -25f);
            PlaceAtTop(maxLevelLabel, -25f);
            PlaceAtTop(attackLabel, -75f);
            PlaceAtTop(experienceLabel, -125f);
            PlaceAtTop(divider != null ? divider.transform as RectTransform : null, -155f);
            PlaceAtTop(runeLabel, -185f);
            PlaceAtTop(runeAttackLabel, -235f);
            PlaceAtTop(attackRangeLabel, -285f);
        }

        private static void PlaceAtTop(Component component, float y)
        {
            PlaceAtTop(component != null ? component.transform as RectTransform : null, y);
        }

        private static void PlaceAtTop(RectTransform target, float y)
        {
            if (target == null)
            {
                return;
            }

            target.anchorMin = new Vector2(target.anchorMin.x, 1f);
            target.anchorMax = new Vector2(target.anchorMax.x, 1f);
            target.anchoredPosition = new Vector2(target.anchoredPosition.x, y);
        }

        private void SetHeight(float height)
        {
            if (rectTransform != null)
            {
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
        }

        private void SetRuneSectionVisible(bool visible)
        {
            if (divider != null) divider.SetActive(visible);
            SetSectionVisible(runeLabel, visible);
            SetSectionVisible(runeAttackLabel, visible);
            SetSectionVisible(attackRangeLabel, visible);
        }

        private static void SetSectionVisible(Component component, bool visible)
        {
            if (component != null)
            {
                component.gameObject.SetActive(visible);
            }
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
