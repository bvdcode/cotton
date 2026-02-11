using System.ComponentModel.DataAnnotations;

namespace Cotton.Server.Models.Dto
{
    public class LoginRequest
    {
        /// <summary>
        /// Gets or sets the username associated with the user account.
        /// </summary>
        [Required]
        public string Username { get; set; } = null!;

        /// <summary>
        /// Gets or sets the password used for authentication.
        /// </summary>
        [Required]
        public string Password { get; set; } = null!;

        /// <summary>
        /// Gets or sets the two-factor authentication code provided by the user.
        /// </summary>
        public string? TwoFactorCode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the current device should be trusted for future authentication
        /// attempts.
        /// </summary>
        public bool TrustDevice { get; set; }
    }
}
