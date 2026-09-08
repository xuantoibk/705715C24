$portName = 'COM4'
$p = New-Object System.IO.Ports.SerialPort($portName, 19200, [System.IO.Ports.Parity]::None, 8, [System.IO.Ports.StopBits]::One)
try {
    $p.Open()
    Write-Output 'OPENED'
}
catch {
    Write-Output $_.Exception.GetType().FullName
    Write-Output $_.Exception.Message
}
finally {
    if ($p.IsOpen) { $p.Close() }
}
