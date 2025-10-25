namespace Cotton.Server.Models.Requests
{
    public class CreateNodeRequest
    {
        public Guid ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
