#!/bin/sh
# 밸런스 시뮬레이션: Core를 다시 빌드하고 시뮬레이터에 복사한 뒤 돌린다. 사용: sh Tools/BalanceSim/run.sh combat|econ [시즌 수]
set -e
P="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$P"
dotnet build Colosseum.Core.csproj -nologo -v q > /dev/null
NJ="$(ls -d "$P"/Library/PackageCache/com.unity.nuget.newtonsoft-json@*/Runtime | head -1)"
dotnet build Tools/BalanceSim/sim.csproj -nologo -v q -p:NewtonsoftDir="$NJ" > /dev/null
dotnet Tools/BalanceSim/bin/Debug/net10.0/sim.dll "$@"
