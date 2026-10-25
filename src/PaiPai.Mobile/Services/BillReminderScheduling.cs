using PaiPai.Core;

namespace PaiPai.Mobile.Services;

public interface IBillReminderScheduler
{
    Task<bool> HasPermissionAsync();
    Task<bool> RequestPermissionAsync();
    Task ScheduleAsync(BillReminder bill);
    Task CancelAsync(int billId);
    Task RescheduleAllAsync();
}
