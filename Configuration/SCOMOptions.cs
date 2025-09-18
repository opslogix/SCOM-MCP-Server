namespace SCOMMCPServer.Configuration
{
    public class SCOMOptions
    {
        public const string SectionName = "SCOM";

        public string ManagementServer { get; set; } = "localhost";

        public int ConnectionTimeout { get; set; } = 30000;

        public bool UseWindowsAuthentication { get; set; } = true;

        public string ManagementGroup { get; set; } = string.Empty;
    }
}