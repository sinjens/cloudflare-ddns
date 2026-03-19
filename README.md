# Cloudflare DDNS

A lightweight Blazor Server app that automatically updates Cloudflare DNS A records with your public IP address. Built for running on Unraid (or any Docker host).

## Features

- Checks public IP every 10 minutes (default setting) via [ipify](https://api.ipify.org)
- Reads A records from the configured Domains in Cloudflare, caches the result in memory
- Compares the A records IP addresses with the public IP and updates Clodflare if the public IP have changed
- Supports multiple domains (comma or semicolon-separated) if you are serving multiple domains
- Web dashboard showing domain status, current IPs, and a log

## Configuration

| Variable | Description | Default |
|---|---|---|
| `Ddns:CloudflareToken` | Cloudflare API bearer token | |
| `Ddns:Domains` | Comma or semicolon-separated list of domains to update | |
| `Ddns:IntervalMinutes` | Check interval in minutes | `10` |

### Creating a Cloudflare API Token

1. Go to https://dash.cloudflare.com/<youraccountid>/api-tokens
2. Click **Create Token**
3. Use the **Edit zone DNS** template or create a custom token with `Zone:Read` and `DNS:Edit` permissions for the relevant zones

<img src="docs/cloudflaretoken.png">

## Docker

### Pull from GHCR

```bash
docker pull ghcr.io/sinjens/cloudflareddns:latest
```

### Run

```bash
docker run -d \
  --name cloudflareddns \
  -e Ddns__CloudflareToken=your_token_here \
  -e Ddns__Domains=example.com,sub.example.com \
  -p 80:80 \
  ghcr.io/sinjens/cloudflareddns:latest
```

### Unraid

Add as a Docker container in bridge mode:
- **Repository:** `ghcr.io/sinjens/cloudflareddns:latest`
- Add environment variables for `Ddns:CloudflareToken` and `Ddns:Domains`

<img src="docs/dockerunraid.png">

- The container will default to port 80, you can override it with adding this variable: ASPNETCORE_URLS=http://+:8080  (setting it to port 8080)

## Local Testing/Development

```bash
# Set the Cloudflare token as a user secret
dotnet user-secrets set "Ddns:CloudflareToken" "your_token_here"

# Run
dotnet run
```

## Build

The GitHub Actions workflow (`.github/workflows/docker-publish.yml`) builds and pushes the Docker image to GHCR on every push to `master`.
