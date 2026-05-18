using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TelemedicineSystem.Core.Entities
{
    [Table("entry_analyses")]
    public class EntryAnalysis
    {
        [Key]
        [Column("entry_analysis_id")]
        public Guid EntryAnalysisId { get; set; }

        [Required]
        [Column("entry_id")]
        public Guid EntryId { get; set; }

        [Required]
        [Column("analysis_id")]
        public Guid AnalysisId { get; set; }

        [ForeignKey("EntryId")]
        public Entry Entry { get; set; }

        [ForeignKey("AnalysisId")]
        public Analysis Analysis { get; set; }
    }
}