$ErrorActionPreference = "Stop"

$solution = Join-Path $PSScriptRoot "..\\apps\\windows\\TruckNav.Connector.sln"
$publishDir = Join-Path $PSScriptRoot "..\\artifacts\\windows\\publish"

dotnet restore $solution
dotnet build $solution -c Release --no-restore
dotnet test $solution -c Release --no-build
dotnet publish (Join-Path $PSScriptRoot "..\\apps\\windows\\src\\TruckNav.Connector.App\\TruckNav.Connector.App.csproj") -c Release -o $publishDir

