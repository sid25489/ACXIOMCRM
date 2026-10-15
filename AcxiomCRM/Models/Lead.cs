using AcxiomCRM.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models
{
    public class Lead
    {
        [Key]
        public int LeadId { get; set; }

        [Required]
        [MaxLength(30)]
        public string LeadCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string LeadName { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(150)]
        public string? CompanyName { get; set; }

        public LeadSource Source { get; set; } = LeadSource.Website;

        public LeadStatus Status { get; set; } = LeadStatus.New;

        public LeadPriority Priority { get; set; } = LeadPriority.Medium;

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 1000000000)]
        public decimal ExpectedValue { get; set; } = 0;

        [Required]
        public string AssignedTo { get; set; } = string.Empty;

        [ForeignKey(nameof(AssignedTo))]
        public virtual ApplicationUser? AssignedUser { get; set; }

        public int? CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? ModifiedDate { get; set; }

        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        public DateTime? ConversionDate { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }

        // Navigations
        public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
        public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
        public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    }
}
