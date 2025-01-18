using System.ComponentModel.DataAnnotations;

namespace Api.Contracts;

public class SendNotificationRequest
{
    [Required] 
    [MinLength(1)]
    public List<string> Emails { get; set; } = null!;
    
    [Required]
    public string Subject { get; set; } = null!;

    [Required] 
    public string Body { get; set; } = null!;
    
    public bool IsBodyHtml { get; set; }
}