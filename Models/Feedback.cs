using System.ComponentModel.DataAnnotations;

namespace FeedbackAPI.Models
{
    public class Feedback
    {
        public int Id { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Event ID must be valid.")]
        public int Event_id { get; set; }

        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }

        [Required]
        [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters.")]
        public string Category { get; set; } = string.Empty;

        [Required]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Message must be between 10 and 1000 characters.")]
        public string Message { get; set; } = string.Empty;

        public bool Is_Anonymous { get; set; }

        public string? Status { get; set; }

        public DateTime Created_at { get; set; } = DateTime.Now;
    }
}