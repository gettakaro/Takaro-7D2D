# Takaro 7D2D Mod

A companion mod for 7 Days to Die servers that connects to Takaro via WebSocket.

## Features

- WebSocket connection to Takaro server
- Configuration via XML file
- Event tracking:
  - Player connections and disconnections
  - Chat messages
  - Entity kills (zombies, animals, etc.)

## Build Instructions

```sh
# Build the solution
dotnet msbuild Takaro7D2D.sln /p:Configuration=Release

# Or use the setup script for Docker-based build
./setup.sh
```

## Dependencies

This mod requires the WebSocket-Sharp library. A copy should be placed in a `lib` directory at the project root:

```
lib/
  └── websocket-sharp.dll
```

You can download WebSocket-Sharp from: https://github.com/sta/websocket-sharp

## Configuration

After installation, a default `Config.xml` file will be created in your mod directory. Edit this file to configure the WebSocket connection:

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Takaro7D2D>
  <WebSocket>
    <Url>wss://your-takaro-websocket-server.com</Url>
    <IdentityToken>your-identity-token</IdentityToken>
    <RegistrationToken>your-registration-token</RegistrationToken>
    <Enabled>true</Enabled>
    <ReconnectIntervalSeconds>30</ReconnectIntervalSeconds>
  </WebSocket>
</Takaro7D2D>
```

### Configuration Options

- **Url**: The WebSocket server URL
- **IdentityToken**: Authentication token for the WebSocket server
- **RegistrationToken**: Registration token for the WebSocket server
- **Enabled**: Set to `true` to enable WebSocket connectivity, `false` to disable
- **ReconnectIntervalSeconds**: Time between reconnection attempts if the connection is lost

## Installation

1. Build the mod using the instructions above
2. Copy the mod files from `bin/Mods/Takaro7D2D` to your 7 Days to Die server's Mods directory
3. Edit the `Config.xml` file to set up your WebSocket connection
4. Restart your 7 Days to Die server

## Docker Installation

When using the Docker setup:

1. Run the setup script: `./setup.sh`
2. The mod will be automatically built and installed in the Docker server
3. Edit the configuration file in `./_data/ServerFiles/Mods/Takaro7D2D/Config.xml`
4. Restart the Docker container: `docker-compose restart 7dtdserver`

## WebSocket Message Format

The mod sends messages to the WebSocket server in the following format:

```json
{
  "Type": "event_type",
  "Data": {
    "key1": "value1",
    "key2": "value2"
  }
}
```

### Event Types

- **heartbeat**: Periodic keepalive message
- **register**: Registration message sent on connection
- **authenticate**: Authentication message sent on connection
- **player_connected**: Sent when a player joins the server
- **player_disconnected**: Sent when a player leaves the server
- **chat_message**: Sent when a player sends a chat message
- **entity_killed**: Sent when a player kills an entity

## License

[Your license information here]
