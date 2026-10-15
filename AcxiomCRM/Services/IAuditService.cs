using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public interface IAuditService
    {
        Task LogAsync(string? userId, string action, string entityName, string? recordId, string? oldValue, string? newValue, string? details = null, string? ipAddress = null, string result = "Success");
        Task<List<AuditLog>> GetLogsAsync(string? userId = null, string? module = null, string? action = null, DateTime? fromDate = null, DateTime? toDate = null, int maxRecords = 100);
    }
}
