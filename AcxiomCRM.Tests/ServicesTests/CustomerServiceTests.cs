using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using AcxiomCRM.Tests.TestHelpers;
using Moq;
using System.Security.Claims;
using Xunit;

namespace AcxiomCRM.Tests.ServicesTests
{
    public class CustomerServiceTests
    {
        private ClaimsPrincipal CreateUserPrincipal(string userId, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, $"user_{userId}@test.com"),
                new Claim(ClaimTypes.Role, role)
            };
            // Explicitly pass nameType and roleType so IsInRole() resolves correctly in tests
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth", ClaimTypes.Name, ClaimTypes.Role));
        }

        [Fact]
        public async Task CreateCustomer_WithInvalidEmail_ReturnsFailure()
        {
            var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
            var accessService = new CrmAccessService();
            var auditMock = new Mock<IAuditService>();
            var dupService = new DuplicateDetectionService();

            var service = new CustomerService(db, accessService, auditMock.Object, dupService);
            var user = CreateUserPrincipal("sales1", AppRoles.SalesExecutive);

            var customer = new Customer
            {
                CustomerName = "Test Client",
                Email = "invalid-email-string",
                Phone = "9876543210"
            };

            var (success, error, created) = await service.CreateCustomerAsync(customer, user);

            Assert.False(success);
            Assert.Contains("valid email", error, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("12345")] // Too short
        [InlineData("abcdefghij")] // Non-digits
        [InlineData("1234567890")] // Starts with 1 (not 6-9)
        public async Task CreateCustomer_WithInvalidPhone_ReturnsFailure(string invalidPhone)
        {
            var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
            var accessService = new CrmAccessService();
            var auditMock = new Mock<IAuditService>();
            var dupService = new DuplicateDetectionService();

            var service = new CustomerService(db, accessService, auditMock.Object, dupService);
            var user = CreateUserPrincipal("sales1", AppRoles.SalesExecutive);

            var customer = new Customer
            {
                CustomerName = "Test Client",
                Email = "client@test.com",
                Phone = invalidPhone
            };

            var (success, error, created) = await service.CreateCustomerAsync(customer, user);

            Assert.False(success);
            Assert.Contains("10-digit Indian mobile", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateCustomer_WithDuplicateEmail_ReturnsDuplicateError()
        {
            var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
            var accessService = new CrmAccessService();
            var auditMock = new Mock<IAuditService>();
            var dupService = new DuplicateDetectionService();

            var service = new CustomerService(db, accessService, auditMock.Object, dupService);
            var user = CreateUserPrincipal("sales1", AppRoles.SalesExecutive);

            var cust1 = new Customer
            {
                CustomerName = "Client One",
                Email = "same@test.com",
                Phone = "9876543210"
            };
            await service.CreateCustomerAsync(cust1, user);

            var cust2 = new Customer
            {
                CustomerName = "Client Two",
                Email = "SAME@TEST.COM", // Case-insensitive duplicate
                Phone = "9876543211"
            };
            var (success, error, _) = await service.CreateCustomerAsync(cust2, user);

            Assert.False(success);
            Assert.Contains("already registered", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateCustomer_WithValidData_GeneratesCodeAndSaves()
        {
            var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
            var accessService = new CrmAccessService();
            var auditMock = new Mock<IAuditService>();
            var dupService = new DuplicateDetectionService();

            var service = new CustomerService(db, accessService, auditMock.Object, dupService);
            var user = CreateUserPrincipal("sales1", AppRoles.SalesExecutive);

            var customer = new Customer
            {
                CustomerName = "Valid Enterprise",
                Email = "valid@enterprise.com",
                Phone = "+91 98765 43210" // Contains +91 and spaces, should normalize
            };

            var (success, error, created) = await service.CreateCustomerAsync(customer, user);

            Assert.True(success);
            Assert.NotNull(created);
            Assert.Equal("9876543210", created.Phone);
            Assert.StartsWith("CUST-", created.CustomerCode);
            Assert.Equal("sales1", created.OwnerId);
        }

        [Fact]
        public async Task SalesExecutive_CannotAccess_OtherExecutiveCustomer()
        {
            var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
            var accessService = new CrmAccessService();
            var auditMock = new Mock<IAuditService>();
            var dupService = new DuplicateDetectionService();

            var service = new CustomerService(db, accessService, auditMock.Object, dupService);
            var user1 = CreateUserPrincipal("sales1", AppRoles.SalesExecutive);
            var user2 = CreateUserPrincipal("sales2", AppRoles.SalesExecutive);

            var (success, createError, created) = await service.CreateCustomerAsync(new Customer
            {
                CustomerName = "Sales1 Customer",
                Email = "sales1cust@test.com",
                Phone = "9876543210"
            }, user1);

            Assert.True(success, $"Customer creation failed unexpectedly: {createError}");
            Assert.NotNull(created);
            Assert.Equal("sales1", created.OwnerId);

            // Sales2 tries to fetch Sales1's customer — should be blocked by row-level security
            var retrieved = await service.GetCustomerByIdAsync(created.CustomerId, user2);
            Assert.Null(retrieved);

            // Owner (sales1) can access their own customer
            var ownerRetrieved = await service.GetCustomerByIdAsync(created.CustomerId, user1);
            Assert.NotNull(ownerRetrieved);

            // Admin can access all customers
            // Use a new ClaimsIdentity with explicit roleClaimType to ensure IsInRole works correctly
            var adminClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "admin1"),
                new Claim(ClaimTypes.Name, "admin@test.com"),
                new Claim(ClaimTypes.Role, AppRoles.Admin)
            };
            var adminIdentity = new ClaimsIdentity(adminClaims, "TestAuth", ClaimTypes.Name, ClaimTypes.Role);
            var adminUser = new ClaimsPrincipal(adminIdentity);

            var adminRetrieved = await service.GetCustomerByIdAsync(created.CustomerId, adminUser);
            Assert.NotNull(adminRetrieved);
        }
    }
}
