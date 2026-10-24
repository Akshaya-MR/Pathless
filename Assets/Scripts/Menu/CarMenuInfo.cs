using UnityEngine;

namespace TheRacer
{
    /// <summary>Optional menu copy and cosmetic comparison values keyed by the real vehicle registry ID.</summary>
    public static class CarMenuInfo
    {
        public struct Entry
        {
            public string displayName;
            public string description;
            public Vector4 stats;
        }

        public static Entry Get(VehicleDefinition vehicle)
        {
            if (vehicle == null) return new Entry { displayName = "UNKNOWN VEHICLE", description = "Ready for the night run.", stats = new Vector4(.5f, .5f, .5f, .5f) };
            switch (vehicle.Id)
            {
                case VehicleId.SilverCoupe: return Make("Silver Coupe", "Clean lines. Quiet menace.", .70f, .65f, .76f, .72f);
                case VehicleId.PorscheGT3RS: return Make("Porsche GT3 RS", "Track-bred. Zero mercy.", .88f, .82f, .79f, .87f);
                case VehicleId.CyberpunkCar: return Make("Cyberpunk Car", "Built in the neon undercity.", .75f, .78f, .88f, .68f);
                case VehicleId.SciFiVehicle: return Make("Sci-Fi Vehicle", "From a future that never slowed down.", .82f, .73f, .70f, .77f);
                default: return new Entry { displayName = vehicle.DisplayName, description = "Ready for the night run.", stats = new Vector4(.5f, .5f, .5f, .5f) };
            }
        }

        private static Entry Make(string title, string copy, float speed, float acceleration, float handling, float braking)
        {
            return new Entry { displayName = title, description = copy, stats = new Vector4(speed, acceleration, handling, braking) };
        }
    }
}
