#!/bin/bash
set -eo pipefail

# Create the build directory and libs directory
mkdir -p ./_data/build
mkdir -p ./_data/7dtd-binaries
mkdir -p ./lib

# WebSocket-Sharp will be built inside the Docker container
echo "WebSocket-Sharp will be built inside the Docker container."

# Copy game DLLs from server files to the binaries directory
echo "Copying game DLLs from server installation..."
cp -f ./_data/ServerFiles/7DaysToDieServer_Data/Managed/*.dll ./_data/7dtd-binaries/
TOTAL_COPIED=$(ls -1 ./_data/7dtd-binaries/*.dll | wc -l)
echo "Copied $TOTAL_COPIED DLLs from server installation."

# Look for Harmony DLL in various locations
cp -f ./_data/ServerFiles/Mods/0_TFP_Harmony/*.dll ./_data/7dtd-binaries/
echo "Copied Harmony DLL from Mods directory."

# Make sure permissions are correct
sudo chmod -R 755 ./_data/build
sudo chmod -R 755 ./_data/7dtd-binaries
sudo chmod -R 755 ./lib

# Copy the built websocket-sharp DLL to the _data directory
docker compose cp builder:/lib/websocket-sharp.dll _data/7dtd-binaries/websocket-sharp.dll

# Run the build process inside the builder container
echo "Starting build process in builder container..."
docker compose run --rm builder /bin/bash -c "msbuild Takaro7D2D.sln /p:Configuration=Release"

# Copy the built files to the server mods directory
echo "Copying mod files to server mods directory..."
if [ -d ./_data/build/Mods/Takaro7D2D ]; then
  mkdir -p ./_data/ServerFiles/Mods/Takaro7D2D
  cp -r ./_data/build/Mods/Takaro7D2D/* ./_data/ServerFiles/Mods/Takaro7D2D/

  echo "Mod files successfully copied to server"
else
  echo "WARNING: Build output directory not found!"
fi

echo "Setup and build complete. Your mod is now installed in the server."
