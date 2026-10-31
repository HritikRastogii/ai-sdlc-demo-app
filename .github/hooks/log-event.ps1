param(
    [Parameter(Mandatory = $true)]
    [string]$EventName
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$logDirectory = Join-Path $repoRoot 'logs'
$logFile = Join-Path $logDirectory 'ai-hook-events.jsonl'

New-Item -ItemType Directory -Force $logDirectory | Out-Null

$rawInput = [Console]::In.ReadToEnd()
$payload = $null

if (-not [string]::IsNullOrWhiteSpace($rawInput)) {
    try {
        $payload = $rawInput | ConvertFrom-Json
    }
    catch {
        $payload = $null
    }
}

# Deliberately exclude prompt text, tool arguments, file contents,
# credentials, tokens, and model reasoning.
$record = [ordered]@{
    timestamp = (Get-Date).ToUniversalTime().ToString('o')
    event = $EventName
    agent = if ($payload -and $payload.agentName) { $payload.agentName } else { $null }
    tool = if ($payload -and $payload.toolName) { $payload.toolName } else { $null }
    session = if ($payload -and $payload.sessionId) { $payload.sessionId } else { $null }
}

$line = $record | ConvertTo-Json -Compress
Add-Content -LiteralPath $logFile -Value $line -Encoding utf8
