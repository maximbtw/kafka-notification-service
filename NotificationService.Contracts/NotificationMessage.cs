using ProtoBuf;

namespace NotificationService.Contracts;

[ProtoContract]
public class NotificationMessage
{
    [ProtoMember(1)]
    public string SenderPassword { get; set; } = string.Empty;

    [ProtoMember(2)]
    public string SenderEmail { get; set; } = string.Empty;

    [ProtoMember(30)]
    public string Body { get; set; } = string.Empty;

    [ProtoMember(4)]
    public string Subject { get; set; } = string.Empty;

    [ProtoMember(5)]
    public bool IsBodyHtml { get; set; }
    
    [ProtoMember(6)]
    public List<string> RecipientEmails { get; set; } = new();
    
    [ProtoMember(7)]
    public Guid Guid { get; set; }
}