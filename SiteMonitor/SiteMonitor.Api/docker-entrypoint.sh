#!/bin/sh
set -e

PORT_VALUE="${PORT:-8080}"
export ASPNETCORE_URLS="http://+:${PORT_VALUE}"

exec dotnet SiteMonitor.Api.dll
