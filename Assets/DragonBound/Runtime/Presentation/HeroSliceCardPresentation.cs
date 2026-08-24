using DragonBound.Combat;
using DragonBound.Recruitment;

namespace DragonBound.Presentation
{
    internal static class HeroSliceCardPresentation
    {
        public static string GetLabel(RecruitCard card, RecruitmentService recruitment = null)
        {
            if (card.Kind == RecruitItemKind.BasicUnit)
            {
                return $"{BasicUnitCatalog.GetDisplayName(card.ConfigId)} {card.Level}";
            }

            if (card.Kind == RecruitItemKind.Shovel)
            {
                return "SHOVEL";
            }

            var name = HeroSliceCatalog.GetComponentDisplayName(card.ConfigId);
            if (HeroSliceCatalog.IsUniqueComponent(card.ConfigId) || card.IsUnique)
            {
                return $"{name}\n唯一";
            }

            if (string.Equals(card.ConfigId, HeroSliceCatalog.DragonSigilComponentId) &&
                recruitment != null &&
                recruitment.HeroSliceMode)
            {
                return $"{name}\n剩余 {recruitment.GetRemainingHeroComponentCount(card.ConfigId)}";
            }

            return name;
        }
    }
}
