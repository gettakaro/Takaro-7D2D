#!/bin/bash
set -eox pipefail

echo "Setting up 7D2D mod development environment..."

# Create directory structure
mkdir -p ./_data/{7dtd-binaries,build,game-files,lib,ServerFiles}

# Download 7D2D dedicated server files via SteamCMD
echo "Downloading 7D2D server files via SteamCMD..."
docker compose run --rm steamcmd

# Verify that the server files were downloaded
if [ ! -d "./_data/game-files/7DaysToDieServer_Data" ]; then
  echo "Error: 7D2D server files not found. Please check the SteamCMD output."
  echo "Contents of _data/game-files:"
  ls -l ./_data/game-files
  exit 1
fi

# Verify that the required DLLs are present
if [ ! -d "./_data/game-files/7DaysToDieServer_Data/Managed" ]; then
  echo "Error: Managed DLLs not found. Please check the server files."
  exit 1
fi

# Extract necessary DLLs from game files
echo "Extracting game DLLs..."
docker compose run --rm builder bash -c "cp -f /app/_data/game-files/7DaysToDieServer_Data/Managed/*.dll /app/_data/7dtd-binaries/ && \
                                         cp -f /app/_data/game-files/Mods/0_TFP_Harmony/*.dll /app/_data/7dtd-binaries/"

# Build dependencies (websocket-sharp, download Newtonsoft.Json)
echo "Preparing dependencies..."
docker compose run --rm deps

echo "Environment setup complete!"
echo "You can now use build-mod.sh to compile the mod."
