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
            ConfigManager.Instance.LoadConfig();

            // Register event handlers
            ModEvents.GameStartDone.RegisterHandler(GameAwake);
            ModEvents.GameShutdown.RegisterHandler(GameShutdown);
            ModEvents.PlayerSpawnedInWorld.RegisterHandler(PlayerSpawnedInWorld);
            ModEvents.PlayerDisconnected.RegisterHandler(PlayerDisconnected);
            ModEvents.ChatMessage.RegisterHandler(ChatMessage);
            ModEvents.PlayerLogin.RegisterHandler(PlayerLogin);
            ModEvents.EntityKilled.RegisterHandler(EntityKilled);
            ModEvents.GameMessage.RegisterHandler(GameMessage);

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
            if (entOffender != null && entKilled != null)
            {
                if (entOffender.entityType == EntityType.Player)
                {
                    ClientInfo ci = ConsoleHelper.ParseParamIdOrName(
                        entOffender.entityId.ToString()
                    );
                    if (ci == null)
                        return;
                    EntityAlive ea = entKilled as EntityAlive;
                    if (ea == null)
                        return;

                    string entityType = "unknown";
                    if (entKilled.entityType == EntityType.Zombie)
                    {
                        entityType = "zombie";
                        Log.Out(
                            $"[Takaro] Entity killed: {ci.playerName} ({ci.PlatformId}) killed zombie {ea.EntityName}"
                        );
                    }
                    else if (entKilled.entityType == EntityType.Animal)
                    {
                        entityType = "animal";
                        Log.Out(
                            $"[Takaro] Entity killed: {ci.playerName} ({ci.PlatformId}) killed animal {ea.EntityName}"
                        );
                    }
                    else
                    {
                        entityType = entKilled.entityType.ToString().ToLower();
                    }

                    _webSocketClient?.SendEntityKilled(ci, ea.EntityName, entityType);
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
            if (cInfo != null && type == EChatType.Global)
            {
                Log.Out($"[Takaro] Chat message: {cInfo.playerName}: {msg}");
                _webSocketClient?.SendChatMessage(cInfo, msg);
            }
            return true;
        }
    }
}
