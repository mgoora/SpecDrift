using System.Collections.Generic;

namespace DriftyApi.Models
{
    public class ClientSearchResult
    {
        public List<Client> Items { get; set; } = new List<Client>();

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }
}
