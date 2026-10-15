$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
dotnet publish "$root\src\SuperRename.csproj" -c Release -o "$root\dist"
Get-ChildItem "$root\dist" -Exclude "NouhinCollector.exe" | Remove-Item -Recurse -Force
Write-Host "=> $root\dist\NouhinCollector.exe"
