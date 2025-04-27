using Newtonsoft.Json;

namespace Takaro
{

  public class TakaroPlayer
  {
    [JsonProperty("gameId")]
    public string GameId { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("ip")]
    public string Ip { get; set; }

    [JsonProperty("ping")]
    public int Ping { get; set; }

    [JsonProperty("steamId")]
    public string SteamId { get; set; }

    [JsonProperty("xboxLiveId")]
    public string XboxLiveId { get; set; }
  }

  public static class Shared
  {
    public static ClientInfo GetClientInfoFromGameId(string gameId)
    {
      PlatformUserIdentifierAbs userId = PlatformUserIdentifierAbs.FromCombinedString($"EOS_{gameId}");
      ClientInfo cInfo = ConnectionManager.Instance.Clients.ForUserId(userId);
      return cInfo;
    }

    public static TakaroPlayer TransformClientInfoToTakaroPlayer(ClientInfo clientInfo)
    {
      if (clientInfo == null)
        return null;

      TakaroPlayer player = new TakaroPlayer
      {
        // Takaro gameId is the EOS ID (CrossPlatform ID) without the EOS_ prefix
        GameId = clientInfo.CrossplatformId.CombinedString.Replace("EOS_", ""),
        Name = clientInfo.playerName,
        Ip = clientInfo.ip,
        Ping = clientInfo.ping
      };

      if (clientInfo.PlatformId != null && clientInfo.PlatformId.CombinedString != null)
      {
        if (clientInfo.PlatformId.CombinedString.StartsWith("Steam_"))
        {
          player.SteamId = clientInfo.PlatformId.CombinedString.Replace("Steam_", "");
        }
        else if (clientInfo.PlatformId.CombinedString.StartsWith("XBL_"))
        {
          player.XboxLiveId = clientInfo.PlatformId.CombinedString.Replace("XBL_", "");
        }
      }

      return player;
    }
  }
}