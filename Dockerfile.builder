FROM mono:latest

WORKDIR /app

# Install dependencies
RUN apt-get update && \
  apt-get install -y nuget curl unzip git && \
  apt-get clean && \
  rm -rf /var/lib/apt/lists/*

# Create the 7dtd-binaries directory
RUN mkdir -p /app/7dtd-binaries
RUN mkdir -p /app/lib
RUN mkdir -p /app/packages

# Clone and build websocket-sharp
# Have to build from source because there is no precompiled version
# The NuGest package is wildly out of date (2016) while the git repo still gets updates
RUN git clone https://github.com/sta/websocket-sharp.git /tmp/websocket-sharp && \
  cd /tmp/websocket-sharp && \
  xbuild /p:Configuration=Release websocket-sharp.sln && \
  cp /tmp/websocket-sharp/Example/bin/Release/websocket-sharp.dll /lib/

# Install required dependencies
RUN nuget install Newtonsoft.Json -Version 13.0.1 -OutputDirectory /lib/

# The build process will be handled by the command in docker-compose
CMD ["bash", "-c", "echo 'Waiting for command...' && tail -f /dev/null"]