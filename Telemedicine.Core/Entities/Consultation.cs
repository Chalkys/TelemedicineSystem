using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Telemedicine.Core.Entities;
using TelemedicineSystem.Core.Entities;

[Table("consultations")]
public class Consultation
{
    [Key]
    [Column("consultationid")]
    public Guid ConsultationId { get; set; }

    [Required]
    [Column("applicationid")]
    public Guid ApplicationId { get; set; }

    [Required]
    [Column("patientid")]
    public Guid PatientId { get; set; }

    [Required]
    [Column("consultantid")]
    public Guid ConsultantId { get; set; }

    [Column("date")]  // ← было без атрибута, добавляем
    public DateTime Date { get; set; }

    [Column("cost")]
    public double? Cost { get; set; }

    [MaxLength(100)]
    [Column("payorder")]
    public string? PayOrder { get; set; }

    [Column("contractnumber")]
    public long? ContractNumber { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; }

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