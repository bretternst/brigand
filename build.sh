dotnet build -c Release

mkdir -p build/Modules

cp -f BrigandCmd/bin/Release/netcoreapp3.1/* build/
