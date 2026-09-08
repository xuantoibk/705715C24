using NModbus.IO;

namespace EolTester.Communication;

/// <summary>
/// Bọc bất kỳ <see cref="Stream"/> nào (cổng COM thật qua <c>SerialPort.BaseStream</c>, hoặc
/// <c>NetworkStream</c> dùng cho test tự động qua TCP loopback) thành <see cref="IStreamResource"/>
/// mà NModbus cần — NModbus không phụ thuộc trực tiếp vào <c>SerialPort</c>, chỉ cần giao diện đọc/ghi byte này.
/// </summary>
public sealed class StreamAdapter(Stream stream, Action? discardInBuffer = null) : IStreamResource
{
    public int InfiniteTimeout => System.IO.Ports.SerialPort.InfiniteTimeout;

    public int ReadTimeout
    {
        get => stream.CanTimeout ? stream.ReadTimeout : InfiniteTimeout;
        set { if (stream.CanTimeout) stream.ReadTimeout = value; }
    }

    public int WriteTimeout
    {
        get => stream.CanTimeout ? stream.WriteTimeout : InfiniteTimeout;
        set { if (stream.CanTimeout) stream.WriteTimeout = value; }
    }

    /// <summary>
    /// Stream thuần (VD NetworkStream dùng khi test) không có khái niệm "buffer phần cứng đang chờ đọc" để xả —
    /// chỉ SerialPort mới có (<c>SerialPort.DiscardInBuffer()</c>), truyền qua callback <paramref name="discardInBuffer"/> khi cần.
    /// </summary>
    public void DiscardInBuffer() => discardInBuffer?.Invoke();

    public int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

    public void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);

    public void Dispose() => stream.Dispose();
}
