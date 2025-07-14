using Microsoft.AspNetCore.Mvc;
using GenoCRM.Models.Domain;
using GenoCRM.Services.Business;

namespace GenoCRM.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class MembersController : ControllerBase
{
    private readonly IMemberService _memberService;
    private readonly ILogger<MembersController> _logger;

    public MembersController(IMemberService memberService, ILogger<MembersController> logger)
    {
        _memberService = memberService;
        _logger = logger;
    }

    /// <summary>
    /// Get all members
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MemberDto>>> GetMembers()
    {
        try
        {
            var members = await _memberService.GetAllMembersAsync();
            var memberDtos = members.Select(m => new MemberDto
            {
                Id = m.Id,
                MemberNumber = m.MemberNumber,
                FirstName = m.FirstName,
                LastName = m.LastName,
                Email = m.Email,
                Phone = m.Phone,
                Status = m.Status.ToString(),
                JoinDate = m.JoinDate,
                TotalShareValue = m.TotalShareValue,
                TotalShareCount = m.TotalShareCount
            });

            return Ok(memberDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving members");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get member by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<MemberDto>> GetMember(int id)
    {
        try
        {
            var member = await _memberService.GetMemberByIdAsync(id);
            if (member == null)
            {
                return NotFound();
            }

            var memberDto = new MemberDto
            {
                Id = member.Id,
                MemberNumber = member.MemberNumber,
                FirstName = member.FirstName,
                LastName = member.LastName,
                Email = member.Email,
                Phone = member.Phone,
                Street = member.Street,
                PostalCode = member.PostalCode,
                City = member.City,
                Country = member.Country,
                BirthDate = member.BirthDate,
                Status = member.Status.ToString(),
                JoinDate = member.JoinDate,
                LeaveDate = member.LeaveDate,
                TotalShareValue = member.TotalShareValue,
                TotalShareCount = member.TotalShareCount
            };

            return Ok(memberDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving member {MemberId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Create new member
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MemberDto>> CreateMember(CreateMemberDto createMemberDto)
    {
        try
        {
            var member = new Member
            {
                MemberNumber = createMemberDto.MemberNumber,
                FirstName = createMemberDto.FirstName,
                LastName = createMemberDto.LastName,
                Email = createMemberDto.Email ?? string.Empty,
                Phone = createMemberDto.Phone ?? string.Empty,
                Street = createMemberDto.Street ?? string.Empty,
                PostalCode = createMemberDto.PostalCode ?? string.Empty,
                City = createMemberDto.City ?? string.Empty,
                Country = createMemberDto.Country ?? string.Empty,
                BirthDate = createMemberDto.BirthDate,
                JoinDate = createMemberDto.JoinDate,
                Status = MemberStatus.Active
            };

            var createdMember = await _memberService.CreateMemberAsync(member);

            var memberDto = new MemberDto
            {
                Id = createdMember.Id,
                MemberNumber = createdMember.MemberNumber,
                FirstName = createdMember.FirstName,
                LastName = createdMember.LastName,
                Email = createdMember.Email,
                Phone = createdMember.Phone,
                Status = createdMember.Status.ToString(),
                JoinDate = createdMember.JoinDate,
                TotalShareValue = createdMember.TotalShareValue,
                TotalShareCount = createdMember.TotalShareCount
            };

            return CreatedAtAction(nameof(GetMember), new { id = memberDto.Id }, memberDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating member");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Search members by term
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<MemberDto>>> SearchMembers([FromQuery] string term)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return BadRequest("Search term is required");
            }

            var members = await _memberService.SearchMembersAsync(term);
            var memberDtos = members.Select(m => new MemberDto
            {
                Id = m.Id,
                MemberNumber = m.MemberNumber,
                FirstName = m.FirstName,
                LastName = m.LastName,
                Email = m.Email,
                Phone = m.Phone,
                Status = m.Status.ToString(),
                JoinDate = m.JoinDate,
                TotalShareValue = m.TotalShareValue,
                TotalShareCount = m.TotalShareCount
            });

            return Ok(memberDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching members with term {SearchTerm}", term);
            return StatusCode(500, "Internal server error");
        }
    }
}

public class MemberDto
{
    public int Id { get; set; }
    public string MemberNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTime? BirthDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime JoinDate { get; set; }
    public DateTime? LeaveDate { get; set; }
    public decimal TotalShareValue { get; set; }
    public int TotalShareCount { get; set; }
}

public class CreateMemberDto
{
    public string MemberNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTime? BirthDate { get; set; }
    public DateTime JoinDate { get; set; }
}