using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telemedicine.Core.Entities;

namespace TelemedicineSystem.Core.Entities
{
    [Table("documents")]
    public class Document
    {
        [Key]
        [Column("documentid")]
        public Guid DocumentId { get; set; }

        [Column("entryid")]
        public Guid? EntryId { get; set; }

        [Column("consultationid")]
        public Guid? ConsultationId { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("filename")]
        public string FileName { get; set; }

        [Required]
        [MaxLength(500)]
        [Column("filepath")]
        public string FilePath { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("doctype")]
        public string DocType { get; set; } // анализ, заключение, скан

        [Column("uploaddate")]
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Column("uploadedby")]
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