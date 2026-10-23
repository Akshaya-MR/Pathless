using System;
using UnityEngine;

namespace TheRacer
{
    /// <summary>Per-racer lap, timing, checkpoint, and safe-respawn state.</summary>
    public sealed class RacerState
    {
        public int Lap { get; private set; } = 1;
        public int TotalLaps { get; private set; }
        public int NextCheckpointIndex { get; private set; }
        public float CurrentLapTime { get; private set; }
        public float BestLapTime { get; private set; }
        public float TotalRaceTime { get; private set; }
        public float DistanceProgress { get; private set; }
        public float LastLapTime { get; private set; }
        public bool Finished { get; private set; }
        public Transform LastSafeCheckpoint { get; private set; }

        public event Action<int> OnCheckpoint;
        public event Action<float, bool> OnLapComplete;
        public event Action OnRaceFinished;

        public RacerState(int totalLaps, Transform initialSafeCheckpoint)
        {
            TotalLaps = Mathf.Max(1, totalLaps);
            LastSafeCheckpoint = initialSafeCheckpoint;
        }

        public void Tick(float deltaTime, float distance, float lapLength)
        {
            if (Finished) return;
            TotalRaceTime += deltaTime;
            CurrentLapTime += deltaTime;
            DistanceProgress = (Lap - 1) * lapLength + Mathf.Clamp(distance, 0f, lapLength);
        }

        /// <summary>Advances a distance race without lap scoring.</summary>
        public void TickDistance(float deltaTime, float distance)
        {
            if (Finished) return;
            TotalRaceTime += deltaTime;
            DistanceProgress = Mathf.Max(0f, distance);
        }

        /// <summary>Marks the racer finished when the configured race distance is reached.</summary>
        public void FinishRace()
        {
            if (Finished) return;
            Finished = true;
            OnRaceFinished?.Invoke();
        }

        public void RecordCheckpoint(int index, Transform safeCheckpoint)
        {
            if (Finished || index != NextCheckpointIndex) return;
            NextCheckpointIndex++;
            LastSafeCheckpoint = safeCheckpoint;
            OnCheckpoint?.Invoke(index);
        }

        public bool CompleteLap()
        {
            if (Finished) return false;
            LastLapTime = CurrentLapTime;
            bool isBest = BestLapTime <= 0f || LastLapTime < BestLapTime;
            if (isBest) BestLapTime = LastLapTime;
            OnLapComplete?.Invoke(LastLapTime, isBest);
            Lap++;
            CurrentLapTime = 0f;
            NextCheckpointIndex = 0;
            if (Lap > TotalLaps)
            {
                Finished = true;
                OnRaceFinished?.Invoke();
            }
            return true;
        }

        public void SetSafeCheckpoint(Transform checkpoint)
        {
            LastSafeCheckpoint = checkpoint;
        }

        /// <summary>Restarts lap and checkpoint fields while keeping registered event listeners.</summary>
        public void ResetRace()
        {
            Lap = 1;
            NextCheckpointIndex = 0;
            CurrentLapTime = 0f;
            BestLapTime = 0f;
            TotalRaceTime = 0f;
            DistanceProgress = 0f;
            LastLapTime = 0f;
            Finished = false;
        }

        public void ClearEvents()
        {
            OnCheckpoint = null;
            OnLapComplete = null;
            OnRaceFinished = null;
        }
    }
}
