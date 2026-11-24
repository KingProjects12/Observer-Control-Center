# Best-effort guard for public GitHub releases. Does NOT replace a human review, git-history audit, or credential rotation.
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

$tracked = @()
if (Get-Command git -ErrorAction SilentlyContinue) {
    $tracked = @(& git ls-files 2>$null)
    if ($LASTEXITCODE -ne 0) { $tracked = @() }
}
if ($tracked.Count -eq 0) {
    $tracked = @(Get-ChildItem -File -Recurse | ForEach-Object {
        [IO.Path]::GetRelativePath((Get-Location).Path, $_.FullName) -replace '\\', '/'
    })
}
$sourceExtensions = @('.cs', '.xaml', '.md', '.txt', '.json', '.yml', '.yaml', '.ps1', '.cmd', '.xml', '.csproj')
$findings = New-Object 'System.Collections.Generic.List[string]'
foreach ($relative in $tracked) {
    $path = $relative -replace '\\', '/'
    if ($path -match '(?i)(^|/)(Results|Raw_JSON|bin|obj|dist)/' -or $path -match '(?i)(^|/)(\.env|secrets\.json|update-settings\.json)$') {
        $findings.Add("Forbidden filename: $path")
        continue
    }
    if ($path -eq 'tools/PublicSourceAudit.ps1' -or [IO.Path]::GetExtension($path) -notin $sourceExtensions) { continue }
    if (-not (Test-Path -LiteralPath $relative -PathType Leaf)) { continue }
    $content = Get-Content -LiteralPath $relative -Raw -ErrorAction Stop
    # The audit flags concrete corporate/private IP addresses, not abstract subnet descriptions.
    $privateIp = '(?<![0-9])(?:10\.(?:[0-9]{1,3}\.){2}[0-9]{1,3}|192\.168\.[0-9]{1,3}\.[0-9]{1,3}|172\.(?:1[6-9]|2[0-9]|3[01])\.[0-9]{1,3}\.[0-9]{1,3})(?![0-9])'
    $tokens = '(?:ghp_|github_pat_|gho_|ghu_|ghs_)[A-Za-z0-9_]{20,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'
    $hardcodedCredential = '(?im)\b(?:password|passwd|api[_-]?key|secret)\s*[:=]\s*["''][^\r\n"''\$]{8,}["'']'
    $patterns = @{
        'Private IPv4' = $privateIp
        'Credential or key' = $tokens
        'Suspicious hardcoded secret' = $hardcodedCredential
    }
    foreach ($label in $patterns.Keys) {
        if ([regex]::IsMatch($content, $patterns[$label])) { $findings.Add("$label in: $path") }
    }
}
if ($findings.Count -gt 0) {
    Write-Host "PUBLIC AUDIT FAILED: $($findings.Count) potential disclosure(s)." -ForegroundColor Red
    $findings | Sort-Object -Unique | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}
Write-Host 'PUBLIC AUDIT PASS: no matching private IPs, credentials or export paths in scanned files.' -ForegroundColor Green
Write-Host 'WARNING: manually review images/assets, git history, external links, dependencies, and legal rights.' -ForegroundColor Yellow
