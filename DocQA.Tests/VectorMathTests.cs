using DocQA.Rag;

namespace DocQA.Tests
{
    public class VectorMathTests
    {
        [Fact]
        public void Bytes_round_trip()
        {
            float[] vector = [0.1f, -0.5f, 2f];

            var bytes = VectorMath.ToBytes(vector);

            Assert.Equal(vector.Length * sizeof(float), bytes.Length);
            Assert.Equal(vector, VectorMath.ToFloats(bytes));
        }
    }
}