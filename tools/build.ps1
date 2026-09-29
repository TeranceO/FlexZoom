param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifacts = Join-Path $taskRoot 'artifacts'
$release = Join-Path $taskRoot 'dist\FlexZoom'
$releaseExe = Join-Path $release 'FlexZoom.exe'
$releaseZip = Join-Path $taskRoot 'dist\FlexZoom-Windows-x64.zip'
$stageRoot = Join-Path $artifacts ('publish-staging\' + [Guid]::NewGuid().ToString('N'))
$stageRelease = Join-Path $stageRoot 'FlexZoom'
$stageZip = Join-Path $stageRoot 'FlexZoom-Windows-x64.zip'
$backup = Join-Path $artifacts 'previous-release'
$logPath = Join-Path $artifacts 'build.log'
$restartNeeded = $false

function Write-BuildMessage([string]$Message) {
    Write-Host $Message
    Add-Content -LiteralPath $logPath -Value $Message -Encoding UTF8
}

function Invoke-DotNet([string]$Phase, [string[]]$Arguments) {
    Write-BuildMessage "${Phase}..."
    # Preserve native stderr as log output in both Windows PowerShell 5.1 and PowerShell 7.
    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & dotnet @Arguments 2>&1 | ForEach-Object { Write-BuildMessage $_.ToString() }
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $savedPreference }
    if ($code -ne 0) { throw "$Phase failed (exit $code). See the preceding .NET error or $logPath. Your installed release was not replaced." }
}

function Get-ReleaseProcesses {
    @(Get-Process -Name FlexZoom -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and [IO.Path]::GetFullPath($_.Path) -eq $releaseExe
    })
}

function Stop-ReleaseProcesses {
    $running = @(Get-ReleaseProcesses)
    if ($running.Count -eq 0) { return }
    $script:restartNeeded = $true
    Write-BuildMessage 'Closing the running release before replacement; it will restart with your saved startup settings.'
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($releaseExe.ToUpperInvariant()))).Replace('-', '') }
    finally { $sha.Dispose() }
    $signal = $null
    $requested = [Threading.EventWaitHandle]::TryOpenExisting("Local\FlexZoom.UpdateQuit.$hash", [ref]$signal)
    if ($requested) {
        try { $signal.Set() | Out-Null } finally { $signal.Dispose() }
    }
    foreach ($process in $running) {
        if ($requested -and $process.WaitForExit(5000)) {
            Write-BuildMessage "Flex Zoom $($process.Id) exited cleanly and flushed preferences."
            continue
        }
        # Versions before 1.1.1 have no update-shutdown signal. Stop only the exact release path.
        $current = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
        if ($current -and $current.Path -and [IO.Path]::GetFullPath($current.Path) -eq $releaseExe) {
            Write-BuildMessage "Stopping older or unresponsive Flex Zoom process $($current.Id) at $releaseExe."
            Stop-Process -InputObject $current
            if (-not $current.WaitForExit(5000)) { throw "Flex Zoom did not exit. Quit it from its tray icon and retry. Log: $logPath" }
        }
    }
}

Push-Location $taskRoot
try {
    New-Item -ItemType Directory -Path $stageRelease -Force | Out-Null
    Set-Content -LiteralPath $logPath -Value "Flex Zoom build started $(Get-Date -Format o)" -Encoding UTF8
    Invoke-DotNet -Phase 'Build' -Arguments @('build', 'src/FlexZoom', '-c', 'Release', '--nologo')
    if (-not $SkipTests) {
        $check = Start-Process -FilePath "$taskRoot\src\FlexZoom\bin\Release\net9.0-windows\FlexZoom.exe" -ArgumentList '--self-test' -WorkingDirectory $taskRoot -PassThru -Wait
        Get-Content "$artifacts\self-test.txt" | ForEach-Object { Write-BuildMessage $_ }
        if ($check.ExitCode -ne 0) { throw "Self-tests failed. See $artifacts\self-test.txt. Your installed release was not replaced." }
    }
    Invoke-DotNet -Phase 'Publish' -Arguments @('publish', 'src/FlexZoom', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=None', '-o', $stageRelease, '--nologo')
    Copy-Item -LiteralPath "$taskRoot\README.md", "$taskRoot\VALIDATION.md", "$taskRoot\LICENSE" -Destination $stageRelease
    Compress-Archive -Path "$stageRelease\*" -DestinationPath $stageZip

    # Back up each file being replaced; never remove unrelated files in the release folder.
    New-Item -ItemType Directory -Path $release, $backup -Force | Out-Null
    $files = @(
        @{ Source = "$stageRelease\FlexZoom.exe"; Target = $releaseExe },
        @{ Source = "$stageRelease\README.md"; Target = "$release\README.md" },
        @{ Source = "$stageRelease\VALIDATION.md"; Target = "$release\VALIDATION.md" },
        @{ Source = "$stageRelease\LICENSE"; Target = "$release\LICENSE" },
        @{ Source = $stageZip; Target = $releaseZip }
    )
    foreach ($file in $files) {
        $file.Existed = Test-Path -LiteralPath $file.Target
        $file.Backup = Join-Path $backup ([IO.Path]::GetFileName($file.Target))
        if ($file.Existed) { Copy-Item -LiteralPath $file.Target -Destination $file.Backup -Force }
    }
    Stop-ReleaseProcesses
    $replaced = @()
    try {
        foreach ($file in $files) {
            $replaced += $file
            Copy-Item -LiteralPath $file.Source -Destination $file.Target -Force
        }
        if ((Get-FileHash -LiteralPath $releaseExe).Hash -ne (Get-FileHash -LiteralPath "$stageRelease\FlexZoom.exe").Hash) {
            throw 'The installed executable does not match the staged build.'
        }
    } catch {
        $replacementError = $_
        $restoreErrors = @()
        foreach ($file in $replaced) {
            try {
                if ($file.Existed) {
                    # A locked file may be unchanged; do not attempt an unnecessary restore.
                    if (-not (Test-Path -LiteralPath $file.Target) -or
                        (Get-FileHash -LiteralPath $file.Target).Hash -ne (Get-FileHash -LiteralPath $file.Backup).Hash) {
                        Copy-Item -LiteralPath $file.Backup -Destination $file.Target -Force
                    }
                }
                elseif (Test-Path -LiteralPath $file.Target) { Remove-Item -LiteralPath $file.Target -Force }
            } catch { $restoreErrors += "$($file.Target): $_" }
        }
        if ($restoreErrors.Count -gt 0) { throw "Release replacement failed: $replacementError Recovery requires the backup at $backup. $($restoreErrors -join '; ') Log: $logPath" }
        throw "Release replacement failed; previous files restored. $replacementError Log: $logPath"
    }
    Write-BuildMessage "Build complete: $releaseExe"
    Write-BuildMessage "Portable ZIP: $releaseZip"
    Get-FileHash -LiteralPath $releaseExe -Algorithm SHA256

    # Only remove this run's generated staging folder, after checking its resolved location.
    $resolvedStage = (Resolve-Path -LiteralPath $stageRoot).ProviderPath
    $resolvedParent = (Resolve-Path -LiteralPath (Join-Path $artifacts 'publish-staging')).ProviderPath
    if ([IO.Path]::GetDirectoryName($resolvedStage) -ne $resolvedParent -or
        ((Get-Item -LiteralPath $resolvedStage).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing to clean unexpected staging path: $resolvedStage"
    }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
} catch {
    Write-BuildMessage "Build stopped: $_"
    throw
} finally {
    try {
        if ($restartNeeded -and (Test-Path -LiteralPath $releaseExe) -and @(Get-ReleaseProcesses).Count -eq 0) {
            Write-BuildMessage 'Restarting Flex Zoom.'
            Start-Process -FilePath $releaseExe -ArgumentList '--startup' -WorkingDirectory $taskRoot -WindowStyle Hidden
        }
    } finally { Pop-Location }
}
