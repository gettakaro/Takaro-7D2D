using System;
using System.Collections.Generic;
using System.Text;
using Takaro.Config;
using Takaro.WebSocket;
using UnityEngine;

namespace Takaro
{
    public class API : IModApi
    {
        private WebSocketClient _webSocketClient;

        public void InitMod(Mod mod)
        {
            Log.Out("[Takaro] Initializing mod");

            // Initialize config
            ConfigManager.Instance.SetPath(mod.Path);
            ConfigManager.Instance.LoadConfig();

            // Register event handlers
            ModEvents.GameStartDone.RegisterHandler(GameAwake);
            ModEvents.GameShutdown.RegisterHandler(GameShutdown);
            ModEvents.SavePlayerData.RegisterHandler(SavePlayerData);
            ModEvents.PlayerSpawnedInWorld.RegisterHandler(PlayerSpawnedInWorld);
            ModEvents.PlayerDisconnected.RegisterHandler(PlayerDisconnected);
            ModEvents.ChatMessage.RegisterHandler(ChatMessage);
            ModEvents.PlayerLogin.RegisterHandler(PlayerLogin);
            ModEvents.EntityKilled.RegisterHandler(EntityKilled);
            ModEvents.GameMessage.RegisterHandler(GameMessage);

            // Register Unity log handler for capturing server logs
            Application.logMessageReceived += HandleLogMessage;

            Log.Out("[Takaro] Mod initialized successfully");
        }

        private bool GameMessage(
            ClientInfo cInfo,
            EnumGameMessages type,
            string msg,
            string mainName,
            string secondaryName
        )
        {
            return true;
        }

        private void GameAwake()
        {
            // Initialize WebSocket client
            _webSocketClient = WebSocketClient.Instance;
            _webSocketClient.Initialize();
        }

        private void GameShutdown()
        {
            Log.Out("[Takaro] Game shutting down");

            // Unregister Unity log handler
            Application.logMessageReceived -= HandleLogMessage;

            // Shutdown WebSocket client
            _webSocketClient?.Shutdown();
        }

        private void PlayerDisconnected(ClientInfo cInfo, bool bShutdown)
        {
            if (cInfo != null && !bShutdown)
            {
                Log.Out($"[Takaro] Player disconnected: {cInfo.playerName} ({cInfo.PlatformId})");
                _webSocketClient?.SendPlayerDisconnected(cInfo);
            }
        }

        public void EntityKilled(Entity entKilled, Entity entOffender)
        {
            if (entKilled != null)
            {
                // Handle player death events
                if (entKilled.entityType == EntityType.Player)
                {
                    ClientInfo killedPlayerInfo = ConsoleHelper.ParseParamIdOrName(
                        entKilled.entityId.ToString()
                    );
                    if (killedPlayerInfo != null)
                    {
                        // Get killer information
                        ClientInfo attackerInfo = null;
                        if (entOffender != null && entOffender.entityType == EntityType.Player)
                        {
                            attackerInfo = ConsoleHelper.ParseParamIdOrName(
                                entOffender.entityId.ToString()
                            );
                        }

                        Vector3 deathPosition = entKilled.position;
                        Log.Out($"[Takaro] Player death: {killedPlayerInfo.playerName} died at {deathPosition}");
                        
                        _webSocketClient?.SendPlayerDeath(killedPlayerInfo, attackerInfo, deathPosition);
                    }
                }
                // Handle entity kill events (player killing something else)
                else if (entOffender != null && entOffender.entityType == EntityType.Player)
                {
                    ClientInfo killerInfo = ConsoleHelper.ParseParamIdOrName(
                        entOffender.entityId.ToString()
                    );
                    if (killerInfo == null)
                        return;
                    EntityAlive ea = entKilled as EntityAlive;
                    if (ea == null)
                        return;

                    string entityType = "unknown";
                    if (entKilled.entityType == EntityType.Zombie)
                    {
                        entityType = "zombie";
                        Log.Out(
                            $"[Takaro] Entity killed: {killerInfo.playerName} ({killerInfo.PlatformId}) killed zombie {ea.EntityName}"
                        );
                    }
                    else if (entKilled.entityType == EntityType.Animal)
                    {
                        entityType = "animal";
                        Log.Out(
                            $"[Takaro] Entity killed: {killerInfo.playerName} ({killerInfo.PlatformId}) killed animal {ea.EntityName}"
                        );
                    }
                    else
                    {
                        entityType = entKilled.entityType.ToString().ToLower();
                    }

                    // Try to get weapon information from player's held item
                    string weapon = null;
                    try
                    {
                        EntityPlayer playerEntity = entOffender as EntityPlayer;
                        if (playerEntity != null && playerEntity.inventory != null)
                        {
                            ItemValue heldItemValue = playerEntity.inventory.holdingItemItemValue;
                            if (heldItemValue != null && !heldItemValue.IsEmpty())
                            {
                                ItemClass itemClass = heldItemValue.ItemClass;
                                weapon = itemClass?.GetLocalizedItemName() ?? itemClass?.GetItemName();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[Takaro] Could not get weapon info: {ex.Message}");
                    }

                    _webSocketClient?.SendEntityKilled(killerInfo, ea.EntityName, entityType, weapon);
                }
            }
        }

        private void PlayerSpawnedInWorld(ClientInfo cInfo, RespawnType respawnReason, Vector3i pos)
        {
            if (cInfo == null)
                return;

            if (
                respawnReason == RespawnType.JoinMultiplayer
                || respawnReason == RespawnType.EnterMultiplayer
            )
            {
                Log.Out($"[Takaro] Player connected: {cInfo.playerName} ({cInfo.PlatformId})");
                _webSocketClient?.SendPlayerConnected(cInfo);
            }
        }

        private void SavePlayerData(ClientInfo cInfo, PlayerDataFile playerDataFile)
        {
            // Can be used to track player stats if needed
        }

        private bool PlayerLogin(ClientInfo cInfo, string compatibilityVersion, StringBuilder sb)
        {
            return true;
        }

        private bool ChatMessage(
            ClientInfo cInfo,
            EChatType type,
            int senderId,
            string msg,
            string mainName,
            List<int> recipientEntityIds
        )
        {
            if (cInfo != null)
            {
                Log.Out($"[Takaro] Chat message: {cInfo.playerName}: {msg}");
                _webSocketClient?.SendChatMessage(cInfo, type, senderId, msg, mainName, recipientEntityIds);
            }
            return true;
        }

        private void HandleLogMessage(string logString, string stackTrace, LogType type)
        {
            // Filter log messages to only send relevant ones to Takaro
            // Avoid infinite loops by not sending our own Takaro log messages
            if (string.IsNullOrEmpty(logString) || logString.Contains("[Takaro]"))
                return;

            // Only send Error and Warning level messages to reduce noise
            if (type == LogType.Error || type == LogType.Warning)
            {
                string formattedMessage = $"[{type}] {logString}";
                if (!string.IsNullOrEmpty(stackTrace) && type == LogType.Error)
                {
                    formattedMessage += $"\nStack Trace: {stackTrace}";
                }

                _webSocketClient?.SendLogEvent(formattedMessage);
            }
        }
    }
}
