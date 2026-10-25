<#
.SYNOPSIS
    Sets SPECDRIFT_API_KEY for the current Windows user.

.DESCRIPTION
    Prompts for the Anthropic API key with hidden input and stores it as a
    user-level environment variable. The key is never written to this script,
    to any file in the repository, or to the PowerShell command history.

    New terminals pick the variable up automatically. When the script is run
    from an existing PowerShell window (.\scripts\set-api-key.ps1), that window
    is updated as well.

.PARAMETER Remove
    Deletes the variable instead of setting it.

.EXAMPLE
    .\scripts\set-api-key.ps1

.EXAMPLE
    .\scripts\set-api-key.ps1 -Remove
#>
[CmdletBinding()]
param(
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
$name = 'SPECDRIFT_API_KEY'

if ($Remove) {
    [Environment]::SetEnvironmentVariable($name, $null, 'User')
    Remove-Item "Env:$name" -ErrorAction SilentlyContinue
    Write-Host "$name removed for user $env:USERNAME."
    return
}

if ([Environment]::GetEnvironmentVariable($name, 'User')) {
    $answer = Read-Host "$name is already set. Overwrite? [y/N]"
    if ($answer -notmatch '^(y|yes)$') {
        Write-Host 'Nothing changed.'
        return
    }
}

$secure = Read-Host 'Paste your Anthropic API key (input is hidden)' -AsSecureString
$key = [System.Net.NetworkCredential]::new('', $secure).Password.Trim()

if ([string]::IsNullOrWhiteSpace($key)) {
    throw 'No key entered. Nothing changed.'
}

if (-not $key.StartsWith('sk-ant-')) {
    Write-Warning "The key does not start with 'sk-ant-'. Check that you pasted an Anthropic API key."
}

[Environment]::SetEnvironmentVariable($name, $key, 'User')
Set-Item -Path "Env:$name" -Value $key

$tail = if ($key.Length -gt 4) { $key.Substring($key.Length - 4) } else { '' }
$key = $null
$secure.Dispose()

Write-Host "$name set for user $env:USERNAME (ends with ...$tail)."
Write-Host 'Open a new terminal to use it there.'
