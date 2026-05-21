using System;
using System.ComponentModel.DataAnnotations;

namespace TelemedicineSystem.Core.DTOs
{
    public class CreateApplicationDto
    {
        [Required]
        public char Type { get; set; }

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; }

        public Guid? ConsultantId { get; set; }

        public DateTime? ConsultationDate { get; set; }

        public string? Complaints { get; set; }

        public string? PreviousDiagnoses { get; set; }

        public string? CurrentMedications { get; set; }
    }
}