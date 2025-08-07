# Elephant Gun Discord Bot

A Discord bot that reacts with 🐘 and 🔫 emojis when a specific user sends a message or when any message contains the word "elephant".

## Prerequisites

- .NET 8.0 SDK
- Docker (optional, for containerized deployment)
- Discord Bot Token

## Setup

1. **Create a Discord Bot Application**

   - Go to [Discord Developer Portal](https://discord.com/developers/applications)
   - Create a new application
   - Go to the "Bot" section and create a bot
   - Copy the bot token

2. **Set Environment Variables**
   ```bash
   export DISCORD_TOKEN="your-bot-token-here"
   ```

## Running Locally

1. **Restore dependencies**

   ```bash
   dotnet restore
   ```

2. **Build the application**

   ```bash
   dotnet build
   ```

3. **Run the bot**
   ```bash
   dotnet run
   ```

## Running with Docker

1. **Build the Docker image**

   ```bash
   docker build -t elephant-gun .
   ```

2. **Run the container**
   ```bash
   docker run -e DISCORD_TOKEN="your-bot-token-here" elephant-gun
   ```

## GitHub Actions CI/CD

This repository includes GitHub Actions workflows for automated Docker image building and publishing:

### Workflows

1. **Build and Push Docker Image** (`.github/workflows/docker-build.yml`)

   - Triggers on pushes to `main`/`master` branches and tags
   - Builds and pushes Docker image to GitHub Container Registry (ghcr.io)
   - Uses semantic versioning for tags
   - Includes caching for faster builds

2. **Test Docker Build** (`.github/workflows/docker-build-test.yml`)
   - Triggers on pull requests
   - Tests Docker build without pushing
   - Ensures builds work before merging

### Usage

- **Automatic builds**: Every push to main/master will trigger a build and push to ghcr.io
- **Tagged releases**: Push a tag (e.g., `v1.0.0`) to create a versioned release
- **Pull request testing**: All PRs will test the Docker build

### Image Location

Built images are available at: `ghcr.io/{your-username}/elephant-gun`

### Pulling the Image

```bash
docker pull ghcr.io/{your-username}/elephant-gun:latest
```

## Features

- Reacts with 🐘 and 🔫 emojis when:
  - A specific user (ID: 646903718641664020) sends any message
  - Any message contains the word "elephant" (case-insensitive)
- Ignores messages from other bots
- Logs connection status and errors

## Configuration

You can modify the `TARGET_USER_ID` constant in `Program.cs` to watch for a different user.

## Requirements

- Discord Bot with the following permissions:
  - Read Messages
  - Send Messages
  - Add Reactions
  - Use Slash Commands (if needed in the future)

## Intents Required

The bot requires the following Gateway Intents:

- Guilds
- Guild Messages
- Message Content
