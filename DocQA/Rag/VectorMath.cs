using System.Runtime.InteropServices;

namespace DocQA.Rag
{
    public class VectorMath
    {
        public static byte[] ToBytes(ReadOnlyMemory<float> vector) =>
            MemoryMarshal.AsBytes(vector.Span).ToArray();

        public static float[] ToFloats(byte[] bytes) =>
            MemoryMarshal.Cast<byte, float>(bytes).ToArray();

        /// <summary>Returns a unit-length copy of the vector. A zero vector stays all zeros.</summary>
        public static float[] Normalize(float[] vector)
        {
            double sumOfSquares = 0;
            foreach (var x in vector) sumOfSquares += x * x;

            var result = new float[vector.Length];
            if (sumOfSquares == 0) return result;

            var inverseNorm = 1.0 / Math.Sqrt(sumOfSquares);
            for (var i = 0; i < vector.Length; i++) result[i] = (float)(vector[i] * inverseNorm);
            return result;
        }

        public static double Dot(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Vectors must have the same length ({a.Length} vs {b.Length}).");

            double sum = 0;
            for (var i = 0; i < a.Length; i++) sum += a[i] * b[i];
            return sum;
        }
    }
}
