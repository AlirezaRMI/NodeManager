#!/usr/bin/env bash
set -e

install_package() {
  if ! command -v "$1" &>/dev/null; then
    echo "Installing $1 ..."
    sudo apt-get update -y
    sudo apt-get install -y "$2"
  fi
}

install_package docker "ca-certificates curl gnupg software-properties-common docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin"
install_package git git

sudo systemctl enable --now docker

REPO_URL="https://github.com/AlirezaRMI/NodeManager.git"
INSTALL_DIR="/opt/nodemanager"

if [ ! -d "$INSTALL_DIR" ]; then
  sudo git clone "$REPO_URL" "$INSTALL_DIR"
else
  sudo git -C "$INSTALL_DIR" pull --ff-only
fi


cd "$INSTALL_DIR"

export NM_API_PORT=5000
export NM_GRPC_PORT=5001

sudo docker compose pull  
sudo docker compose up -d --build

SERVICE_FILE="/etc/systemd/system/nodemanager.service"

sudo tee "$SERVICE_FILE" >/dev/null <<EOF
[Unit]
Description=NodeManager (docker compose)
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=$INSTALL_DIR
ExecStart=/usr/bin/docker compose up -d
ExecStop=/usr/bin/docker compose down
TimeoutStartSec=0

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable nodemanager.service
sudo systemctl restart nodemanager.service

echo "✅  NodeManager systemctl status nodemanager "
