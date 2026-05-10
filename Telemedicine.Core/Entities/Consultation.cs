using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;
using Telemedicine.Core.Entities;

namespace TelemedicineSystem.Core.Entities
{
    public class Consultation
    {
        [Key]
        public Guid ConsultationId { get; set; }

        [Required]
        public Guid ApplicationId { get; set; }

        [Required]
        public Guid PatientId { get; set; }

        [Required]
        public Guid ConsultantId { get; set; }

        public DateTime Date { get; set; }

        public double? Cost { get; set; }

        [MaxLength(100)]
        public string PayOrder { get; set; }

        public long? ContractNumber { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } // scheduled, in_progress, completed, cancelled

        // Навигационные свойства
        [ForeignKey("ApplicationId")]
        public Application Application { get; set; }

        [ForeignKey("PatientId")]
        public Patient Patient { get; set; }

        [ForeignKey("ConsultantId")]
        public Consultant Consultant { get; set; }

        public ICollection<Entry> Entries { get; set; }
        public ICollection<Document> Documents { get; set; }
    }
}