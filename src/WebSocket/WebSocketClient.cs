using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Takaro.Config;
using WebSocketSharp;
using UnityEngine;
using System.Threading.Tasks;

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

    public class TakaroGiveItemArgs
    {
        public string GameId { get; set; }
        public string Item { get; set; }
        public int Amount { get; set; }
        public string Quality { get; set; }
    }

    public class TakaroExecuteCommandArgs 
    {
        public string Command { get; set; }
    }

    public class TakaroSendMessageArgs 
    {
        public string Message { get; set; }
        public TakaroSendMessageRecipientArgs Recipient { get; set; }
    }

    public class TakaroSendMessageRecipientArgs
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

        #region HandleMessage
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

                try
                {
                                    // Handle different message types
                switch (action)
                {
                    case "testReachability":
                        HandleTestReachability(requestId);
                        break;
                    case "getPlayers":
                        HandleGetPlayers(requestId);
                        break;
                    case "getPlayer":
                        var playerArgs = WebSocketArgs<TakaroPlayerReferenceArgs>.Parse(args);
                        if (playerArgs == null || string.IsNullOrEmpty(playerArgs.GameId))
                        {
                            SendErrorResponse(requestId, "Invalid or missing gameId parameter");
                            return;
                        }
                        HandleGetPlayer(requestId, playerArgs.GameId);
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
                    case "listBans":
                        HandleListBans(requestId);
                        break;
                    case "giveItem":
                        var giveItemArgs = WebSocketArgs<TakaroGiveItemArgs>.Parse(args);
                        HandleGiveItem(requestId, giveItemArgs);
                        break;
                    case "executeConsoleCommand":
                        var executeCommandArgs = WebSocketArgs<TakaroExecuteCommandArgs>.Parse(args);
                        HandleExecuteCommand(requestId, executeCommandArgs);
                        break;
                    case "sendMessage":
                        var sendMessageArgs = WebSocketArgs<TakaroSendMessageArgs>.Parse(args);
                        HandleSendMessage(requestId, sendMessageArgs);
                        break;
                    default:
                        Log.Warning($"[Takaro] Unknown message type: {action}");
                        SendErrorResponse(requestId, $"Unknown message type: {action}");
                        break;
                }
                }
                catch (System.Exception)
                {
                    SendErrorResponse(requestId, "Error processing request");
                    throw;
                }


            }
            catch (Exception ex)
            {
                Log.Error($"[Takaro] Error handling WebSocket message: {ex.Message}");
                Log.Exception(ex);
            }
        }
        #endregion

        #region Helpers
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

        #endregion
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

        private void HandleGetPlayer(string requestId, string gameId)
        {
            ClientInfo cInfo = Shared.GetClientInfoFromGameId(gameId);
            if (cInfo == null)
            {
                SendErrorResponse(requestId, "Player not found");
                return;
            }

            TakaroPlayer takaroPlayer = Shared.TransformClientInfoToTakaroPlayer(cInfo);
            if (takaroPlayer == null)
            {
                SendErrorResponse(requestId, "Player not found");
                return;
            }

            WebSocketMessage message = WebSocketMessage.CreateResponse(requestId, takaroPlayer);
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
            for (int i = 0; i < ItemClass.itemNames.Count; i++) {
				string itemName = ItemClass.itemNames [i];
                ItemClass item = ItemClass.nameToItem[itemName];
                if (item == null) {
                    continue;
                }
                allItems.Add(Shared.TransformItemToTakaroItem(item));

			}
            WebSocketMessage message = WebSocketMessage.Create(
                WebSocketMessage.MessageTypes.Response,
                allItems.ToArray(),
                requestId
            );
            SendMessage(message);
        }

        private void HandleListBans(string requestId)
        {
            // TODO: This currently only works for EOS IDs
            // It should be smarter and fetch data from persistent player data somehow
            // But I dunno how to do that, and I just want this to reply with _some_ data for now :) 
            List<TakaroBan> bans = new List<TakaroBan>();
            PersistentPlayerList playerList = GameManager.Instance.GetPersistentPlayerList();
            foreach (var ban in GameManager.Instance.adminTools.Blacklist.GetBanned())
            {
                if(!ban.UserIdentifier.CombinedString.StartsWith("EOS_")) continue;
                PersistentPlayerData playerData = playerList.GetPlayerData(ban.UserIdentifier);
                if (playerData == null) continue;


                TakaroPlayer takaroPlayer = new TakaroPlayer
                {
                    GameId = ban.UserIdentifier.CombinedString.Replace("EOS_", ""),
                    Name = playerData.PlayerName.playerName.Text,
                    EpicOnlineServicesId = ban.UserIdentifier.CombinedString.Replace("EOS_", ""),
                };


                TakaroBan takaroBan = new TakaroBan
                {
                    Player = takaroPlayer,
                    Reason = ban.BanReason,
                    ExpiresAt = ban.BannedUntil.ToString("o")
                };
                bans.Add(takaroBan);
            }

            WebSocketMessage message = WebSocketMessage.Create(
                WebSocketMessage.MessageTypes.Response,
                bans.ToArray(),
                requestId
            );
            SendMessage(message);
        }

        private void HandleGiveItem(string requestId, TakaroGiveItemArgs args)
        {
            if (args == null || args.GameId == null || string.IsNullOrEmpty(args.Item))
            {
                SendErrorResponse(requestId, "Invalid or missing parameters");
                return;
            }
            
            ClientInfo cInfo = Shared.GetClientInfoFromGameId(args.GameId);
            if (cInfo == null)
            {
                SendErrorResponse(requestId, "Player not found");
                return;
            }
            
            ItemValue itemValue = ItemClass.GetItem(args.Item);
            if (itemValue == null || itemValue.type == ItemValue.None.type)
            {
                SendErrorResponse(requestId, "Item not found");
                return;
            }
            
            if(!GameManager.Instance.World.Players.dict.TryGetValue(cInfo.entityId, out EntityPlayer player))
            {
                SendErrorResponse(requestId, "Player entity not found");
                return;
            }
            
            if(!player.IsSpawned())
            {
                SendErrorResponse(requestId, "Player is not spawned");
                return;
            }
            
            if(player.IsDead())
            {
                SendErrorResponse(requestId, "Player is dead");
                return;
            }
            
            if (args.Amount <= 0)
            {
                SendErrorResponse(requestId, "Invalid item amount");
                return;
            }

            // Parse quality parameter or use default max quality
            ushort quality = Constants.cItemMaxQuality;
            if (!string.IsNullOrEmpty(args.Quality))
            {
                if (ushort.TryParse(args.Quality, out ushort parsedQuality) && 
                    parsedQuality >= 0 && 
                    parsedQuality <= Constants.cItemMaxQuality)
                {
                    quality = parsedQuality;
                }
                else
                {
                    SendErrorResponse(requestId, "Invalid quality value");
                    return;
                }
            }
            
            // Create a new ItemValue with appropriate quality
            ItemValue iv = new ItemValue(itemValue.type, true);
            
            // Handle quality for items with sub-items or that have quality
            if (ItemClass.list[iv.type].HasSubItems)
            {
                for (int i = 0; i < iv.Modifications.Length; i++)
                {
                    ItemValue tmp = iv.Modifications[i];
                    tmp.Quality = quality;
                    iv.Modifications[i] = tmp;
                }
            }
            else if (ItemClass.list[iv.type].HasQuality)
            {
                iv.Quality = quality;
            }
            
            // Create the item stack with the specified amount
            ItemStack itemStack = new ItemStack(iv, args.Amount);
            World world = GameManager.Instance.World;
            EntityItem entityItem = (EntityItem)EntityFactory.CreateEntity(new EntityCreationData
            {
                entityClass = EntityClass.FromString("item"),
                id = EntityFactory.nextEntityID++,
                itemStack = itemStack,
                pos = world.Players.dict[cInfo.entityId].position,
                rot = new Vector3(20f, 0f, 20f),
                lifetime = 60f,
                belongsPlayerId = cInfo.entityId
            });
            world.SpawnEntityInWorld(entityItem);
            cInfo.SendPackage(NetPackageManager.GetPackage<NetPackageEntityCollect>().Setup(entityItem.entityId, cInfo.entityId));
            world.RemoveEntity(entityItem.entityId, EnumRemoveEntityReason.Despawned);
            WebSocketMessage message = WebSocketMessage.Create(
                WebSocketMessage.MessageTypes.Response,
                new Dictionary<string, object> {},
                requestId
            );
            SendMessage(message);
        }

        private void HandleSendMessage(string requestId, TakaroSendMessageArgs args)
        {
            if (args == null || string.IsNullOrEmpty(args.Message))
            {
                SendErrorResponse(requestId, "Invalid or missing parameters");
                return;
            }

            // If a GameId is provided, send the message to that player as a whisper
            if(args.Recipient != null && args.Recipient.GameId != null) {
                ClientInfo cInfo = Shared.GetClientInfoFromGameId(args.Recipient.GameId);
                if (cInfo == null)
                {
                    SendErrorResponse(requestId, "Player not found");
                    return;
                }

                cInfo.SendPackage (NetPackageManager.GetPackage<NetPackageChat> ().Setup (EChatType.Whisper, -1,args.Message, null, EMessageSender.Server));
            // Otherwise, send a global message
            } else {
                GameManager.Instance.ChatMessageServer(null, EChatType.Global, -1, args.Message, null, EMessageSender.Server);
            }

            WebSocketMessage message = WebSocketMessage.Create(
                WebSocketMessage.MessageTypes.Response,
                new Dictionary<string, object> {},
                requestId
            );
            SendMessage(message);
        }

        private async Task HandleExecuteCommand(string requestId, TakaroExecuteCommandArgs args)
        {
            if (args == null || string.IsNullOrEmpty(args.Command))
            {
                SendErrorResponse(requestId, "Invalid or missing command");
                return;
            }
            var tcs = new TaskCompletionSource<string>();
            var cr = new CommandResult(args.Command, tcs);
            SdtdConsole.Instance.ExecuteAsync (args.Command, cr);
            string result = await tcs.Task;

            WebSocketMessage message = WebSocketMessage.Create(
                WebSocketMessage.MessageTypes.Response,
                new Dictionary<string, object> { 
                    { "rawResult", result },
                    {"success", true}
                     },
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