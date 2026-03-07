using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public interface ILoanProjectService
{
    Task<IEnumerable<LoanProject>> GetAllProjectsAsync();
    Task<LoanProject?> GetProjectByIdAsync(int id);
    Task<LoanProject> CreateProjectAsync(LoanProject project);
    Task<LoanProject> UpdateProjectAsync(LoanProject project);
    Task<bool> DeleteProjectAsync(int id);
    Task<string> GenerateNextProjectNumberAsync();
}

public class LoanProjectService : ILoanProjectService
{
    private readonly GenoDbContext _context;
    private readonly ILogger<LoanProjectService> _logger;

    public LoanProjectService(GenoDbContext context, ILogger<LoanProjectService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<LoanProject>> GetAllProjectsAsync()
    {
        try
        {
            return await _context.LoanProjects
                .Include(p => p.LoanOffers)
                    .ThenInclude(o => o.Subscriptions)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all loan projects");
            throw;
        }
    }

    public async Task<LoanProject?> GetProjectByIdAsync(int id)
    {
        try
        {
            return await _context.LoanProjects
                .Include(p => p.LoanOffers)
                    .ThenInclude(o => o.Subscriptions)
                        .ThenInclude(s => s.Member)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving loan project with ID {ProjectId}", id);
            throw;
        }
    }

    public async Task<LoanProject> CreateProjectAsync(LoanProject project)
    {
        try
        {
            if (string.IsNullOrEmpty(project.ProjectNumber))
            {
                project.ProjectNumber = await GenerateNextProjectNumberAsync();
            }

            project.CreatedAt = DateTime.UtcNow;
            project.UpdatedAt = DateTime.UtcNow;

            _context.LoanProjects.Add(project);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Loan project created with ID {ProjectId}, number {ProjectNumber}",
                project.Id, project.ProjectNumber);

            return project;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating loan project");
            throw;
        }
    }

    public async Task<LoanProject> UpdateProjectAsync(LoanProject project)
    {
        try
        {
            var existing = await _context.LoanProjects.FindAsync(project.Id);
            if (existing == null)
                throw new InvalidOperationException($"Loan project with ID {project.Id} not found");

            existing.Title = project.Title;
            existing.StartDate = project.StartDate;
            existing.FinancingAmount = project.FinancingAmount;
            existing.Status = project.Status;
            existing.Description = project.Description;
            existing.Notes = project.Notes;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Loan project updated with ID {ProjectId}", project.Id);

            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating loan project with ID {ProjectId}", project.Id);
            throw;
        }
    }

    public async Task<bool> DeleteProjectAsync(int id)
    {
        try
        {
            var project = await _context.LoanProjects.FindAsync(id);
            if (project == null) return false;

            project.Status = LoanProjectStatus.Cancelled;
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Loan project cancelled with ID {ProjectId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting loan project with ID {ProjectId}", id);
            throw;
        }
    }

    public async Task<string> GenerateNextProjectNumberAsync()
    {
        try
        {
            var existingNumbers = await _context.LoanProjects
                .Select(p => p.ProjectNumber)
                .ToListAsync();

            if (!existingNumbers.Any())
                return "LP001";

            var highestNumber = existingNumbers
                .Select(n =>
                {
                    if (n.StartsWith("LP") && int.TryParse(n.Substring(2), out int num))
                        return num;
                    return 0;
                })
                .DefaultIfEmpty(0)
                .Max();

            var nextNumber = highestNumber + 1;
            var candidate = $"LP{nextNumber:D3}";

            while (await _context.LoanProjects.AnyAsync(p => p.ProjectNumber == candidate))
            {
                nextNumber++;
                candidate = $"LP{nextNumber:D3}";
            }

            return candidate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating next project number");
            throw;
        }
    }
}
