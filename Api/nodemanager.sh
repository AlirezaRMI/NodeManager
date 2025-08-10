#!/usr/bin/env bash

set -e

echo "🚀 Starting NodeManager Full Installation..."

# --- 1. System Update & Upgrade ---
echo "Updating and upgrading system packages..."
sudo apt-get update
sudo apt-get upgrade -y

# --- 2. Install Dependencies ---
echo "Installing dependencies (ufw, curl, git, jq)..."
sudo apt-get install -y ufw ca-certificates curl gnupg software-properties-common git jq

# --- 3. Configure Firewall (UFW) ---
echo "Configuring firewall to be secure by default..."
sudo ufw allow ssh
sudo ufw allow 5050/tcp
sudo ufw default deny incoming
sudo ufw default allow outgoing
echo "y" | sudo ufw enable
echo "Firewall configured and enabled. Current status:"
sudo ufw status verbose

# --- 4. Install Docker (from Ubuntu Repo) ---
echo "Installing Docker from Ubuntu's default repository..."
sudo apt-get install -y docker.io docker-compose
sudo systemctl enable --now docker.service

# --- 5. Clone/Update NodeManager Repo ---
REPO_URL="https://github.com/AlirezaRMI/NodeManager.git"
INSTALL_DIR="/opt/nodemanager"
if [ ! -d "$INSTALL_DIR" ]; then
    echo "Cloning NodeManager repository..."
    sudo git clone "$REPO_URL" "$INSTALL_DIR"
else
    echo "Updating NodeManager repository..."
    sudo git -C "$INSTALL_DIR" pull
fi
cd "$INSTALL_DIR"

# --- 6. Build NodeManager Image ---
echo "Building NodeManager docker image..."
sudo docker build -t nodemanager:latest -f Api/Dockerfile .

# --- 7. Create and Enable NodeManager Service ---
echo "Creating systemd service for NodeManager..."
sudo tee /etc/systemd/system/nodemanager.service > /dev/null <<EOF
[Unit]
Description=NodeManager Docker Container
After=docker.service
Requires=docker.service
[Service]
Restart=always
RestartSec=10s
ExecStartPre=-/usr/bin/docker stop nodemanager
ExecStartPre=-/usr/bin/docker rm nodemanager
ExecStart=/usr/bin/docker run --name nodemanager --privileged -p 5050:8080 -v /var/run/docker.sock:/var/run/docker.sock -v /var/lib/easyhub-instance-data:/var/lib/easyhub-instance-data nodemanager:latest
ExecStop=-/usr/bin/docker stop nodemanager
[Install]
WantedBy=multi-user.target
EOF

# --- 8. Start The Service ---
echo "Reloading systemd, enabling and starting NodeManager service..."
sudo systemctl daemon-reload
sudo systemctl enable --now nodemanager.service

echo ""
echo "✅ NodeManager installation completed successfully."
echo "   Everything is running and configured to start on boot."