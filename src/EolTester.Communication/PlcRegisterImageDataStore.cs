using NModbus;

namespace EolTester.Communication;

/// <summary>
/// Cầu nối giữa NModbus (khi PC làm Modbus Slave) và <see cref="PlcRegisterImage"/> — "biến D1005" dùng
/// chung cho cả 2 chiều Master/Slave. NModbus gọi <see cref="RegisterTablePointSource.ReadPoints"/> mỗi
/// khi PLC hỏi đọc, và <see cref="RegisterTablePointSource.WritePoints"/> mỗi khi PLC ghi tới — đọc/ghi
/// thẳng vào <see cref="PlcRegisterImage"/> theo thời gian thực, không cần đồng bộ định kỳ qua timer.
/// </summary>
public sealed class PlcRegisterImageDataStore : ISlaveDataStore
{
    public IPointSource<ushort> HoldingRegisters { get; }
    public IPointSource<ushort> InputRegisters { get; }

    /// <summary>
    /// Round này chỉ tập trung vào không gian thanh ghi (Dxxxx/Dxxxx.b, đúng "biến D1005" đã thiết kế) —
    /// coil rời (địa chỉ 0xxxx/1xxxx kiểu Modbus cổ điển) chưa có nhu cầu cụ thể nên dùng bảng riêng trong bộ
    /// nhớ, không liên kết với PlcRegisterImage. Mở rộng khi có yêu cầu thật.
    /// </summary>
    public IPointSource<bool> CoilDiscretes { get; } = new ArrayBoolPointSource();
    public IPointSource<bool> CoilInputs { get; } = new ArrayBoolPointSource();

    public PlcRegisterImageDataStore(PlcRegisterImage registerImage)
    {
        HoldingRegisters = new RegisterTablePointSource(registerImage);
        InputRegisters = new RegisterTablePointSource(registerImage);
    }

    private sealed class RegisterTablePointSource(PlcRegisterImage registerImage) : IPointSource<ushort>
    {
        public ushort[] ReadPoints(ushort startAddress, ushort numberOfPoints)
        {
            var result = new ushort[numberOfPoints];
            for (var i = 0; i < numberOfPoints; i++)
                registerImage.TryGetWord(startAddress + i, out result[i]);
            return result;
        }

        public void WritePoints(ushort startAddress, ushort[] points)
        {
            for (var i = 0; i < points.Length; i++)
                registerImage.TryUpdateWord(startAddress + i, points[i]);
        }
    }

    private sealed class ArrayBoolPointSource : IPointSource<bool>
    {
        private readonly bool[] _data = new bool[ushort.MaxValue + 1];

        public bool[] ReadPoints(ushort startAddress, ushort numberOfPoints)
        {
            var result = new bool[numberOfPoints];
            Array.Copy(_data, startAddress, result, 0, numberOfPoints);
            return result;
        }

        public void WritePoints(ushort startAddress, bool[] points) =>
            Array.Copy(points, 0, _data, startAddress, points.Length);
    }
}
