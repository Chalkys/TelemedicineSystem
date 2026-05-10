using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Telemedicine.Core.Entities
{
    [Table("patients")]
    public class Patient
    {
        [Key]
        [Column("patientid")]
        public Guid PatientId { get; set; }

        [ForeignKey("User")]
        [Column("userid")]
        public Guid UserId { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("surname")]
        public string Surname { get; set; }

        [Column("middlename")]
        public string MiddleName { get; set; }

        [Column("dateofbirth")]
        public DateTime DateOfBirth { get; set; }

        [Column("gender")]
        public string Gender { get; set; }

        [Column("contactinfo")]
        public string ContactInfo { get; set; }

        [Column("insurancepolicynumber")]
        public string InsurancePolicyNumber { get; set; }

        public virtual User User { get; set; }
    }
}