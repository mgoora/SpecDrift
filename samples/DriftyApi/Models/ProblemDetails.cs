namespace DriftyApi.Models
{
    /// <summary>RFC 7807 problem details body for an error response.</summary>
    public class ProblemDetails
    {
        public string Type { get; set; }

        public string Title { get; set; }

        public int Status { get; set; }

        public string Detail { get; set; }

        public string Instance { get; set; }
    }
}
