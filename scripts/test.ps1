param(
  [switch]$Regen
)

$ErrorActionPreference = "Stop"

if ($Regen) {
  dotnet test --filter "Category=Regen"
} else {
  dotnet test --filter "Category!=Regen"
}
