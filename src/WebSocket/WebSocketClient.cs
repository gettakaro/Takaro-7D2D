using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Takaro.Config;
using WebSocketSharp;

namespace Takaro.WebSocket
{
    public class WebSocketArgs<T>
    {
        public static T Parse(object argsObject)
        {
            try
            {
                if (argsObject == null)
                    return default;

                // Handle case where args is already a string that needs deserialization
                if (argsObject is string argsString)
                {
                    return JsonConvert.DeserializeObject<T>(argsString);
                }

                // Handle case where args is already a JObject or Dictionary
                if (argsObject is Newtonsoft.Json.Linq.JObject jObject)
                {
                    return jObject.ToObject<T>();
                }

                if (argsObject is Dictionary<string, object> dict)
                {
                    string json = JsonConvert.SerializeObject(dict);
                    return JsonConvert.DeserializeObject<T>(json);
                }

                // As a last resort, try direct conversion
                return (T)Convert.ChangeType(argsObject, typeof(T));
            }
            catch (Exception ex)
            {
                Log.Error($"[Takaro] Error parsing WebSocket args: {ex.Message}");
                return default;
            }
        }
    }

    public class TakaroPlayerReferenceArgs
    {
        public string GameId { get; set; }
    }

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
                    Log.Out(
                        "[Takaro] WebSocket client is disabled in config. Skipping initialization."
                    );
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
                    if (
                        string.IsNullOrEmpty(config.RegistrationToken)
                        || string.IsNullOrEmpty(config.IdentityToken)
                    )
                    {
                        Log.Error(
                            "[Takaro] Registration token or identity token is not set in config."
                        );
                        return;
                    }

                    SendMessage(
                        WebSocketMessage.CreateIdentify(
                            config.RegistrationToken,
                            config.IdentityToken
                        )
                    );
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
                Dictionary<string, object> payloadDict =
                    webSocketMessage.Payload as Dictionary<string, object>;

                if (payloadDict == null)
                {
                    if (webSocketMessage.Payload is Newtonsoft.Json.Linq.JObject jObject)
                    {
                        payloadDict = jObject.ToObject<Dictionary<string, object>>();
                    }
                    else
                    {
                        Log.Warning(
                            "[Takaro] Received message with payload that is not a dictionary"
                        );
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

                // Extract args if present
                object args = null;
                if (payloadDict.ContainsKey("args"))
                {
                    args = payloadDict["args"];
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
                    case "getPlayerLocation":
                        var locationArgs = WebSocketArgs<TakaroPlayerReferenceArgs>.Parse(args);
                        if (locationArgs == null || string.IsNullOrEmpty(locationArgs.GameId))
                        {
                            SendErrorResponse(requestId, "Invalid or missing gameId parameter");
                            return;
                        }
                        HandleGetPlayerLocation(requestId, locationArgs.GameId);
                        break;
                    case "getPlayerInventory":
                        var inventoryArgs = WebSocketArgs<TakaroPlayerReferenceArgs>.Parse(args);
                        if (inventoryArgs == null || string.IsNullOrEmpty(inventoryArgs.GameId))
                        {
                            SendErrorResponse(requestId, "Invalid or missing gameId parameter");
                            return;
                        }
                        HandleGetPlayerInventory(requestId, inventoryArgs.GameId);
                        break;
                    case "listItems":
                        HandleListItems(requestId);
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

        private void SendErrorResponse(string requestId, string errorMessage)
        {
            WebSocketMessage message = WebSocketMessage.CreateErrorResponse(
                requestId,
                errorMessage
            );
            SendMessage(message);
        }

        private string SerializeToJson(WebSocketMessage message)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(message);
        }

        private void StartHeartbeat()
        {
            StopHeartbeatTimer();

            _heartbeatTimer = new Timer(
                state =>
                {
                    if (_isConnected)
                    {
                        SendMessage(WebSocketMessage.CreateHeartbeat());
                    }
                },
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30)
            );
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
                Log.Error(
                    $"[Takaro] Maximum reconnection attempts ({MAX_RECONNECT_ATTEMPTS}) reached. Giving up."
                );
                return;
            }

            _reconnectAttempts++;
            var interval = TimeSpan.FromSeconds(ConfigManager.Instance.ReconnectIntervalSeconds);
            Log.Out(
                $"[Takaro] Scheduling reconnect attempt {_reconnectAttempts} in {interval.TotalSeconds} seconds"
            );

            _reconnectTimer = new Timer(
                state =>
                {
                    ConnectToServer();
                },
                null,
                interval,
                Timeout.InfiniteTimeSpan
            );
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
            WebSocketMessage message = WebSocketMessage.Create(
                WebSocketMessage.MessageTypes.Response,
                new Dictionary<string, object> { { "connectable", true } },
                requestId
            );
            SendMessage(message);
        }

        private void HandleGetPlayers(string requestId)
        {
            List<TakaroPlayer> players = new List<TakaroPlayer>();
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

            WebSocketMessage message = WebSocketMessage.Create(
                WebSocketMessage.MessageTypes.Response,
                players.ToArray(),
                requestId
            );
            SendMessage(message);
        }

        private void HandleGetPlayerLocation(string requestId, string gameId)
        {
            ClientInfo cInfo = Shared.GetClientInfoFromGameId(gameId);
            if (cInfo == null)
            {
                SendErrorResponse(requestId, "Player not found");
                return;
            }

            EntityPlayer player = GameManager.Instance.World.Players.dict[cInfo.entityId];
            if (player == null)
            {
                SendErrorResponse(requestId, "Player entity not found");
                return;
            }

            Vector3i pos = new Vector3i(player.GetPosition());
            WebSocketMessage message = WebSocketMessage.Create(
                WebSocketMessage.MessageTypes.Response,
                new Dictionary<string, object>
                {
                    { "x", pos.x },
                    { "y", pos.y },
                    { "z", pos.z }
                },
                requestId
            );
            SendMessage(message);
        }

        private void HandleGetPlayerInventory(string requestId, string gameId)
        {
            ClientInfo cInfo = Shared.GetClientInfoFromGameId(gameId);
            if (cInfo == null)
            {
                SendErrorResponse(requestId, "Player not found");
                return;
            }

            List<TakaroItem> items = new List<TakaroItem>();

            ProcessItemStacks(cInfo.latestPlayerData.inventory, items);
            ProcessItemStacks(cInfo.latestPlayerData.bag, items);
            ProcessEquippedItems(cInfo.latestPlayerData.equipment.GetItems(), items);

            WebSocketMessage message = WebSocketMessage.Create(
                WebSocketMessage.MessageTypes.Response,
                items.ToArray(),
                requestId
            );
            SendMessage(message);
        }

        private void ProcessItemStacks(ItemStack[] itemStacks, List<TakaroItem> itemsList)
        {
            if (itemStacks == null)
                return;

            foreach (var item in itemStacks)
            {
                ItemValue itemValue = item.itemValue;

                if (itemValue == null || itemValue.Equals(ItemValue.None))
                {
                    continue;
                }

                ItemClass itemClass = itemValue.ItemClass;
                TakaroItem takaroItem = Shared.TransformItemToTakaroItem(itemClass);
                takaroItem.Amount = item.count;
                takaroItem.Quality = itemValue.Quality.ToString();
                itemsList.Add(takaroItem);
            }
        }

        private void ProcessEquippedItems(ItemValue[] equippedItems, List<TakaroItem> itemsList)
        {
            if (equippedItems == null)
                return;

            foreach (var itemValue in equippedItems)
            {
                if (itemValue == null || itemValue.Equals(ItemValue.None))
                {
                    continue;
                }

                ItemClass itemClass = itemValue.ItemClass;
                TakaroItem takaroItem = Shared.TransformItemToTakaroItem(itemClass);
                takaroItem.Amount = 1;
                takaroItem.Quality = itemValue.Quality.ToString();
                itemsList.Add(takaroItem);
            }
        }

        private void HandleListItems(string requestId)
        {
            List<TakaroItem> allItems = new List<TakaroItem>();
            for (int i = 0; i < ItemClass.list.Length; i++) {
				ItemClass item = ItemClass.list [i];
                allItems.Add(Shared.TransformItemToTakaroItem(item));

			}
            WebSocketMessage message = WebSocketMessage.Create(
                WebSocketMessage.MessageTypes.Response,
                allItems.ToArray(),
                requestId
            );
            SendMessage(message);
        }

        #endregion

        #region Event Handlers
        public void SendGameEvent(string type, object data)
        {
            if (data == null)
                return;

            WebSocketMessage message = WebSocketMessage.Create(
                WebSocketMessage.MessageTypes.GameEvent,
                new Dictionary<string, object> { 
                    { "type", type },
                    { "data", data }
                 }
            );

            SendMessage(message);
        }

        // Public methods to send game events
        public void SendPlayerConnected(ClientInfo cInfo)
        {
            if (cInfo == null) return;

            SendGameEvent(
                "player-connected",
                new Dictionary<string, object>
                {
                    { "player", Shared.TransformClientInfoToTakaroPlayer(cInfo) },
                }
            );
        }

        public void SendPlayerDisconnected(ClientInfo cInfo)
        {
            if (cInfo == null)
                return;

        SendGameEvent(
                "player-disconnected",
                new Dictionary<string, object>
                {
                    { "player", Shared.TransformClientInfoToTakaroPlayer(cInfo) },
                }
            );
        }

        public void SendChatMessage(ClientInfo cInfo, EChatType type, int _senderId, string msg, string mainName, List<int> recipientEntityIds)
        {
            if (cInfo == null) return;

            string channel = "unknown";

            switch (type)
            {
                case EChatType.Global:
                    channel = "global";
                    break;
                case EChatType.Whisper:
                    channel = "whisper";
                    break;
                case EChatType.Friends:
                    channel = "friends";
                    break;
                case EChatType.Party:
                    channel = "team";
                    break;                    
                default:
                    channel = "unknown";
                    break;
            }

            SendGameEvent(
                "chat-message",
                new Dictionary<string, object>
                {
                    { "player", Shared.TransformClientInfoToTakaroPlayer(cInfo) },
                    { "msg", msg },
                    { "channel", channel }
                }
            );
        }

        public void SendEntityKilled(ClientInfo killerInfo, string entityName, string entityType)
        {
            if (killerInfo == null)
                return;

            SendGameEvent(
                "entity-killed",
                new Dictionary<string, object>
                {
                    { "name", killerInfo.playerName },
                    { "entityId", killerInfo.entityId.ToString() },
                    { "platformId", killerInfo.PlatformId.ToString() },
                    { "entityName", entityName },
                    { "entityType", entityType }
                }
            );
        }

        #endregion
    }
}