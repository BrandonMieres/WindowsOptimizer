using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsOptimizer.Core.Models;

namespace WindowsOptimizer.Core.Abstractions;

public interface IAppxService
{
    Task<List<AppxPackage>> GetInstalledAppsAsync();
    Task<bool> RemoveAppAsync(string packageFullName);
}
