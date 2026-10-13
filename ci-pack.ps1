$project = Join-Path $PSScriptRoot 'src/ImageSharp/ImageSharp.csproj'
dotnet clean $project -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$repositoryUrl = "https://github.com/$env:GITHUB_REPOSITORY"

# Building for packing and publishing.
# Pack only the library: solution-wide packing also restores Windows-only profiling tools.
dotnet pack $project -c Release --output "$PSScriptRoot/artifacts" /p:RepositoryUrl=$repositoryUrl
exit $LASTEXITCODE
