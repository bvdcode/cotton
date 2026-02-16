namespace Cotton.Server.Models.Requests
{
    public class ForgotPasswordRequestDto
    {
        public string UsernameOrEmail { get; set; } = null!;
    }
}
