using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlazorSignalRApp.Server.Hubs
{
  public class GameSession
  {
    private int boardSize = 19; // Always odd number. Never below 13
    private char[,] emptyBoard;
    private char[,] currentBoard;
    private List<(int X, int Y)> plays = new List<(int, int)>();

    private DateTime sessionUpdatedAt;

    public GameSession()
    {
      emptyBoard = new char[boardSize, boardSize];

      // Default crosses
      for (int i = 0; i < emptyBoard.GetLength(0); ++i)
        for (int j = 0; j < emptyBoard.GetLength(1); ++j)
          emptyBoard[i, j] = '5';
      // Straight Borders
      for (int i = 0; i < emptyBoard.GetLength(0); ++i)
      {
        emptyBoard[i, 0] = '8';
        emptyBoard[i, emptyBoard.GetLength(1) - 1] = '2';
      }
      for (int j = 0; j < emptyBoard.GetLength(1); ++j)
      {
        emptyBoard[0, j] = '4';
        emptyBoard[emptyBoard.GetLength(0) - 1, j] = '6';
      }
      // Corners
      emptyBoard[0, 0] = '7';
      emptyBoard[0, emptyBoard.GetLength(1) - 1] = '1';
      emptyBoard[emptyBoard.GetLength(0) - 1, 0] = '9';
      emptyBoard[emptyBoard.GetLength(0) - 1, emptyBoard.GetLength(1) - 1] = '3';
      // Top dots
      emptyBoard[3, 3] = '+';
      emptyBoard[emptyBoard.GetLength(0) / 2, 3] = '+';
      emptyBoard[emptyBoard.GetLength(0) - 4, 3] = '+';
      // Middle dots
      emptyBoard[3, emptyBoard.GetLength(1) / 2] = '+';
      emptyBoard[emptyBoard.GetLength(0) / 2, emptyBoard.GetLength(1) / 2] = '+';
      emptyBoard[emptyBoard.GetLength(0) - 4, emptyBoard.GetLength(1) / 2] = '+';
      // Bottom dots
      emptyBoard[3, emptyBoard.GetLength(1) - 4] = '+';
      emptyBoard[emptyBoard.GetLength(0) / 2, emptyBoard.GetLength(1) - 4] = '+';
      emptyBoard[emptyBoard.GetLength(0) - 4, emptyBoard.GetLength(1) - 4] = '+';

      currentBoard = emptyBoard.Clone() as char[,];

      sessionUpdatedAt = DateTime.Now;
    }

    public bool OldGame() => sessionUpdatedAt < DateTime.Now - TimeSpan.FromHours(6);

    public string PrintCurrentBoard()
    {
      StringBuilder boardString = new StringBuilder();
      for (int j = 0; j < currentBoard.GetLength(1); ++j)
      {
        for (int i = 0; i < currentBoard.GetLength(0); ++i)
          boardString.Append(currentBoard[i, j]);
        boardString.AppendLine("");
      }
      return boardString.ToString();
    }

    public void PlaceStone(int x, int y)
    {
      if (currentBoard[x, y] != 'w' && currentBoard[x, y] != 'b')
      {
        currentBoard[x, y] = CurrentTurn();
        plays.Add((x, y));
      }
      sessionUpdatedAt = DateTime.Now;
    }

    public void UndoStone()
    {
      if (plays.Count > 0)
      {
        var lastCoordinate = plays.Last();
        currentBoard[lastCoordinate.X, lastCoordinate.Y] = emptyBoard[lastCoordinate.X, lastCoordinate.Y];
        plays.RemoveAt(plays.Count - 1);
      }
      sessionUpdatedAt = DateTime.Now;
    }

    public char CurrentTurn(int turn)
    {
      if (turn == 0)
        return 'b';
      return (((turn - 1) / 2) % 2 == 0) ? 'w' : 'b';
    }

    public int CurrentTurnRemaining(int turn) => (turn + 1) % 2 == 0 ? 2 : 1;


    public char CurrentTurn() => CurrentTurn(plays.Count);

    public int CurrentTurnRemaining() => CurrentTurnRemaining(plays.Count);
  }
}