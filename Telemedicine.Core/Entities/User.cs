using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Telemedicine.Core.Entities
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("userid")]
        public Guid UserId { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("password")]
        public string PasswordHash { get; set; }

        [Column("role")]
        public string Role { get; set; }

        // Навигационные свойства
        public virtual Patient Patient { get; set; }
        public virtual Consultant Consultant { get; set; }
        public virtual Operator Operator { get; set; }
    }
}