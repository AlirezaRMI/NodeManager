#!/usr/bin/env bash

set -e

if ! command -v docker &> /dev/null; then
    echo "Docker not found. Installing Docker..."
    sudo apt-get update
    sudo apt-get install -y ca-certificates curl gnupg software-properties-common
    sudo install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
    echo \
      "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
      $(. /etc/os-release && echo \"$VERSION_CODENAME\") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
    sudo apt-get update
    sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi

sudo systemctl enable docker.service

if ! command -v git &> /dev/null; then
    echo "Git not found. Installing git..."
    sudo apt-get update
    sudo apt-get install -y git
fi

REPO_URL="https://github.com/AlirezaRMI/NodeManager.git"
INSTALL_DIR="/opt/nodemanager"
if [ ! -d "$INSTALL_DIR" ]; then
    echo "Cloning NodeManager repository from GitHub..."
    sudo git clone "$REPO_URL" "$INSTALL_DIR"
    cd "$INSTALL_DIR/Api"
else
    echo "Updating existing NodeManager repository..."
    sudo git -C "$INSTALL_DIR" pull
fi

cd "$INSTALL_DIR"
if [ -f "compose.yml" ]; then
    echo "compose.yml found. Bringing up services with Docker Compose..."
    sudo docker compose up -d --build
else
    echo "No compose.yml. Building Docker image..."
    sudo docker build -t nodemanager:latest .
    
    if sudo docker ps -a --format '{{.Names}}' | grep -qw nodemanager; then
        echo "Stopping and removing existing container..."
        sudo docker stop nodemanager || true
        sudo docker rm nodemanager || true
    fi
    echo "Running NodeManager container..."
    sudo docker run -d --name nodemanager --restart=always -p 5050:5050 -p 5252:5252 nodemanager:latest
fi

SERVICE_FILE="/etc/systemd/system/nodemanager.service"
echo "Creating systemd service file at $SERVICE_FILE..."
sudo tee "$SERVICE_FILE" > /dev/null << EOF
[Unit]
Description=NodeManager Docker Container
After=docker.service
Requires=docker.service

[Service]
Restart=always

ExecStartPre=-/usr/bin/docker rm -f nodemanager
ExecStart=/usr/bin/docker start -a nodemanager
ExecStop=/usr/bin/docker stop -t 10 nodemanager

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable nodemanager.service
sudo systemctl restart nodemanager.service

echo "NodeManager installation and setup completed successfully."
echo "Service is running and enabled to start on boot. Use 'systemctl status nodemanager' to check status."
