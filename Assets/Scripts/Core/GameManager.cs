using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Blokfit.Board;
using Blokfit.Commands;
using Blokfit.Generation;
using Blokfit.Pieces;
using Blokfit.ScriptableObjects;
using Blokfit.UI;

namespace Blokfit.Core
{

    public class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelLoader     _levelLoader;
        [SerializeField] private LevelGenerator  _levelGenerator;
        [SerializeField] private BoardController _boardController;
        [SerializeField] private PieceSpawner    _pieceSpawner;
        [SerializeField] private SnapSystem      _snapSystem;
        [SerializeField] private UIOverlay       _uiOverlay;
        [SerializeField] private DifficultyConfig _difficultyConfig;

        private DifficultyConfig       _currentConfig;
        private readonly Stack<ICommand> _commandHistory = new();
        private int                    _moveCount;

        private void Awake()
        {
            _uiOverlay.SetRestartCallback(RestartLevel);
        }

        private void Start()
        {
            _boardController.OnBoardCompleted += HandleBoardCompleted;
            _snapSystem.OnMoveExecuted         = HandleMoveExecuted;

            StartLevel(_difficultyConfig);
        }

        private void OnDestroy()
        {
            _boardController.OnBoardCompleted -= HandleBoardCompleted;
        }

        public void StartLevel(DifficultyConfig config)
        {
            _currentConfig = config;
            _commandHistory.Clear();
            _moveCount = 0;
            _uiOverlay.HideCompletion();
            _uiOverlay.UpdateMoveCounter(0);

            StartCoroutine(LoadLevelCoroutine(config));
        }

        public void RestartLevel()
        {
            _pieceSpawner.DestroyAll();
            _boardController.ResetBoard();
            StartLevel(_currentConfig);
        }

        public void UndoLastMove()
        {
            if (_commandHistory.Count == 0) return;

            var cmd = _commandHistory.Pop();
            cmd.Undo();
            _moveCount = Mathf.Max(0, _moveCount - 1);
            _uiOverlay.UpdateMoveCounter(_moveCount);
        }

        private IEnumerator LoadLevelCoroutine(DifficultyConfig config)
        {
            LevelData levelData = null;
            yield return _levelLoader.FetchOrGenerate(config, data => levelData = data);

            if (levelData == null)
            {
                Debug.LogError("[GameManager] Level load failed — generating fallback.");
                levelData = _levelGenerator.Generate(config);
            }

            _boardController.Initialize(levelData.grid.size, config.boardWorldSize);
            _snapSystem.SetSnapThreshold(_boardController.CellSize);
            _pieceSpawner.SpawnAll(levelData.pieces, levelData.grid.size, config);
        }

        private void HandleBoardCompleted()
        {
            _uiOverlay.ShowCompletion();
        }

        private void HandleMoveExecuted(ICommand cmd)
        {
            _commandHistory.Push(cmd);
            _moveCount++;
            _uiOverlay.UpdateMoveCounter(_moveCount);
        }
    }
}
