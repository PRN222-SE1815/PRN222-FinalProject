using System;
using System.Collections.Generic;

namespace BusinessLogic.DTOs.Response;

public sealed class PagedResultDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<object> Items { get; set; } = Array.Empty<object>();
}

public sealed class PagedResultDto<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
}
