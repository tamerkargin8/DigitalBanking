namespace DigitalBanking.Application.DTOs.Common;

/// <summary>
/// Standardized paginated response wrapper for list endpoints.
/// </summary>
public class PaginatedResponse<T>
{
    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of items across all pages.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

    /// <summary>
    /// Collection of items on the current page.
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Indicates if there are more pages after the current one.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// Indicates if there are pages before the current one.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    public PaginatedResponse() { }

    public PaginatedResponse(List<T> items, int pageNumber, int pageSize, int totalCount)
    {
        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
    }
}
