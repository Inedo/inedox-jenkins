@echo off

dotnet new tool-manifest --force
dotnet tool install inedo.extensionpackager

cd Jenkins\InedoExtension
dotnet inedoxpack pack . C:\LocalDev\BuildMaster\Extensions\Jenkins.upack --build=Debug -o
cd ..\..