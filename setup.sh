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

docker compose up -d builder
# Copy the built websocket-sharp DLL to the _data directory
docker compose cp builder:/lib/websocket-sharp.dll _data/7dtd-binaries/websocket-sharp.dll

# Create packages directory if it doesn't exist
mkdir -p ./packages
# Copy Newtonsoft.Json from the builder container
echo "Copying Newtonsoft.Json from builder container..."
docker compose cp builder:/lib/Newtonsoft.Json.13.0.1 _data/7dtd-binaries/Newtonsoft.Json.13.0.1

# Run the build process inside the builder container
echo "Starting build process in builder container..."
docker compose run --rm builder /bin/bash -c "msbuild Takaro.sln /p:Configuration=Release"

# Copy the built files to the server mods directory
echo "Copying mod files to server mods directory..."
if [ -d ./_data/build/Mods/Takaro ]; then
  mkdir -p ./_data/ServerFiles/Mods/Takaro
  cp -r ./_data/build/Mods/Takaro/* ./_data/ServerFiles/Mods/Takaro/
  echo "Mod files successfully copied to server"
else
  echo "WARNING: Build output directory not found!"
fi

echo "Setup and build complete. Your mod is now installed in the server."
