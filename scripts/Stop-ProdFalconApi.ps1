# Stops ProdFalcon API processes and frees port 5014 (and 7036 for HTTPS profile).
$ports = @(5014, 7036)

foreach ($port in $ports) {
    $connections = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    foreach ($conn in $connections) {
        $processId = $conn.OwningProcess
        if ($processId -and $processId -ne 0) {
            Write-Host "Stopping process $processId on port $port..."
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        }
    }
}

Get-Process -Name "ProdFalcon.API" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "Stopping ProdFalcon.API (PID $($_.Id))..."
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
}

Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | ForEach-Object {
    if ($_.CommandLine -like "*ProdFalcon.API*") {
        Write-Host "Stopping dotnet ProdFalcon.API (PID $($_.ProcessId))..."
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

Start-Sleep -Seconds 1
Write-Host "Done. You can now run: cd ProdFalcon.API; dotnet run"
