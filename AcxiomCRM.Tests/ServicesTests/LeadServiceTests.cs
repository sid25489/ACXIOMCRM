using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using AcxiomCRM.Tests.TestHelpers;
using Moq;
using System.Security.Claims;
using Xunit;

namespace AcxiomCRM.Tests.ServicesTests
{
    public class LeadServiceTests
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
        public async Task CreateLead_WithNegativeValue_Fails()
        {
            var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
            var accessService = new CrmAccessService();
            var auditMock = new Mock<IAuditService>();
            var dupService = new DuplicateDetectionService();

            var service = new LeadService(db, accessService, auditMock.Object, dupService);
            var user = CreateUserPrincipal("sales1", AppRoles.SalesExecutive);

            var lead = new Lead
            {
                LeadName = "Negative Value Lead",
                ExpectedValue = -5000m
            };

            var (success, error, _) = await service.CreateLeadAsync(lead, user);

            Assert.False(success);
            Assert.Contains("negative", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void IsValidStatusTransition_EnforcesWorkflow()
        {
            var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
            var service = new LeadService(db, new CrmAccessService(), Mock.Of<IAuditService>(), new DuplicateDetectionService());

            // Valid transitions
            Assert.True(service.IsValidStatusTransition(LeadStatus.New, LeadStatus.Contacted, false));
            Assert.True(service.IsValidStatusTransition(LeadStatus.Contacted, LeadStatus.Qualified, false));
            Assert.True(service.IsValidStatusTransition(LeadStatus.Qualified, LeadStatus.Converted, false));

            // Invalid transitions
            Assert.False(service.IsValidStatusTransition(LeadStatus.New, LeadStatus.Qualified, false)); // Cannot skip Contacted
            Assert.False(service.IsValidStatusTransition(LeadStatus.Converted, LeadStatus.New, true)); // Converted is terminal
        }

        [Fact]
        public async Task ConvertLead_WhenQualified_CreatesCustomerAndOpportunity()
        {
            var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
            var service = new LeadService(db, new CrmAccessService(), Mock.Of<IAuditService>(), new DuplicateDetectionService());
            var user = CreateUserPrincipal("sales1", AppRoles.SalesExecutive);

            var (createSuccess, _, lead) = await service.CreateLeadAsync(new Lead
            {
                LeadName = "Qualifiable Prospect",
                Email = "prospect@testcorp.com",
                Phone = "9876543210",
                CompanyName = "TestCorp India",
                ExpectedValue = 500000m
            }, user);

            Assert.True(createSuccess);

            // Move to Qualified
            lead!.Status = LeadStatus.Qualified;
            await db.SaveChangesAsync();

            var convertModel = new ConvertLeadModel
            {
                LeadId = lead.LeadId,
                CreateOpportunity = true,
                OpportunityName = "TestCorp Cloud CRM Deal",
                OpportunityAmount = 750000m,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(20)
            };

            var (convSuccess, error, customer, opp) = await service.ConvertLeadAsync(convertModel, user);

            Assert.True(convSuccess);
            Assert.NotNull(customer);
            Assert.NotNull(opp);
            Assert.Equal(LeadStatus.Converted, lead.Status);
            Assert.Equal(750000m, opp.Amount);
            Assert.Equal(customer.CustomerId, opp.CustomerId);
        }

        [Fact]
        public async Task ConvertLead_WhenNotQualified_RejectsConversion()
        {
            var db = TestDbContextFactory.CreateInMemoryDbContext(Guid.NewGuid().ToString());
            var service = new LeadService(db, new CrmAccessService(), Mock.Of<IAuditService>(), new DuplicateDetectionService());
            var user = CreateUserPrincipal("sales1", AppRoles.SalesExecutive);

            var (_, _, lead) = await service.CreateLeadAsync(new Lead
            {
                LeadName = "Unqualified Lead",
                Status = LeadStatus.New
            }, user);

            var convertModel = new ConvertLeadModel
            {
                LeadId = lead!.LeadId
            };

            var (convSuccess, error, _, _) = await service.ConvertLeadAsync(convertModel, user);

            Assert.False(convSuccess);
            Assert.Contains("Qualified", error, StringComparison.OrdinalIgnoreCase);
        }
    }
}
