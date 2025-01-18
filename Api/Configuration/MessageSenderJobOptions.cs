using System.ComponentModel;

namespace Api.Configuration;

public class MessageSenderJobOptions
{
    [DefaultValue(5)]
    public int UpdateIntervalInSeconds { get; set; }
    
    public string Body { get; set; } = string.Empty;
    
    public List<string> Emails { get; set; } = new();
}