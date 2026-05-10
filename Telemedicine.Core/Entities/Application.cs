using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telemedicine.Core.Entities;

namespace TelemedicineSystem.Core.Entities
{
    public class Application
    {
        [Key]
        public Guid ApplicationId { get; set; }

        [Required]
        public Guid PatientId { get; set; }

        [Required]
        [MaxLength(1)]
        public char Type { get; set; } // 'F' - бесплатная, 'P' - платная

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } // pending, accepted, rejected

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        [ForeignKey("PatientId")]
        public Patient Patient { get; set; }

        public Consultation Consultation { get; set; }
    }
}