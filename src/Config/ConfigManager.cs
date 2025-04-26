using System;
using System.IO;
using System.Xml;

namespace Takaro7D2D.Config
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

        private string ConfigFilePath => Path.Combine(GameIO.GetGamePath(), "Mods", "Takaro7D2D", "Config.xml");

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

        public void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    Log.Warning("[Takaro] Config file not found. Creating default config at " + ConfigFilePath);
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

                    var reconnectIntervalNode = webSocketNode.SelectSingleNode("ReconnectIntervalSeconds");
                    if (reconnectIntervalNode != null)
                    {
                        int.TryParse(reconnectIntervalNode.InnerText, out int interval);
                        ReconnectIntervalSeconds = interval > 0 ? interval : 30;
                    }
                }

                Log.Out($"[Takaro] Config loaded. WebSocket URL: {WebSocketUrl}, Enabled: {WebSocketEnabled}");
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
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigFilePath));

                using (XmlWriter writer = XmlWriter.Create(ConfigFilePath, new XmlWriterSettings { Indent = true }))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("Takaro7D2D");

                    writer.WriteStartElement("WebSocket");
                    writer.WriteElementString("Url", WebSocketUrl);
                    writer.WriteElementString("IdentityToken", IdentityToken);
                    writer.WriteElementString("RegistrationToken", RegistrationToken);
                    writer.WriteElementString("Enabled", WebSocketEnabled.ToString().ToLower());
                    writer.WriteElementString("ReconnectIntervalSeconds", ReconnectIntervalSeconds.ToString());
                    writer.WriteEndElement(); // WebSocket

                    writer.WriteEndElement(); // Takaro7D2D
                    writer.WriteEndDocument();
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Takaro] Error creating default config: {ex.Message}");
                Log.Exception(ex);
            }
        }
    }
}