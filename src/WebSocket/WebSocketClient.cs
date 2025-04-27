using System;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using WebSocketSharp;
using System.Reflection;
using Takaro.Config;
using System.IO;
using Newtonsoft.Json;

namespace Takaro.WebSocket
{
    public class WebSocketClient
    {
        private static WebSocketClient _instance;
        private static readonly object _lock = new object();

        private WebSocketSharp.WebSocket _webSocket;
        private Timer _heartbeatTimer;
        private Timer _reconnectTimer;
        private bool _isConnected = false;
        private bool _shuttingDown = false;
        private int _reconnectAttempts = 0;
        private const int MAX_RECONNECT_ATTEMPTS = 5;

        public static WebSocketClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new WebSocketClient();
                        }
                    }
                }
                return _instance;
            }
        }

        private WebSocketClient()
        {
            // Private constructor for singleton
        }

        public void Initialize()
        {
            try
            {
                var config = ConfigManager.Instance;
                if (!config.WebSocketEnabled)
                {
                    Log.Out("[Takaro] WebSocket client is disabled in config. Skipping initialization.");
                    return;
                }

                Log.Out($"[Takaro] Initializing WebSocket client to {config.WebSocketUrl}");

                ConnectToServer();
            }
            catch (Exception ex)
            {
                Log.Error($"[Takaro] Error initializing WebSocket client: {ex.Message}");
                Log.Exception(ex);
            }
        }

        public void Shutdown()
        {
            _shuttingDown = true;
            StopTimers();
            CloseConnection();
        }

        private void ConnectToServer()
        {
            try
            {
                var config = ConfigManager.Instance;
                if (string.IsNullOrEmpty(config.WebSocketUrl))
                {
                    Log.Error("[Takaro] WebSocket URL is not set in config.");
                    return;
                }

                _webSocket = new WebSocketSharp.WebSocket(config.WebSocketUrl);

                _webSocket.OnOpen += (sender, e) =>
                {
                    _isConnected = true;
                    _reconnectAttempts = 0;
                    Log.Out("[Takaro] WebSocket connection established");

                    // Send registration message
                    if (string.IsNullOrEmpty(config.RegistrationToken) || string.IsNullOrEmpty(config.IdentityToken))
                    {
                        Log.Error("[Takaro] Registration token or identity token is not set in config.");
                        return;
                    }

                    SendMessage(WebSocketMessage.CreateIdentify(config.RegistrationToken, config.IdentityToken));
                    // Start heartbeat
                    StartHeartbeat();
                };

                _webSocket.OnMessage += (sender, e) =>
                {
                    HandleMessage(e.Data);
                };

                _webSocket.OnError += (sender, e) =>
                {
                    Log.Error($"[Takaro] WebSocket error: {e.Message}");
                };

                _webSocket.OnClose += (sender, e) =>
                {
                    _isConnected = false;
                    Log.Out($"[Takaro] WebSocket connection closed: {e.Code} - {e.Reason}");

                    StopTimers();

                    if (!_shuttingDown)
                    {
                        ScheduleReconnect();
                    }
                };

                _webSocket.Connect();
            }
            catch (Exception ex)
            {
                Log.Error($"[Takaro] Error connecting to WebSocket server: {ex.Message}");
                Log.Exception(ex);
                ScheduleReconnect();
            }
        }

        private void HandleMessage(string message)
        {
            try
            {
                Log.Out($"[Takaro] Received WebSocket message: {message}");
                var webSocketMessage = JsonConvert.DeserializeObject<WebSocketMessage>(message);

                if (webSocketMessage == null || webSocketMessage.Payload == null)
                {
                    // No data in the message, so nothing to do
                    return;
                }

                string requestId = webSocketMessage.RequestId;

                if (string.IsNullOrEmpty(requestId))
                {
                    Log.Warning("[Takaro] Received message without requestId");
                    return;
                }

                // Handle the Payload property which could be a dictionary or array
                Dictionary<string, object> payloadDict = webSocketMessage.Payload as Dictionary<string, object>;

                if (payloadDict == null)
                {
                    if (webSocketMessage.Payload is Newtonsoft.Json.Linq.JObject jObject)
                    {
                        payloadDict = jObject.ToObject<Dictionary<string, object>>();
                    }
                    else
                    {
                        Log.Warning("[Takaro] Received message with payload that is not a dictionary");
                        return;
                    }
                }

                string action = null;
                if (payloadDict.ContainsKey("action"))
                {
                    action = payloadDict["action"].ToString();
                }
                else
                {
                    // No action in the message, so nothing to do
                    return;
                }

                // Handle different message types
                switch (action)
                {
                    case "testReachability":
                        HandleTestReachability(requestId);
                        break;
                    case "getPlayers":
                        HandleGetPlayers(requestId);
                        break;
                    default:
                        Log.Warning($"[Takaro] Unknown message type: {action}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Takaro] Error handling WebSocket message: {ex.Message}");
                Log.Exception(ex);
            }
        }

        public void SendMessage(WebSocketMessage message)
        {
            try
            {
                if (_webSocket == null || !_isConnected)
                {
                    Log.Warning("[Takaro] Cannot send message - WebSocket not connected");
                    return;
                }

                string json = SerializeToJson(message);
                Log.Out($"[Takaro] Sending WebSocket message: {json}");
                _webSocket.Send(json);
            }
            catch (Exception ex)
            {
                Log.Error($"[Takaro] Error sending WebSocket message: {ex.Message}");
                Log.Exception(ex);
            }
        }

        private string SerializeToJson(WebSocketMessage message)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(message);
        }

        private void StartHeartbeat()
        {
            StopHeartbeatTimer();

            _heartbeatTimer = new Timer(state =>
            {
                if (_isConnected)
                {
                    SendMessage(WebSocketMessage.CreateHeartbeat());
                }
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        private void StopHeartbeatTimer()
        {
            if (_heartbeatTimer != null)
            {
                _heartbeatTimer.Dispose();
                _heartbeatTimer = null;
            }
        }

        private void ScheduleReconnect()
        {
            if (_reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
            {
                Log.Error($"[Takaro] Maximum reconnection attempts ({MAX_RECONNECT_ATTEMPTS}) reached. Giving up.");
                return;
            }

            _reconnectAttempts++;
            var interval = TimeSpan.FromSeconds(ConfigManager.Instance.ReconnectIntervalSeconds);
            Log.Out($"[Takaro] Scheduling reconnect attempt {_reconnectAttempts} in {interval.TotalSeconds} seconds");

            _reconnectTimer = new Timer(state =>
            {
                ConnectToServer();
            }, null, interval, Timeout.InfiniteTimeSpan);
        }

        private void StopTimers()
        {
            StopHeartbeatTimer();

            if (_reconnectTimer != null)
            {
                _reconnectTimer.Dispose();
                _reconnectTimer = null;
            }
        }

        private void CloseConnection()
        {
            if (_webSocket != null && _isConnected)
            {
                try
                {
                    _webSocket.Close(CloseStatusCode.Normal, "Application shutting down");
                }
                catch (Exception ex)
                {
                    Log.Error($"[Takaro] Error closing WebSocket connection: {ex.Message}");
                }
                finally
                {
                    _webSocket = null;
                    _isConnected = false;
                }
            }
        }

        #region Action Handlers

        private void HandleTestReachability(string requestId)
        {
            SendMessage(WebSocketMessage.CreateTestReachabilityResponse(requestId));
        }

        private void HandleGetPlayers(string requestId)
        {
            var players = new List<TakaroPlayer>();
            foreach (var player in GameManager.Instance.World.Players.list)
            {
                int entityId = player.entityId;
                ClientInfo cInfo = ConnectionManager.Instance.Clients.ForEntityId(entityId);

                TakaroPlayer takaroPlayer = Shared.TransformClientInfoToTakaroPlayer(cInfo);
                if (takaroPlayer != null)
                {
                    players.Add(takaroPlayer);
                }
            }

            SendMessage(WebSocketMessage.CreatePlayersResponse(requestId, players));
        }

        private void HandleGetPlayerLocation(string requestId, string takaroGameId)
        {
            var players = new List<Dictionary<string, object>>();

        }

        #endregion

        #region Event Handlers        

        // Public methods to send game events
        public void SendPlayerConnected(ClientInfo cInfo)
        {
            if (cInfo == null) return;

            SendMessage(WebSocketMessage.CreatePlayerConnected(
                cInfo.playerName,
                cInfo.entityId.ToString(),
                cInfo.PlatformId.ToString()
            ));
        }

        public void SendPlayerDisconnected(ClientInfo cInfo)
        {
            if (cInfo == null) return;

            SendMessage(WebSocketMessage.CreatePlayerDisconnected(
                cInfo.playerName,
                cInfo.entityId.ToString(),
                cInfo.PlatformId.ToString()
            ));
        }

        public void SendChatMessage(ClientInfo cInfo, string message)
        {
            if (cInfo == null) return;

            SendMessage(WebSocketMessage.CreateChatMessage(
                cInfo.playerName,
                cInfo.entityId.ToString(),
                cInfo.PlatformId.ToString(),
                message
            ));
        }

        public void SendEntityKilled(ClientInfo killerInfo, string entityName, string entityType)
        {
            if (killerInfo == null) return;

            SendMessage(WebSocketMessage.CreateEntityKilled(
                killerInfo.playerName,
                killerInfo.entityId.ToString(),
                killerInfo.PlatformId.ToString(),
                entityName,
                entityType
            ));
        }

        #endregion
    }
}
