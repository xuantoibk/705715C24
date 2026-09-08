---
name: verify-ui
description: Chạy app HV356 EOL Tester thật, chụp ảnh cửa sổ đúng chuẩn an toàn (PrintWindow, không full-screen capture) để xác minh 1 thay đổi giao diện. Gọi khi cần xác nhận trực quan sau khi sửa UI.
---

Áp dụng đúng quy tắc chụp ảnh bắt buộc của dự án (CLAUDE.md Phần I mục 7): **tuyệt đối không chụp toàn màn hình** (`Screen.Bounds`/`CopyFromScreen`) vì rủi ro chụp nhầm cửa sổ khác trên máy có DPI scaling khác 100%. Luôn dùng `SetProcessDPIAware()` + `PrintWindow` theo đúng `MainWindowHandle` của process.

Nếu thư mục scratchpad của phiên đã có sẵn script capture (thường đặt tên `capture.ps1`), dùng lại nó thay vì viết mới. Nếu chưa có, tạo 1 script PowerShell ngắn:

```powershell
param([int]$ProcessId, [string]$OutFile)
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Cap {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
"@
[Win32Cap]::SetProcessDPIAware() | Out-Null
$proc = Get-Process -Id $ProcessId
$hwnd = $proc.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) { throw "No main window handle for process $ProcessId" }
[Win32Cap]::ShowWindow($hwnd, 9) | Out-Null
Start-Sleep -Milliseconds 300
[Win32Cap]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 300
$rect = New-Object Win32Cap+RECT
[Win32Cap]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
$bmp = New-Object System.Drawing.Bitmap $width, $height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[Win32Cap]::PrintWindow($hwnd, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc)
$g.Dispose()
$bmp.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output "Saved $OutFile ($width x $height)"
```

## Quy trình
1. Đảm bảo app đang chạy (build + khởi động nếu chưa) đúng bản build cần kiểm tra.
2. Điều hướng tới màn hình cần chụp nếu cần (đăng nhập, chuyển tab...) — tính toạ độ click theo tỉ lệ (`fraction × kích thước cửa sổ thật`, đọc từ `GetWindowRect`), không dùng toạ độ tuyệt đối cố định.
3. Gọi script capture, lưu PNG vào thư mục scratchpad của phiên (không lưu vào `src/`).
4. Đọc lại ảnh bằng tool Read và đối chiếu với yêu cầu trước khi báo cáo hoàn thành — không suy đoán kết quả từ code.
