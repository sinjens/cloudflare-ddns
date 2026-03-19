# Cloudflare DDNS

A lightweight Blazor Server app that automatically updates Cloudflare DNS A records with your public IP address. Built for running on Unraid (or any Docker host) in bridge mode.

## Features

- Checks public IP every 10 minutes via [ipify](https://api.ipify.org)
- Updates Cloudflare A records only when the IP has changed
- Caches zone/record IDs and IPs in memory to minimize API calls
- Supports multiple domains (comma-separated)
- Web dashboard showing domain status, current IPs, and a live log

## Configuration

| Variable | Description | Default |
|---|---|---|
| `Ddns__CloudflareToken` | Cloudflare API bearer token | |
| `Ddns__Domains` | Comma-separated list of domains to update | |
| `Ddns__IntervalMinutes` | Check interval in minutes | `10` |

The Cloudflare token needs `Zone:Read` and `DNS:Edit` permissions for the relevant zones.

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
- **Network Type:** Bridge
- **Port:** 80 -> 80
- Add environment variables for `Ddns__CloudflareToken` and `Ddns__Domains`

## Local Development

```bash
# Set the Cloudflare token as a user secret
dotnet user-secrets set "Ddns:CloudflareToken" "your_token_here"

# Run
dotnet run
```

## Build

The GitHub Actions workflow (`.github/workflows/docker-publish.yml`) builds and pushes the Docker image to GHCR on every push to `main`.
