using Microsoft.AspNetCore.Mvc;
using GenoCRM.Attributes;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;

namespace GenoCRM.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class RegistrationController : ControllerBase
{
    private readonly IMemberService _memberService;
    private readonly ILogger<RegistrationController> _logger;

    public RegistrationController(IMemberService memberService, ILogger<RegistrationController> logger)
    {
        _memberService = memberService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new member application from the website
    /// </summary>
    [HttpPost("apply")]
    [ApiKeyAuth]
    public async Task<ActionResult<MemberApplicationResponse>> ApplyForMembership(MemberApplicationDto application)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(application.FirstName) ||
                string.IsNullOrWhiteSpace(application.LastName) ||
                string.IsNullOrWhiteSpace(application.Email))
            {
                return BadRequest(new { message = "FirstName, LastName, and Email are required" });
            }

            // Validate requested shares
            if (application.RequestedShares < 1)
            {
                return BadRequest(new { message = "RequestedShares must be at least 1" });
            }

            var maxSharesPerMember = await _memberService.GetMaxSharesPerMemberAsync();
            if (application.RequestedShares > maxSharesPerMember)
            {
                return BadRequest(new { message = $"RequestedShares cannot exceed {maxSharesPerMember}" });
            }

            // Check if email already exists
            var existingMembers = await _memberService.SearchMembersAsync(application.Email);
            if (existingMembers.Any(m => m.Email.Equals(application.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return Conflict(new { message = "A member with this email already exists" });
            }

            var member = new Member
            {
                MemberType = application.MemberType,
                Prefix = application.Prefix,
                FirstName = application.FirstName,
                LastName = application.LastName,
                CompanyName = application.CompanyName ?? string.Empty,
                ContactPerson = application.ContactPerson,
                Email = application.Email,
                Phone = application.Phone ?? string.Empty,
                Street = application.Street ?? string.Empty,
                PostalCode = application.PostalCode ?? string.Empty,
                City = application.City ?? string.Empty,
                Country = application.Country ?? "Deutschland",
                BirthDate = application.BirthDate,
                JoinDate = DateTime.UtcNow,
                Status = MemberStatus.Applied,
                Notes = application.Notes
            };

            var createdMember = await _memberService.CreateMemberAsync(member, application.RequestedShares);

            _logger.LogInformation("New member application received: {MemberId} - {Email}",
                createdMember.Id, createdMember.Email);

            return Ok(new MemberApplicationResponse
            {
                Success = true,
                MemberId = createdMember.Id,
                Message = "Your application has been received successfully. We will review it and contact you soon."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing member application for {Email}", application.Email);
            return StatusCode(500, new { message = "An error occurred while processing your application" });
        }
    }
}

public class MemberApplicationDto
{
    public MemberType MemberType { get; set; } = MemberType.Individual;
    public string? Prefix { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? ContactPerson { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Notes { get; set; }
    public int RequestedShares { get; set; } = 1;
}

public class MemberApplicationResponse
{
    public bool Success { get; set; }
    public int MemberId { get; set; }
    public string Message { get; set; } = string.Empty;
}
