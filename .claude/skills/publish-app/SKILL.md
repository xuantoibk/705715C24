---
name: publish-app
description: Build + test + publish Release + smoke-test cho máy 705715-C24 EOL Tester, cập nhật thư mục APP/. Gọi khi người dùng yêu cầu "publish app"/"build lại app".
---

Từ 2026-09-15, dự án này tiêu thụ lõi dùng chung `EolTester.Platform.Wpf` qua NuGet (feed local
`D:\claude\Day1-PLC CONNECT RS485-MC\NugetLocalFeed`) — xem CLAUDE.md mục 4 "Kiến trúc đa máy". Thực hiện đúng
trình tự sau bằng PowerShell, dừng lại báo lỗi ngay khi 1 bước thất bại. Đường dẫn dưới đây đã cập nhật theo
việc thư mục dự án chuyển vào `705715_C24\` (trước đây nằm trực tiếp ở gốc workspace).

1. Dừng process cũ nếu đang chạy:
   ```powershell
   Get-Process EolTester.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
   ```
2. Build Debug, dừng nếu có lỗi (đây cũng là bước xác nhận package `EolTester.Platform.Wpf` đã khai trong
   `EolTester.App.csproj` thực sự resolve được từ feed local — lỗi `NU1101`/không tìm thấy package nghĩa là
   chưa từng `pack.ps1` phiên bản đó, hoặc gõ sai version):
   ```powershell
   dotnet build "d:\Claude\Day1-PLC CONNECT RS485-MC\705715_C24\EolTester.slnx" -c Debug
   ```
3. Chạy unit test cho **lõi dùng chung** (repo này không còn test riêng — toàn bộ logic thuần túy đã chuyển
   sang `EolTester.Platform`, xem CLAUDE.md mục 5), dừng nếu có test fail:
   ```powershell
   dotnet test "d:\Claude\Day1-PLC CONNECT RS485-MC\EolTester.Platform\EolTester.Platform.slnx" -c Debug
   ```
   Lưu ý: bước này test đúng bản package đang có trong **source code** của `EolTester.Platform` (build lại từ
   đầu), KHÔNG phải bản `.nupkg` đã đóng gói trong feed — xem cảnh báo version ở bước 4b.
4. Publish Release:
   ```powershell
   dotnet publish "d:\Claude\Day1-PLC CONNECT RS485-MC\705715_C24\src\EolTester.App\EolTester.App.csproj" -c Release -r win-x64 --self-contained true -o "d:\Claude\Day1-PLC CONNECT RS485-MC\705715_C24\APP"
   ```
4b. **Cảnh báo version package** — đọc `Version="x.y.z"` của `PackageReference Include="EolTester.Platform.Wpf"`
   trong `EolTester.App.csproj`, so với `Version` hiện tại trong `EolTester.Platform\Directory.Build.props`.
   Nếu KHÁC nhau: báo cho người dùng biết app này đang publish với 1 phiên bản lõi CŨ hơn phiên bản đang có
   trong source `EolTester.Platform` — có thể cần `EolTester.Platform\pack.ps1` (sau khi bump `Version`) rồi
   sửa lại `Version` trong `EolTester.App.csproj` trước khi publish, nếu muốn app này có thay đổi mới nhất của
   lõi. Đây chỉ là CẢNH BÁO tham khảo, không tự ý sửa file hay pack lại — hỏi người dùng trước.
5. Smoke-test: khởi động `APP\EolTester.App.exe`, chờ + poll tới ~15 giây (cửa sổ có thể hiện chậm ~10s nếu máy không có PLC), xác nhận `MainWindowTitle` khác rỗng, rồi đóng lại (`Stop-Process`).
6. Xác nhận các file `SeedData/*.csv` đã được copy đúng vào `APP/SeedData/`.
7. Báo cáo ngắn gọn: build/test (lõi dùng chung)/publish/cảnh báo version (nếu có)/smoke-test đều pass hay có bước nào fail (kèm nguyên văn lỗi nếu có).

Ghi chú: dự án này KHÔNG có tiện ích `ResetCache` (khác máy 705/715) — không có bước publish/copy riêng nào cho nó.
