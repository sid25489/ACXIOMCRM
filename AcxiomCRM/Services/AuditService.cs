using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditService> _logger;

        public AuditService(ApplicationDbContext context, ILogger<AuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAsync(
            string? userId,
            string action,
            string entityName,
            string? recordId,
            string? oldValue,
            string? newValue,
            string? details = null,
            string? ipAddress = null,
            string result = "Success")
        {
            try
            {
                var audit = new AuditLog
                {
                    UserId = userId,
                    Action = action,
                    EntityName = entityName,
                    RecordId = recordId,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Details = details,
                    IpAddress = ipAddress,
                    Result = result,
                    CreatedDate = DateTime.UtcNow
                };

                _context.AuditLogs.Add(audit);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist audit log for Action: {Action}, Entity: {EntityName}, Record: {RecordId}", action, entityName, recordId);
            }
        }

        public async Task<List<AuditLog>> GetLogsAsync(
            string? userId = null,
            string? module = null,
            string? action = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int maxRecords = 100)
        {
            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(a => a.UserId == userId);
            }

            if (!string.IsNullOrWhiteSpace(module))
            {
                query = query.Where(a => a.EntityName == module);
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(a => a.Action == action);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(a => a.CreatedDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(a => a.CreatedDate <= endOfDay);
            }

            return await query
                .OrderByDescending(a => a.CreatedDate)
                .Take(maxRecords)
                .ToListAsync();
        }
    }
}
