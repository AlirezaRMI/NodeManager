using Domain.DTOs.Instance;

namespace Application.Services.Interfaces;

public interface ILocalInstanceStore
{
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    Task<List<InstanceInfo>> GetAllAsync();
    /// <summary>
    /// 
    /// </summary>
    /// <param name="newInstance"></param>
    /// <returns></returns>
    Task AddAsync(InstanceInfo newInstance);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="instanceId"></param>
    /// <returns></returns>
    Task RemoveAsync(long instanceId);
}