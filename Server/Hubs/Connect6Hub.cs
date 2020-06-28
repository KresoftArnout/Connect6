using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace BlazorSignalRApp.Server.Hubs
{
  public class Connect6Hub : Hub
  {
    static Dictionary<string, GameSession> gameSessions = new Dictionary<string, GameSession>();

    public async Task CreateNewGame()
    {
      var toRemove = gameSessions.Where(pair => pair.Value.OldGame()).Select(pair => pair.Key).ToList();
      foreach (string key in toRemove)
        gameSessions.Remove(key);

      string gameId = "";
      do
      {
        Guid g = Guid.NewGuid();
        gameId = g.ToString().Substring(0, 8);
      } while (gameSessions.ContainsKey(gameId));
      gameSessions.Add(gameId, new GameSession());

      await Clients.Caller.SendAsync("NewGameIdReceived", gameId);
    }

    public async Task InitializeBoardAndConnection(string gameId)
    {
      if (await HandleNoGameFound(gameId))
        return;
      await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
      await SendCurrentStateAsync(gameId);
    }

    public async Task PlaceStone(string gameId, int x, int y)
    {
      if (await HandleNoGameFound(gameId))
        return;
      gameSessions[gameId].PlaceStone(x, y);
      await SendCurrentStateAsync(gameId);
    }

    public async Task UndoStone(string gameId)
    {
      if (await HandleNoGameFound(gameId))
        return;
      gameSessions[gameId].UndoStone();
      await SendCurrentStateAsync(gameId);
    }

    private async Task SendCurrentStateAsync(string gameId)
    {
      Dictionary<string, string> state = new Dictionary<string, string>();
      state.Add("currentTurn", gameSessions[gameId].CurrentTurn().ToString());
      state.Add("currentTurnRemaining", gameSessions[gameId].CurrentTurnRemaining().ToString());
      state.Add("boardString", gameSessions[gameId].PrintCurrentBoard());
      await Clients.Group(gameId).SendAsync("CurrentBoard", state);
    }

    private async Task<Boolean> HandleNoGameFound(string gameId)
    {
      if (gameSessions.ContainsKey(gameId))
        return false;
      else
      {
        await Clients.Caller.SendAsync("ServerMessage", "No game found");
        return true;
      }
    }
  }
}