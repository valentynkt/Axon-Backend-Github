#!/usr/bin/env pwsh
<#
.SYNOPSIS
Auto-generates Docs/INDEX.md based on existing documentation files

.DESCRIPTION
Scans the Docs/ directory structure and creates an organized index of all documentation files.
Maintains consistent structure: Root → ADR → Contracts → Features (by module/feature)
#>

param(
    [string]$DocsPath = "Docs",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

function Get-RelativePath([string]$FullPath, [string]$BasePath) {
    return $FullPath.Substring($BasePath.Length + 1).Replace('\', '/')
}

function Build-IndexContent([string]$DocsRoot) {
    $content = @("# Docs Index", "")
    
    # Root Documentation (exclude INDEX.md itself)
    $rootFiles = Get-ChildItem "$DocsRoot\*.md" | Where-Object { $_.Name -ne "INDEX.md" }
    if ($rootFiles) {
        $content += "## Root Documentation"
        foreach ($file in $rootFiles) {
            $relativePath = Get-RelativePath $file.FullName $DocsRoot
            $content += "- [$($file.Name)]($relativePath)"
        }
        $content += ""
    }
    
    # ADR Templates
    $adrFiles = Get-ChildItem "$DocsRoot\adr\*.md" -ErrorAction SilentlyContinue
    if ($adrFiles) {
        $content += "## ADR Templates"
        foreach ($file in $adrFiles) {
            $relativePath = Get-RelativePath $file.FullName $DocsRoot
            $content += "- [$relativePath]($relativePath)"
        }
        $content += ""
    }
    
    # API Contracts (organized by module)
    $contractFiles = Get-ChildItem "$DocsRoot\contracts\**\*.md" -ErrorAction SilentlyContinue
    if ($contractFiles) {
        $content += "## API Contracts"
        $contractsByModule = $contractFiles | Group-Object { $_.Directory.Name }
        foreach ($group in $contractsByModule) {
            foreach ($file in $group.Group) {
                $relativePath = Get-RelativePath $file.FullName $DocsRoot
                $content += "- [$relativePath]($relativePath)"
            }
        }
        $content += ""
    }
    
    # References
    $referenceFiles = Get-ChildItem "$DocsRoot\references\*.md" -ErrorAction SilentlyContinue
    if ($referenceFiles) {
        $content += "## References"
        foreach ($file in $referenceFiles) {
            $relativePath = Get-RelativePath $file.FullName $DocsRoot
            $content += "- [$relativePath]($relativePath)"
        }
        $content += ""
    }
    
    # Feature Documentation (organized by module/feature)
    $featureFiles = Get-ChildItem "$DocsRoot\features\**\*.md" -ErrorAction SilentlyContinue
    if ($featureFiles) {
        $content += "## Feature Documentation"
        $content += ""
        
        # Group by module/feature path
        $featuresByPath = $featureFiles | Group-Object { 
            $parts = $_.Directory.FullName.Replace($DocsRoot + '\features\', '').Split('\')
            "$($parts[0])/$($parts[1])"
        }
        
        foreach ($group in $featuresByPath) {
            $content += "### $($group.Name)"
            foreach ($file in $group.Group | Sort-Object Name) {
                $relativePath = Get-RelativePath $file.FullName $DocsRoot
                $content += "- [$relativePath]($relativePath)"
            }
            $content += ""
        }
    }
    
    return $content
}

try {
    $docsFullPath = Resolve-Path $DocsPath -ErrorAction Stop
    $indexPath = Join-Path $docsFullPath "INDEX.md"
    
    Write-Host "Scanning documentation in: $docsFullPath" -ForegroundColor Cyan
    
    $indexContent = Build-IndexContent $docsFullPath
    
    if ($WhatIf) {
        Write-Host "`nGenerated INDEX.md content:" -ForegroundColor Yellow
        $indexContent | ForEach-Object { Write-Host $_ }
    } else {
        $indexContent | Out-File -FilePath $indexPath -Encoding utf8
        Write-Host "✅ Updated $indexPath" -ForegroundColor Green
        Write-Host "   Generated index with $($indexContent.Count) lines" -ForegroundColor Gray
    }
}
catch {
    Write-Error "Failed to update docs index: $_"
    exit 1
}