using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gml.Launcher.Core.Services;

/// <summary>How long a profile's game client has been running on this computer.</summary>
public interface IClientTimeService
{
    Task<TimeSpan> GetTotalAsync(string profileName, CancellationToken token = default);
}
