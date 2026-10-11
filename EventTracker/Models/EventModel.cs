using System.ComponentModel.DataAnnotations;

namespace EventTracker.Models
{
    public class EventModel
    {
        [Required]
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; } 
    }
}
