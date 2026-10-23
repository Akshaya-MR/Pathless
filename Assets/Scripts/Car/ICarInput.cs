namespace TheRacer
{
    /// <summary>Frame-sampled controls consumed by the car and camera.</summary>
    public interface ICarInput
    {
        float Throttle { get; }
        float Brake { get; }
        float Steer { get; }
        bool Handbrake { get; }
        bool Boost { get; }

        /// <summary>Edge flags latch until their getter is read, then clear.</summary>
        bool ResetPressed { get; }
        bool PausePressed { get; }
        bool CameraTogglePressed { get; }
    }
}
