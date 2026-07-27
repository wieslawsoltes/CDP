using System.Collections.Generic;
using System.Threading.Tasks;
using WindowsRdpApp.Models;

namespace WindowsRdpApp.Services;

public interface IProfileStorageService
{
    Task<List<RdpConnectionProfile>> LoadProfilesAsync();
    Task SaveProfilesAsync(IEnumerable<RdpConnectionProfile> profiles);
    Task AddProfileAsync(RdpConnectionProfile profile);
    Task UpdateProfileAsync(RdpConnectionProfile profile);
    Task DeleteProfileAsync(string profileId);
}
