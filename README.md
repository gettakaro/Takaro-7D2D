# Takaro 7D2D Mod

A companion mod for 7 Days to Die servers that connects to Takaro via WebSocket.

## Build Instructions

```sh
docker compose up -d builder
sudo ./setup.sh
# Then, restart the gameserver to apply the changes
docker compose down && docker compose up -d 7dtdserver

```
