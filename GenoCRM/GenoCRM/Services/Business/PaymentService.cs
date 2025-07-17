using Microsoft.EntityFrameworkCore;
using GenoCRM.Data;
using GenoCRM.Models.Domain;

namespace GenoCRM.Services.Business;

public interface IPaymentService
{
    Task<IEnumerable<Payment>> GetPaymentsByShareIdAsync(int shareId);
    Task<IEnumerable<Payment>> GetPaymentsByMemberIdAsync(int memberId);
    Task<Payment?> GetPaymentByIdAsync(int id);
    Task<Payment> CreatePaymentAsync(Payment payment);
    Task<Payment> UpdatePaymentAsync(Payment payment);
    Task<bool> DeletePaymentAsync(int id);
    Task<string> GenerateNextPaymentNumberAsync();
    Task<bool> PaymentNumberExistsAsync(string paymentNumber);
    Task<decimal> GetTotalPaymentsForShareAsync(int shareId);
    Task<decimal> GetTotalPaymentsForMemberAsync(int memberId);
    Task<Payment> RecordSharePaymentAsync(int shareId, decimal amount, PaymentMethod method, string? reference = null, DateTime? paymentDate = null);
}

public class PaymentService : IPaymentService
{
    private readonly GenoDbContext _context;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(GenoDbContext context, ILogger<PaymentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Payment>> GetPaymentsByShareIdAsync(int shareId)
    {
        try
        {
            return await _context.Payments
                .Include(p => p.Member)
                .Include(p => p.Share)
                .Where(p => p.ShareId == shareId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payments for share {ShareId}", shareId);
            throw;
        }
    }

    public async Task<IEnumerable<Payment>> GetPaymentsByMemberIdAsync(int memberId)
    {
        try
        {
            return await _context.Payments
                .Include(p => p.Member)
                .Include(p => p.Share)
                .Include(p => p.SubordinatedLoan)
                .Where(p => p.MemberId == memberId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payments for member {MemberId}", memberId);
            throw;
        }
    }

    public async Task<Payment?> GetPaymentByIdAsync(int id)
    {
        try
        {
            return await _context.Payments
                .Include(p => p.Member)
                .Include(p => p.Share)
                .Include(p => p.SubordinatedLoan)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment with ID {PaymentId}", id);
            throw;
        }
    }

    public async Task<Payment> CreatePaymentAsync(Payment payment)
    {
        try
        {
            // Validate payment number uniqueness
            if (await PaymentNumberExistsAsync(payment.PaymentNumber))
            {
                throw new InvalidOperationException($"Payment number {payment.PaymentNumber} already exists");
            }

            // Generate payment number if not provided
            if (string.IsNullOrEmpty(payment.PaymentNumber))
            {
                payment.PaymentNumber = await GenerateNextPaymentNumberAsync();
            }

            // Validate member exists
            var memberExists = await _context.Members.AnyAsync(m => m.Id == payment.MemberId);
            if (!memberExists)
            {
                throw new InvalidOperationException($"Member with ID {payment.MemberId} not found");
            }

            // Validate share exists if ShareId is provided
            if (payment.ShareId.HasValue)
            {
                var shareExists = await _context.CooperativeShares.AnyAsync(s => s.Id == payment.ShareId.Value);
                if (!shareExists)
                {
                    throw new InvalidOperationException($"Share with ID {payment.ShareId.Value} not found");
                }
            }

            // Set default values
            payment.CreatedAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;
            
            if (payment.Status == PaymentStatus.Completed && !payment.ProcessedDate.HasValue)
            {
                payment.ProcessedDate = payment.PaymentDate;
            }

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment created with ID {PaymentId} and number {PaymentNumber}", 
                payment.Id, payment.PaymentNumber);

            return payment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment");
            throw;
        }
    }

    public async Task<Payment> UpdatePaymentAsync(Payment payment)
    {
        try
        {
            var existingPayment = await _context.Payments.FindAsync(payment.Id);
            if (existingPayment == null)
            {
                throw new InvalidOperationException($"Payment with ID {payment.Id} not found");
            }

            // Check if payment number changed and if it's already taken
            if (existingPayment.PaymentNumber != payment.PaymentNumber)
            {
                if (await PaymentNumberExistsAsync(payment.PaymentNumber))
                {
                    throw new InvalidOperationException($"Payment number {payment.PaymentNumber} already exists");
                }
            }

            // Update properties
            existingPayment.PaymentNumber = payment.PaymentNumber;
            existingPayment.Amount = payment.Amount;
            existingPayment.Type = payment.Type;
            existingPayment.Method = payment.Method;
            existingPayment.PaymentDate = payment.PaymentDate;
            existingPayment.Status = payment.Status;
            existingPayment.Reference = payment.Reference;
            existingPayment.Notes = payment.Notes;
            existingPayment.UpdatedAt = DateTime.UtcNow;

            // Update processed date based on status
            if (payment.Status == PaymentStatus.Completed && !existingPayment.ProcessedDate.HasValue)
            {
                existingPayment.ProcessedDate = payment.PaymentDate;
            }
            else if (payment.Status != PaymentStatus.Completed)
            {
                existingPayment.ProcessedDate = null;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment updated with ID {PaymentId}", payment.Id);

            return existingPayment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment with ID {PaymentId}", payment.Id);
            throw;
        }
    }

    public async Task<bool> DeletePaymentAsync(int id)
    {
        try
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment == null)
            {
                return false;
            }

            // Only allow deletion of pending or failed payments
            if (payment.Status == PaymentStatus.Completed)
            {
                throw new InvalidOperationException("Cannot delete completed payments. Cancel or refund instead.");
            }

            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Payment deleted with ID {PaymentId}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting payment with ID {PaymentId}", id);
            throw;
        }
    }

    public async Task<string> GenerateNextPaymentNumberAsync()
    {
        try
        {
            var currentYear = DateTime.UtcNow.Year;
            var yearPrefix = $"PAY{currentYear}";
            
            // Get the highest payment number for this year
            var highestPayment = await _context.Payments
                .Where(p => p.PaymentNumber.StartsWith(yearPrefix))
                .OrderByDescending(p => p.PaymentNumber)
                .Select(p => p.PaymentNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (highestPayment != null)
            {
                var numberPart = highestPayment.Substring(yearPrefix.Length);
                if (int.TryParse(numberPart, out int currentNumber))
                {
                    nextNumber = currentNumber + 1;
                }
            }

            return $"{yearPrefix}{nextNumber:D4}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating next payment number");
            throw;
        }
    }

    public async Task<bool> PaymentNumberExistsAsync(string paymentNumber)
    {
        return await _context.Payments.AnyAsync(p => p.PaymentNumber == paymentNumber);
    }

    public async Task<decimal> GetTotalPaymentsForShareAsync(int shareId)
    {
        try
        {
            return await _context.Payments
                .Where(p => p.ShareId == shareId && p.Status == PaymentStatus.Completed)
                .SumAsync(p => p.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total payments for share {ShareId}", shareId);
            throw;
        }
    }

    public async Task<decimal> GetTotalPaymentsForMemberAsync(int memberId)
    {
        try
        {
            return await _context.Payments
                .Where(p => p.MemberId == memberId && p.Status == PaymentStatus.Completed)
                .SumAsync(p => p.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total payments for member {MemberId}", memberId);
            throw;
        }
    }

    public async Task<Payment> RecordSharePaymentAsync(int shareId, decimal amount, PaymentMethod method, string? reference = null, DateTime? paymentDate = null)
    {
        try
        {
            // Get the share to validate it exists and get member ID
            var share = await _context.CooperativeShares.FindAsync(shareId);
            if (share == null)
            {
                throw new InvalidOperationException($"Share with ID {shareId} not found");
            }

            // Create payment record
            var payment = new Payment
            {
                MemberId = share.MemberId,
                ShareId = shareId,
                Amount = amount,
                Type = PaymentType.ShareCapital,
                Method = method,
                PaymentDate = paymentDate ?? DateTime.UtcNow,
                Status = PaymentStatus.Completed,
                ProcessedDate = paymentDate ?? DateTime.UtcNow,
                Reference = reference,
                Notes = $"Payment for share {share.CertificateNumber}"
            };

            return await CreatePaymentAsync(payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording share payment for share {ShareId}", shareId);
            throw;
        }
    }
}