using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace BlazorSignalRApp.Server.Hubs
{
  [Serializable]
  public class GameSession
  {
    public int BoardSize { get; set; } = 19; // Always odd number. Never below 11

    public char[][] EmptyBoard { get; set; }

    public char[][] CurrentBoard { get; set; }

    public List<int> PlaysX { get; set; } = new List<int>();
    public List<int> PlaysY { get; set; } = new List<int>();

    private DateTime SessionUpdatedAt { get; set; }

    public GameSession()
    {
      EmptyBoard = new char[BoardSize][];
      CurrentBoard = new char[BoardSize][];

      // Default crosses
      for (int j = 0; j < BoardSize; ++j)
      {
        EmptyBoard[j] = new char[BoardSize];
        CurrentBoard[j] = new char[BoardSize];
        for (int i = 0; i < BoardSize; ++i)
          EmptyBoard[j][i] = '5';
      }
      // Straight Borders
      for (int i = 0; i < BoardSize; ++i)
      {
        EmptyBoard[0][i] = '8';
        EmptyBoard[BoardSize - 1][i] = '2';
      }
      for (int j = 0; j < BoardSize; ++j)
      {
        EmptyBoard[j][0] = '4';
        EmptyBoard[j][BoardSize - 1] = '6';
      }
      // Corners
      EmptyBoard[0][0] = '7';
      EmptyBoard[0][BoardSize - 1] = '9';
      EmptyBoard[BoardSize - 1][0] = '1';
      EmptyBoard[BoardSize - 1][BoardSize - 1] = '3';
      // Left dots
      EmptyBoard[3][3] = '+';
      EmptyBoard[BoardSize / 2][3] = '+';
      EmptyBoard[BoardSize - 4][3] = '+';
      // Center dots
      EmptyBoard[3][BoardSize / 2] = '+';
      EmptyBoard[BoardSize / 2][BoardSize / 2] = '+';
      EmptyBoard[BoardSize - 4][BoardSize / 2] = '+';
      // Right dots
      EmptyBoard[3][BoardSize - 4] = '+';
      EmptyBoard[BoardSize / 2][BoardSize - 4] = '+';
      EmptyBoard[BoardSize - 4][BoardSize - 4] = '+';

      for (int j = 0; j < BoardSize; ++j)
        for (int i = 0; i < BoardSize; ++i)
          CurrentBoard[j][i] = EmptyBoard[j][i];

      SessionUpdatedAt = DateTime.Now;
    }

    public bool OldGame() => SessionUpdatedAt < DateTime.Now - TimeSpan.FromMinutes(30);

    public string PrintCurrentBoard()
    {
      StringBuilder boardString = new StringBuilder();
      for (int j = 0; j < BoardSize; ++j)
      {
        for (int i = 0; i < BoardSize; ++i)
          boardString.Append(CurrentBoard[j][i]);
        boardString.AppendLine("");
      }
      SessionUpdatedAt = DateTime.Now;
      return boardString.ToString().Trim('\r', '\n').Trim();
    }

    public void PlaceStone(int x, int y)
    {
      if (CurrentBoard[y][x] != 'w' && CurrentBoard[y][x] != 'b')
      {
        CurrentBoard[y][x] = CurrentTurn();
        PlaysX.Add(x);
        PlaysY.Add(y);
      }
    }

    public void UndoStone()
    {
      if (PlaysX.Count > 0)
      {
        var lastCoordinateX = PlaysX.Last();
        var lastCoordinateY = PlaysY.Last();
        CurrentBoard[lastCoordinateY][lastCoordinateX] = EmptyBoard[lastCoordinateY][lastCoordinateX];
        PlaysX.RemoveAt(PlaysX.Count - 1);
        PlaysY.RemoveAt(PlaysY.Count - 1);
      }
    }

    public char CurrentTurn(int turn)
    {
      if (turn == 0)
        return 'b';
      return (((turn - 1) / 2) % 2 == 0) ? 'w' : 'b';
    }

    public int CurrentTurnRemaining(int turn) => (turn + 1) % 2 == 0 ? 2 : 1;


    public char CurrentTurn() => CurrentTurn(PlaysX.Count);

    public int CurrentTurnRemaining() => CurrentTurnRemaining(PlaysX.Count);
  }
}