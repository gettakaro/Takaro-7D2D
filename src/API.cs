using System.Collections.Generic;
using System.Text;

namespace Takaro7D2D
{
    public class API : IModApi
    {
        public void InitMod(Mod mod)
        {
            ModEvents.GameStartDone.RegisterHandler(GameAwake);
            ModEvents.GameShutdown.RegisterHandler(GameShutdown);
            ModEvents.SavePlayerData.RegisterHandler(SavePlayerData);
            ModEvents.PlayerSpawnedInWorld.RegisterHandler(PlayerSpawnedInWorld);
            ModEvents.PlayerDisconnected.RegisterHandler(PlayerDisconnected);
            ModEvents.ChatMessage.RegisterHandler(ChatMessage);
            ModEvents.PlayerLogin.RegisterHandler(PlayerLogin);
            ModEvents.EntityKilled.RegisterHandler(EntityKilled);
            ModEvents.GameMessage.RegisterHandler(GameMessage);
            ModEvents.CalcChunkColorsDone.RegisterHandler(CalcChunkColorsDone);
        }

        private bool GameMessage(ClientInfo cInfo, EnumGameMessages type, string msg, string mainName, string secondaryName)
        {
            return true;
        }

        private void GameAwake()
        {
            
        }

        private void GameShutdown()
        {
        }

        private void PlayerDisconnected(ClientInfo cInfo, bool bShutdown)
        {
        }

        public void EntityKilled(Entity entKilled, Entity entOffender)
        {
            if (entOffender != null && entKilled != null)
            {
                if (entOffender.entityType == EntityType.Player)
                {
                    ClientInfo ci = ConsoleHelper.ParseParamIdOrName(entOffender.entityId.ToString());
                    if (ci == null) return;
                    EntityAlive ea = entKilled as EntityAlive;
                    int? entID = entOffender.entityId;
                    
                    if (entKilled.entityType == EntityType.Zombie)
                    {
                        Log.Out($"[Catalysm]entityKilled: {ci.playerName} ({ci.PlatformId}) killed zombie {ea.EntityName} with unknown weapon");
                    }
                    else
                    {
                        Log.Out($"[Catalysm]entityKilled: {ci.playerName} ({ci.PlatformId}) killed animal {ea.EntityName} with unknown weapon");
                    }
                }
            }
        }

        private void PlayerSpawnedInWorld(ClientInfo cInfo, RespawnType respawnReason, Vector3i pos)
        {
        }

        private void SavePlayerData(ClientInfo cInfo, PlayerDataFile playerDataFile)
        {
        }

        private bool PlayerLogin(ClientInfo cInfo, string compatibilityVersion, StringBuilder sb)
        {
            return true;
        }

        private bool ChatMessage(ClientInfo cInfo, EChatType type, int senderId, string msg, string mainName, List<int> recipientEntityIds)
        {
            return true;
        }

        private void CalcChunkColorsDone(Chunk chunk)
        {
        }
    }
}