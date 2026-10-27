using System.Runtime.InteropServices;

namespace DocQA.Rag
{
    public class VectorMath
    {
        public static byte[] ToBytes(ReadOnlyMemory<float> vector) =>
            MemoryMarshal.AsBytes(vector.Span).ToArray();

        public static float[] ToFloats(byte[] bytes) =>
            MemoryMarshal.Cast<byte, float>(bytes).ToArray();
    }
}
