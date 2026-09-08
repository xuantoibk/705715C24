using EolTester.Communication;
using EolTester.Configuration.Models;

namespace EolTester.App.Services;

/// <summary>
/// Chọn cài đặt <see cref="IPlcCommunicationDriver"/> (vai trò Master) theo <see cref="ProtocolType"/> đã cấu
/// hình ở tab Setup — đây là điểm DUY NHẤT quyết định driver cụ thể nào được dùng; <see cref="PlcPollingService"/>
/// chỉ biết interface từ đây trở đi (xem CLAUDE.md mục 6). Sống ở tầng App (không phải Communication) vì cần
/// resolve qua DI container — các driver cụ thể vẫn đăng ký Singleton trong <c>App.xaml.cs</c>.
/// </summary>
public interface IPlcCommunicationDriverFactory
{
    IPlcCommunicationDriver GetDriver(ProtocolType protocol);
}
