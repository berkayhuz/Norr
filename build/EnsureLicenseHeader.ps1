param(
    [Parameter(Mandatory = $true)]
    [string]$ListFile
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$NL = "`r`n"

$copyrightLines = @(
    "// Copyright (c) Norr",
    "// Licensed under the MIT license."
)

function Normalize([string]$text) {
    if ($null -eq $text) { return "" }
    
    $t = $text -replace "`r?`n", $NL
    if ($t.Length -gt 0 -and [int]$t[0] -eq 0xFEFF) { $t = $t.Substring(1) }
    return $t
}

if (!(Test-Path -LiteralPath $ListFile)) {
    throw "List file not found: $ListFile"
}

$files = Get-Content -LiteralPath $ListFile | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

$added = 0; $skipped = 0; $checked = 0

foreach ($f in $files) {
    $checked++
    if (!(Test-Path -LiteralPath $f)) { continue }

    $raw  = Get-Content -LiteralPath $f -Raw
    $norm = Normalize $raw

    if ($norm -match '^\s*//\s*Copyright\s*\(c\)\s*Norr') { $skipped++; continue }

    $hasNullable = [regex]::IsMatch(
        $norm,
        '^\s*#nullable\s+(enable|disable|restore)(\s+(warnings|annotations))?\b',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
        [System.Text.RegularExpressions.RegexOptions]::Multiline
    )

    $headerLines = @()
    $headerLines += $copyrightLines
    if (-not $hasNullable) { $headerLines += "#nullable enable" }
    
    $headerLines += ""

    $headerText = ($headerLines -join $NL) + $NL
    $headerText = Normalize $headerText

    if ($norm.StartsWith($headerText)) { $skipped++; continue }

    $firstLines = ($norm -split $NL, 3)
    $headCheck  = ($firstLines[0..([Math]::Min(1, $firstLines.Length - 1))] -join $NL)
    if ($headCheck -match 'Copyright\s*\(c\)\s*Norr') { $skipped++; continue }

    $newContent = $headerText + $norm
    Set-Content -LiteralPath $f -Value $newContent -Encoding UTF8

    $added++
}

Write-Host "[Norr] checked=$checked added=$added skipped=$skipped"
