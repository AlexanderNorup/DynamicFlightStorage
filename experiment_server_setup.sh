#!/bin/bash

download_url=https://github.com/AlexanderNorup/DynamicFlightStorage/releases/download/v1.2/experimentrunner.tar
username=$(whoami)
ssh_key="ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIP5h3CtfrIA6fMsjKRPdmSlVICAaqF46sBImEcPbKpMZ semon20@student.sdu.dk"

# Install Software
sudo apt update
sudo apt install -y cmake dotnet-runtime-8.0

# Download and extract the experiment runner
mkdir -p ~/experimentrunner
cd ~/experimentrunner
wget $download_url
tar -xvf experimentrunner.tar
rm experimentrunner.tar

# Add user to docker group
sudo usermod -aG docker $username

# Add SSH key to authorized keys
echo $ssh_key >> ~/.ssh/authorized_keys

# Download pre-filled config file from trusted server (will prompt for auth)
scp alex@hosting.alexandernorup.com:~/dynamicflightstorage/appsettings.json ~/experimentrunner/appsettings.json

echo "Startup script complete. Please log out and back in to apply group changes."
