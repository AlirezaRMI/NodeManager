#!/usr/bin/env bash
set -e

REPO_URL="https://github.com/AlirezaRMI/NodeManager.git"
INSTALL_DIR="/opt/nodemanager"
DATA_DIR="/var/lib/marzban-node"      
DOCKER_SOCK="/var/run/docker.sock"

if ! command -v docker &>/dev/null; then
  echo "🔧 Docker not found. Installing..."
  sudo apt-get update
  sudo apt-get install -y ca-certificates curl gnupg software-properties-common
  sudo install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
  echo \
    "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
    $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
    sudo tee /etc/apt/sources.list.d/docker.list >/dev/null
  sudo apt-get update
  sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi
sudo systemctl enable docker.service

if ! command -v git &>/dev/null; then
  echo "🔧 Git not found. Installing..."
  sudo apt-get update
  sudo apt-get install -y git
fi

sudo mkdir -p "$DATA_DIR"
sudo chown 1000:1000 "$DATA_DIR"

if [ ! -d "$INSTALL_DIR" ]; then
  echo "⬇️  Cloning NodeManager..."
  sudo git clone "$REPO_URL" "$INSTALL_DIR"
else
  echo "🔄 Pulling latest NodeManager changes..."
  sudo git -C "$INSTALL_DIR" pull
fi

cd "$INSTALL_DIR"


if [ -f "compose.yml" ]; then
  echo "📦 Running docker compose for NodeManager stack…"
  sudo docker compose up -d
else
  echo "🛠  Building NodeManager image…"
  sudo docker build -t nodemanager:latest .


  if sudo docker ps -a --format '{{.Names}}' | grep -qw nodemanager; then
    sudo docker rm -f nodemanager || true
  fi

  echo "🚀 Starting NodeManager container…"
  sudo docker run -d --name nodemanager --restart=always \
    -p 5000:5000 -p 5001:5001 \
    -v "$DOCKER_SOCK":"$DOCKER_SOCK" \
    nodemanager:latest
fi

if ! sudo docker ps -a --format '{{.Names}}' | grep -qw marzban-node; then
  echo "🚀 Starting Marzban-Node container…"
  sudo docker run -d --name marzban-node --restart=always \
    -p 8444:8444 \
    -v "$DATA_DIR":/var/lib/marzban-node \
    gozargah/marzban-node:latest
fi

SERVICE_FILE="/etc/systemd/system/nodemanager.service"
sudo tee "$SERVICE_FILE" >/dev/null <<EOF
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

echo "✅  NodeManager & Marzban-Node deployed successfully."
echo "   • NodeManager API:  http://<host>:5000/"
echo "   • Marzban-Node:     https://<host>:8444/"
echo "👉  Check status with: sudo systemctl status nodemanager"
