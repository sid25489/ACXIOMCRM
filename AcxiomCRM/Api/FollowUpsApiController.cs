using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Api
{
    [ApiController]
    [Route("api/followups")]
    [Authorize]
    public class FollowUpsApiController : ControllerBase
    {
        private readonly IFollowUpService _followUpService;

        public FollowUpsApiController(IFollowUpService followUpService)
        {
            _followUpService = followUpService;
        }

        [HttpGet]
        public async Task<IActionResult> GetFollowUps(
            [FromQuery] FollowUpStatus? status = null,
            [FromQuery] FollowUpType? type = null,
            [FromQuery] string? assignedTo = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var (items, totalCount) = await _followUpService.GetFollowUpsAsync(
                User, status, type, assignedTo, fromDate, toDate, page, pageSize);

            var dtos = items.Select(f => new FollowUpDto
            {
                FollowUpId = f.FollowUpId,
                CustomerId = f.CustomerId,
                CustomerName = f.Customer?.CustomerName,
                LeadId = f.LeadId,
                LeadName = f.Lead?.LeadName,
                OpportunityId = f.OpportunityId,
                OpportunityName = f.Opportunity?.OpportunityName,
                FollowUpDate = f.FollowUpDate,
                FollowUpType = f.FollowUpType,
                Subject = f.Subject,
                Remarks = f.Remarks,
                Status = f.Status,
                AssignedTo = f.AssignedTo,
                AssignedUserName = f.AssignedUser?.FullName ?? f.AssignedUser?.UserName,
                CreatedDate = f.CreatedDate,
                CompletedDate = f.CompletedDate
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
        public async Task<IActionResult> GetFollowUp(int id)
        {
            var f = await _followUpService.GetFollowUpByIdAsync(id, User);
            if (f == null)
            {
                return NotFound(new { message = $"Follow-up with ID {id} was not found or is inaccessible." });
            }

            var dto = new FollowUpDto
            {
                FollowUpId = f.FollowUpId,
                CustomerId = f.CustomerId,
                CustomerName = f.Customer?.CustomerName,
                LeadId = f.LeadId,
                LeadName = f.Lead?.LeadName,
                OpportunityId = f.OpportunityId,
                OpportunityName = f.Opportunity?.OpportunityName,
                FollowUpDate = f.FollowUpDate,
                FollowUpType = f.FollowUpType,
                Subject = f.Subject,
                Remarks = f.Remarks,
                Status = f.Status,
                AssignedTo = f.AssignedTo,
                AssignedUserName = f.AssignedUser?.FullName ?? f.AssignedUser?.UserName,
                CreatedDate = f.CreatedDate,
                CompletedDate = f.CompletedDate
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateFollowUp([FromBody] CreateFollowUpDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { title = "Validation failed", status = 400, errors = ModelState });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var followUp = new FollowUp
            {
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                OpportunityId = dto.OpportunityId,
                FollowUpDate = dto.FollowUpDate,
                FollowUpType = dto.FollowUpType,
                Subject = dto.Subject,
                Remarks = dto.Remarks,
                Status = dto.Status,
                AssignedTo = dto.AssignedTo ?? string.Empty
            };

            var (success, error, created) = await _followUpService.CreateFollowUpAsync(followUp, User, ip);
            if (!success)
            {
                return BadRequest(new { title = "Validation failed", status = 400, message = error });
            }

            var responseDto = new FollowUpDto
            {
                FollowUpId = created!.FollowUpId,
                CustomerId = created.CustomerId,
                LeadId = created.LeadId,
                OpportunityId = created.OpportunityId,
                FollowUpDate = created.FollowUpDate,
                FollowUpType = created.FollowUpType,
                Subject = created.Subject,
                Remarks = created.Remarks,
                Status = created.Status,
                AssignedTo = created.AssignedTo,
                CreatedDate = created.CreatedDate
            };

            return CreatedAtAction(nameof(GetFollowUp), new { id = created.FollowUpId }, responseDto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateFollowUp(int id, [FromBody] UpdateFollowUpDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { title = "Validation failed", status = 400, errors = ModelState });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var followUp = new FollowUp
            {
                FollowUpId = id,
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                OpportunityId = dto.OpportunityId,
                FollowUpDate = dto.FollowUpDate,
                FollowUpType = dto.FollowUpType,
                Subject = dto.Subject,
                Remarks = dto.Remarks,
                Status = dto.Status,
                AssignedTo = dto.AssignedTo ?? string.Empty
            };

            var (success, error, updated) = await _followUpService.UpdateFollowUpAsync(followUp, User, ip);
            if (!success)
            {
                return BadRequest(new { title = "Validation failed", status = 400, message = error });
            }

            return Ok(new { success = true, message = "Follow-up updated successfully.", data = updated });
        }
    }
}
