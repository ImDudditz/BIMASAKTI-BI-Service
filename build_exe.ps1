# Bimasakti Manager Executable Native Builder
# Uses the built-in .NET Framework compiler present on all Windows machines.

# Locate the built-in Windows C# compiler csc.exe
$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    # Fallback to 32-bit if 64-bit folder is missing
    $cscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $cscPath)) {
    Write-Error "Could not locate built-in csc.exe C# compiler. Please install .NET SDK or .NET Framework."
    Exit 1
}

# Automatically convert BIMASAKTI.png to BIMASAKTI.ico if needed
$pngPath = "backend\assets\BMS\img\BIMASAKTI.png"
$icoPath = "backend\assets\BMS\img\BIMASAKTI.ico"

if (Test-Path $pngPath) {
    if (-not (Test-Path $icoPath) -or (Get-Item $pngPath).LastWriteTime -gt (Get-Item $icoPath).LastWriteTime) {
        Write-Host "Converting $pngPath to $icoPath..." -ForegroundColor Green
        try {
            Add-Type -AssemblyName System.Drawing
            $bmp = New-Object System.Drawing.Bitmap($pngPath)
            $hIcon = $bmp.GetHicon()
            $icon = [System.Drawing.Icon]::FromHandle($hIcon)
            $fs = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create)
            $icon.Save($fs)
            $fs.Close()
            $bmp.Dispose()
            Write-Host "Successfully generated ICO file." -ForegroundColor Green
        } catch {
            Write-Warning "Failed to convert PNG to ICO: $_"
        }
    }
}

Write-Host "Compiling BimasaktiManager.exe natively..." -ForegroundColor Green
$argsList = "/target:winexe"
if (Test-Path $icoPath) {
    $argsList += " /win32icon:`"$icoPath`""
}
$argsList += " /out:BimasaktiManager.exe `"manager.cs`""

Start-Process -FilePath $cscPath -ArgumentList $argsList -NoNewWindow -Wait

if (Test-Path "BimasaktiManager.exe") {
    Write-Host "SUCCESS: BimasaktiManager.exe successfully compiled with custom icon!" -ForegroundColor Green
    Write-Host "You can now run BimasaktiManager.exe directly from the explorer." -ForegroundColor Yellow
} else {
    Write-Error "Compilation failed. Check C# syntax or framework settings."
}
