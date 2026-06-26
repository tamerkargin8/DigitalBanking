namespace DigitalBanking.Application.DTOs.Transaction;

/// <summary>
/// Paginated transaction history for an account.
/// </summary>
public class TransactionHistoryDto
{
    /// <summary>
    /// Account ID.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Account number.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Current account balance.
    /// </summary>
    public decimal CurrentBalance { get; set; }

    /// <summary>
    /// Paging information.
    /// </summary>
    public PaginationInfo Pagination { get; set; } = new();

    /// <summary>
    /// List of transactions on the current page.
    /// </summary>
    public List<TransactionDto> Transactions { get; set; } = new();

    public class PaginationInfo
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
