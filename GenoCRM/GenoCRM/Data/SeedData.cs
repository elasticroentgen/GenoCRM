using GenoCRM.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace GenoCRM.Data;

public static class SeedData
{
    public static async Task SeedAsync(GenoDbContext context)
    {
        if (await context.Members.AnyAsync())
        {
            return; // Data already seeded
        }

        var members = new List<Member>
        {
            new Member
            {
                MemberNumber = "M001",
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                Phone = "+1234567890",
                Street = "123 Main St",
                PostalCode = "12345",
                City = "Anytown",
                Country = "Germany",
                BirthDate = new DateTime(1980, 5, 15),
                JoinDate = new DateTime(2020, 1, 15),
                Status = MemberStatus.Active
            },
            new Member
            {
                MemberNumber = "M002",
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane.smith@example.com",
                Phone = "+1234567891",
                Street = "456 Oak Ave",
                PostalCode = "12346",
                City = "Otherville",
                Country = "Germany",
                BirthDate = new DateTime(1985, 8, 22),
                JoinDate = new DateTime(2020, 3, 10),
                Status = MemberStatus.Active
            },
            new Member
            {
                MemberNumber = "M003",
                FirstName = "Bob",
                LastName = "Johnson",
                Email = "bob.johnson@example.com",
                Phone = "+1234567892",
                Street = "789 Pine Rd",
                PostalCode = "12347",
                City = "Somewhere",
                Country = "Germany",
                BirthDate = new DateTime(1975, 12, 3),
                JoinDate = new DateTime(2019, 11, 20),
                Status = MemberStatus.Active
            },
            new Member
            {
                MemberNumber = "M004",
                FirstName = "Alice",
                LastName = "Williams",
                Email = "alice.williams@example.com",
                Phone = "+1234567893",
                Street = "321 Elm St",
                PostalCode = "12348",
                City = "Elsewhere",
                Country = "Germany",
                BirthDate = new DateTime(1990, 4, 18),
                JoinDate = new DateTime(2021, 6, 5),
                Status = MemberStatus.Active
            },
            new Member
            {
                MemberNumber = "M005",
                FirstName = "Charlie",
                LastName = "Brown",
                Email = "charlie.brown@example.com",
                Phone = "+1234567894",
                Street = "654 Maple Dr",
                PostalCode = "12349",
                City = "Nowhere",
                Country = "Germany",
                BirthDate = new DateTime(1983, 9, 12),
                JoinDate = new DateTime(2018, 8, 30),
                Status = MemberStatus.Inactive
            }
        };

        context.Members.AddRange(members);
        await context.SaveChangesAsync();

        // Add some shares
        var shares = new List<CooperativeShare>
        {
            new CooperativeShare
            {
                MemberId = members[0].Id,
                CertificateNumber = "CERT001",
                Quantity = 10,
                NominalValue = 100.00m,
                Value = 100.00m,
                IssueDate = new DateTime(2020, 1, 20),
                Status = ShareStatus.Active
            },
            new CooperativeShare
            {
                MemberId = members[1].Id,
                CertificateNumber = "CERT002",
                Quantity = 5,
                NominalValue = 100.00m,
                Value = 100.00m,
                IssueDate = new DateTime(2020, 3, 15),
                Status = ShareStatus.Active
            },
            new CooperativeShare
            {
                MemberId = members[2].Id,
                CertificateNumber = "CERT003",
                Quantity = 20,
                NominalValue = 100.00m,
                Value = 100.00m,
                IssueDate = new DateTime(2019, 12, 1),
                Status = ShareStatus.Active
            },
            new CooperativeShare
            {
                MemberId = members[3].Id,
                CertificateNumber = "CERT004",
                Quantity = 15,
                NominalValue = 100.00m,
                Value = 100.00m,
                IssueDate = new DateTime(2021, 6, 10),
                Status = ShareStatus.Active
            }
        };

        context.CooperativeShares.AddRange(shares);
        await context.SaveChangesAsync();

        // Add some payments
        var payments = new List<Payment>
        {
            new Payment
            {
                MemberId = members[0].Id,
                ShareId = shares[0].Id,
                PaymentNumber = "PAY001",
                Amount = 1000.00m,
                Type = PaymentType.ShareCapital,
                Method = PaymentMethod.BankTransfer,
                PaymentDate = new DateTime(2020, 1, 25),
                ProcessedDate = new DateTime(2020, 1, 25),
                Status = PaymentStatus.Completed
            },
            new Payment
            {
                MemberId = members[1].Id,
                ShareId = shares[1].Id,
                PaymentNumber = "PAY002",
                Amount = 500.00m,
                Type = PaymentType.ShareCapital,
                Method = PaymentMethod.BankTransfer,
                PaymentDate = new DateTime(2020, 3, 20),
                ProcessedDate = new DateTime(2020, 3, 20),
                Status = PaymentStatus.Completed
            },
            new Payment
            {
                MemberId = members[2].Id,
                ShareId = shares[2].Id,
                PaymentNumber = "PAY003",
                Amount = 2000.00m,
                Type = PaymentType.ShareCapital,
                Method = PaymentMethod.BankTransfer,
                PaymentDate = new DateTime(2019, 12, 5),
                ProcessedDate = new DateTime(2019, 12, 5),
                Status = PaymentStatus.Completed
            }
        };

        context.Payments.AddRange(payments);
        await context.SaveChangesAsync();

        // Add some dividends
        var dividends = new List<Dividend>
        {
            new Dividend
            {
                MemberId = members[0].Id,
                ShareId = shares[0].Id,
                FiscalYear = 2023,
                Amount = 30.00m,
                Rate = 0.03m,
                BaseAmount = 1000.00m,
                DeclarationDate = new DateTime(2024, 3, 15),
                Status = DividendStatus.Declared
            },
            new Dividend
            {
                MemberId = members[1].Id,
                ShareId = shares[1].Id,
                FiscalYear = 2023,
                Amount = 15.00m,
                Rate = 0.03m,
                BaseAmount = 500.00m,
                DeclarationDate = new DateTime(2024, 3, 15),
                Status = DividendStatus.Declared
            },
            new Dividend
            {
                MemberId = members[2].Id,
                ShareId = shares[2].Id,
                FiscalYear = 2023,
                Amount = 60.00m,
                Rate = 0.03m,
                BaseAmount = 2000.00m,
                DeclarationDate = new DateTime(2024, 3, 15),
                PaymentDate = new DateTime(2024, 4, 1),
                Status = DividendStatus.Paid
            }
        };

        context.Dividends.AddRange(dividends);
        await context.SaveChangesAsync();

        // Add some subordinated loans
        var loans = new List<SubordinatedLoan>
        {
            new SubordinatedLoan
            {
                MemberId = members[0].Id,
                LoanNumber = "LOAN001",
                Amount = 5000.00m,
                InterestRate = 0.025m,
                IssueDate = new DateTime(2021, 1, 15),
                Status = LoanStatus.Active,
                NoticePeriodDays = 90
            },
            new SubordinatedLoan
            {
                MemberId = members[2].Id,
                LoanNumber = "LOAN002",
                Amount = 10000.00m,
                InterestRate = 0.030m,
                IssueDate = new DateTime(2020, 6, 1),
                Status = LoanStatus.Active,
                NoticePeriodDays = 180
            }
        };

        context.SubordinatedLoans.AddRange(loans);
        await context.SaveChangesAsync();
    }
}