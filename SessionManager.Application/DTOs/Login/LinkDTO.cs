namespace SessionManager.Application.DTOs
{
    public class Link
    {
        public string Rel { get; set; } = string.Empty;
        public string Href { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;

        public Link(string rel, string href, string method)
        {
            Rel = rel;
            Href = href;
            Method = method;
        }
    }
}