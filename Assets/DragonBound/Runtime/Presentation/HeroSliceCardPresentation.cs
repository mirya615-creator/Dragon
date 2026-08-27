using DragonBound.Combat;
using DragonBound.Recruitment;

namespace DragonBound.Presentation
{
    internal static class HeroSliceCardPresentation
    {
        public static string GetLabel(RecruitCard card, RecruitmentService recruitment = null)
        {
            return GetLabel(card, recruitment, true);
        }

        public static string GetEnglishLabel(RecruitCard card, RecruitmentService recruitment = null)
        {
            return GetLabel(card, recruitment, true);
        }

        private static string GetLabel(
            RecruitCard card,
            RecruitmentService recruitment,
            bool useEnglish)
        {
            if (card.Kind == RecruitItemKind.BasicUnit)
            {
                return BasicUnitCatalog.GetDisplayName(card.ConfigId);
            }

            if (card.Kind == RecruitItemKind.Shovel)
            {
                return "SHOVEL";
            }

            var name = useEnglish
                ? HeroSliceCatalog.GetComponentDisplayNameEn(card.ConfigId)
                : HeroSliceCatalog.GetComponentDisplayName(card.ConfigId);
            return name;
        }
    }
}
