using ProjectManagement.Application.Interfaces;

namespace ProjectManagement.Infrastructure.Services;

public class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}
