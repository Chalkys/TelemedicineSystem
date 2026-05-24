using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TelemedicineSystem.Core.Entities
{
    [Table("analysis_referrals")]
    public class AnalysisReferral
    {
        [Key]
        [Column("referral_id")]
        public Guid ReferralId { get; set; }

        [Required]
        [Column("entry_id")]
        public Guid EntryId { get; set; }

        [Column("patient_name")]
        public string? PatientName { get; set; }

        [Column("patient_age")]
        public int? PatientAge { get; set; }

        [Column("medical_book_number")]
        public string? MedicalBookNumber { get; set; }

        [Column("organization_name")]
        public string? OrganizationName { get; set; }

        [Column("referral_date")]
        public DateTime ReferralDate { get; set; } = DateTime.UtcNow;

        [Column("doctor_name")]
        public string? DoctorName { get; set; }

        [MaxLength(10)]
        [Column("mkb_code")]
        public string? MkbCode { get; set; }

        [Column("referral_purpose")]
        public string? ReferralPurpose { get; set; }

        [Column("tests")]
        public string? Tests { get; set; }

        [MaxLength(50)]
        [Column("service_code")]
        public string? ServiceCode { get; set; }

        [ForeignKey("EntryId")]
        public Entry Entry { get; set; }
    }
}