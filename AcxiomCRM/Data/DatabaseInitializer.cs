using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Data
{
    public static class DatabaseInitializer
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

            // Apply migrations
            try
            {
                await context.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MigrateAsync failed or using in-memory database. Ensuring created.");
                await context.Database.EnsureCreatedAsync();
            }

            // 1. Seed Roles
            foreach (var roleName in AppRoles.AllRoles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                    logger.LogInformation("Seeded role: {Role}", roleName);
                }
            }

            // 2. Seed Default Users
            var adminEmail = "admin@acxiomcrm.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "Acxiom System Administrator",
                    Department = "Administration",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, AppRoles.Admin);
                    logger.LogInformation("Seeded admin user: {Email}", adminEmail);
                }
            }

            var managerEmail = "manager@acxiomcrm.com";
            var managerUser = await userManager.FindByEmailAsync(managerEmail);
            if (managerUser == null)
            {
                managerUser = new ApplicationUser
                {
                    UserName = managerEmail,
                    Email = managerEmail,
                    FullName = "Operations Sales Manager",
                    Department = "Enterprise Sales",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(managerUser, "Manager@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(managerUser, AppRoles.Manager);
                    logger.LogInformation("Seeded manager user: {Email}", managerEmail);
                }
            }

            var salesEmail = "sales@acxiomcrm.com";
            var salesUser = await userManager.FindByEmailAsync(salesEmail);
            if (salesUser == null)
            {
                salesUser = new ApplicationUser
                {
                    UserName = salesEmail,
                    Email = salesEmail,
                    FullName = "Rahul Sharma",
                    Department = "Direct Sales",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(salesUser, "Sales@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(salesUser, AppRoles.SalesExecutive);
                    logger.LogInformation("Seeded sales user: {Email}", salesEmail);
                }
            }

            var sales2Email = "sales2@acxiomcrm.com";
            var sales2User = await userManager.FindByEmailAsync(sales2Email);
            if (sales2User == null)
            {
                sales2User = new ApplicationUser
                {
                    UserName = sales2Email,
                    Email = sales2Email,
                    FullName = "Priya Patel",
                    Department = "Direct Sales",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(sales2User, "Sales@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(sales2User, AppRoles.SalesExecutive);
                    logger.LogInformation("Seeded second sales user: {Email}", sales2Email);
                }
            }

            // 3. Seed Sample Data if no customers exist
            if (!await context.Customers.AnyAsync())
            {
                var cust1 = new Customer
                {
                    CustomerCode = "CUST-00001",
                    CustomerName = "Tata Consultancy Services",
                    Email = "procurement@tcs-sample.com",
                    Phone = "9876543210",
                    CompanyName = "TCS Ltd",
                    Address = "Bandra Kurla Complex",
                    City = "Mumbai",
                    State = "Maharashtra",
                    Status = CustomerStatus.Active,
                    OwnerId = salesUser!.Id,
                    CreatedBy = adminUser!.UserName ?? "System",
                    CreatedDate = DateTime.UtcNow.AddMonths(-2),
                    Notes = "Strategic IT enterprise account"
                };

                var cust2 = new Customer
                {
                    CustomerCode = "CUST-00002",
                    CustomerName = "Infosys Enterprise Solutions",
                    Email = "contact@infosys-sample.com",
                    Phone = "9812345678",
                    CompanyName = "Infosys Ltd",
                    Address = "Electronics City Phase 1",
                    City = "Bangalore",
                    State = "Karnataka",
                    Status = CustomerStatus.Active,
                    OwnerId = salesUser.Id,
                    CreatedBy = adminUser.UserName ?? "System",
                    CreatedDate = DateTime.UtcNow.AddMonths(-1),
                    Notes = "Expanding cloud infrastructure services"
                };

                var cust3 = new Customer
                {
                    CustomerCode = "CUST-00003",
                    CustomerName = "Reliance Digital Commerce",
                    Email = "crm@reliance-sample.com",
                    Phone = "9988776655",
                    CompanyName = "Reliance Retail",
                    Address = "Maker Chambers IV, Nariman Point",
                    City = "Mumbai",
                    State = "Maharashtra",
                    Status = CustomerStatus.Active,
                    OwnerId = sales2User!.Id,
                    CreatedBy = adminUser.UserName ?? "System",
                    CreatedDate = DateTime.UtcNow.AddDays(-20),
                    Notes = "High priority retail partner"
                };

                context.Customers.AddRange(cust1, cust2, cust3);
                await context.SaveChangesAsync();

                // Seed Leads
                var lead1 = new Lead
                {
                    LeadCode = "LEAD-00001",
                    LeadName = "HDFC FinTech Division",
                    Email = "banking@hdfc-sample.com",
                    Phone = "9123456780",
                    CompanyName = "HDFC Bank Ltd",
                    Source = LeadSource.Referral,
                    Status = LeadStatus.Qualified,
                    Priority = LeadPriority.High,
                    ExpectedValue = 450000.00m,
                    AssignedTo = salesUser.Id,
                    CreatedBy = salesUser.UserName ?? "Sales",
                    CreatedDate = DateTime.UtcNow.AddDays(-15),
                    Notes = "Looking for secure transactional CRM integration"
                };

                var lead2 = new Lead
                {
                    LeadCode = "LEAD-00002",
                    LeadName = "Zomato Logistics Corp",
                    Email = "supply@zomato-sample.com",
                    Phone = "9234567891",
                    CompanyName = "Zomato Media Pvt Ltd",
                    Source = LeadSource.Website,
                    Status = LeadStatus.New,
                    Priority = LeadPriority.Urgent,
                    ExpectedValue = 280000.00m,
                    AssignedTo = salesUser.Id,
                    CreatedBy = salesUser.UserName ?? "Sales",
                    CreatedDate = DateTime.UtcNow.AddDays(-5),
                    Notes = "Inbound website lead interested in fleet CRM"
                };

                var lead3 = new Lead
                {
                    LeadCode = "LEAD-00003",
                    LeadName = "Wipro Technologies",
                    Email = "solutions@wipro-sample.com",
                    Phone = "9345678902",
                    CompanyName = "Wipro Ltd",
                    Source = LeadSource.Advertisement,
                    Status = LeadStatus.Contacted,
                    Priority = LeadPriority.Medium,
                    ExpectedValue = 195000.00m,
                    AssignedTo = sales2User.Id,
                    CreatedBy = sales2User.UserName ?? "Sales2",
                    CreatedDate = DateTime.UtcNow.AddDays(-8),
                    Notes = "Initial demo scheduled"
                };

                var lead4 = new Lead
                {
                    LeadCode = "LEAD-00004",
                    LeadName = "Airtel Business Telecom",
                    Email = "enterprise@airtel-sample.com",
                    Phone = "9456789013",
                    CompanyName = "Bharti Airtel Ltd",
                    Source = LeadSource.ColdCall,
                    Status = LeadStatus.Converted,
                    Priority = LeadPriority.High,
                    ExpectedValue = 650000.00m,
                    AssignedTo = salesUser.Id,
                    CustomerId = cust1.CustomerId,
                    ConversionDate = DateTime.UtcNow.AddDays(-3),
                    CreatedBy = salesUser.UserName ?? "Sales",
                    CreatedDate = DateTime.UtcNow.AddDays(-25),
                    Notes = "Successfully converted to Tata account expansion"
                };

                context.Leads.AddRange(lead1, lead2, lead3, lead4);
                await context.SaveChangesAsync();

                // Seed Opportunities
                var opp1 = new Opportunity
                {
                    OpportunityName = "TCS Cloud Migration CRM Suite",
                    CustomerId = cust1.CustomerId,
                    LeadId = lead4.LeadId,
                    Amount = 750000.00m,
                    Stage = OpportunityStage.Proposal,
                    Probability = 60,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(25),
                    Status = OpportunityStatus.Open,
                    AssignedTo = salesUser.Id,
                    CreatedBy = salesUser.UserName ?? "Sales",
                    CreatedDate = DateTime.UtcNow.AddDays(-10),
                    Notes = "Finalizing scope of work document"
                };

                var opp2 = new Opportunity
                {
                    OpportunityName = "Infosys AI Automation Portal",
                    CustomerId = cust2.CustomerId,
                    Amount = 1200000.00m,
                    Stage = OpportunityStage.Negotiation,
                    Probability = 80,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(15),
                    Status = OpportunityStatus.Open,
                    AssignedTo = salesUser.Id,
                    CreatedBy = salesUser.UserName ?? "Sales",
                    CreatedDate = DateTime.UtcNow.AddDays(-20),
                    Notes = "Contract with legal team for SLA signoff"
                };

                var opp3 = new Opportunity
                {
                    OpportunityName = "Reliance Omnichannel Integration",
                    CustomerId = cust3.CustomerId,
                    Amount = 900000.00m,
                    Stage = OpportunityStage.Won,
                    Probability = 100,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(-2),
                    Status = OpportunityStatus.Won,
                    AssignedTo = sales2User.Id,
                    CreatedBy = sales2User.UserName ?? "Sales2",
                    CreatedDate = DateTime.UtcNow.AddMonths(-1),
                    Notes = "Deal closed and purchase order received"
                };

                var opp4 = new Opportunity
                {
                    OpportunityName = "TCS Hardware Support Renewals",
                    CustomerId = cust1.CustomerId,
                    Amount = 350000.00m,
                    Stage = OpportunityStage.Lost,
                    Probability = 0,
                    ExpectedCloseDate = DateTime.UtcNow.AddDays(-12),
                    Status = OpportunityStatus.Lost,
                    AssignedTo = salesUser.Id,
                    CreatedBy = salesUser.UserName ?? "Sales",
                    CreatedDate = DateTime.UtcNow.AddDays(-40),
                    Notes = "Customer chose internal IT tooling"
                };

                context.Opportunities.AddRange(opp1, opp2, opp3, opp4);
                await context.SaveChangesAsync();

                // Seed FollowUps
                var follow1 = new FollowUp
                {
                    CustomerId = cust1.CustomerId,
                    OpportunityId = opp1.OpportunityId,
                    FollowUpDate = DateTime.UtcNow.AddDays(2),
                    FollowUpType = FollowUpType.Meeting,
                    Subject = "Technical Architecture Review",
                    Remarks = "Present security architecture and SSO integration",
                    Status = FollowUpStatus.Planned,
                    AssignedTo = salesUser.Id,
                    CreatedBy = salesUser.UserName ?? "Sales",
                    CreatedDate = DateTime.UtcNow.AddDays(-1)
                };

                var follow2 = new FollowUp
                {
                    CustomerId = cust2.CustomerId,
                    OpportunityId = opp2.OpportunityId,
                    FollowUpDate = DateTime.UtcNow.AddDays(-2), // Overdue
                    FollowUpType = FollowUpType.Call,
                    Subject = "Legal Terms and Pricing Discussion",
                    Remarks = "Confirm final payment milestones",
                    Status = FollowUpStatus.Planned,
                    AssignedTo = salesUser.Id,
                    CreatedBy = salesUser.UserName ?? "Sales",
                    CreatedDate = DateTime.UtcNow.AddDays(-4)
                };

                var follow3 = new FollowUp
                {
                    LeadId = lead1.LeadId,
                    FollowUpDate = DateTime.UtcNow.AddDays(4),
                    FollowUpType = FollowUpType.Email,
                    Subject = "Follow up on Product Demo Deck",
                    Remarks = "Send brochure and pricing sheet",
                    Status = FollowUpStatus.Planned,
                    AssignedTo = salesUser.Id,
                    CreatedBy = salesUser.UserName ?? "Sales",
                    CreatedDate = DateTime.UtcNow.AddDays(-2)
                };

                var follow4 = new FollowUp
                {
                    CustomerId = cust3.CustomerId,
                    OpportunityId = opp3.OpportunityId,
                    FollowUpDate = DateTime.UtcNow.AddDays(-3),
                    FollowUpType = FollowUpType.Meeting,
                    Subject = "Contract Signing Ceremony",
                    Remarks = "Signed contract delivered to executive team",
                    Status = FollowUpStatus.Completed,
                    AssignedTo = sales2User.Id,
                    CreatedBy = sales2User.UserName ?? "Sales2",
                    CompletedDate = DateTime.UtcNow.AddDays(-3),
                    CreatedDate = DateTime.UtcNow.AddDays(-10)
                };

                context.FollowUps.AddRange(follow1, follow2, follow3, follow4);
                await context.SaveChangesAsync();

                // Seed Activities
                var act1 = new Activity
                {
                    ActivityType = ActivityType.Meeting,
                    Subject = "Executive briefing on Q4 CRM expansion",
                    Description = "Met CIO and VP of Procurement. Positive feedback on pipeline roadmap.",
                    ActivityDate = DateTime.UtcNow.AddDays(-5),
                    CustomerId = cust1.CustomerId,
                    OpportunityId = opp1.OpportunityId,
                    AssignedTo = salesUser.Id,
                    Status = ActivityStatus.Completed,
                    CreatedBy = salesUser.UserName ?? "Sales",
                    CreatedDate = DateTime.UtcNow.AddDays(-5)
                };

                var act2 = new Activity
                {
                    ActivityType = ActivityType.Call,
                    Subject = "Introductory screening call with HDFC tech team",
                    Description = "Discussed data residency requirements and SLA terms.",
                    ActivityDate = DateTime.UtcNow.AddDays(-7),
                    LeadId = lead1.LeadId,
                    AssignedTo = salesUser.Id,
                    Status = ActivityStatus.Completed,
                    CreatedBy = salesUser.UserName ?? "Sales",
                    CreatedDate = DateTime.UtcNow.AddDays(-7)
                };

                context.Activities.AddRange(act1, act2);
                await context.SaveChangesAsync();

                // Seed Initial Audit Log entries
                var auditEntries = new List<AuditLog>
                {
                    new AuditLog
                    {
                        UserId = adminUser.Id,
                        Action = "SystemSeed",
                        EntityName = "Database",
                        RecordId = "0",
                        CreatedDate = DateTime.UtcNow.AddMonths(-2),
                        IpAddress = "127.0.0.1",
                        Result = "Success",
                        Details = "System database initialized with baseline roles, users, and enterprise records."
                    },
                    new AuditLog
                    {
                        UserId = adminUser.Id,
                        Action = "Create",
                        EntityName = "Customer",
                        RecordId = cust1.CustomerId.ToString(),
                        NewValue = $"CustomerCode={cust1.CustomerCode}, Name={cust1.CustomerName}",
                        CreatedDate = DateTime.UtcNow.AddMonths(-2),
                        IpAddress = "127.0.0.1",
                        Result = "Success",
                        Details = "Created customer Tata Consultancy Services"
                    },
                    new AuditLog
                    {
                        UserId = salesUser.Id,
                        Action = "ConvertLead",
                        EntityName = "Lead",
                        RecordId = lead4.LeadId.ToString(),
                        NewValue = $"Converted to CustomerId={cust1.CustomerId}",
                        CreatedDate = DateTime.UtcNow.AddDays(-3),
                        IpAddress = "127.0.0.1",
                        Result = "Success",
                        Details = "Converted lead Airtel Business Telecom to existing customer"
                    }
                };

                context.AuditLogs.AddRange(auditEntries);
                await context.SaveChangesAsync();

                logger.LogInformation("Sample CRM data seeded successfully.");
            }
        }
    }
}
