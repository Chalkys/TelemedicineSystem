using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Telemedicine.Core.Entities;

namespace TelemedicineSystem.Core.Entities
{
    [Table("consultation_messages")]
    public class ConsultationMessage
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("consultation_id")]
        public Guid ConsultationId { get; set; }

        [Required]
        [Column("sender_id")]
        public Guid SenderId { get; set; }

        [Required]
        [Column("text")]
        public string Text { get; set; }

        [Column("sent_at")]
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ConsultationId")]
        public Consultation Consultation { get; set; }

        [ForeignKey("SenderId")]
        public User Sender { get; set; }
    }
}