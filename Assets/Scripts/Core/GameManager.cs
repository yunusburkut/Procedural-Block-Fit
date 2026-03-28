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
        [SerializeField] private LevelGenerator  _levelGenerator;
        [SerializeField] private BoardController _boardController;
        [SerializeField] private PieceSpawner    _pieceSpawner;
        [SerializeField] private SnapSystem      _snapSystem;
        [SerializeField] private UIOverlay       _uiOverlay;

        private DifficultyConfig _easyConfig;
        private DifficultyConfig _mediumConfig;
        private DifficultyConfig _hardConfig;

        private DifficultyConfig         _currentConfig;
        private readonly Stack<ICommand> _commandHistory = new();
        private int                      _moveCount;

        private void Awake()
        {
            _easyConfig   = MakeConfig("easy",   gridSize: 4, minPieces: 5,  maxPieces: 6,  minPieceSize: 2, maxPieceSize: 4);
            _mediumConfig = MakeConfig("medium", gridSize: 5, minPieces: 7,  maxPieces: 9,  minPieceSize: 3, maxPieceSize: 5);
            _hardConfig   = MakeConfig("hard",   gridSize: 6, minPieces: 10, maxPieces: 12, minPieceSize: 3, maxPieceSize: 5);

            _uiOverlay.SetRestartCallback(RestartLevel);
            _uiOverlay.SetDifficultyCallbacks(
                easy:   () => BeginLevel(_easyConfig),
                medium: () => BeginLevel(_mediumConfig),
                hard:   () => BeginLevel(_hardConfig));
        }

        private void Start()
        {
            _boardController.OnBoardCompleted += HandleBoardCompleted;
            _snapSystem.OnMoveExecuted         = HandleMoveExecuted;

            BeginLevel(_easyConfig);
        }

        private void OnDestroy()
        {
            _boardController.OnBoardCompleted -= HandleBoardCompleted;
        }

        public void RestartLevel()
        {
            if (_currentConfig != null)
                BeginLevel(_currentConfig);
        }

        public void UndoLastMove()
        {
            if (_commandHistory.Count == 0) return;
            var cmd = _commandHistory.Pop();
            cmd.Undo();
            _moveCount = Mathf.Max(0, _moveCount - 1);
            _uiOverlay.UpdateMoveCounter(_moveCount);
        }

        private void BeginLevel(DifficultyConfig config)
        {
            _currentConfig = config;
            _commandHistory.Clear();
            _moveCount = 0;
            _uiOverlay.HideCompletion();
            _uiOverlay.UpdateMoveCounter(0);

            _pieceSpawner.DestroyAll();
            _boardController.ResetBoard();

            StartCoroutine(LoadLevelCoroutine(config));
        }

        private IEnumerator LoadLevelCoroutine(DifficultyConfig config)
        {
            LevelData levelData = _levelGenerator.Generate(config);

            _boardController.Initialize(levelData.grid.size, config.boardWorldSize);
            _snapSystem.SetSnapThreshold(_boardController.CellSize);
            _pieceSpawner.SpawnAll(levelData.pieces, levelData.grid.size, config);

            yield break;
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

        private static DifficultyConfig MakeConfig(
            string name, int gridSize, int minPieces, int maxPieces,
            int minPieceSize, int maxPieceSize)
        {
            var cfg = ScriptableObject.CreateInstance<DifficultyConfig>();
            cfg.difficultyName = name;
            cfg.gridSize       = gridSize;
            cfg.boardWorldSize = 7f;
            cfg.minPieces      = minPieces;
            cfg.maxPieces      = maxPieces;
            cfg.minPieceSize   = minPieceSize;
            cfg.maxPieceSize   = maxPieceSize;
            return cfg;
        }
    }
}

