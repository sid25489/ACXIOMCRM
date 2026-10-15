using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public interface IActivityService
    {
        Task<(bool Success, string? Error, Activity? Activity)> CreateActivityAsync(Activity activity, ClaimsPrincipal user, string? ipAddress = null);
        Task<Activity?> GetActivityByIdAsync(int activityId, ClaimsPrincipal user);
        Task<(List<Activity> Items, int TotalCount)> GetActivitiesAsync(ClaimsPrincipal user, ActivityType? type, ActivityStatus? status, string? assignedTo, int? customerId, int? leadId, int? opportunityId, int page, int pageSize);
    }
}
