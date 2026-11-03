#!/bin/sh
# Unity 에디터 없이 EditMode 테스트를 돌린다. 먼저 테스트 어셈블리를 컴파일한 뒤 리플렉션으로 [Test]를 부른다.
# 사용: sh ColosseumUnity/Tools/TestRunner/run.sh   (저장소 루트에서)
set -e
P="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$P" && dotnet build Colosseum.Tests.csproj -nologo -v q
NUNIT="$(ls -d "$P"/Library/PackageCache/com.unity.ext.nunit@*/net40/unity-custom | head -1)"
NJ="$(ls -d "$P"/Library/PackageCache/com.unity.nuget.newtonsoft-json@*/Runtime | head -1)"
UE="C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Data/Managed/UnityEngine"
cd "$P/Tools/TestRunner" && dotnet run -v q -- "$P" "$NUNIT" "$NJ" "$UE"
