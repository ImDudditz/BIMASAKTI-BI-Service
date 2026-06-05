param([string]$path)

Get-ChildItem -Path $path -Filter *.cs -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $modified = $false
    
    if ($content -match 'BI_SERVICE\.Core') {
        $content = $content -replace 'BI_SERVICE\.Core', 'Bimasakti.BiService.Api'
        $modified = $true
    }
    if ($content -match 'BiPortal\.FinancialReports\.Manager') {
        $content = $content -replace 'BiPortal\.FinancialReports\.Manager', 'Bimasakti.BiService.Mgr'
        $modified = $true
    }
    
    if ($modified) {
        Set-Content -Path $_.FullName -Value $content -NoNewline
        Write-Host "Updated $($_.FullName)"
    }
}
