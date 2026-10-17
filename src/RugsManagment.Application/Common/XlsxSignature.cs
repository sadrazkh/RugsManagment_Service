namespace RugsManagment.Application.Common;
public static class XlsxSignature
{
    public const long MaxFileBytes = 10_485_760;
    public static bool IsZip(ReadOnlySpan<byte> bytes) => bytes.Length >= 4
        && bytes[0] == 0x50 && bytes[1] == 0x4b && bytes[2] == 3 && bytes[3] == 4;
}
