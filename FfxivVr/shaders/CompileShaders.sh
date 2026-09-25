#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

docker run --rm -v "$SCRIPT_DIR:/fxc/shaders" -w /fxc gwihlidal/fxc /Fo shaders/VertexShader.cso /T vs_5_0 shaders/VertexShader.hlsl
docker run --rm -v "$SCRIPT_DIR:/fxc/shaders" -w /fxc gwihlidal/fxc /Fo shaders/PixelShader.cso /T ps_5_0 shaders/PixelShader.hlsl
