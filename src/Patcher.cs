using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Catalysm
{
    internal static class Patcher
    {
        public static void DoPatching()
        {
            try
            {
                Harmony harmonyPatcher = new Harmony("com.csmm.patrons");
                
                var gameMethod = AccessTools.Method(typeof(NetPackagePlayerStats), "ProcessPackage");
                if (gameMethod == null)
                {
                    Log.Out("[Catalysm] GameMethod ProcessPackage not found. Abort patching.");
                }
                else
                {
                    var prf = typeof(StateManager).GetMethod("LLB");
                    var pof = typeof(StateManager).GetMethod("LLA");

                    if (prf == null || pof == null)
                    {
                        Log.Out("[Catalysm] PatchMethod LLB or LLA not found. Abort patching.");
                    }
                    else
                    {
                        harmonyPatcher.Patch(gameMethod, new HarmonyMethod(prf), new HarmonyMethod(pof), null);
                    }
                }
                
                gameMethod = AccessTools.Method(typeof(EntityAlive), "ClientKill");
                if (gameMethod == null)
                {
                    Log.Out("[Catalysm] GameMethod ClientKill not found. Abort patching.");
                }
                else
                {
                    var prf = typeof(StateManager).GetMethod("CKP");

                    if (prf == null)
                    {
                        Log.Out("[Catalysm] PatchMethod CKP not found. Abort patching.");
                    }
                    else
                    {
                        harmonyPatcher.Patch(gameMethod, new HarmonyMethod(prf), null, null);
                    }
                }

                gameMethod = AccessTools.Method(typeof(EntityAlive), "OnEntityDeath");
                if (gameMethod == null)
                {
                    Log.Out("[Catalysm] GameMethod OnEntityDeath not found. Abort patching.");
                }
                else
                {
                    var prf = typeof(StateManager).GetMethod("OED");

                    if (prf == null)
                    {
                        Log.Out("[Catalysm] PatchMethod OED not found. Abort patching.");
                    }
                    else
                    {
                        harmonyPatcher.Patch(gameMethod, new HarmonyMethod(prf), null, null);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Out(string.Format("[Catalysm] Error in applying Harmony patches: {0}", e.Message));
            }
        }
    }

    public static class StateManager
    {
        public class StatePlayerLevel
        {
            public int EntityId;
            public int Level;
        }
              

        public static bool LLB(NetPackagePlayerStats __instance, ref StatePlayerLevel __state, World _world)
        {
            try
            {
                if (_world == null)
                    return true;

                if (_world.GetEntity(__instance.Sender.entityId) is EntityPlayer player)
                {
                    __state = new StatePlayerLevel()
                    {
                        EntityId = player.entityId,
                        Level = player.Progression.Level,
                    };
                }
            }
            catch { }
            
            return true;
        }

        public static void LLA(NetPackagePlayerStats __instance, StatePlayerLevel __state,  World _world)
        {
            try
            {
                if (_world == null || __state == null)
                    return;

                if (_world.GetEntity(__state.EntityId) is EntityPlayer player)
                {
                    if (player.Progression.Level > __state.Level)
                    {
                        ClientInfo clientInfo = ConnectionManager.Instance.Clients.ForEntityId(player.entityId);
                        if (clientInfo == null) return;

                        Log.Out($"[Catalysm]playerLeveled: {clientInfo.playerName} ({clientInfo.PlatformId}) made level {player.Progression.Level} (was {__state.Level})");

                        //check if level jump is > 1. Anticheat.
                        if((player.Progression.Level - __state.Level) >= 2) //removed settings dependency. Hardcoded 2 for leveljumping
                        {
                            Log.Out($"[Catalysm] WARNING: {clientInfo.playerName} ({clientInfo.PlatformId}) jumped up more than one level ({__state.Level} -> {player.Progression.Level}).");
                        }
                    }
                }
            }
            catch { }
            
        }
        public static bool CKP(EntityAlive __instance, DamageResponse _dmResponse)
        {

            if (__instance == null || _dmResponse.Source == null)
            {
                return true;
            }

            if (_dmResponse.Strength >= 5000) //removed settings dependency. Hardcoded 5000 for minimum damage
            {
                if (__instance.IsAlive())
                {
                    var offenderEntity0 = GameManager.Instance.World.GetEntity(_dmResponse.Source.getEntityId()) as EntityAlive;
                    if (offenderEntity0 == null) return true;

                    if (offenderEntity0 is EntityPlayer)
                    {
                        var offenderClientInfo0 = ConnectionManager.Instance.Clients.ForEntityId(offenderEntity0.entityId);

                        if (offenderClientInfo0 == null) return true;

                        int AdminLvL = GameManager.Instance.adminTools.Users.GetUserPermissionLevel(offenderClientInfo0);

                        if (AdminLvL > 0) //removed settings dependency. Hardcoded 0 for adminlevel
                        {
                            DamageHandler.LogDamageDetection(offenderClientInfo0.playerName, offenderClientInfo0.PlatformId.ToString(), _dmResponse.Strength);
                        }

                        return true;
                    }
                }
            }

            if (__instance is EntityPlayer)
            //player died -> take action
            {
                if (__instance.IsAlive())
                {
                    var offenderEntity = GameManager.Instance.World.GetEntity(_dmResponse.Source.getEntityId()) as EntityAlive;
                    if (offenderEntity == null) return true;

                    if (offenderEntity is EntityPlayer)
                    {
                        var victimClientInfo = ConnectionManager.Instance.Clients.ForEntityId(__instance.entityId);
                        var offenderClientInfo = ConnectionManager.Instance.Clients.ForEntityId(offenderEntity.entityId);

                        if (victimClientInfo == null || offenderClientInfo == null)
                            return true;

                        if (victimClientInfo.PlatformId.ToString() == offenderClientInfo.PlatformId.ToString())
                        {
                            return true;
                        }

                        var damprops = new DamageHandler.DamageProperties
                        {
                            VictimPosition = __instance.GetBlockPosition(),
                            OffenderPosition = offenderEntity.GetBlockPosition(),
                            VictimEntityId = __instance.entityId,
                            VictimName = __instance.EntityName,
                            VictimSteamId = victimClientInfo.PlatformId.ToString(),
                            OffenderEntityId = offenderEntity.entityId,
                            OffenderName = offenderEntity.EntityName,
                            OffenderSteamId = offenderClientInfo.PlatformId.ToString()
                        };

                        DamageHandler.HandleDamagePlayer(damprops, true);
                    }
                    else
                    {
                        var victimClientInfo = ConnectionManager.Instance.Clients.ForEntityId(__instance.entityId);

                        if (victimClientInfo == null)
                            return true;

                        var damprops = new DamageHandler.DamageProperties
                        {
                            VictimPosition = __instance.GetBlockPosition(),
                            OffenderPosition = offenderEntity.GetBlockPosition(),
                            VictimEntityId = __instance.entityId,
                            VictimName = __instance.EntityName,
                            VictimSteamId = victimClientInfo.PlatformId.ToString(),
                            OffenderEntityId = offenderEntity.entityId,
                            OffenderName = offenderEntity.EntityName,
                            OffenderSteamId = null
                        };

                        DamageHandler.HandleDamageOther(damprops, true);
                    }
                }
            }
            return true;
        }

        public static bool OED(EntityAlive __instance)
        {
            if(__instance == null)
            {
                return true;
            }
            
            if(__instance.GetType() == typeof(EntityPlayer))
            {
                ClientInfo ci = ConnectionManager.Instance.Clients.ForEntityId(__instance.entityId);
                if (ci != null)
                {
                    Log.Out($"[Catalysm]playerDied: {ci.playerName} ({ci.PlatformId}) died @ {(int)Math.Floor(__instance.position.x)} {(int)Math.Floor(__instance.position.y)} {(int)Math.Floor(__instance.position.z)}");
                }
            }

            return true;
        }
    }
}
