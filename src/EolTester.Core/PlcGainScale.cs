namespace EolTester.Core;

/// <summary>
/// Hệ số Gain (scale) cho các biến đo lường/giới hạn ở tab Set Spec — quy ước bắt buộc chỉ 1/10/100/1000
/// (xem TestStepDefinition.Scale, đọc từ cột "Scale" của spec-register-map.csv). Giá trị đọc CSV không hợp
/// lệ hoặc thiếu coi như 1 (không scale). Số chữ số thập phân hiển thị/làm tròn đi kèm 1-1 với Scale — người
/// dùng chốt: Scale 1 -> làm tròn đến 1 (số nguyên), 10 -> 0.1, 100 -> 0.01, 1000 -> 0.001.
/// </summary>
public static class PlcGainScale
{
    public static bool IsValid(int scale) => scale is 1 or 10 or 100 or 1000;

    public static int DecimalDigits(int scale) => scale switch
    {
        10 => 1,
        100 => 2,
        1000 => 3,
        _ => 0,
    };

    public static decimal Round(decimal value, int scale) =>
        Math.Round(value, DecimalDigits(scale), MidpointRounding.AwayFromZero);
}
