# Windows PowerShell 5.1. All checks run before target files are changed.
[CmdletBinding()]
param(
    [ValidateSet('Apply','Verify','Restore')][string]$Action = 'Apply',
    [string]$ModPath,
    [string]$BackupPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$originalXml = '15C5877D6D905713A36E10DFE5B57A6A28FCD5303F4106ABA21A1E6A4357E3DE'
$patchedXml = '61EC909F6794EF0FC10907F56B2A4092A91AD8577E514029BA9CAAFA94DA9483'
$shimHash = '526E7B915EE87B6C18DD8B94D2508C7A2C4BC7A8E4CF433139C3192DAA7CED0A'
$shimRelative = 'bin\Win64_Shipping_Client\MixMashBannerlord148Compatibility.dll'
function Assert-Hash([string]$Path, [string]$Expected) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required file missing: $Path" }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected) {
        throw "SHA-256 mismatch; no changes authorized for: $Path"
    }
}
function Assert-NoLink([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force
    while ($null -ne $item) {
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Linked/junction paths are not supported: $($item.FullName)"
        }
        if ($item -is [IO.FileInfo]) { $item = $item.Directory } else { $item = $item.Parent }
    }
}
try {
    if (!$ModPath) { $ModPath = Read-Host 'Enter the full path of your MixMashMagicArmorPack folder' }
    $ModPath = (Get-Item -LiteralPath $ModPath -Force).FullName.TrimEnd('\')
    if (!(Test-Path -LiteralPath $ModPath -PathType Container)) { throw 'Select a mod folder.' }
    Assert-NoLink $ModPath
    $xmlPath = Join-Path $ModPath 'SubModule.xml'
    $shimPath = Join-Path $ModPath $shimRelative
    Assert-NoLink $xmlPath
    Assert-NoLink (Split-Path -Parent $shimPath)
    if ($Action -ne 'Verify' -and @(Get-Process -Name 'Bannerlord','Bannerlord.Native' -ErrorAction SilentlyContinue).Count -gt 0) {
        throw 'Close Bannerlord before applying or restoring this patch.'
    }
    if ($Action -eq 'Restore') {
        if (!$BackupPath) { $BackupPath = Read-Host 'Enter the backup folder printed when the patch was applied' }
        $BackupPath = (Get-Item -LiteralPath $BackupPath -Force).FullName
        Assert-NoLink $BackupPath
        $savedXml = Join-Path $BackupPath 'SubModule.xml'
        Assert-NoLink $savedXml
        Assert-Hash $savedXml $originalXml
        Assert-Hash $xmlPath $patchedXml
        Assert-Hash $shimPath $shimHash
        Assert-NoLink $shimPath
        # The only removed file is the exact compatibility DLL added by this patch.
        $restoreStage = Join-Path $BackupPath ('restore-' + [Guid]::NewGuid().ToString('N') + '.tmp')
        [IO.File]::Copy($savedXml, $restoreStage, $false)
        [IO.File]::Replace($restoreStage, $xmlPath, [NullString]::Value)
        Remove-Item -LiteralPath $shimPath
        Assert-Hash $xmlPath $originalXml
        Write-Output 'RESTORED: original SubModule.xml; compatibility DLL removed. Backup retained.'
        exit 0
    }
    $expected = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'expected-original.json') -Raw | ConvertFrom-Json
    foreach ($entry in $expected.PSObject.Properties) {
        $relative = $entry.Name
        if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)') { throw 'Unsafe manifest path.' }
        $target = Join-Path $ModPath $relative
        Assert-NoLink $target
        Assert-Hash $target $entry.Value.sha256
    }
    Assert-Hash $xmlPath $originalXml
    if (Test-Path -LiteralPath $shimPath) { throw 'Compatibility DLL already exists; refusing to overwrite it.' }
    $payload = Join-Path $PSScriptRoot 'payload\MixMashBannerlord148Compatibility.dll'
    Assert-Hash $payload $shimHash
    if ($Action -eq 'Verify') {
        Write-Output 'VERIFIED: all 23 original files and patch payload match; no target files changed.'
        exit 0
    }
    $encoding = New-Object Text.UTF8Encoding($false, $true)
    $text = $encoding.GetString([IO.File]::ReadAllBytes($xmlPath))
    $text = $text.Replace("`t<DependedModules>`r`n", "`t<DependedModules>`r`n        <DependedModule Id=`"Bannerlord.Harmony`"/>`r`n")
    $addition = @(
        '      <SubModule>',
        '            <Name value="MixMash Bannerlord 1.4.8 Compatibility"/>',
        '            <DLLName value="MixMashBannerlord148Compatibility.dll"/>',
        '            <SubModuleClassType value="MixMashBannerlord148Compatibility.SubModule"/>',
        '            <Tags>',
        '                <Tag key="DedicatedServerType" value="none"/>',
        '                <Tag key="IsNoRenderModeElement" value="false"/>',
        '            </Tags>',
        '      </SubModule>'
    ) -join "`n"
    # Reproduce the reference's mixed line endings exactly.
    $text = $text.Replace("`t<SubModules>`r`n", "`t<SubModules>`n$addition`r`n")
    $bytes = $encoding.GetBytes($text)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $resultHash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','') } finally { $sha.Dispose() }
    if ($resultHash -ne $patchedXml) { throw 'Generated XML does not match the verified working reference.' }
    $backup = Join-Path $ModPath ('_GeraltCompatibilityBackup_' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '_' + [Guid]::NewGuid().ToString('N').Substring(0,8))
    [void][IO.Directory]::CreateDirectory($backup)
    $savedXml = Join-Path $backup 'SubModule.xml'
    [IO.File]::Copy($xmlPath, $savedXml, $false)
    Assert-Hash $savedXml $originalXml
    [IO.File]::WriteAllText((Join-Path $backup 'recovery.txt'), "Original SubModule.xml SHA256: $originalXml`r`nAdded DLL: $shimRelative`r`nAdded DLL SHA256: $shimHash`r`nUse Patch.ps1 -Action Restore -ModPath <mod folder> -BackupPath <this folder>.`r`n")
    $stagedXml = Join-Path $backup 'patched-SubModule.tmp'
    [IO.File]::WriteAllBytes($stagedXml, $bytes)
    $addedShim = $false
    $replacedXml = $false
    try {
        Assert-Hash $xmlPath $originalXml
        [IO.File]::Copy($payload, $shimPath, $false)
        $addedShim = $true
        Assert-Hash $shimPath $shimHash
        [IO.File]::Replace($stagedXml, $xmlPath, [NullString]::Value)
        $replacedXml = $true
        Assert-Hash $xmlPath $patchedXml
    } catch {
        $failure = $_
        if ($replacedXml) { [IO.File]::Copy($savedXml, $xmlPath, $true) }
        if ($addedShim -and (Test-Path -LiteralPath $shimPath)) { Remove-Item -LiteralPath $shimPath }
        throw $failure
    }
    Write-Output "APPLIED: verified compatibility patch. Backup: $backup"
    Write-Output 'Enable Bannerlord.Harmony before MixMashMagicArmorPack in the launcher. See README.md for scope and known limitations.'
    exit 0
} catch {
    Write-Error $_
    exit 1
}
