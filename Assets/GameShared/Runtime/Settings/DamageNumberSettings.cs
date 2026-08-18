using UnityEngine;

namespace GameShared.Settings
{
    /// <summary>
    /// Player-owned presentation preference shared by the Main UI and battle scenes.
    /// </summary>
    public static class DamageNumberSettings
    {
        private const string VisibleKey = "dragonbound.settings.damage-numbers-visible";

        public static bool Visible
        {
            get => PlayerPrefs.GetInt(VisibleKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(VisibleKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
