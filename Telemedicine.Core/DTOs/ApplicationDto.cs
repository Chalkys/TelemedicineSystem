using System;

namespace TelemedicineSystem.Core.DTOs
{
    public class ApplicationDto
    {
        public Guid ApplicationId { get; set; }
        public Guid PatientId { get; set; }
        public string PatientFullName { get; set; }
        public char Type { get; set; }
        public string Subject { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}