using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telemedicine.Core.Entities;

namespace TelemedicineSystem.Core.Entities
{
    [Table("treatment_courses")]
    public class TreatmentCourse
    {
        [Key]
        [Column("treatment_course_id")]
        public Guid TreatmentCourseId { get; set; }

        [Required]
        [Column("patient_id")]
        public Guid PatientId { get; set; }

        [Required]
        [Column("consultant_id")]
        public Guid ConsultantId { get; set; }

        [Column("start_date")]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("cause_of_end")]
        public string? CauseOfEnd { get; set; }

        [Column("history_code")]
        public string? HistoryCode { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "active";

        // Навигационные свойства
        [ForeignKey("PatientId")]
        public Patient Patient { get; set; }

        [ForeignKey("ConsultantId")]
        public Consultant Consultant { get; set; }

        public ICollection<Entry> Entries { get; set; }
    }
}