namespace Cotton.Email
{
    /// <summary>
    /// Represents an inline (CID-referenced) attachment for an HTML email.
    /// The HTML body references it via <c>cid:{ContentId}</c>.
    /// </summary>
    public sealed class InlineAttachment
    {
        /// <summary>
        /// Content-ID referenced in the HTML body (without angle brackets).
        /// </summary>
        public string ContentId { get; }

        /// <summary>
        /// MIME content type (e.g., "image/png").
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// File name for the attachment.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Raw bytes of the attachment content.
        /// </summary>
        public byte[] Content { get; }

        public InlineAttachment(string contentId, string contentType, string fileName, byte[] content)
        {
            ContentId = contentId;
            ContentType = contentType;
            FileName = fileName;
            Content = content;
        }

        /// <summary>
        /// Returns the attachment content as a Base64-encoded string.
        /// </summary>
        public string GetBase64Content()
        {
            return System.Convert.ToBase64String(Content);
        }
    }
}
