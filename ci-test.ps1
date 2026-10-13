param(
  [Parameter(Mandatory, Position = 0)]
  [string]$os,
  [Parameter(Mandatory, Position = 1)]
  [string]$targetFramework,
  [Parameter(Mandatory, Position = 2)]
  [string]$platform,
  [Parameter(Mandatory, Position = 3)]
  [string]$codecov,
  [Parameter(Position = 4)]
  [string]$codecovProfile = 'Release'
)

$netFxRegex = '^net\d+$'

if ($codecov -eq 'true') {

  # Allow toggling of profile to workaround any potential JIT errors caused by code injection.
  dotnet clean -c $codecovProfile
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  dotnet test --collect "XPlat Code Coverage" --settings .\tests\coverlet.runsettings -c $codecovProfile -f $targetFramework /p:CodeCov=true
}
elseif ($platform -eq '-x86' -and $targetFramework -match $netFxRegex) {

  # Run the existing AnyCPU assembly in an x86 VSTest host. The legacy dotnet-xunit
  # CLI requires .NET Core 2.0 and cannot run with the SDKs installed by CI.
  dotnet test --no-build -c Release -f $targetFramework -- RunConfiguration.TargetPlatform=x86
}
else {

  dotnet test --no-build -c Release -f $targetFramework
}

exit $LASTEXITCODE
