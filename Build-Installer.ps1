[CmdletBinding()]
param(
	[ValidateSet("Debug", "Release")]
	[string]$Configuration = "Debug",

	[switch]$RunTests
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$vswhereCandidates = @(
	(Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"),
	(Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe")
)
$vswhere = $vswhereCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $vswhere) { throw "Visual Studio Installer discovery was not found." }
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
	Select-Object -First 1
if ([String]::IsNullOrWhiteSpace($msbuild)) { throw "MSBuild was not found." }

[xml]$reduxProject = Get-Content -LiteralPath (Join-Path $repositoryRoot "src\GUI\GUI.csproj") -Raw
[xml]$installerProject = Get-Content -LiteralPath (Join-Path $repositoryRoot "src\Installer\ReduxInstaller.csproj") -Raw
$reduxVersion = ([string]$reduxProject.Project.PropertyGroup.InformationalVersion | Select-Object -First 1).Trim()
$installerVersion = ([string]$installerProject.Project.PropertyGroup.InformationalVersion | Select-Object -First 1).Trim()
if ($reduxVersion -ne $installerVersion)
{
	throw "Redux ($reduxVersion) and Setup ($installerVersion) versions do not agree."
}

$installer = Join-Path $repositoryRoot "src\Installer\ReduxInstaller.csproj"
Write-Host "Building standalone Redux Setup $Configuration x64"
& $msbuild $installer /restore /t:Rebuild "/p:Configuration=$Configuration" /p:Platform=x64 /v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$builtSetup = Join-Path $repositoryRoot "bin\InstallerPayload\$Configuration\BG3ModManager-Redux-Setup.exe"
if (-not (Test-Path -LiteralPath $builtSetup)) { throw "The standalone Setup executable was not created." }
& $builtSetup --self-test
if ($LASTEXITCODE -ne 0) { throw "The standalone Setup executable failed its embedded-dependency check." }

if ($RunTests)
{
	$tests = Join-Path $repositoryRoot "tests\Redux.Installer.Tests\Redux.Installer.Tests.csproj"
	& $msbuild $tests /restore /t:Rebuild "/p:Configuration=$Configuration" /p:Platform=x64 /v:minimal
	if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
	$testExecutable = Join-Path $repositoryRoot "tests\Redux.Installer.Tests\bin\x64\$Configuration\net48\Redux.Installer.Tests.exe"
	& $testExecutable
	if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if ($Configuration -eq "Release")
{
	$artifactDirectory = Join-Path $repositoryRoot "artifacts\installer"
	New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
	$artifact = Join-Path $artifactDirectory "BG3ModManager-Redux-Setup.exe"
	Copy-Item -LiteralPath $builtSetup -Destination $artifact -Force
	$info = Get-Item -LiteralPath $artifact
	if ($info.Length -gt 10MB) { throw "The web bootstrap Setup artifact exceeds the 10 MiB size budget." }
	$hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
	Write-Host "Created $($info.Name) ($($info.Length) bytes, SHA-256 $hash)."
}
