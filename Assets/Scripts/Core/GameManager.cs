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
