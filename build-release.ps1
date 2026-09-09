$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  Write-Host '需要 .NET 8 SDK 才能构建；普通用户运行发布后的 EXE 不需要安装 .NET。'
  exit 1
}
dotnet publish .\BaguaLiveWallpaper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\publish
Write-Host '构建完成：publish\BaguaLiveWallpaper.exe'
