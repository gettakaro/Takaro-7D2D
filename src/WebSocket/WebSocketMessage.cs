using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Takaro7D2D.WebSocket
{
    [Serializable]
    public class WebSocketMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("payload")]
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
        
        public WebSocketMessage() { }
        
        public WebSocketMessage(string type)
        {
            Type = type;
        }
        
        public WebSocketMessage(string type, Dictionary<string, object> data)
        {
            Type = type;
            Data = data ?? new Dictionary<string, object>();
        }
        
        public static WebSocketMessage CreateHeartbeat()
        {
            return new WebSocketMessage("ping", new Dictionary<string, object>
            {
                { "timestamp", DateTime.UtcNow.ToString("o") }
            });
        }
        
        public static WebSocketMessage CreateIdentify(string registrationToken, string identityToken)
        {
            return new WebSocketMessage("identify", new Dictionary<string, object>
            {
                { "registrationToken", registrationToken },
                { "identityToken", identityToken }
            });
        }
        
        public static WebSocketMessage CreatePlayerConnected(string playerName, string playerId, string steamId)
        {
            return new WebSocketMessage("player_connected", new Dictionary<string, object>
            {
                { "playerName", playerName },
                { "playerId", playerId },
                { "steamId", steamId }
            });
        }
        
        public static WebSocketMessage CreatePlayerDisconnected(string playerName, string playerId, string steamId)
        {
            return new WebSocketMessage("player_disconnected", new Dictionary<string, object>
            {
                { "playerName", playerName },
                { "playerId", playerId },
                { "steamId", steamId }
            });
        }
        
        public static WebSocketMessage CreateChatMessage(string playerName, string playerId, string steamId, string message)
        {
            return new WebSocketMessage("chat_message", new Dictionary<string, object>
            {
                { "playerName", playerName },
                { "playerId", playerId },
                { "steamId", steamId },
                { "message", message }
            });
        }
        
        public static WebSocketMessage CreateEntityKilled(string killerName, string killerId, string killerSteamId,
            string entityName, string entityType)
        {
            return new WebSocketMessage("entity_killed", new Dictionary<string, object>
            {
                { "killerName", killerName },
                { "killerId", killerId },
                { "killerSteamId", killerSteamId },
                { "entityName", entityName },
                { "entityType", entityType }
            });
        }
    }
}