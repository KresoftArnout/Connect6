using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace BlazorSignalRApp.Server.Hubs
{
  public class Connect6Hub : Hub
  {
    public Connect6Hub() : base()
    {
      if (!initialized)
      {
        string totalSessionsFileName = Path.Combine(Directory.GetParent(".").FullName, "totalSessions.dat");
        if (File.Exists(totalSessionsFileName))
        {
          StreamReader sr = new StreamReader(totalSessionsFileName);
          totalSessions = UInt64.Parse(sr.ReadLine() as string);
          totalConnections = UInt64.Parse(sr.ReadLine() as string);
          totalMultiplayerGame = UInt64.Parse(sr.ReadLine() as string);
          sr.Close();
        }
        string gameSessionsFileName = Path.Combine(Directory.GetParent(".").FullName, "gameSessions.dat");
        if (File.Exists(gameSessionsFileName))
        {
          string dataRead = File.ReadAllText(gameSessionsFileName);
          gameSessions = JsonSerializer.Deserialize<Dictionary<string, GameSession>>(dataRead);
          foreach (var gameId in gameSessions.Keys)
            connections.Add(gameId, new HashSet<string>());
        }

        initialized = true;
      }
    }

    static bool initialized = false;

    static UInt64 totalSessions = 0;
    static UInt64 totalConnections = 0;
    static UInt64 totalMultiplayerGame = 0;
    static Dictionary<string, GameSession> gameSessions = new Dictionary<string, GameSession>();
    static Dictionary<string, HashSet<string>> connections = new Dictionary<string, HashSet<string>>();
    static Dictionary<string, string> reverseMapping = new Dictionary<string, string>();
    static Queue<string> serverLogsQueue = new Queue<string>();

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
          await Report(gameIdKey, "Session destroyed");
        }
        catch { }
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
      await Report(gameId, "New game made");
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
      await Report(gameId, "New user connected to game");
    }

    public async Task RegisterAdminConnection() => await Groups.AddToGroupAsync(Context.ConnectionId, "AdminAdminAdmin");

    public async Task PlaceStone(string gameId, int x, int y)
    {
      if (await HandleNoGameFound(gameId))
        return;
      gameSessions[gameId].PlaceStone(x, y);
      await SendCurrentStateAsync(gameId);
      await Report(gameId, $"User placed stone ({x.ToString("D2")}, {y.ToString("D2")})");
    }

    public async Task UndoStone(string gameId)
    {
      if (await HandleNoGameFound(gameId))
        return;
      gameSessions[gameId].UndoStone();
      await SendCurrentStateAsync(gameId);
      await Report(gameId, "User undid");
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
          await Report(gameId, "Board reset");
        }
      }
      catch { }
    }

    private async Task SendCurrentStateAsync(string gameId)
    {
      Dictionary<string, string> state = new Dictionary<string, string>();
      state.Add("currentTurn", gameSessions[gameId].CurrentTurn().ToString());
      state.Add("currentTurnRemaining", gameSessions[gameId].CurrentTurnRemaining().ToString());
      state.Add("boardString", gameSessions[gameId].PrintCurrentBoard());

      if (gameSessions[gameId].PlaysX.Count > 0)
      {
        var lastPlayX = gameSessions[gameId].PlaysX.Last();
        var lastPlayY = gameSessions[gameId].PlaysY.Last();
        state.Add("lastPlayX", lastPlayX.ToString());
        state.Add("lastPlayY", lastPlayY.ToString());
        if (gameSessions[gameId].PlaysX.Count > 1)
        {
          char lastTurn = gameSessions[gameId].CurrentTurn(gameSessions[gameId].PlaysX.Count - 1);
          char lastLastTurn = gameSessions[gameId].CurrentTurn(gameSessions[gameId].PlaysX.Count - 2);
          if (lastTurn == lastLastTurn)
          {
            var lastLastPlayX = gameSessions[gameId].PlaysX[^2];
            var lastLastPlayY = gameSessions[gameId].PlaysY[^2];
            state.Add("lastLastPlayX", lastLastPlayX.ToString());
            state.Add("lastLastPlayY", lastLastPlayY.ToString());
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
          await Report(gameId, "User disconnected");
        }
        catch { }
      }
    }

    public void ParkAndExit()
    {
      File.WriteAllText(Path.Combine(Directory.GetParent(".").FullName, "totalSessions.dat"), $"{totalSessions}\n{totalConnections}\n{totalMultiplayerGame}");
      var jsonString = JsonSerializer.Serialize(gameSessions);
      File.WriteAllText(Path.Combine(Directory.GetParent(".").FullName, "gameSessions.dat"), jsonString);
      Environment.Exit(0);
    }

    private async Task Report(string gameId, string message)
    {
      string reportMessage = $"{DateTime.Now} [{totalSessions} TS, {totalConnections} TU, {totalMultiplayerGame} MUS, {gameSessions.Keys.Count} CS, {reverseMapping.Count} CU] {gameId} ({connections[gameId].Count}) : {message.PadRight(30)}{Context.ConnectionId}";
      while (serverLogsQueue.Count > 30)
        serverLogsQueue.Dequeue();
      serverLogsQueue.Enqueue(reportMessage);
      await Clients.Group("AdminAdminAdmin").SendAsync("ServerLogReceived", serverLogsQueue.ToList());
    }
  }
}