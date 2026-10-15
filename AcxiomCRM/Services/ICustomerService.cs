using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using System.Security.Claims;

namespace AcxiomCRM.Services
{
    public interface ICustomerService
    {
        Task<(bool Success, string? Error, Customer? Customer)> CreateCustomerAsync(Customer customer, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error, Customer? Customer)> UpdateCustomerAsync(Customer customer, ClaimsPrincipal user, string? ipAddress = null);
        Task<(bool Success, string? Error)> DeleteCustomerAsync(int customerId, ClaimsPrincipal user, string? ipAddress = null);
        Task<Customer?> GetCustomerByIdAsync(int customerId, ClaimsPrincipal user);
        Task<(List<Customer> Items, int TotalCount)> GetCustomersAsync(ClaimsPrincipal user, string? search, CustomerStatus? status, string? ownerId, string? sortBy, bool descending, int page, int pageSize);
        Task<bool> EmailExistsAsync(string email, int? excludeCustomerId = null);
        Task<bool> PhoneExistsAsync(string phone, int? excludeCustomerId = null);
    }
}
