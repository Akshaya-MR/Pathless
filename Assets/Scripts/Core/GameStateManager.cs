using System;
using UnityEngine;

namespace TheRacer
{
    public enum GameState
    {
        MainMenu,
        RaceSelect,
        Countdown,
        Racing,
        Paused,
        Results
    }

    /// <summary>Central state machine for menu, race, pause, and results flow.</summary>
    public sealed class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }
        public GameState Current { get; private set; } = GameState.MainMenu;
        public event Action<GameState, GameState> StateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetState(GameState next)
        {
            if (Current == next) return;
            GameState previous = Current;
            Current = next;
            Time.timeScale = next == GameState.Paused ? 0f : 1f;
            StateChanged?.Invoke(previous, next);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }
    }
}
