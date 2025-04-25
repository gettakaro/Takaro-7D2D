FROM mono:latest

WORKDIR /app

# Install dependencies
RUN apt-get update && \
  apt-get install -y nuget && \
  apt-get clean && \
  rm -rf /var/lib/apt/lists/*

# Create the 7dtd-binaries directory
RUN mkdir -p /app/7dtd-binaries

# The build process will be handled by the command in docker-compose
CMD ["bash", "-c", "echo 'Waiting for command...' && tail -f /dev/null"]