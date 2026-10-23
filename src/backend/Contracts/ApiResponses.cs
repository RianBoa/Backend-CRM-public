namespace backend.Contracts;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
public sealed record SelectResponse<T>(IReadOnlyList<T> Items);
public sealed record ErrorResponse(string Code, string Message);
