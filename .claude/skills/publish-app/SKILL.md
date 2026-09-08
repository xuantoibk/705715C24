---
name: publish-app
description: Build + test + publish Release + smoke-test cho HV356 EOL Tester, cập nhật thư mục APP/. Gọi khi người dùng yêu cầu "publish app"/"build lại app".
---

Thực hiện đúng trình tự sau bằng PowerShell, dừng lại báo lỗi ngay khi 1 bước thất bại:

1. Dừng process cũ nếu đang chạy:
   ```powershell
   Get-Process EolTester.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
   ```
2. Build Debug, dừng nếu có lỗi:
   ```powershell
   dotnet build "d:\Claude\Day1-PLC CONNECT RS485-MC\EolTester.slnx" -c Debug
   ```
3. Chạy unit test, dừng nếu có test fail:
   ```powershell
   dotnet test "d:\Claude\Day1-PLC CONNECT RS485-MC\EolTester.slnx" --no-build -c Debug
   ```
4. Publish Release:
   ```powershell
   dotnet publish "d:\Claude\Day1-PLC CONNECT RS485-MC\src\EolTester.App\EolTester.App.csproj" -c Release -r win-x64 --self-contained true -o "d:\Claude\Day1-PLC CONNECT RS485-MC\APP"
   ```
5. Smoke-test: khởi động `APP\EolTester.App.exe`, chờ ~3 giây, xác nhận `MainWindowTitle` khác rỗng, rồi đóng lại (`Stop-Process`).
6. Báo cáo ngắn gọn: build/test/publish/smoke-test đều pass hay có bước nào fail (kèm nguyên văn lỗi nếu có).
