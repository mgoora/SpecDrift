namespace DriftyApi.Core.Models;

public class ClientSearchResult
{
    public List<Client> Items { get; set; } = new();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
