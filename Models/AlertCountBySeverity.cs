namespace SCOMMCPServer.Models
{
    public class AlertCountBySeverity
    {
        public int Total { get; set; }
        public int Critical { get; set; }
        public int Warning { get; set; }
        public int Informational { get; set; }
    }
}