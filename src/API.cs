using System.Collections.Generic;
using System.Text;
using Takaro.Config;
using Takaro.WebSocket;

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

            Log.Out("[Takaro] Mod initialized successfully");
        }

        private ModEvents.EModEventResult GameMessage(ref ModEvents.SGameMessageData data)
        {
            return ModEvents.EModEventResult.Continue;
        }

        private void GameAwake(ref ModEvents.SGameStartDoneData data)
        {
            // Initialize WebSocket client
            _webSocketClient = WebSocketClient.Instance;
            _webSocketClient.Initialize();
        }

        private void GameShutdown(ref ModEvents.SGameShutdownData data)
        {
            Log.Out("[Takaro] Game shutting down");

            // Shutdown WebSocket client
            _webSocketClient?.Shutdown();
        }

        private void PlayerDisconnected(ref ModEvents.SPlayerDisconnectedData data)
        {
            if (data.ClientInfo != null && !data.GameShuttingDown)
            {
                Log.Out($"[Takaro] Player disconnected: {data.ClientInfo.playerName} ({data.ClientInfo.PlatformId})");
                _webSocketClient?.SendPlayerDisconnected(data.ClientInfo);
            }
        }

        public void EntityKilled(ref ModEvents.SEntityKilledData data)
        {
            if (data.KillingEntity != null && data.KilledEntitiy != null)
            {
                if (data.KillingEntity.entityType == EntityType.Player)
                {
                    ClientInfo ci = ConsoleHelper.ParseParamIdOrName(
                        data.KillingEntity.entityId.ToString()
                    );
                    if (ci == null)
                        return;
                    EntityAlive ea = data.KilledEntitiy as EntityAlive;
                    if (ea == null)
                        return;

                    string entityType = "unknown";
                    if (data.KilledEntitiy.entityType == EntityType.Zombie)
                    {
                        entityType = "zombie";
                        Log.Out(
                            $"[Takaro] Entity killed: {ci.playerName} ({ci.PlatformId}) killed zombie {ea.EntityName}"
                        );
                    }
                    else if (data.KilledEntitiy.entityType == EntityType.Animal)
                    {
                        entityType = "animal";
                        Log.Out(
                            $"[Takaro] Entity killed: {ci.playerName} ({ci.PlatformId}) killed animal {ea.EntityName}"
                        );
                    }
                    else
                    {
                        entityType = data.KilledEntitiy.entityType.ToString().ToLower();
                    }

                    _webSocketClient?.SendEntityKilled(ci, ea.EntityName, entityType);
                }
            }
        }

        private void PlayerSpawnedInWorld(ref ModEvents.SPlayerSpawnedInWorldData data)
        {
            if (data.ClientInfo == null)
                return;

            if (
                data.RespawnType == RespawnType.JoinMultiplayer
                || data.RespawnType == RespawnType.EnterMultiplayer
            )
            {
                Log.Out($"[Takaro] Player connected: {data.ClientInfo.playerName} ({data.ClientInfo.PlatformId})");
                _webSocketClient?.SendPlayerConnected(data.ClientInfo);
            }
        }

        private void SavePlayerData(ref ModEvents.SSavePlayerDataData data)
        {
            // Can be used to track player stats if needed
        }

        private ModEvents.EModEventResult PlayerLogin(ref ModEvents.SPlayerLoginData data)
        {
            return ModEvents.EModEventResult.Continue;
        }

        private ModEvents.EModEventResult ChatMessage(ref ModEvents.SChatMessageData data)
        {
            if (data.ClientInfo != null)
            {
                Log.Out($"[Takaro] Chat message: {data.ClientInfo.playerName}: {data.Message}");
                _webSocketClient?.SendChatMessage(data.ClientInfo, data.ChatType, data.SenderEntityId, data.Message, data.MainName, data.RecipientEntityIds);
            }
            return ModEvents.EModEventResult.Continue;
        }
    }
}
