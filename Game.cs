using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Core.Abstractions;
using Core.Gamble;
using Core.Game;
using Core.Spins;

namespace VampireRichesFullClassesStepType_2;

//Game Modes will be associated with runners in the following way:
public class GameModeRunners
{
    public static Dictionary<GameMode, IGameModeRunner> Runners =
        new()
        {
            { GameModeExtension.BaseSpin, new SpinGameModeRunner<BaseSpinState>(new SpinModeBaseSpin(GameModeExtension.BaseSpin), boardWidth: 5, boardHeight: 3) },{ GameModeExtension.FreeSpins7, new SpinGameModeRunner<FreeSpinsState>(new SpinModeFreeSpins(GameModeExtension.FreeSpins7), boardWidth: 5, boardHeight: 3) },{ GameModeExtension.FreeSpins9, new SpinGameModeRunner<FreeSpinsState>(new SpinModeFreeSpins(GameModeExtension.FreeSpins9), boardWidth: 5, boardHeight: 3) },{ GameModeExtension.FreeSpins11, new SpinGameModeRunner<FreeSpinsState>(new SpinModeFreeSpins(GameModeExtension.FreeSpins11), boardWidth: 5, boardHeight: 3) },
        };
}
public static class FeatureExtension
{
    public static Feature Avalanche => new("Avalanche");

}
public static class GameModeExtension
{
    public static GameMode BaseSpin = new(GameModeType.Spin, "BaseSpin");
public static GameMode FreeSpins7 = new(GameModeType.Spin, "FreeSpins7");
public static GameMode FreeSpins9 = new(GameModeType.Spin, "FreeSpins9");
public static GameMode FreeSpins11 = new(GameModeType.Spin, "FreeSpins11");

}
public static class SymbolExtensions
{
    public static Symbol Hi1 = new("Hi1");
public static Symbol Hi2 = new("Hi2");
public static Symbol Hi3 = new("Hi3");
public static Symbol Hi4 = new("Hi4");
public static Symbol Low1 = new("Low1");
public static Symbol Low2 = new("Low2");
public static Symbol Low3 = new("Low3");
public static Symbol Low4 = new("Low4");
public static Symbol Scatter = new("Scatter");
public static Symbol Wild = new("Wild");

}
public class SpinModeBaseSpin : ISpinMode<BaseSpinState>
{
    private readonly GameMode _gameMode;
    private readonly Symbol[] _allowedSymbols =
    {
        SymbolExtensions.Hi1,
        SymbolExtensions.Hi2,
        SymbolExtensions.Hi3,
        SymbolExtensions.Hi4,
        SymbolExtensions.Low1,
        SymbolExtensions.Low2,
        SymbolExtensions.Low3,
        SymbolExtensions.Low4,
        SymbolExtensions.Scatter
    };
    private readonly Dictionary<Symbol, List<float>> _payTable = new()
    {
        { SymbolExtensions.Wild, new List<float> {0.5f,1f,2.5f} },
        { SymbolExtensions.Hi1, new List<float> {0.5f,1f,2.5f} },
        { SymbolExtensions.Hi2, new List<float> {0.3f,0.6f,1.5f} },
        { SymbolExtensions.Hi3, new List<float> {0.2f,0.4f,1f} },
        { SymbolExtensions.Hi4, new List<float> {0.2f,0.4f,1f} },
        { SymbolExtensions.Low1, new List<float> {0.1f,0.2f,0.5f} },
        { SymbolExtensions.Low2, new List<float> {0.1f,0.2f,0.5f} },
        { SymbolExtensions.Low3, new List<float> {0.1f,0.2f,0.5f} },
        { SymbolExtensions.Low4, new List<float> {0.1f,0.2f,0.5f} }
    };

    public SpinModeBaseSpin(GameMode gameMode)
    {
        _gameMode = gameMode;
    }

    private List<CellCombination> GetWonCombinations(Board board, Symbol? wildSymbol = null)
    {
        return BoardEvaluations.WaysLeftToRight(board, _payTable, wildSymbol);
    }

    public GameMode GetNextMode(Step lastStep)
    {
        List<GameMode> allowedTransitions = new()
        {
            GameModeExtension.FreeSpins7,
            GameModeExtension.FreeSpins9,
            GameModeExtension.FreeSpins11,
        };
        var nextGameMode = GetNextModeInternal(lastStep);
        if (!allowedTransitions.Contains(nextGameMode) && nextGameMode != GameMode.None)
            throw new Exception($"Transition from BaseSpin to {nextGameMode} is not allowed");
        return nextGameMode;
    }

    public GameMode GetCurrentMode() => _gameMode;

    public Board InitializeBoard(int width, int height, IRng rng)
    {
        var board = BoardFunctions.GetRandomBoard(width, height, rng, _allowedSymbols);
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (board.GetCellAtPosition(new Position(x,y)).Symbol == SymbolExtensions.Wild)
                    board.GetCellAtPosition(new Position(x,y)).Symbol = _allowedSymbols[rng.Next(_allowedSymbols.Length)];
            }
        }
        return board;
    }

    public BaseSpinState GetInitialState()
    {
        return new BaseSpinState
        {
            CascadeCount = 0
        };
    }

    public Step EvaluateStep(Board board, BaseSpinState gameState, bool isInitialStep)
    {
        var wonCombinations = GetWonCombinations(board, SymbolExtensions.Wild);
        float baseWin = wonCombinations.Sum(x => x.CombinationPayout);
        float multiplier = (float)Math.Pow(2, gameState.CascadeCount);
        float stepWin = baseWin * multiplier;
        var step = new Step(board)
        {
            WonCombinations = wonCombinations,
            StepWin = stepWin,
            SpinStateCopy = gameState.CloneState(),
            StepType = isInitialStep ? StepType.InitialStep : StepType.Cascade
        };
        if (wonCombinations.Any())
            gameState = gameState with { CascadeCount = gameState.CascadeCount + 1 };
        return step;
    }

    public Board PrepareBoardForNextStep(IRng rng, Step lastStep, BaseSpinState gameState)
    {
        var board = lastStep.Board.Clone();
        if (!lastStep.HasWins)
            return board;
        var removedPositions = lastStep.WonCombinations
            .SelectMany(c => c.ActivatedCell)
            .Distinct()
            .ToList();
        var chosenPos = removedPositions[rng.Next(removedPositions.Count)];
        foreach (var pos in removedPositions)
            board.GetCellAtPosition(pos).Symbol = new Symbol("Empty");
        board.GetCellAtPosition(chosenPos).Symbol = SymbolExtensions.Wild;
        for (int x = 0; x < board.Width; x++)
        {
            var reel = board.Reels[x];
            for (int y = board.Height - 1; y >= 0; y--)
            {
                if (reel.Cells[y].Symbol.SymbolCode == "Empty")
                {
                    int above = y - 1;
                    while (above >= 0 && reel.Cells[above].Symbol.SymbolCode == "Empty")
                        above--;
                    if (above >= 0)
                    {
                        reel.Cells[y].Symbol = reel.Cells[above].Symbol;
                        reel.Cells[above].Symbol = new Symbol("Empty");
                    }
                }
            }
            for (int y = 0; y < board.Height; y++)
            {
                if (reel.Cells[y].Symbol.SymbolCode == "Empty")
                {
                    reel.Cells[y].Symbol = _allowedSymbols[rng.Next(_allowedSymbols.Length)];
                }
            }
        }
        return board;
    }

    public bool ShouldHaveNextStep(IRng rng, Step step, BaseSpinState gameState)
    {
        return step.HasWins;
    }

    public Dictionary<Feature, int> CountFeatureOccurrencesInStep(Step step)
    {
        var dict = new Dictionary<Feature, int>();
        if (step.StepType.Code == StepType.Cascade.Code && step.HasWins)
            dict[FeatureExtension.Avalanche] = 1;
        return dict;
    }

    private GameMode GetNextModeInternal(Step lastStep)
    {
        var scatterCount = 0;
        for (int x = 0; x < lastStep.Board.Width; x++)
        {
            for (int y = 0; y < lastStep.Board.Height; y++)
            {
                var cell = lastStep.Board.Reels[x].Cells[y];
                if (cell.Symbol == SymbolExtensions.Scatter)
                    scatterCount++;
            }
        }
        if (scatterCount == 3)
            return GameModeExtension.FreeSpins7;
        if (scatterCount == 4)
            return GameModeExtension.FreeSpins9;
        if (scatterCount >= 5)
            return GameModeExtension.FreeSpins11;
        return GameMode.None;
    }
}

public record BaseSpinState : ISpinState
{
    public int CascadeCount { get; init; }
    public ISpinState CloneState() => this with { };
}

public class SpinModeFreeSpins : ISpinMode<FreeSpinsState>
{
    private readonly GameMode _gameMode;
    private readonly Symbol[] _allowedSymbols =
    {
        SymbolExtensions.Hi1,
        SymbolExtensions.Hi2,
        SymbolExtensions.Hi3,
        SymbolExtensions.Hi4,
        SymbolExtensions.Low1,
        SymbolExtensions.Low2,
        SymbolExtensions.Low3,
        SymbolExtensions.Low4,
        SymbolExtensions.Scatter
    };
    private readonly Dictionary<Symbol, List<float>> _payTable = new()
    {
        { SymbolExtensions.Wild, new List<float> {0.5f,1f,2.5f} },
        { SymbolExtensions.Hi1, new List<float> {0.5f,1f,2.5f} },
        { SymbolExtensions.Hi2, new List<float> {0.3f,0.6f,1.5f} },
        { SymbolExtensions.Hi3, new List<float> {0.2f,0.4f,1f} },
        { SymbolExtensions.Hi4, new List<float> {0.2f,0.4f,1f} },
        { SymbolExtensions.Low1, new List<float> {0.1f,0.2f,0.5f} },
        { SymbolExtensions.Low2, new List<float> {0.1f,0.2f,0.5f} },
        { SymbolExtensions.Low3, new List<float> {0.1f,0.2f,0.5f} },
        { SymbolExtensions.Low4, new List<float> {0.1f,0.2f,0.5f} }
    };
    public SpinModeFreeSpins(GameMode gameMode)
    {
        _gameMode = gameMode;
    }
    private List<CellCombination> GetWonCombinations(Board board, Symbol? wildSymbol = null)
    {
        return BoardEvaluations.WaysLeftToRight(board, _payTable, wildSymbol);
    }
    public GameMode GetNextMode(Step lastStep)
    {
        return GameMode.None;
    }
    public GameMode GetCurrentMode() => _gameMode;
    public Board InitializeBoard(int width, int height, IRng rng)
    {
        var board = BoardFunctions.GetRandomBoard(width, height, rng, _allowedSymbols);
        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (board.GetCellAtPosition(new Position(x,y)).Symbol == SymbolExtensions.Wild)
                    board.GetCellAtPosition(new Position(x,y)).Symbol = _allowedSymbols[rng.Next(_allowedSymbols.Length)];
            }
        }
        return board;
    }
    public FreeSpinsState GetInitialState()
    {
        var s = new FreeSpinsState
        {
            CascadeCount = 0,
            FreeSpinsRemaining = _gameMode == GameModeExtension.FreeSpins7 ? 7 : _gameMode == GameModeExtension.FreeSpins9 ? 9 : 11
        };
        return s;
    }
    public Step EvaluateStep(Board board, FreeSpinsState gameState, bool isInitialStep)
    {
        var wc = GetWonCombinations(board, SymbolExtensions.Wild);
        float baseWin = wc.Sum(x => x.CombinationPayout);
        float multiplier = (float)Math.Pow(2, gameState.CascadeCount);
        float stepWin = baseWin * multiplier;
        var step = new Step(board)
        {
            WonCombinations = wc,
            StepWin = stepWin,
            SpinStateCopy = gameState.CloneState(),
            StepType = isInitialStep ? StepType.InitialStep : (wc.Any() ? StepType.Cascade : StepType.VirtualRespin)
        };
        if (wc.Any())
            gameState = gameState with { CascadeCount = gameState.CascadeCount + 1 };
        return step;
    }
    public Board PrepareBoardForNextStep(IRng rng, Step lastStep, FreeSpinsState gameState)
    {
        var board = lastStep.Board.Clone();
        if (lastStep.HasWins)
        {
            var removedPositions = lastStep.WonCombinations
                .SelectMany(c => c.ActivatedCell)
                .Distinct()
                .ToList();
            var chosenPos = removedPositions[rng.Next(removedPositions.Count)];
            foreach (var pos in removedPositions)
                board.GetCellAtPosition(pos).Symbol = new Symbol("Empty");
            board.GetCellAtPosition(chosenPos).Symbol = SymbolExtensions.Wild;
            for (int x = 0; x < board.Width; x++)
            {
                var reel = board.Reels[x];
                for (int y = board.Height - 1; y >= 0; y--)
                {
                    if (reel.Cells[y].Symbol.SymbolCode == "Empty")
                    {
                        int above = y - 1;
                        while (above >= 0 && reel.Cells[above].Symbol.SymbolCode == "Empty")
                            above--;
                        if (above >= 0)
                        {
                            reel.Cells[y].Symbol = reel.Cells[above].Symbol;
                            reel.Cells[above].Symbol = new Symbol("Empty");
                        }
                    }
                }
                for (int y = 0; y < board.Height; y++)
                {
                    if (reel.Cells[y].Symbol.SymbolCode == "Empty")
                    {
                        reel.Cells[y].Symbol = _allowedSymbols[rng.Next(_allowedSymbols.Length)];
                    }
                }
            }
            return board;
        }
        if (!lastStep.HasWins && gameState.FreeSpinsRemaining > 0)
        {
            gameState = gameState with { FreeSpinsRemaining = gameState.FreeSpinsRemaining - 1 };
            return BoardFunctions.GetRandomBoard(board.Width, board.Height, rng, _allowedSymbols);
        }
        return board;
    }
    public bool ShouldHaveNextStep(IRng rng, Step step, FreeSpinsState gameState)
    {
        if (step.HasWins)
            return true;
        return gameState.FreeSpinsRemaining > 0;
    }
    public Dictionary<Feature, int> CountFeatureOccurrencesInStep(Step step)
    {
        var dict = new Dictionary<Feature, int>();
        if (step.StepType.Code == StepType.Cascade.Code && step.HasWins)
            dict[FeatureExtension.Avalanche] = 1;
        return dict;
    }
    private GameMode GetNextModeInternal(Step lastStep)
    {
        return GameMode.None;
    }
}

public record FreeSpinsState : ISpinState
{
    public int CascadeCount { get; init; }
    public int FreeSpinsRemaining { get; init; }
    public ISpinState CloneState() => this with { };
}

