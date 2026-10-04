param([string]$Tag)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
Push-Location $taskRoot
try {
    [xml]$project = Get-Content -LiteralPath 'src/FlexZoom/FlexZoom.csproj' -Raw
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Release versions must use major.minor.patch.' }
    if ($Tag -and $Tag -cne "v$version") { throw "Tag $Tag does not match project version v$version." }
    dotnet build src/FlexZoom -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $check = Start-Process -FilePath "$taskRoot/src/FlexZoom/bin/Release/net9.0-windows/FlexZoom.exe" -ArgumentList '--self-test-headless' -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru -Wait
    Get-Content -LiteralPath 'artifacts/headless-self-test.txt'
    if ($check.ExitCode -ne 0) { throw 'Headless checks failed.' }
    # CI never replaces or restarts a local installed app.
    $output = Join-Path $taskRoot 'artifacts/unsigned'
    if (Test-Path -LiteralPath $output) { throw 'Build output already exists. Use a fresh checkout or preserve it before retrying.' }
    dotnet publish src/FlexZoom -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $output --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    $metadata = (Get-Item -LiteralPath "$output/FlexZoom.exe").VersionInfo
    if ($metadata.ProductName -cne 'Flex Zoom' -or $metadata.ProductVersion -cne $version -or $metadata.OriginalFilename -cne 'FlexZoom.dll') {
        throw 'Executable metadata does not match the SignPath artifact configuration.'
    }
    Copy-Item -LiteralPath 'README.md', 'VALIDATION.md', 'LICENSE', 'CODE_SIGNING.md', 'SIGNING.md' -Destination $output
    if ($env:GITHUB_OUTPUT) { Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "version=$version" -Encoding utf8 }
    Write-Output "Unsigned CI candidate: $output/FlexZoom.exe ($version). Not a public release."
} finally { Pop-Location }
