namespace Middagsasen.Planner.Api.Services
{
    public class PagedResponse<T> where T : class
    {
        public int TotalCount { get; set; }
        public IEnumerable<T> Result { get; set; } = [];
    }
}
