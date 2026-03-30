using System.Collections.Generic;
using UnityEngine;
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

        private DifficultyConfig _easyConfig;
        private DifficultyConfig _mediumConfig;
        private DifficultyConfig _hardConfig;

        private DifficultyConfig         _currentConfig;
        private readonly Stack<ICommand> _commandHistory = new();
        private int                      _moveCount;

        private void Awake()
        {
            _easyConfig   = MakeConfig("easy",   gridSize: 4, minPieces: 5,  maxPieces: 8,  minPieceSize: 2, maxPieceSize: 3);
            _mediumConfig = MakeConfig("medium", gridSize: 5, minPieces: 7,  maxPieces: 10, minPieceSize: 2, maxPieceSize: 4);
            _hardConfig   = MakeConfig("hard",   gridSize: 6, minPieces: 9,  maxPieces: 12, minPieceSize: 3, maxPieceSize: 5);

            _uiOverlay.SetNextLevelCallback(StartNextLevel);
            _uiOverlay.SetDifficultyCallbacks(
                easy:   () => BeginLevel(_easyConfig),
                medium: () => BeginLevel(_mediumConfig),
                hard:   () => BeginLevel(_hardConfig));
        }

        private void Start()
        {
            _boardController.OnBoardCompleted += HandleBoardCompleted;
            _snapSystem.OnMoveExecuted         += HandleMoveExecuted;

            BeginLevel(_easyConfig);
        }

        private void OnDestroy()
        {
            _boardController.OnBoardCompleted -= HandleBoardCompleted;
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
            _moveCount = Mathf.Max(0, _moveCount - 1);
            _uiOverlay.UpdateMoveCounter(_moveCount);
        }

        private void BeginLevel(DifficultyConfig config)
        {
            _inputHandler.IsBlocked = false;
            _currentConfig = config;
            _commandHistory.Clear();
            _moveCount = 0;
            _uiOverlay.HideCompletion();
            _uiOverlay.UpdateMoveCounter(0);

            _pieceSpawner.DestroyAll();
            _boardController.ResetBoard();

            LoadLevel(config);
        }

        private void LoadLevel(DifficultyConfig config)
        {
            LevelData levelData = _levelGenerator.Generate(config);

            _boardController.Initialize(levelData.grid.size, config.boardWorldSize);
            _snapSystem.SetSnapThreshold(_boardController.CellSize);
            _pieceSpawner.SpawnAll(levelData.pieces, levelData.grid.size, config);
        }

        private void HandleBoardCompleted()
        {
            _inputHandler.IsBlocked = true;
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
            int minPieceSize, int maxPieceSize) => new DifficultyConfig
        {
            difficultyName = name,
            gridSize       = gridSize,
            boardWorldSize = 7f,
            minPieces      = minPieces,
            maxPieces      = maxPieces,
            minPieceSize   = minPieceSize,
            maxPieceSize   = maxPieceSize,
        };
    }
}

