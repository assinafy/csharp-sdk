namespace Assinafy.Sdk.Models;

/// <summary>Pagination metadata parsed from the <c>X-Pagination-*</c> response headers.</summary>
public sealed record PaginationMeta
{
    /// <summary>The current page number (<c>X-Pagination-Current-Page</c>), or <see langword="null"/> when not reported.</summary>
    public int? CurrentPage { get; init; }

    /// <summary>The total number of pages (<c>X-Pagination-Page-Count</c>), or <see langword="null"/> when not reported.</summary>
    public int? LastPage { get; init; }

    /// <summary>Number of items per page (<c>X-Pagination-Per-Page</c>), or <see langword="null"/> when not reported.</summary>
    public int? PerPage { get; init; }

    /// <summary>Total number of items across all pages (<c>X-Pagination-Total-Count</c>), or <see langword="null"/> when not reported.</summary>
    public int? Total { get; init; }
}

/// <summary>A single page of results together with its pagination metadata.</summary>
/// <typeparam name="T">The element type of the page.</typeparam>
public sealed record PaginatedResult<T>
{
    /// <summary>The items on the current page.</summary>
    public IReadOnlyList<T> Data { get; init; } = [];

    /// <summary>Pagination metadata, or <see langword="null"/> when the response carried no <c>X-Pagination-*</c> headers.</summary>
    public PaginationMeta? Meta { get; init; }
}
