namespace MemSentinel.Contracts;

public readonly record struct TypeDelta(
    string TypeName,
    int CountA,
    int CountB,
    long BytesA,
    long BytesB)
{
    public int CountDelta => CountB - CountA;
    public long BytesDelta => BytesB - BytesA;
}
