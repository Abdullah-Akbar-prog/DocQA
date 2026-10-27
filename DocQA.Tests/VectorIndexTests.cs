using DocQA.Rag;

namespace DocQA.Tests;

public class VectorIndexTests
{
    private static VectorEntry Entry(string name, params float[] vector) =>
        new(Guid.NewGuid(), Guid.NewGuid(), name, 0, VectorMath.Normalize(vector));

    private static float[] Query(params float[] vector) => VectorMath.Normalize(vector);

    [Fact]
    public void Closest_entries_come_first()
    {
        var entries = new[] { Entry("far", 0, 1), Entry("near", 1, 0.1f), Entry("exact", 1, 0) };

        var hits = VectorIndex.Rank(entries, Query(1, 0), k: 3);

        Assert.Equal(new[] { "exact", "near", "far" }, hits.Select(h => h.Entry.FileName));
    }

    [Fact]
    public void Returns_at_most_k_hits()
    {
        var entries = Enumerable.Range(0, 5).Select(i => Entry($"e{i}", 1, i)).ToArray();

        var hits = VectorIndex.Rank(entries, Query(1, 0), k: 2);

        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public void Distance_is_0_for_same_direction_1_for_orthogonal_2_for_opposite()
    {
        var hits = VectorIndex.Rank(
            [Entry("same", 2, 0), Entry("orthogonal", 0, 3), Entry("opposite", -1, 0)],
            Query(1, 0),
            k: 3);

        Assert.Equal(0.0, hits[0].Distance, 5);
        Assert.Equal(1.0, hits[1].Distance, 5);
        Assert.Equal(2.0, hits[2].Distance, 5);
    }

    [Fact]
    public void Entries_with_a_different_dimension_are_ignored()
    {
        var entries = new[] { Entry("ok", 1, 0), Entry("wrong-size", 1, 0, 0) };

        var hits = VectorIndex.Rank(entries, Query(1, 0), k: 5);

        Assert.Single(hits);
        Assert.Equal("ok", hits[0].Entry.FileName);
    }

    [Fact]
    public void Empty_index_returns_no_hits()
    {
        Assert.Empty(VectorIndex.Rank([], Query(1, 0), k: 5));
    }
}