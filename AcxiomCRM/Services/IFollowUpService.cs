using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public interface IFollowUpService
    {
        Task<(bool Success, string? Error, FollowUp? FollowUp)> CreateFollowUpAsync(FollowUp followUp, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error, FollowUp? FollowUp)> UpdateFollowUpAsync(FollowUp followUp, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error)> CompleteFollowUpAsync(int followUpId, string? remarks, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error)> RescheduleFollowUpAsync(int followUpId, DateTime newDate, string? remarks, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error)> CancelFollowUpAsync(int followUpId, ClaimsPrincipal user, string? ipAddress = null);
        Task<FollowUp?> GetFollowUpByIdAsync(int followUpId, ClaimsPrincipal user);
        Task<(List<FollowUp> Items, int TotalCount)> GetFollowUpsAsync(ClaimsPrincipal user, FollowUpStatus? status, FollowUpType? type, string? assignedTo, DateTime? fromDate, DateTime? toDate, int page, int pageSize);
        Task<List<FollowUp>> GetUpcomingFollowUpsAsync(ClaimsPrincipal user, int days = 7);
        Task<List<FollowUp>> GetOverdueFollowUpsAsync(ClaimsPrincipal user);
    }
}
