namespace Cotton.Server.Models.Requests
{
    public record ConfirmTotpRequestDto
    {
        public string TwoFactorCode { get; init; } = null!;
    }
}
