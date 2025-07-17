using Microsoft.Extensions.Configuration;

namespace GenoCRM.Services.Business;

public class FiscalYearService : IFiscalYearService
{
    private readonly int _fiscalYearStartMonth;
    private readonly int _fiscalYearStartDay;

    public FiscalYearService(IConfiguration configuration)
    {
        _fiscalYearStartMonth = configuration.GetValue<int>("FiscalYear:StartMonth", 1);
        _fiscalYearStartDay = configuration.GetValue<int>("FiscalYear:StartDay", 1);
    }

    public DateTime GetFiscalYearStart(int year)
    {
        return new DateTime(year, _fiscalYearStartMonth, _fiscalYearStartDay);
    }

    public DateTime GetFiscalYearEnd(int year)
    {
        var start = GetFiscalYearStart(year);
        return start.AddYears(1).AddDays(-1);
    }

    public DateTime GetCurrentFiscalYearStart()
    {
        var now = DateTime.Now;
        var currentYearStart = GetFiscalYearStart(now.Year);
        
        // If we're before the fiscal year start, the current fiscal year started last year
        if (now < currentYearStart)
        {
            return GetFiscalYearStart(now.Year - 1);
        }
        
        return currentYearStart;
    }

    public DateTime GetCurrentFiscalYearEnd()
    {
        var fiscalYearStart = GetCurrentFiscalYearStart();
        return GetFiscalYearEnd(fiscalYearStart.Year);
    }

    public DateTime GetNextFiscalYearStart()
    {
        var currentFiscalYearStart = GetCurrentFiscalYearStart();
        return currentFiscalYearStart.AddYears(1);
    }

    public DateTime GetNextFiscalYearEnd()
    {
        var nextFiscalYearStart = GetNextFiscalYearStart();
        return GetFiscalYearEnd(nextFiscalYearStart.Year);
    }

    public int GetCurrentFiscalYear()
    {
        return GetCurrentFiscalYearStart().Year;
    }

    public int GetFiscalYearForDate(DateTime date)
    {
        var yearStart = GetFiscalYearStart(date.Year);
        
        // If the date is before the fiscal year start, it belongs to the previous fiscal year
        if (date < yearStart)
        {
            return date.Year - 1;
        }
        
        return date.Year;
    }

    public bool IsDateInCurrentFiscalYear(DateTime date)
    {
        var currentFiscalYearStart = GetCurrentFiscalYearStart();
        var currentFiscalYearEnd = GetCurrentFiscalYearEnd();
        
        return date >= currentFiscalYearStart && date <= currentFiscalYearEnd;
    }

    public bool CanProcessOffboarding()
    {
        var now = DateTime.Now;
        var currentFiscalYearEnd = GetCurrentFiscalYearEnd();
        
        // Can process offboarding after the fiscal year has ended
        return now > currentFiscalYearEnd;
    }

    public DateTime GetOffboardingProcessingDate()
    {
        // Processing happens after the fiscal year ends
        return GetCurrentFiscalYearEnd().AddDays(1);
    }
}