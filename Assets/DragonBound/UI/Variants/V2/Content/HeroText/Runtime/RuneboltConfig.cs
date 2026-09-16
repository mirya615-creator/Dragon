using UnityEngine;
namespace Drakeforge.Runebolt {
    [CreateAssetMenu(menuName="Drakeforge/Runebolt presentation")]
    public sealed class RuneboltConfig : ScriptableObject {
        public string heroId="HERO_RUNEBOLT_MAGE";
        [Header("Presentation seconds / units (1 unit = 1 cell in demo)")]
        public float idleDuration=2, attackDuration=.5f, attackRelease=.22f, skillDuration=.85f, skillRelease=.40f;
        public float projectileSpeed=24, skillProjectileSpeed=38;
        [Header("V11 baseline; verify against production configuration")]
        public float baseAttack=8, baseAttackSpeed=1.75f, acquisitionRange=3, pierceLength=5, lineWidth=.35f;
        [Tooltip("0 = unspecified/unlimited in demo. V11 supplied does not specify a cap. Not 4/5/6.")]
        public int demoTargetCap=0;
        public float Attack(int level) => baseAttack * new[]{1f,1.05f,1.10f}[Mathf.Clamp(level-1,0,2)];
        public float AttackSpeed(int level) => baseAttackSpeed * new[]{1f,1.25f,1.56f}[Mathf.Clamp(level-1,0,2)];
        public float SkillMultiplier(int level) => new[]{1f,1.1f,1.25f}[Mathf.Clamp(level-1,0,2)];
    }
}
