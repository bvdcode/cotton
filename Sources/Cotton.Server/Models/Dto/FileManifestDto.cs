using EasyExtensions.Models.Dto;

namespace Cotton.Server.Models.Dto
{
    public class FileManifestDto : BaseDto<Guid>
    {
        public Guid? OwnerId { get; set; }

        public string Name { get; set; } = null!;

        public string Folder { get; set; } = null!;

        public string ContentType { get; set; } = null!;

        public long SizeBytes { get; set; }

        public byte[] Sha256 { get; set; } = null!;
    }
}
