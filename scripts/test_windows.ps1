$ErrorActionPreference = "Stop"

$solution = Join-Path $PSScriptRoot "..\\apps\\windows\\TruckNav.Connector.sln"

dotnet restore $solution
dotnet build $solution -c Debug --no-restore
dotnet test $solution -c Debug --no-build

