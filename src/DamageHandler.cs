namespace Catalysm
{
    public class DamageHandler
    {

        public static void HandleDamagePlayer(DamageProperties damProps, bool died)
        {
            if (!died) return;

            if (string.IsNullOrEmpty(damProps.VictimName) || string.IsNullOrEmpty(damProps.OffenderName)) return;

            //log all kills, pve or not
            Log.Out($"[Catalysm]playerKilledByPlayer: {damProps.OffenderName} (offenderSteamId={damProps.OffenderSteamId}) @ {damProps.OffenderPosition.x} {damProps.OffenderPosition.y} {damProps.OffenderPosition.z} killed {damProps.VictimName} (victimSteamId={damProps.VictimSteamId}) @ {damProps.VictimPosition.x} {damProps.VictimPosition.y} {damProps.VictimPosition.z} with {weaponUsed(damProps.OffenderEntityId)}");
        }

        public static void HandleDamageOther(DamageProperties damProps, bool died)
        {
            if (!died) return;

            if (string.IsNullOrEmpty(damProps.VictimName) || string.IsNullOrEmpty(damProps.OffenderName)) return;

            Log.Out($"[Catalysm]playerKilledByEntity: {damProps.OffenderName} ({damProps.OffenderPosition.x},{damProps.OffenderPosition.y},{damProps.OffenderPosition.z}) killed {damProps.VictimName} ({damProps.VictimSteamId}) @ {damProps.VictimPosition.x} {damProps.VictimPosition.y} {damProps.VictimPosition.z}");
        }
        public static void LogDamageDetection(string name, string steamId, int strength)
        {
            Log.Out($"[Catalysm]damageDetection(Entity): Player {name} ({steamId}) triggered damage detection! Damage done: {strength}");
        }

        public static string weaponUsed(int? entityId)
        {
            if (entityId == null)
            {
                return "unknown";
            }

            EntityPlayer pl = GameManager.Instance.World.Players.dict[(int)entityId];
            if (pl != null && pl.IsSpawned())
            {
                string hi = pl.inventory.holdingItem.Name;
                if (!string.IsNullOrEmpty(pl.inventory.holdingItem.Name))
                {
                    ItemValue _itemValue = ItemClass.GetItem(hi, true);
                    if (_itemValue.type != ItemValue.None.type)
                    {
                        hi = _itemValue.ItemClass.GetLocalizedItemName() ?? _itemValue.ItemClass.GetItemName();
                        return hi;
                    }
                }
                else
                {
                    return "unknown";
                }
            }

            return "unknown";
        }
        public class DamageProperties
        {
            public Vector3i VictimPosition;
            public Vector3i OffenderPosition;
            public int VictimEntityId;
            public string VictimName;
            public string VictimSteamId;
            public int? OffenderEntityId;
            public string OffenderName;
            public string OffenderSteamId;
        }
    }
}
