#!/usr/bin/env bash
# Shell environment for GenoCRM development on this machine.
#
# Source it from your shell profile once:
#   echo 'source ~/dev/GenoCRM/scripts/dev-env.sh' >> ~/.zshrc
# or ad hoc in a terminal:
#   source scripts/dev-env.sh
#
# - .NET SDK installed in user space via dotnet-install.sh (~/.dotnet)
# - dotnet global tools (dotnet-ef) in ~/.dotnet/tools
# - rootless Podman exposed as a Docker API socket, so `docker`, `docker compose`
#   and Rider's Docker plugin all talk to Podman.
# - ~/.local/bin holds `rider` and `docker-compose` (compose v2 binary).

export DOTNET_ROOT="$HOME/.dotnet"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
case ":$PATH:" in
  *":$DOTNET_ROOT:"*) ;;
  *) export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH" ;;
esac
case ":$PATH:" in
  *":$HOME/.local/bin:"*) ;;
  *) export PATH="$HOME/.local/bin:$PATH" ;;
esac

_podman_sock="/run/user/$(id -u)/podman/podman.sock"
if [ -z "${DOCKER_HOST:-}" ] && [ -S "$_podman_sock" ]; then
  export DOCKER_HOST="unix://$_podman_sock"
fi
unset _podman_sock
