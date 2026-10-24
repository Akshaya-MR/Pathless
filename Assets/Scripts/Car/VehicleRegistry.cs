using UnityEngine;

namespace TheRacer
{
    /// <summary>Stable IDs and model data for vehicles that can be assigned to players or AI.</summary>
    public enum VehicleId
    {
        SilverCoupe,
        PorscheGT3RS,
        CyberpunkCar,
        SciFiVehicle
    }

    public sealed class VehicleDefinition
    {
        public VehicleId Id { get; }
        public string DisplayName { get; }
        public string ModelResourcePath { get; }
        public float ModelYawCorrection { get; }
        public Color BodyColor { get; }
        public Color NeonColor { get; }
        public int NeonAccentStyle { get; }
        public bool UsesImportedModel => !string.IsNullOrEmpty(ModelResourcePath);

        internal VehicleDefinition(VehicleId id, string displayName, string modelResourcePath, float modelYawCorrection,
            Color bodyColor, Color neonColor, int neonAccentStyle)
        {
            Id = id;
            DisplayName = displayName;
            ModelResourcePath = modelResourcePath;
            ModelYawCorrection = modelYawCorrection;
            BodyColor = bodyColor;
            NeonColor = neonColor;
            NeonAccentStyle = neonAccentStyle;
        }

        public CarVisualProfile CreateVisualProfile(CarVisualProfile appearance = null)
        {
            CarVisualProfile profile = appearance != null ? appearance.Copy() : new CarVisualProfile();
            profile.modelResourcePath = ModelResourcePath;
            profile.modelYawCorrection = ModelYawCorrection;
            profile.bodyColor = BodyColor;
            profile.neonColor = NeonColor;
            profile.neonAccentStyle = NeonAccentStyle;
            return profile;
        }
    }

    /// <summary>Central vehicle data source; future selection UI should select one of these IDs.</summary>
    public static class VehicleRegistry
    {
        private static readonly VehicleDefinition[] Vehicles =
        {
            new VehicleDefinition(VehicleId.SilverCoupe, "Silver Coupe", ThemeText.SilverCarResourcePath, 180f,
                ThemePalette.PanelDark, ThemePalette.NeonCyan, 0),
            new VehicleDefinition(VehicleId.PorscheGT3RS, "Porsche GT3 RS",
                "VehicleModels/PorscheGT3/source/GT3 RS/GT3 RS", 0f,
                new Color(0.28f, 0.035f, 0.018f), new Color(1f, 0.25f, 0.025f), 0),
            new VehicleDefinition(VehicleId.CyberpunkCar, "Cyberpunk Car",
                "VehicleModels/CyberpunkCar/source/model", 0f,
                new Color(0.055f, 0.025f, 0.19f), ThemePalette.NeonCyan, 1),
            new VehicleDefinition(VehicleId.SciFiVehicle, "Sci-Fi Vehicle",
                "VehicleModels/SciFiVehicle/source/sci-fi_vehicle_006_01", 0f,
                new Color(0.015f, 0.12f, 0.19f), ThemePalette.ToxicGreen, 2)
        };

        public static VehicleDefinition[] All => (VehicleDefinition[])Vehicles.Clone();

        public static VehicleDefinition Get(VehicleId id)
        {
            for (int i = 0; i < Vehicles.Length; i++)
                if (Vehicles[i].Id == id)
                    return Vehicles[i];
            return Vehicles[0];
        }

        /// <summary>Returns a uniformly selected vehicle other than the player's chosen model.</summary>
        public static VehicleDefinition GetRandomAiVehicle(VehicleId playerVehicle, System.Random random)
        {
            int availableCount = 0;
            for (int i = 0; i < Vehicles.Length; i++)
                if (Vehicles[i].Id != playerVehicle)
                    availableCount++;

            if (availableCount == 0)
                return Get(playerVehicle);

            int selected = random.Next(availableCount);
            for (int i = 0; i < Vehicles.Length; i++)
            {
                if (Vehicles[i].Id == playerVehicle)
                    continue;
                if (selected-- == 0)
                    return Vehicles[i];
            }
            return Get(playerVehicle);
        }

        /// <summary>Builds a randomized rival lineup that uses every eligible model before repeating.</summary>
        public static VehicleDefinition[] GetAiVehicleLineup(VehicleId playerVehicle, System.Random random, int count)
        {
            count = Mathf.Max(0, count);
            VehicleDefinition[] available = new VehicleDefinition[Vehicles.Length];
            int availableCount = 0;
            for (int i = 0; i < Vehicles.Length; i++)
                if (Vehicles[i].Id != playerVehicle)
                    available[availableCount++] = Vehicles[i];

            if (count == 0) return new VehicleDefinition[0];
            if (availableCount == 0)
            {
                VehicleDefinition[] onlyPlayer = new VehicleDefinition[count];
                for (int i = 0; i < count; i++) onlyPlayer[i] = Get(playerVehicle);
                return onlyPlayer;
            }

            VehicleDefinition[] lineup = new VehicleDefinition[count];
            int written = 0;
            while (written < count)
            {
                // Shuffle the eligible models for each pass, avoiding repeats within a pass.
                for (int i = availableCount - 1; i > 0; i--)
                {
                    int swapIndex = random.Next(i + 1);
                    VehicleDefinition swap = available[i];
                    available[i] = available[swapIndex];
                    available[swapIndex] = swap;
                }

                int passCount = Mathf.Min(availableCount, count - written);
                for (int i = 0; i < passCount; i++) lineup[written++] = available[i];
            }
            return lineup;
        }
    }
}
