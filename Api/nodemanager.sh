#!/usr/bin/env bash

set -e

echo "🚀 Starting NodeManager Full Installation..."

echo "Updating and upgrading system packages..."
sudo apt-get update
sudo apt-get upgrade -y

echo "Installing dependencies (ufw, curl, git, jq)..."
sudo apt-get install -y ufw ca-certificates curl gnupg software-properties-common git jq

echo "Configuring firewall to be secure by default..."
sudo ufw allow ssh
sudo ufw allow 5050/tcp
sudo ufw default deny incoming
sudo ufw default allow outgoing
echo "y" | sudo ufw enable
echo "Firewall configured and enabled. Current status:"
sudo ufw status verbose


echo "Installing Docker from Ubuntu's default repository..."
sudo apt-get install -y docker.io docker-compose

sudo systemctl enable --now docker.service

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

echo "Building NodeManager docker image..."
sudo docker build -t nodemanager:latest -f Api/Dockerfile .

REPORTER_SCRIPT_PATH="/opt/nodemanager/usage_reporter.sh"
EASYHUB_ENDPOINT="https://easyui.samanii.com/api/instance/report"
INSTANCE_DB_PATH="/var/lib/easyhub-instance-data/instances.json"

echo "Creating usage reporter script..."
sudo tee "$REPORTER_SCRIPT_PATH" > /dev/null <<'EOF'
#!/bin/bash
INSTANCE_DB_PATH_VAR
EASYHUB_ENDPOINT_VAR
if [ ! -f "$INSTANCE_DB_PATH" ]; then exit 0; fi
IPTABLES_OUTPUT=$(sudo iptables -L DOCKER-USER -v -n -x)
INSTANCES_JSON=$(jq -c '.[] | {id: .Id, port: .InboundPort}' "$INSTANCE_DB_PATH")
JSON_BODY="{\"Usages\":["
FIRST_ITEM=true
while IFS= read -r line; do
    INSTANCE_ID=$(echo "$line" | jq '.id')
    PORT=$(echo "$line" | jq '.port')
    BYTES=$(echo "$IPTABLES_OUTPUT" | grep -E "tcp dpt:$PORT|tcp spt:$PORT" | awk '{s+=$2} END {print s}')
    BYTES=${BYTES:-0}
    if [ "$FIRST_ITEM" = false ]; then JSON_BODY="$JSON_BODY,"; fi
    JSON_BODY="$JSON_BODY{\"InstanceId\":$INSTANCE_ID, \"TotalUsageInBytes\":$BYTES}"
    FIRST_ITEM=false
done <<< "$INSTANCES_JSON"
JSON_BODY="$JSON_BODY]}"
if [ "$FIRST_ITEM" = true ]; then exit 0; fi
curl -X POST -H "Content-Type: application/json" -d "$JSON_BODY" "$EASYHUB_ENDPOINT"
EOF
sudo sed -i "s|INSTANCE_DB_PATH_VAR|INSTANCE_DB_PATH=\"$INSTANCE_DB_PATH\"|" "$REPORTER_SCRIPT_PATH"
sudo sed -i "s|EASYHUB_ENDPOINT_VAR|EASYHUB_ENDPOINT=\"$EASYHUB_ENDPOINT\"|" "$REPORTER_SCRIPT_PATH"
sudo chmod +x "$REPORTER_SCRIPT_PATH"

echo "Creating systemd timer for usage reporter..."
sudo tee /etc/systemd/system/usage-reporter.service > /dev/null <<EOF
[Unit]
Description=Usage Reporter for EasyHub NodeManager
[Service]
Type=oneshot
ExecStart=$REPORTER_SCRIPT_PATH
EOF
sudo tee /etc/systemd/system/usage-reporter.timer > /dev/null <<EOF
[Unit]
Description=Run Usage Reporter every 10 seconds
[Timer]
OnBootSec=1min
OnUnitActiveSec=10s
[Install]
WantedBy=timers.target
EOF

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
ExecStart=/usr/bin/docker run --name nodemanager -p 5050:8080 -v /var/run/docker.sock:/var/run/docker.sock -v /var/lib/easyhub-instance-data:/var/lib/easyhub-instance-data nodemanager:latest
ExecStop=-/usr/bin/docker stop nodemanager
[Install]
WantedBy=multi-user.target
EOF

echo "Reloading systemd, enabling and starting services..."
sudo systemctl daemon-reload
sudo systemctl enable --now nodemanager.service
sudo systemctl enable --now usage-reporter.timer

echo ""
echo "✅ NodeManager and Usage Reporter setup completed successfully."
echo "   Everything is running and configured to start on boot."
