using AcxiomCRM.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs
{
    // Auth DTOs
    public class ApiLoginRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;
    }

    public class ApiLoginResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Role { get; set; }
    }

    // Customer DTOs
    public class CustomerDto
    {
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public CustomerStatus Status { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateCustomerDto
    {
        [Required(ErrorMessage = "Customer name is required.")]
        [MaxLength(150, ErrorMessage = "Customer name cannot exceed 150 characters.")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required.")]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? CompanyName { get; set; }

        [MaxLength(300)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(100)]
        public string? State { get; set; }

        public CustomerStatus Status { get; set; } = CustomerStatus.Active;

        public string? OwnerId { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    public class UpdateCustomerDto : CreateCustomerDto
    {
    }

    // Lead DTOs
    public class LeadDto
    {
        public int LeadId { get; set; }
        public string LeadCode { get; set; } = string.Empty;
        public string LeadName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? CompanyName { get; set; }
        public LeadSource Source { get; set; }
        public LeadStatus Status { get; set; }
        public LeadPriority Priority { get; set; }
        public decimal ExpectedValue { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public string? AssignedUserName { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ConversionDate { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateLeadDto
    {
        [Required(ErrorMessage = "Lead name is required.")]
        [MaxLength(150)]
        public string LeadName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(150)]
        public string? CompanyName { get; set; }

        public LeadSource Source { get; set; } = LeadSource.Website;
        public LeadStatus Status { get; set; } = LeadStatus.New;
        public LeadPriority Priority { get; set; } = LeadPriority.Medium;

        [Range(0, 1000000000, ErrorMessage = "Expected value must be non-negative.")]
        public decimal ExpectedValue { get; set; }

        public string? AssignedTo { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    public class UpdateLeadDto : CreateLeadDto
    {
    }

    // Opportunity DTOs
    public class OpportunityDto
    {
        public int OpportunityId { get; set; }
        public string OpportunityName { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? LeadId { get; set; }
        public decimal Amount { get; set; }
        public OpportunityStage Stage { get; set; }
        public int Probability { get; set; }
        public DateTime ExpectedCloseDate { get; set; }
        public OpportunityStatus Status { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public string? AssignedUserName { get; set; }
        public decimal WeightedPipelineValue => (Amount * Probability) / 100m;
        public DateTime CreatedDate { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateOpportunityDto
    {
        [Required(ErrorMessage = "Opportunity name is required.")]
        [MaxLength(150)]
        public string OpportunityName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer is required.")]
        public int CustomerId { get; set; }

        public int? LeadId { get; set; }

        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, 1000000000, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        public int Probability { get; set; } = 20;

        [Required(ErrorMessage = "Expected Close Date is required.")]
        public DateTime ExpectedCloseDate { get; set; }

        public string? AssignedTo { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }

    public class UpdateOpportunityDto : CreateOpportunityDto
    {
        public OpportunityStatus Status { get; set; } = OpportunityStatus.Open;
    }

    // FollowUp DTOs
    public class FollowUpDto
    {
        public int FollowUpId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? LeadId { get; set; }
        public string? LeadName { get; set; }
        public int? OpportunityId { get; set; }
        public string? OpportunityName { get; set; }
        public DateTime FollowUpDate { get; set; }
        public FollowUpType FollowUpType { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? Remarks { get; set; }
        public FollowUpStatus Status { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public string? AssignedUserName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public bool IsOverdue => Status == FollowUpStatus.Planned && FollowUpDate.Date < DateTime.UtcNow.Date;
    }

    public class CreateFollowUpDto
    {
        public int? CustomerId { get; set; }
        public int? LeadId { get; set; }
        public int? OpportunityId { get; set; }

        [Required(ErrorMessage = "Follow-up date is required.")]
        public DateTime FollowUpDate { get; set; }

        public FollowUpType FollowUpType { get; set; } = FollowUpType.Call;

        [Required(ErrorMessage = "Subject is required.")]
        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Remarks { get; set; }

        public FollowUpStatus Status { get; set; } = FollowUpStatus.Planned;

        public string? AssignedTo { get; set; }
    }

    public class UpdateFollowUpDto : CreateFollowUpDto
    {
    }
}
