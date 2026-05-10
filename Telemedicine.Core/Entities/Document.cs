using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telemedicine.Core.Entities;

namespace TelemedicineSystem.Core.Entities
{
    public class Document
    {
        [Key]
        public Guid DocumentId { get; set; }

        public Guid? EntryId { get; set; }

        public Guid? ConsultationId { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; }

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; }

        [Required]
        [MaxLength(50)]
        public string DocType { get; set; } // анализ, заключение, скан

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [Required]
        public Guid UploadedBy { get; set; }

        // Навигационные свойства
        [ForeignKey("EntryId")]
        public Entry Entry { get; set; }

        [ForeignKey("ConsultationId")]
        public Consultation Consultation { get; set; }

        [ForeignKey("UploadedBy")]
        public User UploadedByUser { get; set; }
    }
}