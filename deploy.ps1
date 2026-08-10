param([switch]$Force, [switch]$HashOnly)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# ─── Config ──────────────────────────────────────────
$cfgPath = Join-Path $root ".deploy-config.json"
if (!(Test-Path $cfgPath)) { Write-Host ".deploy-config.json not found!" -Fore Red; exit 1 }
$cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json

$publishDir = Join-Path $root $cfg.publishDir
$serverDir  = Join-Path $publishDir "server"
$hashFile   = Join-Path $publishDir ".filehashes.json"

# ─── Build ───────────────────────────────────────────
Write-Host "`n=== Building ===" -Fore Cyan
Push-Location (Join-Path $root "ErrorService\Server")
dotnet publish -c Release -o $serverDir --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Server publish failed" }
Pop-Location
Write-Host "Build OK." -Fore Green

# ─── Detect changes ──────────────────────────────────
Write-Host "`n=== Checking changes ===" -Fore Cyan
$prevHashes = @{}
if (Test-Path $hashFile) {
    $obj = Get-Content $hashFile -Raw | ConvertFrom-Json
    foreach ($p in $obj.PSObject.Properties) { $prevHashes[$p.Name] = $p.Value }
}

$newHashes = @{}
$toUpload = @()
$hasDllChanges = $false
$excludePrefixes = @("upload/", "wwwroot/upload/")
$total = 0; $skipped = 0

foreach ($f in Get-ChildItem $serverDir -Recurse -File) {
    $rel = $f.FullName.Substring($serverDir.Length).TrimStart('\') -replace '\\', '/'

    $skip = $false
    foreach ($prefix in $excludePrefixes) {
        if ($rel.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { $skip = $true; break }
    }
    if ($skip) { continue }

    $total++
    $hash = (Get-FileHash $f.FullName -Algorithm MD5).Hash
    $newHashes[$rel] = $hash

    if ($Force -or !$prevHashes.ContainsKey($rel) -or $prevHashes[$rel] -ne $hash) {
        $toUpload += $f
        $ext = [System.IO.Path]::GetExtension($rel).ToLower()
        if ($ext -in '.dll', '.exe') { $hasDllChanges = $true }
    } else {
        $skipped++
    }
}

Write-Host "  $($toUpload.Count) changed, $skipped unchanged" -Fore Yellow

if ($HashOnly) {
    Write-Host "`n=== HashOnly mode - saving hashes without uploading ===" -Fore Cyan
    $newHashes | ConvertTo-Json | Set-Content $hashFile -Encoding UTF8
    Write-Host "=== Hash file saved. Future runs will only upload changed files. ===" -Fore Green
    return
}

if ($toUpload.Count -eq 0) {
    Write-Host "`nNothing to upload." -Fore Green
    $newHashes | ConvertTo-Json | Set-Content $hashFile -Encoding UTF8
    Write-Host "=== Done ===" -Fore Green
    return
}

function Ftp-Url($path) {
    return "ftp://$($cfg.ftpHost):$($cfg.ftpPort)$path"
}

function Ftp-Call($path, $method) {
    $req = [System.Net.FtpWebRequest]::Create((Ftp-Url $path))
    $req.Method = $method
    $req.Credentials = New-Object System.Net.NetworkCredential($cfg.ftpUser, $cfg.ftpPass)
    $req.UsePassive = $true
    $req.KeepAlive = $false
    $req.Timeout = 15000
    return $req
}

function Ensure-Dir($path) {
    try {
        $req = Ftp-Call $path "MKD"
        $req.GetResponse().Close()
    } catch {
        # 550 = already exists (or permission issue) — ignore silently
        # Also ignore any other transient FTP directory errors
    }
}

function Ftp-Upload($localFile, $remotePath) {
    $req = Ftp-Call $remotePath "STOR"
    $req.ContentLength = $localFile.Length
    $fs = $localFile.OpenRead()
    $rs = $req.GetRequestStream()
    $fs.CopyTo($rs)
    $rs.Close(); $fs.Close()
    $req.GetResponse().Close()
}

function Ftp-Delete($remotePath) {
    try {
        $req = Ftp-Call $remotePath "DELE"
        $req.GetResponse().Close()
    } catch {}
}

# ─── Take offline if needed ──────────────────────────
$offlinePath = "$($cfg.serverRootPath.TrimEnd('/'))/app_offline.htm"
$offlineFile = Get-Item (Join-Path $root "app_offline.htm")

if ($hasDllChanges) {
    Write-Host ''
    Write-Host '=== DLL/exe changed — taking app offline ===' -Fore Magenta
    Ftp-Upload $offlineFile $offlinePath
    Write-Host "  app_offline.htm uploaded, waiting 3s..." -Fore Magenta
    Start-Sleep -Seconds 3
}

# ─── Upload ──────────────────────────────────────────
Write-Host "`n=== Uploading $($toUpload.Count) files ===" -Fore Cyan
$i = 0
foreach ($f in $toUpload) {
    $i++
    $rel = $f.FullName.Substring($serverDir.Length).TrimStart('\') -replace '\\', '/'
    $parent = Split-Path $rel -Parent
    if ($parent) {
        $parts = $parent -split '/'
        $p = $cfg.serverRootPath.TrimEnd('/')
        foreach ($part in $parts) { $p += "/$part"; Ensure-Dir $p }
    }

    $remoteFile = "$($cfg.serverRootPath.TrimEnd('/'))/$rel"
    Write-Host "  [$i/$($toUpload.Count)] $rel" -Fore Green
    try {
        Ftp-Upload $f $remoteFile
    } catch {
        Write-Host "  FAILED: $_" -Fore Yellow
    }
}

# ─── Bring back online ───────────────────────────────
if ($hasDllChanges) {
    Write-Host ''
    Write-Host '=== Deleting app_offline.htm to restart ===' -Fore Magenta
    Ftp-Delete $offlinePath
    Start-Sleep -Seconds 3
    Write-Host "  App should be back online." -Fore Green
}

# ─── Save hashes ─────────────────────────────────────
$newHashes | ConvertTo-Json | Set-Content $hashFile -Encoding UTF8
Write-Host "`n=== Done ($($toUpload.Count) uploaded) ===" -Fore Green
