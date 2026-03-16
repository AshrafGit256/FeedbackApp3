namespace FeedbackAPI.Models
{
    public class EventModel
    {
        public int Id { get; set; }
        public string Organiser_id { get; set; }
        public string Event_Name { get; set; }
        public string Event_Type { get; set; }
        public string Event_Description { get; set; }
        public string Event_Status { get; set; }
        public DateTime Event_Date { get; set; }
        public DateTime Event_End_Date { get; set; }
        public string Event_Time { get; set; }
        public DateTime Created_at { get; set; }
        public DateTime Updated_at { get; set; }
        public string Created_by { get; set; }
        public string AccessToken { get; set; }
    }
}