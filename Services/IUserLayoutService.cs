using BlazorReporting.Models;

namespace BlazorReporting.Services;

public interface IUserLayoutService
{
    Task<UserPivotLayout?> LoadAsync(string userId, string reportName, string layoutName = "Default");
    Task<IReadOnlyList<UserPivotLayout>> ListAsync(string userId, string reportName);
    Task SaveAsync(UserPivotLayout layout);
    Task DeleteAsync(int id);
}
