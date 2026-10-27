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

        [Fact]
        public void Normalize_returns_a_unit_vector()
        {
            var unit = VectorMath.Normalize([3f, 4f]);

            Assert.Equal(0.6, unit[0], 5);
            Assert.Equal(0.8, unit[1], 5);
        }

        [Fact]
        public void Normalize_does_not_modify_its_input()
        {
            float[] original = [3f, 4f];

            VectorMath.Normalize(original);

            Assert.Equal(new[] { 3f, 4f }, original);
        }

        [Fact]
        public void Normalize_keeps_a_zero_vector_at_zero()
        {
            Assert.Equal(new[] { 0f, 0f }, VectorMath.Normalize([0f, 0f]));
        }

        [Fact]
        public void Dot_multiplies_and_sums()
        {
            Assert.Equal(32.0, VectorMath.Dot([1f, 2f, 3f], [4f, 5f, 6f]), 9);
        }

        [Fact]
        public void Dot_with_different_lengths_throws()
        {
            Assert.Throws<ArgumentException>(() => VectorMath.Dot([1f], [1f, 2f]));
        }
    }
}