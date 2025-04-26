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
        private bool _isAuthenticated = false;
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
                
                _webSocket.OnOpen += (sender, e) => {
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
                
                _webSocket.OnMessage += (sender, e) => {
                    HandleMessage(e.Data);
                };
                
                _webSocket.OnError += (sender, e) => {
                    Log.Error($"[Takaro] WebSocket error: {e.Message}");
                };
                
                _webSocket.OnClose += (sender, e) => {
                    _isConnected = false;
                    _isAuthenticated = false;
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
                Log.Out($"[Takaro] Received message: {message}");
                
                // Here you would handle incoming messages from the server
                // For example, parsing commands to execute in the game
                
                // For now, we'll just acknowledge authentication
                if (message.Contains("authenticated"))
                {
                    _isAuthenticated = true;
                    Log.Out("[Takaro] WebSocket authenticated successfully");
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
            
            _heartbeatTimer = new Timer(state => {
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
            
            _reconnectTimer = new Timer(state => {
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
                    _isAuthenticated = false;
                }
            }
        }

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
    }
}