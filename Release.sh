#!/bin/sh
set -e

##clean
rm -rf publish

export DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0

# 创建nuget临时存放目录
publishdir=publish/nuget/$(date +%Y%m%d)

mkdir $publishdir -p

publishdir=$(cd ${publishdir}; pwd)

echo "begin pack..."

echo "pack grpc extension"
dotnet pack src/Grpc.Extension/Grpc.Extension.csproj -c Release -o ${publishdir}
echo "pack grpc extension success"


# 发布到nuget.org
echo "begin push..."
for nugetfile in ${publishdir}/*; do
	echo $nugetfile
    dotnet nuget push $nugetfile -k ${followmetechNugetKey} -s https://api.nuget.org/v3/index.json
done
echo "push success"

