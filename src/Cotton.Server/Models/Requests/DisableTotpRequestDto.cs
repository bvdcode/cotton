namespace Cotton.Server.Models.Requests
{
    public record DisableTotpRequestDto
    {
        public string Password { get; init; } = null!;
    }
}
