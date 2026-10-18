using SalsCaterSuite.Application.Common.Interfaces;

namespace SalsCaterSuite.Infrastructure.Services;

public class DateTimeService : IDateTime
{
    public DateTime Now => DateTime.Now;
    public DateTime UtcNow => DateTime.UtcNow;
}

