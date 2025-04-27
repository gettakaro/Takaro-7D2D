using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Takaro.WebSocket
{
    [Serializable]
    public class WebSocketMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        
        [JsonProperty("payload")]
        public object Payload { get; set; } 
        
        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        public WebSocketMessage() { }
        
        public WebSocketMessage(string type)
        {
            Type = type;
            Payload = new Dictionary<string, object>();
        }
        
        public WebSocketMessage(string type, Dictionary<string, object> data, string requestId = null)
        {
            Type = type;
            RequestId = requestId;
            Payload = data ?? new Dictionary<string, object>();
        }

        // New constructor for array payloads
        public WebSocketMessage(string type, List<Dictionary<string, object>> arrayData, string requestId = null)
        {
            Type = type;
            RequestId = requestId;
            Payload = arrayData ?? new List<Dictionary<string, object>>();
        }

        // Basic message types
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
        
        // Game event messages
        public static WebSocketMessage CreatePlayerConnected(string playerName, string playerId, string steamId)
        {
            return new WebSocketMessage("player-connected", new Dictionary<string, object>
            {
                { "playerName", playerName },
                { "playerId", playerId },
                { "steamId", steamId }
            });
        }
        
        public static WebSocketMessage CreatePlayerDisconnected(string playerName, string playerId, string steamId)
        {
            return new WebSocketMessage("player-disconnected", new Dictionary<string, object>
            {
                { "playerName", playerName },
                { "playerId", playerId },
                { "steamId", steamId }
            });
        }
        
        public static WebSocketMessage CreateChatMessage(string playerName, string playerId, string steamId, string message)
        {
            return new WebSocketMessage("chat-message", new Dictionary<string, object>
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
            return new WebSocketMessage("entity-killed", new Dictionary<string, object>
            {
                { "killerName", killerName },
                { "killerId", killerId },
                { "killerSteamId", killerSteamId },
                { "entityName", entityName },
                { "entityType", entityType }
            });
        }
        
        // Response messages
        public static WebSocketMessage CreateResponse(string requestId, object data)
        {
            // If data is already a Dictionary<string, object>, use it directly
            if (data is Dictionary<string, object> dictData)
            {
                return new WebSocketMessage("response", dictData, requestId);
            }
            
            // If data is a List<Dictionary<string, object>>, use the array constructor
            if (data is List<Dictionary<string, object>> listData)
            {
                return new WebSocketMessage("response", listData, requestId);
            }
            
            // Otherwise, convert the data to a Dictionary with appropriate key(s)
            var payload = new Dictionary<string, object>();
            
            if (data != null)
            {
                // If it's a simple value or complex object that's not a dictionary
                foreach (var prop in data.GetType().GetProperties())
                {
                    payload[prop.Name] = prop.GetValue(data);
                }
                
                // If no properties were found, serialize the entire object as a single value
                if (payload.Count == 0)
                {
                    payload["value"] = data;
                }
            }
            
            return new WebSocketMessage("response", payload, requestId);
        }
        
        public static WebSocketMessage CreateErrorResponse(string requestId, string errorMessage)
        {
            return new WebSocketMessage("error", new Dictionary<string, object>
            {
                { "requestId", requestId },
                { "error", errorMessage }
            });
        }
        
        // Command-related messages
        public static WebSocketMessage CreateCommandOutput(string requestId, bool success, string rawResult, string errorMessage = null)
        {
            var data = new Dictionary<string, object>
            {
                { "success", success },
                { "rawResult", rawResult }
            };
            
            if (!string.IsNullOrEmpty(errorMessage))
            {
                data["errorMessage"] = errorMessage;
            }
            
            return new WebSocketMessage("response", data, requestId);
        }
        
        // Reachability test response
        public static WebSocketMessage CreateTestReachabilityResponse(string requestId)
        {
            return new WebSocketMessage("response", new Dictionary<string, object>
            {
                { "connectable", true }
            }, requestId);
        }
        
        // Player-related response messages
        public static WebSocketMessage CreatePlayerResponse(string requestId, Dictionary<string, object> playerData)
        {
            return new WebSocketMessage("response", playerData, requestId);
        }
        
        // Modified to pass array directly as payload
        public static WebSocketMessage CreatePlayersResponse(string requestId, List<Dictionary<string, object>> playersData)
        {
            return new WebSocketMessage("response", playersData, requestId);
        }
        
        public static WebSocketMessage CreatePlayerLocationResponse(string requestId, double x, double y, double z)
        {
            return new WebSocketMessage("response", new Dictionary<string, object>
            {
                { "x", x },
                { "y", y },
                { "z", z }
            }, requestId);
        }
        
        public static WebSocketMessage CreatePlayerInventoryResponse(string requestId, List<Dictionary<string, object>> inventoryItems)
        {
            return new WebSocketMessage("response", new Dictionary<string, object>
            {
                { "items", inventoryItems }
            }, requestId);
        }
        
        // Item-related responses - can be modified to use array payload if needed
        public static WebSocketMessage CreateItemsListResponse(string requestId, List<Dictionary<string, object>> items)
        {
            return new WebSocketMessage("response", new Dictionary<string, object>
            {
                { "items", items }
            }, requestId);
        }
        
        // Ban-related responses - can be modified to use array payload if needed
        public static WebSocketMessage CreateBansListResponse(string requestId, List<Dictionary<string, object>> bans)
        {
            return new WebSocketMessage("response", new Dictionary<string, object>
            {
                { "bans", bans }
            }, requestId);
        }
        
        // Map-related responses
        public static WebSocketMessage CreateMapInfoResponse(string requestId, Dictionary<string, object> mapInfo)
        {
            return new WebSocketMessage("response", mapInfo, requestId);
        }
        
        public static WebSocketMessage CreateMapTileResponse(string requestId, string base64EncodedImage)
        {
            return new WebSocketMessage("response", new Dictionary<string, object>
            {
                { "imageData", base64EncodedImage }
            }, requestId);
        }
    }
}