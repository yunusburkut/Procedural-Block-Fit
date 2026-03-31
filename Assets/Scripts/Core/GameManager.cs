using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Blokfit.Board;
using Blokfit.Commands;
using Blokfit.Generation;
using Blokfit.Input;
using Blokfit.Pieces;
using Blokfit.ScriptableObjects;
using Blokfit.UI;
using System;
using Random = System.Random;

namespace Blokfit.Core
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelGenerator  _levelGenerator;
        [SerializeField] private BoardController _boardController;
        [SerializeField] private PieceSpawner    _pieceSpawner;
        [SerializeField] private SnapSystem      _snapSystem;
        [SerializeField] private UIOverlay       _uiOverlay;
        [SerializeField] private InputHandler    _inputHandler;

        [SerializeField] private DifficultyConfig _easyConfig;
        [SerializeField] private DifficultyConfig _mediumConfig;
        [SerializeField] private DifficultyConfig _hardConfig;

        // Optional: set a base URL to download levels from a server.
        // Leave empty to always generate procedurally.
        // Expected format: {_serverBaseUrl}/{difficultyName}.json
        // e.g. https://example.com/levels/easy.json
        [SerializeField] private string _serverBaseUrl = "";

        // ── Handmade levels ───────────────────────────────────────────────────
        [Header("Handmade Levels")]
        [Tooltip("JSON files exported from the Level Editor.")]
        [SerializeField] private TextAsset[] _handmadeLevels;

        [Tooltip("Center point around which pieces with spawnX/Y = 0 are scattered.")]
        [SerializeField] private Transform   _handmadeTrayCenter;

        [Tooltip("Scatter radius for automatic spawn distribution (units).")]
        [SerializeField] private float       _handmadeTraySpread = 1.2f;

        private int    _handmadeIndex;
        private Random _handmadeRng = new();

        private enum GameState { Idle, LevelComplete }

        private DifficultyConfig         _currentConfig;
        private readonly Stack<ICommand> _commandHistory = new();

        private void Awake()
        {
            _uiOverlay.SetNextLevelCallback(StartNextLevel);
            _uiOverlay.SetDifficultyCallbacks(
                easy:   () => BeginLevel(_easyConfig),
                medium: () => BeginLevel(_mediumConfig),
                hard:   () => BeginLevel(_hardConfig));
            _uiOverlay.SetHandmadeCallback(LoadHandmadeLevel);
        }

        private void Start()
        {
            _boardController.OnBoardCompleted += HandleBoardCompleted;
            _snapSystem.OnMoveExecuted        += HandleMoveExecuted;

            BeginLevel(_easyConfig);
        }

        private void OnDestroy()
        {
            _boardController.OnBoardCompleted -= HandleBoardCompleted;
            _snapSystem.OnMoveExecuted        -= HandleMoveExecuted;
        }

        public void StartNextLevel()
        {
            if (_currentConfig != null)
                BeginLevel(_currentConfig);
        }

        // ── Handmade level loading ────────────────────────────────────────────

        /// <summary>
        /// Loads the next level from the <see cref="_handmadeLevels"/> list in the Inspector.
        /// Called by the "Handmade" button in UIOverlay.
        /// </summary>
        public void LoadHandmadeLevel()
        {
            if (_handmadeLevels == null || _handmadeLevels.Length == 0)
            {
                Debug.LogWarning("[GameManager] _handmadeLevels list is empty!");
                return;
            }

            TextAsset asset = _handmadeLevels[_handmadeIndex % _handmadeLevels.Length];
            _handmadeIndex++;

            LevelData levelData = LevelSerializer.FromJson(asset.text);
            ScatterSpawnPositions(levelData);

            SetState(GameState.Idle);
            _currentConfig = null;
            _commandHistory.Clear();
            _uiOverlay.HideCompletion();
            _pieceSpawner.DestroyAll();
            _boardController.ResetBoard();
            ApplyLevel(levelData);
        }

        /// <summary>
        /// Assigns automatic tray positions to pieces whose spawnX and spawnY are both 0.
        /// Levels exported from the Level Editor always have these values set to 0.
        /// </summary>
        private void ScatterSpawnPositions(LevelData data)
        {
            if (data.pieces == null) return;

            bool anyZero = false;
            foreach (var p in data.pieces)
                if (p.spawnX == 0f && p.spawnY == 0f) { anyZero = true; break; }

            if (!anyZero) return;

            Vector2 center = _handmadeTrayCenter != null
                ? (Vector2)_handmadeTrayCenter.position
                : new Vector2(0f, -2.4f);

            int n = data.pieces.Length;
            for (int i = 0; i < n; i++)
            {
                var p = data.pieces[i];
                if (p.spawnX != 0f || p.spawnY != 0f) continue;

                // Distribute pieces along an arc with a small random jitter
                float angle  = (float)i / n * Mathf.PI * 2f;
                float rx     = ((float)_handmadeRng.NextDouble() * 2f - 1f) * 0.3f;
                float ry     = ((float)_handmadeRng.NextDouble() * 2f - 1f) * 0.3f;
                p.spawnX     = center.x + Mathf.Cos(angle) * _handmadeTraySpread + rx;
                p.spawnY     = center.y + Mathf.Sin(angle) * _handmadeTraySpread * 0.5f + ry;
            }
        }

        public void UndoLastMove()
        {
            if (_commandHistory.Count == 0) return;
            var cmd = _commandHistory.Pop();
            cmd.Undo();
        }

        private void BeginLevel(DifficultyConfig config)
        {
            SetState(GameState.Idle);
            _currentConfig = config;
            _commandHistory.Clear();
            _uiOverlay.HideCompletion();

            _pieceSpawner.DestroyAll();
            _boardController.ResetBoard();

            if (!string.IsNullOrEmpty(_serverBaseUrl))
                StartCoroutine(LoadFromServer(config));
            else
                ApplyLevel(_levelGenerator.Generate(config));
        }

        private IEnumerator LoadFromServer(DifficultyConfig config)
        {
            string url = $"{_serverBaseUrl}/{config.difficultyName}.json";

            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                ApplyLevel(LevelSerializer.FromJson(req.downloadHandler.text));
            else
            {
                Debug.LogWarning($"[GameManager] Server fetch failed ({req.error}), falling back to procedural.");
                ApplyLevel(_levelGenerator.Generate(config));
            }
        }

        private void ApplyLevel(LevelData levelData)
        {
            _boardController.Initialize(levelData.grid.size);
            _snapSystem.SetSnapThreshold(_boardController.CellSize);
            _pieceSpawner.SpawnAll(levelData.pieces, levelData.grid.size);
        }

        private void HandleBoardCompleted()
        {
            SetState(GameState.LevelComplete);
            _uiOverlay.ShowCompletion();
        }

        private void HandleMoveExecuted(ICommand cmd)
        {
            _commandHistory.Push(cmd);
        }

        private void SetState(GameState state)
        {
            _inputHandler.IsBlocked = (state == GameState.LevelComplete);
        }
    }
}
