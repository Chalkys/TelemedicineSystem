using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Telemedicine.Core.Entities
{
    [Table("consultants")]
    public class Consultant
    {
        [Key]
        [Column("consultantid")]
        public Guid ConsultantId { get; set; }

        [ForeignKey("User")]
        [Column("userid")]
        public Guid UserId { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("surname")]
        public string Surname { get; set; }

        [Column("middlename")]
        public string? MiddleName { get; set; }

        [Column("specialty")]
        public string Specialty { get; set; }

        [Column("post")]
        public string? Post { get; set; }

        [Column("educationinfo")]
        public string? EducationInfo { get; set; }

        [Column("seniority")]
        public int? Seniority { get; set; }

        [Column("expirience")]
        public string? Experience { get; set; }

        [Column("workhours")]
        public string? WorkHours { get; set; }

        [Column("connectiontocenter")]
        public string? ConnectionToCenter { get; set; }

        public virtual User User { get; set; }
    }
}