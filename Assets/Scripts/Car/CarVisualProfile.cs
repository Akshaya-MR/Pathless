using System;
using UnityEngine;

namespace TheRacer
{
    public enum CarBodyType
    {
        StreetWedge,
        TurbineGT,
        GhostCoupe,
        BruteMK2
    }

    /// <summary>Inspector-ready appearance selection for the player car.</summary>
    [Serializable]
    public sealed class CarVisualProfile
    {
        public CarBodyType bodyType = CarBodyType.StreetWedge;
        public Color bodyColor = ThemePalette.PanelDark;
        public Color neonColor = ThemePalette.NeonCyan;
        public bool spoiler = true;
        public bool widebody;
        public string handle = ThemeText.DefaultDriverHandle;
        public string modelResourcePath;
        public float modelYawCorrection = 180f;

        public CarVisualProfile Copy()
        {
            return new CarVisualProfile
            {
                bodyType = bodyType,
                bodyColor = bodyColor,
                neonColor = neonColor,
                spoiler = spoiler,
                widebody = widebody,
                handle = handle,
                modelResourcePath = modelResourcePath,
                modelYawCorrection = modelYawCorrection
            };
        }
    }
}
