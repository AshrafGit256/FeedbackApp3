using System.ComponentModel.DataAnnotations;

namespace FeedbackAPI.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        [Required]
        public string Action { get; set; } = string.Empty;      // e.g. "SUBMIT_FEEDBACK"

        [Required]
        public string Entity { get; set; } = string.Empty;      // e.g. "Feedback"

        public int? EntityId { get; set; }                      // e.g. feedback ID

        public string? Details { get; set; }                    // extra context

        public string? IpAddress { get; set; }                  // who did it

        public bool Success { get; set; }                       // did it succeed

        public string? ErrorMessage { get; set; }               // if it failed

        public DateTime Created_at { get; set; } = DateTime.UtcNow;
    }
}