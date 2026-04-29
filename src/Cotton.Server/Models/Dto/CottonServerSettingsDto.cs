using Cotton.Database.Models.Enums;
using Cotton.Server.Infrastructure;
using System.Text.Json.Serialization;

namespace Cotton.Server.Models.Dto
{
    [Obsolete("This class is deprecated and will be removed in future versions. Please use the new configuration system.")]
    public class CottonServerSettingsDto
    {
        public EmailConfig? EmailConfig { get; init; }
    }
}
