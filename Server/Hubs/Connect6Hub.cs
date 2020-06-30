using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace BlazorSignalRApp.Server.Hubs
{
  public class Connect6Hub : Hub
  {
    static UInt64 totalSessions = 0;
    static UInt64 totalConnections = 0;
    static UInt64 totalMultiplayerGame = 0;
    static Dictionary<string, GameSession> gameSessions = new Dictionary<string, GameSession>();
    static Dictionary<string, HashSet<string>> connections = new Dictionary<string, HashSet<string>>();
    static Dictionary<string, string> reverseMapping = new Dictionary<string, string>();

    public async Task CreateNewGame()
    {
      var toRemove = gameSessions.Where(pair => pair.Value.OldGame()).Select(pair => pair.Key).ToList();
      foreach (string gameIdKey in toRemove)
      {
        try
        {
          gameSessions.Remove(gameIdKey);
          connections.Remove(gameIdKey);
          foreach (var keyValuePair in reverseMapping.ToList())
            if (keyValuePair.Value == gameIdKey)
              reverseMapping.Remove(keyValuePair.Key);
          Report(gameIdKey, "Session destroyed");
        }
        catch {}
      }

      string gameId = "";
      do
      {
        Guid g = Guid.NewGuid();
        gameId = g.ToString().Substring(0, 8);
      } while (gameSessions.ContainsKey(gameId));
      gameSessions.Add(gameId, new GameSession());
      connections.Add(gameId, new HashSet<string>());
      ++totalSessions;
      await Clients.Caller.SendAsync("NewGameIdReceived", gameId);
      Report(gameId, "New game made");
    }

    public async Task InitializeBoardAndConnection(string gameId)
    {
      if (await HandleNoGameFound(gameId))
        return;
      await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
      if (!connections[gameId].Contains(Context.ConnectionId))
      {
        connections[gameId].Add(Context.ConnectionId);
        reverseMapping.Add(Context.ConnectionId, gameId);
      }
      await SendCurrentStateAsync(gameId);
      await SendConnectionSize(gameId);
      ++totalConnections;
      if (connections[gameId].Count == 2)
        ++totalMultiplayerGame;
      Report(gameId, "New user connected to game");
    }

    public async Task PlaceStone(string gameId, int x, int y)
    {
      if (await HandleNoGameFound(gameId))
        return;
      gameSessions[gameId].PlaceStone(x, y);
      await SendCurrentStateAsync(gameId);
      Report(gameId, $"User played stone ({x}, {y})");
    }

    public async Task UndoStone(string gameId)
    {
      if (await HandleNoGameFound(gameId))
        return;
      gameSessions[gameId].UndoStone();
      await SendCurrentStateAsync(gameId);
      Report(gameId, "User undid");
    }

    public async Task NewGame(string gameId)
    {
      if (await HandleNoGameFound(gameId))
        return;
      try
      {
        if (gameSessions.ContainsKey(gameId))
        {
          gameSessions[gameId] = new GameSession();
          await SendCurrentStateAsync(gameId);
        }
      } catch {}
    }

    private async Task SendCurrentStateAsync(string gameId)
    {
      Dictionary<string, string> state = new Dictionary<string, string>();
      state.Add("currentTurn", gameSessions[gameId].CurrentTurn().ToString());
      state.Add("currentTurnRemaining", gameSessions[gameId].CurrentTurnRemaining().ToString());
      state.Add("boardString", gameSessions[gameId].PrintCurrentBoard());

      if (gameSessions[gameId].Plays.Count > 0)
      {
        var lastPlay = gameSessions[gameId].Plays.Last();
        state.Add("lastPlayX", lastPlay.X.ToString());
        state.Add("lastPlayY", lastPlay.Y.ToString());
        if (gameSessions[gameId].Plays.Count > 1)
        {
          char lastTurn = gameSessions[gameId].CurrentTurn(gameSessions[gameId].Plays.Count - 1);
          char lastLastTurn = gameSessions[gameId].CurrentTurn(gameSessions[gameId].Plays.Count - 2);
          if (lastTurn == lastLastTurn)
          {
            var lastLastPlay = gameSessions[gameId].Plays[^2];
            state.Add("lastLastPlayX", lastLastPlay.X.ToString());
            state.Add("lastLastPlayY", lastLastPlay.Y.ToString());
          }
          else
          {
            state.Add("lastLastPlayX", (-1).ToString());
            state.Add("lastLastPlayY", (-1).ToString());
          }
        }
        else
        {
          state.Add("lastLastPlayX", (-1).ToString());
          state.Add("lastLastPlayY", (-1).ToString());
        }
      }
      else
      {
        state.Add("lastPlayX", (-1).ToString());
        state.Add("lastPlayY", (-1).ToString());
        state.Add("lastLastPlayX", (-1).ToString());
        state.Add("lastLastPlayY", (-1).ToString());
      }
      await Clients.Group(gameId).SendAsync("CurrentBoard", state);
    }

    private async Task SendConnectionSize(string gameId) => await Clients.Group(gameId).SendAsync("ConnectionSize", connections[gameId].Count);

    private async Task<Boolean> HandleNoGameFound(string gameId)
    {
      if (gameSessions.ContainsKey(gameId))
        return false;
      else
      {
        await Clients.Caller.SendAsync("NoGameFound", "");
        return true;
      }
    }

    public async override Task OnDisconnectedAsync(Exception exception)
    {
      if (reverseMapping.ContainsKey(Context.ConnectionId))
      {
        try
        {
          string gameId = reverseMapping[Context.ConnectionId];
          reverseMapping.Remove(Context.ConnectionId);
          connections[gameId].Remove(Context.ConnectionId);
          await SendConnectionSize(gameId);
          Report(gameId, "User disconnected");
        }
        catch {}
      }
    }

    private void Report(string gameId, string message) => Console.WriteLine($"{DateTime.Now} [{totalSessions} TS, {totalConnections} TU, {totalMultiplayerGame} MUS, {gameSessions.Keys.Count} CS, {reverseMapping.Count} CU] {gameId} ({connections[gameId].Count}) : {message.PadRight(30)}{Context.ConnectionId}");
  }
}