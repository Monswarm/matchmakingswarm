#!/bin/sh
# Regenerates the C# classes from Protos/ using the protoc shipped in Grpc.Tools.
# Run "dotnet restore" first so the package is in the nuget cache.
set -e

cd "$(dirname "$0")"

TOOLS_VERSION=1.16.0
NUGET_PACKAGES=${NUGET_PACKAGES:-$HOME/.nuget/packages}
TOOLS=$NUGET_PACKAGES/grpc.tools/$TOOLS_VERSION/tools

case "$(uname -s)" in
  Darwin) PLATFORM=macosx_x64 ;;
  *) PLATFORM=linux_x64 ;;
esac

chmod +x "$TOOLS/$PLATFORM/protoc" "$TOOLS/$PLATFORM/grpc_csharp_plugin"

"$TOOLS/$PLATFORM/protoc" \
  --proto_path=Protos \
  --csharp_out=Generated \
  --grpc_out=Generated \
  --plugin=protoc-gen-grpc="$TOOLS/$PLATFORM/grpc_csharp_plugin" \
  Protos/matchmaking.proto
