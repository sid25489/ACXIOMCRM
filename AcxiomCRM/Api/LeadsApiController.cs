using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Api
{
    [ApiController]
    [Route("api/leads")]
    [Authorize]
    public class LeadsApiController : ControllerBase
    {
        private readonly ILeadService _leadService;

        public LeadsApiController(ILeadService leadService)
        {
            _leadService = leadService;
        }

        [HttpGet]
        public async Task<IActionResult> GetLeads(
            [FromQuery] string? search = null,
            [FromQuery] LeadStatus? status = null,
            [FromQuery] LeadSource? source = null,
            [FromQuery] string? assignedTo = null,
            [FromQuery] string? sortBy = "createdDate",
            [FromQuery] bool descending = true,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var (items, totalCount) = await _leadService.GetLeadsAsync(
                User, search, status, source, assignedTo, sortBy, descending, page, pageSize);

            var dtos = items.Select(l => new LeadDto
            {
                LeadId = l.LeadId,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Email = l.Email,
                Phone = l.Phone,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                AssignedTo = l.AssignedTo,
                AssignedUserName = l.AssignedUser?.FullName ?? l.AssignedUser?.UserName,
                CustomerId = l.CustomerId,
                CustomerName = l.Customer?.CustomerName,
                CreatedDate = l.CreatedDate,
                ConversionDate = l.ConversionDate,
                Notes = l.Notes
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
        public async Task<IActionResult> GetLead(int id)
        {
            var l = await _leadService.GetLeadByIdAsync(id, User);
            if (l == null)
            {
                return NotFound(new { message = $"Lead with ID {id} was not found or is inaccessible." });
            }

            var dto = new LeadDto
            {
                LeadId = l.LeadId,
                LeadCode = l.LeadCode,
                LeadName = l.LeadName,
                Email = l.Email,
                Phone = l.Phone,
                CompanyName = l.CompanyName,
                Source = l.Source,
                Status = l.Status,
                Priority = l.Priority,
                ExpectedValue = l.ExpectedValue,
                AssignedTo = l.AssignedTo,
                AssignedUserName = l.AssignedUser?.FullName ?? l.AssignedUser?.UserName,
                CustomerId = l.CustomerId,
                CustomerName = l.Customer?.CustomerName,
                CreatedDate = l.CreatedDate,
                ConversionDate = l.ConversionDate,
                Notes = l.Notes
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateLead([FromBody] CreateLeadDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { title = "Validation failed", status = 400, errors = ModelState });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var lead = new Lead
            {
                LeadName = dto.LeadName,
                Email = dto.Email,
                Phone = dto.Phone,
                CompanyName = dto.CompanyName,
                Source = dto.Source,
                Status = dto.Status,
                Priority = dto.Priority,
                ExpectedValue = dto.ExpectedValue,
                AssignedTo = dto.AssignedTo ?? string.Empty,
                Notes = dto.Notes
            };

            var (success, error, created) = await _leadService.CreateLeadAsync(lead, User, ip);
            if (!success)
            {
                return BadRequest(new { title = "Validation failed", status = 400, message = error });
            }

            var responseDto = new LeadDto
            {
                LeadId = created!.LeadId,
                LeadCode = created.LeadCode,
                LeadName = created.LeadName,
                Email = created.Email,
                Phone = created.Phone,
                CompanyName = created.CompanyName,
                Source = created.Source,
                Status = created.Status,
                Priority = created.Priority,
                ExpectedValue = created.ExpectedValue,
                AssignedTo = created.AssignedTo,
                CreatedDate = created.CreatedDate,
                Notes = created.Notes
            };

            return CreatedAtAction(nameof(GetLead), new { id = created.LeadId }, responseDto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateLead(int id, [FromBody] UpdateLeadDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { title = "Validation failed", status = 400, errors = ModelState });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var lead = new Lead
            {
                LeadId = id,
                LeadName = dto.LeadName,
                Email = dto.Email,
                Phone = dto.Phone,
                CompanyName = dto.CompanyName,
                Source = dto.Source,
                Status = dto.Status,
                Priority = dto.Priority,
                ExpectedValue = dto.ExpectedValue,
                AssignedTo = dto.AssignedTo ?? string.Empty,
                Notes = dto.Notes
            };

            var (success, error, updated) = await _leadService.UpdateLeadAsync(lead, User, ip);
            if (!success)
            {
                return BadRequest(new { title = "Validation failed", status = 400, message = error });
            }

            return Ok(new { success = true, message = "Lead updated successfully.", data = updated });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteLead(int id)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _leadService.DeleteLeadAsync(id, User, ip);
            if (!success)
            {
                return BadRequest(new { title = "Operation failed", message = error });
            }

            return NoContent();
        }
    }
}
