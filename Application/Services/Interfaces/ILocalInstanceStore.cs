using Domain.DTOs.Instance;
using Domain.Model;

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
    
    /// <summary>
    /// this for update saved traffics
    /// </summary>
    /// <param name="instanceToUpdate"></param>
    /// <returns></returns>
    Task UpdateAsync(InstanceInfo instanceToUpdate);
}