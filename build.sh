#!/bin/bash
dotnet build -c Release
mkdir -p build/Modules
cp -rf BrigandCmd/bin/Release/net6.0/* build/
