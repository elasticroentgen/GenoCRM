namespace GenoCRM.Services.Business;

public interface IFiscalYearService
{
    DateTime GetFiscalYearStart(int year);
    DateTime GetFiscalYearEnd(int year);
    DateTime GetCurrentFiscalYearStart();
    DateTime GetCurrentFiscalYearEnd();
    DateTime GetNextFiscalYearStart();
    DateTime GetNextFiscalYearEnd();
    int GetCurrentFiscalYear();
    int GetFiscalYearForDate(DateTime date);
    bool IsDateInCurrentFiscalYear(DateTime date);
    bool CanProcessOffboarding();
    DateTime GetOffboardingProcessingDate();
}