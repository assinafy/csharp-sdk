namespace Assinafy.Sdk.Models;

public sealed record PaginationMeta
{
    public int? CurrentPage { get; init; }
    public int? LastPage { get; init; }
    public int? PerPage { get; init; }
    public int? Total { get; init; }
}

public sealed record PaginatedResult<T>
{
    public IReadOnlyList<T> Data { get; init; } = [];
    public PaginationMeta? Meta { get; init; }
}
