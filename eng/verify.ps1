[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",
    [switch] $NoRestore
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "Driftya.SpriteAtlasForge.slnx"
$coverageDirectory = Join-Path $repositoryRoot ".artifacts\coverage"

if (-not $NoRestore) {
    & dotnet restore $solutionPath --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

& dotnet build $solutionPath --configuration $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$suites = @(
    @{
        Name = "Domain"
        Project = "tests\Driftya.SpriteAtlasForge.Domain.Tests\Driftya.SpriteAtlasForge.Domain.Tests.csproj"
        Package = "Driftya.SpriteAtlasForge.Domain"
        MinimumLineCoverage = 80
    },
    @{
        Name = "Application"
        Project = "tests\Driftya.SpriteAtlasForge.Application.Tests\Driftya.SpriteAtlasForge.Application.Tests.csproj"
        Package = "Driftya.SpriteAtlasForge.Application"
        MinimumLineCoverage = 75
    },
    @{
        Name = "Infrastructure"
        Project = "tests\Driftya.SpriteAtlasForge.Infrastructure.Tests\Driftya.SpriteAtlasForge.Infrastructure.Tests.csproj"
        Package = "Driftya.SpriteAtlasForge.Infrastructure"
        MinimumLineCoverage = 80
    },
    @{
        Name = "CliApplication"
        Project = "tests\Driftya.SpriteAtlasForge.CliApplication.Tests\Driftya.SpriteAtlasForge.CliApplication.Tests.csproj"
        Package = "Driftya.SpriteAtlasForge.CliApplication"
        MinimumLineCoverage = 70
    },
    @{
        Name = "ClientApplication"
        Project = "tests\Driftya.SpriteAtlasForge.ClientApplication.Tests\Driftya.SpriteAtlasForge.ClientApplication.Tests.csproj"
        Package = "Driftya.SpriteAtlasForge.ClientApplication.Tests"
        MinimumLineCoverage = 80
        Settings = "tests\Driftya.SpriteAtlasForge.ClientApplication.Tests\coverage.runsettings"
    }
)

New-Item -ItemType Directory -Force -Path $coverageDirectory | Out-Null
$coverageResults = @()

foreach ($suite in $suites) {
    $projectPath = Join-Path $repositoryRoot $suite.Project
    $coveragePath = Join-Path $coverageDirectory "$($suite.Name).cobertura.xml"
    $arguments = @(
        "run",
        "--project", $projectPath,
        "--configuration", $Configuration,
        "--no-build",
        "--no-restore",
        "--coverage",
        "--coverage-output", $coveragePath,
        "--coverage-output-format", "cobertura",
        "--disable-logo"
    )

    if ($suite.Settings) {
        $arguments += @("--coverage-settings", (Join-Path $repositoryRoot $suite.Settings))
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    [xml] $coverage = Get-Content -Raw $coveragePath
    $package = $coverage.coverage.packages.package |
        Where-Object { $_.name -eq $suite.Package } |
        Select-Object -First 1
    if ($null -eq $package) {
        throw "Coverage package '$($suite.Package)' was not found in $coveragePath."
    }

    $lineCoverage = [Math]::Round([double] $package.'line-rate' * 100, 1)
    $coverageResults += [pscustomobject]@{
        Project = $suite.Name
        LineCoverage = $lineCoverage
        Required = $suite.MinimumLineCoverage
        Passed = $lineCoverage -ge $suite.MinimumLineCoverage
    }
}

$coverageResults | Format-Table -AutoSize
$failedCoverage = $coverageResults | Where-Object { -not $_.Passed }
if ($failedCoverage) {
    throw "One or more production projects are below their line-coverage threshold."
}
