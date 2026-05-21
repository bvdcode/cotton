using System.Collections.Generic;

namespace Cotton.Server.Services
{
    internal static class NotificationTemplateMetadata
    {
        public const string TitleKey = "i18n.titleKey";
        public const string ContentKey = "i18n.contentKey";
        public static Dictionary<string, string> Create(
            string titleKey,
            string contentKey,
            IReadOnlyDictionary<string, string>? metadata = null)
        {
            var result = metadata is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(metadata);

            result[TitleKey] = titleKey;
            result[ContentKey] = contentKey;

            return result;
        }
    }
}
