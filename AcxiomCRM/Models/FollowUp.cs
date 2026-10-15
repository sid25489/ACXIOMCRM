using AcxiomCRM.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models
{
    public class FollowUp
    {
        [Key]
        public int FollowUpId { get; set; }

        public int? CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }

        public int? LeadId { get; set; }

        [ForeignKey(nameof(LeadId))]
        public virtual Lead? Lead { get; set; }

        public int? OpportunityId { get; set; }

        [ForeignKey(nameof(OpportunityId))]
        public virtual Opportunity? Opportunity { get; set; }

        [Required]
        public DateTime FollowUpDate { get; set; }

        public FollowUpType FollowUpType { get; set; } = FollowUpType.Call;

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Remarks { get; set; }

        public FollowUpStatus Status { get; set; } = FollowUpStatus.Planned;

        [Required]
        public string AssignedTo { get; set; } = string.Empty;

        [ForeignKey(nameof(AssignedTo))]
        public virtual ApplicationUser? AssignedUser { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime? CompletedDate { get; set; }

        [NotMapped]
        public bool IsOverdue => Status == FollowUpStatus.Planned && FollowUpDate.Date < DateTime.UtcNow.Date;
    }
}
