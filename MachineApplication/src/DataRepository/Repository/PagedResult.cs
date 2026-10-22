namespace DataRepository.Repository;

/// <summary>分页请求参数，页码从 1 开始。</summary>
public sealed record PageRequest
{
    /// <summary>当前页码。</summary>
    public int PageNumber { get; }
    /// <summary>每页记录数，范围为 1 到 1000。</summary>
    public int PageSize { get; }
    /// <summary>传递给 Skip 的偏移量。</summary>
    public int Offset { get; }

    /// <summary>创建并校验分页参数，同时防止偏移量溢出。</summary>
    public PageRequest(int pageNumber = 1, int pageSize = 20)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 1000);
        var offset = ((long)pageNumber - 1) * pageSize;
        if (offset > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Pagination offset exceeds Int32.MaxValue.");
        PageNumber = pageNumber;
        PageSize = pageSize;
        Offset = (int)offset;
    }
}

/// <summary>分页查询结果。</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, long TotalCount, int PageNumber, int PageSize)
{
    /// <summary>总页数。</summary>
    public long TotalPages => TotalCount / PageSize + (TotalCount % PageSize == 0 ? 0 : 1);
    /// <summary>是否存在上一页。</summary>
    public bool HasPreviousPage => PageNumber > 1;
    /// <summary>是否存在下一页。</summary>
    public bool HasNextPage => PageNumber < TotalPages;
}
