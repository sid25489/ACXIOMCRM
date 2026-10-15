using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Api
{
    [ApiController]
    [Route("api/opportunities")]
    [Authorize]
    public class OpportunitiesApiController : ControllerBase
    {
        private readonly IOpportunityService _opportunityService;

        public OpportunitiesApiController(IOpportunityService opportunityService)
        {
            _opportunityService = opportunityService;
        }

        [HttpGet]
        public async Task<IActionResult> GetOpportunities(
            [FromQuery] string? search = null,
            [FromQuery] OpportunityStage? stage = null,
            [FromQuery] OpportunityStatus? status = null,
            [FromQuery] string? assignedTo = null,
            [FromQuery] int? customerId = null,
            [FromQuery] string? sortBy = "createdDate",
            [FromQuery] bool descending = true,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var (items, totalCount) = await _opportunityService.GetOpportunitiesAsync(
                User, search, stage, status, assignedTo, customerId, sortBy, descending, page, pageSize);

            var dtos = items.Select(o => new OpportunityDto
            {
                OpportunityId = o.OpportunityId,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer?.CustomerName,
                LeadId = o.LeadId,
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status = o.Status,
                AssignedTo = o.AssignedTo,
                AssignedUserName = o.AssignedUser?.FullName ?? o.AssignedUser?.UserName,
                CreatedDate = o.CreatedDate,
                Notes = o.Notes
            });

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                data = dtos
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOpportunity(int id)
        {
            var o = await _opportunityService.GetOpportunityByIdAsync(id, User);
            if (o == null)
            {
                return NotFound(new { message = $"Opportunity with ID {id} was not found or is inaccessible." });
            }

            var dto = new OpportunityDto
            {
                OpportunityId = o.OpportunityId,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer?.CustomerName,
                LeadId = o.LeadId,
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status = o.Status,
                AssignedTo = o.AssignedTo,
                AssignedUserName = o.AssignedUser?.FullName ?? o.AssignedUser?.UserName,
                CreatedDate = o.CreatedDate,
                Notes = o.Notes
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateOpportunity([FromBody] CreateOpportunityDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { title = "Validation failed", status = 400, errors = ModelState });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var opp = new Opportunity
            {
                OpportunityName = dto.OpportunityName,
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                Amount = dto.Amount,
                Stage = dto.Stage,
                Probability = dto.Probability,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                AssignedTo = dto.AssignedTo ?? string.Empty,
                Notes = dto.Notes
            };

            var (success, error, created) = await _opportunityService.CreateOpportunityAsync(opp, User, ip);
            if (!success)
            {
                return BadRequest(new { title = "Validation failed", status = 400, message = error });
            }

            var responseDto = new OpportunityDto
            {
                OpportunityId = created!.OpportunityId,
                OpportunityName = created.OpportunityName,
                CustomerId = created.CustomerId,
                LeadId = created.LeadId,
                Amount = created.Amount,
                Stage = created.Stage,
                Probability = created.Probability,
                ExpectedCloseDate = created.ExpectedCloseDate,
                Status = created.Status,
                AssignedTo = created.AssignedTo,
                CreatedDate = created.CreatedDate,
                Notes = created.Notes
            };

            return CreatedAtAction(nameof(GetOpportunity), new { id = created.OpportunityId }, responseDto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateOpportunity(int id, [FromBody] UpdateOpportunityDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { title = "Validation failed", status = 400, errors = ModelState });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var opp = new Opportunity
            {
                OpportunityId = id,
                OpportunityName = dto.OpportunityName,
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                Amount = dto.Amount,
                Stage = dto.Stage,
                Probability = dto.Probability,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Status = dto.Status,
                AssignedTo = dto.AssignedTo ?? string.Empty,
                Notes = dto.Notes
            };

            var (success, error, updated) = await _opportunityService.UpdateOpportunityAsync(opp, User, ip);
            if (!success)
            {
                return BadRequest(new { title = "Validation failed", status = 400, message = error });
            }

            return Ok(new { success = true, message = "Opportunity updated successfully.", data = updated });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteOpportunity(int id)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _opportunityService.DeleteOpportunityAsync(id, User, ip);
            if (!success)
            {
                return BadRequest(new { title = "Operation failed", message = error });
            }

            return NoContent();
        }
    }
}
