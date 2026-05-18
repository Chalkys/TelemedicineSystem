using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TelemedicineSystem.Core.Entities
{
    [Table("diseases")]
    public class Disease
    {
        [Key]
        [Column("disease_id")]
        public Guid DiseaseId { get; set; }

        [Required]
        [MaxLength(10)]
        [Column("mkb_code")]
        public string MkbCode { get; set; }

        [Required]
        [MaxLength(500)]
        [Column("name")]
        public string Name { get; set; }
    }
}