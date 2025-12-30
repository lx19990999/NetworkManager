@echo off
echo 正在构建 NetworkManager 项目...

REM 恢复 NuGet 包
echo 恢复依赖...
dotnet restore SysManager/SysManager.csproj

REM 构建项目
echo 构建项目...
dotnet build SysManager/SysManager.csproj --configuration Release --no-restore

REM 发布为单文件应用
echo 发布为单文件应用...
dotnet publish SysManager/SysManager.csproj --configuration Release --output ./publish --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=false

echo.
echo 构建完成！
echo 可执行文件位于 ./publish/SysManager.exe
pause