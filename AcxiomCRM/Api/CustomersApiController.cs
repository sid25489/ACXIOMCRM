using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Api
{
    [ApiController]
    [Route("api/customers")]
    [Authorize]
    public class CustomersApiController : ControllerBase
    {
        private readonly ICustomerService _customerService;

        public CustomersApiController(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomers(
            [FromQuery] string? search = null,
            [FromQuery] CustomerStatus? status = null,
            [FromQuery] string? ownerId = null,
            [FromQuery] string? sortBy = "createdDate",
            [FromQuery] bool descending = true,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var (items, totalCount) = await _customerService.GetCustomersAsync(
                User, search, status, ownerId, sortBy, descending, page, pageSize);

            var dtos = items.Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Status = c.Status,
                OwnerId = c.OwnerId,
                OwnerName = c.Owner?.FullName ?? c.Owner?.UserName,
                CreatedDate = c.CreatedDate,
                Notes = c.Notes
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
        public async Task<IActionResult> GetCustomer(int id)
        {
            var c = await _customerService.GetCustomerByIdAsync(id, User);
            if (c == null)
            {
                return NotFound(new { message = $"Customer with ID {id} was not found or is inaccessible." });
            }

            var dto = new CustomerDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Status = c.Status,
                OwnerId = c.OwnerId,
                OwnerName = c.Owner?.FullName ?? c.Owner?.UserName,
                CreatedDate = c.CreatedDate,
                Notes = c.Notes
            };

            return Ok(dto);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { title = "Validation failed", status = 400, errors = ModelState });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var customer = new Customer
            {
                CustomerName = dto.CustomerName,
                Email = dto.Email,
                Phone = dto.Phone,
                CompanyName = dto.CompanyName,
                Address = dto.Address,
                City = dto.City,
                State = dto.State,
                Status = dto.Status,
                OwnerId = dto.OwnerId ?? string.Empty,
                Notes = dto.Notes
            };

            var (success, error, created) = await _customerService.CreateCustomerAsync(customer, User, ip);
            if (!success)
            {
                return BadRequest(new { title = "Validation failed", status = 400, message = error });
            }

            var responseDto = new CustomerDto
            {
                CustomerId = created!.CustomerId,
                CustomerCode = created.CustomerCode,
                CustomerName = created.CustomerName,
                Email = created.Email,
                Phone = created.Phone,
                CompanyName = created.CompanyName,
                Address = created.Address,
                City = created.City,
                State = created.State,
                Status = created.Status,
                OwnerId = created.OwnerId,
                CreatedDate = created.CreatedDate,
                Notes = created.Notes
            };

            return CreatedAtAction(nameof(GetCustomer), new { id = created.CustomerId }, responseDto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { title = "Validation failed", status = 400, errors = ModelState });
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var customer = new Customer
            {
                CustomerId = id,
                CustomerName = dto.CustomerName,
                Email = dto.Email,
                Phone = dto.Phone,
                CompanyName = dto.CompanyName,
                Address = dto.Address,
                City = dto.City,
                State = dto.State,
                Status = dto.Status,
                OwnerId = dto.OwnerId ?? string.Empty,
                Notes = dto.Notes
            };

            var (success, error, updated) = await _customerService.UpdateCustomerAsync(customer, User, ip);
            if (!success)
            {
                return BadRequest(new { title = "Validation failed", status = 400, message = error });
            }

            return Ok(new { success = true, message = "Customer updated successfully.", data = updated });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, error) = await _customerService.DeleteCustomerAsync(id, User, ip);
            if (!success)
            {
                return BadRequest(new { title = "Operation failed", message = error });
            }

            return NoContent();
        }
    }
}
