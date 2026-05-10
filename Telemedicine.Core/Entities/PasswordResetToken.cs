using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Telemedicine.Core.Entities
{
    [Table("passwordresettokens")]
    public class PasswordResetToken
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("email")]
        public string Email { get; set; }

        [Column("token")]
        public string Token { get; set; }

        [Column("createdat")]
        public DateTime CreatedAt { get; set; }

        [Column("expiresat")]
        public DateTime ExpiresAt { get; set; }

        [Column("isused")]
        public bool IsUsed { get; set; }
    }
}