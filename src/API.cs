using System.Collections.Generic;
using System.Text;

namespace Catalysm
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

        private bool GameMessage(ClientInfo _cInfo, EnumGameMessages _type, string _msg, string _mainName, bool _localizeMain, string _secondaryName, bool _localizeSecondary)
        {
            
            return true;
        }

        private void GameAwake()
        {
            Patcher.DoPatching();
        }

        private void GameShutdown()
        {
            
        }

        private void PlayerDisconnected(ClientInfo _cInfo, bool _bShutdown)
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

                    string weap = DamageHandler.weaponUsed(entID);

                    if (entKilled.entityType == EntityType.Zombie)
                    {
                        Log.Out($"[Catalysm]entityKilled: {ci.playerName} ({ci.PlatformId}) killed zombie {ea.EntityName} with {weap}");
                    }
                    else
                    {
                        Log.Out($"[Catalysm]entityKilled: {ci.playerName} ({ci.PlatformId}) killed animal {ea.EntityName} with {weap}");
                    }
                }
            }
        }

        private void PlayerSpawnedInWorld(ClientInfo _cInfo, RespawnType _respawnReason, Vector3i _pos)
        {
            
        }

        private void SavePlayerData(ClientInfo _cInfo, PlayerDataFile _playerDataFile)
        {
            
        }

        private bool PlayerLogin(ClientInfo _cInfo, string _compatibilityVersion, StringBuilder sb)
        {
            
            return true;
        }

        private bool ChatMessage(ClientInfo _cInfo, EChatType _type, int _senderId, string _msg, string _mainName, bool _localizeMain, List<int> _recipientEntityIds)
        {
            
            return true;
        }

        private void CalcChunkColorsDone(Chunk _chunk)
        {
            
        }
    }
}

