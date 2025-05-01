using System;
using System.IO;
using System.Xml;

namespace Takaro.Config
{
    public class ConfigManager
    {
        private static ConfigManager _instance;
        private static readonly object _lock = new object();

        public string WebSocketUrl { get; private set; } = "wss://your-takaro-websocket-server.com";
        public string IdentityToken { get; private set; } = "";
        public string RegistrationToken { get; private set; } = "";
        public bool WebSocketEnabled { get; private set; } = true;
        public int ReconnectIntervalSeconds { get; private set; } = 30;

        private const string DEFAULT_IDENTITY_TOKEN = "your-identity-token";
        private string ConfigFilePath = "";

        private ConfigManager()
        {
            LoadConfig();
        }

        public static ConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ConfigManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public void SetPath(string path)
        {
            ConfigFilePath = Path.Combine(GameIO.GetGamePath(), path, "Config.xml");
        }

        public void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    Log.Warning(
                        "[Takaro] Config file not found. Creating default config at "
                            + ConfigFilePath
                    );
                    CreateDefaultConfig();
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(ConfigFilePath);

                var root = doc.DocumentElement;
                if (root == null)
                {
                    Log.Error("[Takaro] Invalid config file. Using default settings.");
                    return;
                }

                var webSocketNode = root.SelectSingleNode("WebSocket");
                if (webSocketNode != null)
                {
                    var urlNode = webSocketNode.SelectSingleNode("Url");
                    if (urlNode != null)
                    {
                        WebSocketUrl = urlNode.InnerText;
                    }

                    var identityTokenNode = webSocketNode.SelectSingleNode("IdentityToken");
                    if (identityTokenNode != null)
                    {
                        IdentityToken = identityTokenNode.InnerText;
                    }

                    var registrationTokenNode = webSocketNode.SelectSingleNode("RegistrationToken");
                    if (registrationTokenNode != null)
                    {
                        RegistrationToken = registrationTokenNode.InnerText;
                    }

                    var enabledNode = webSocketNode.SelectSingleNode("Enabled");
                    if (enabledNode != null)
                    {
                        bool.TryParse(enabledNode.InnerText, out bool enabled);
                        WebSocketEnabled = enabled;
                    }

                    var reconnectIntervalNode = webSocketNode.SelectSingleNode(
                        "ReconnectIntervalSeconds"
                    );
                    if (reconnectIntervalNode != null)
                    {
                        int.TryParse(reconnectIntervalNode.InnerText, out int interval);
                        ReconnectIntervalSeconds = interval > 0 ? interval : 30;
                    }
                }

                // Check if identity token is the default value and generate a new one if needed
                if (string.IsNullOrEmpty(IdentityToken) || IdentityToken == DEFAULT_IDENTITY_TOKEN)
                {
                    IdentityToken = GenerateUuid();
                    UpdateIdentityTokenInConfig(IdentityToken);
                    Log.Out($"[Takaro] Generated new identity token: {IdentityToken}");
                }

                Log.Out(
                    $"[Takaro] Config loaded. WebSocket URL: {WebSocketUrl}, Enabled: {WebSocketEnabled}"
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[Takaro] Error loading config: {ex.Message}");
                Log.Exception(ex);
            }
        }

        private void CreateDefaultConfig()
        {
            try
            {
                // Generate a new identity token for default config
                string identityToken = GenerateUuid();

                Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath));

                using (
                    XmlWriter writer = XmlWriter.Create(
                        ConfigFilePath,
                        new XmlWriterSettings { Indent = true }
                    )
                )
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("Takaro");

                    writer.WriteStartElement("WebSocket");
                    writer.WriteElementString("Url", WebSocketUrl);
                    writer.WriteElementString("IdentityToken", identityToken);
                    writer.WriteElementString("RegistrationToken", RegistrationToken);
                    writer.WriteElementString("Enabled", WebSocketEnabled.ToString().ToLower());
                    writer.WriteElementString(
                        "ReconnectIntervalSeconds",
                        ReconnectIntervalSeconds.ToString()
                    );
                    writer.WriteEndElement(); // WebSocket

                    writer.WriteEndElement(); // Takaro
                    writer.WriteEndDocument();
                }

                // Update the instance property
                IdentityToken = identityToken;

                Log.Out(
                    $"[Takaro] Default config created with generated identity token: {identityToken}"
                );
            }
            catch (Exception ex)
            {
                Log.Error($"[Takaro] Error creating default config: {ex.Message}");
                Log.Exception(ex);
            }
        }

        private string GenerateUuid()
        {
            // Generate a unique identifier using System.Guid
            return Guid.NewGuid().ToString();
        }

        private void UpdateIdentityTokenInConfig(string newToken)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(ConfigFilePath);

                var node = doc.SelectSingleNode("//Takaro/WebSocket/IdentityToken");
                if (node != null)
                {
                    node.InnerText = newToken;
                    doc.Save(ConfigFilePath);
                    Log.Out("[Takaro] Updated identity token in config file");
                }
                else
                {
                    Log.Error("[Takaro] Could not find IdentityToken node in config to update");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Takaro] Error updating identity token in config: {ex.Message}");
                Log.Exception(ex);
            }
        }
    }
}
