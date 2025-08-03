using Application.Services.Interfaces;
using Domain.DTOs.Instance;
using Newtonsoft.Json;

namespace Application.Services.implementations;

public class LocalInstanceStore : ILocalInstanceStore
{
    private const string DbPath = "/var/lib/easyhub-instance-data/instances.json";
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public async Task<List<InstanceInfo>> GetAllAsync()
    {
        await Semaphore.WaitAsync();
        try
        {
            if (!File.Exists(DbPath)) return [];
            var json = await File.ReadAllTextAsync(DbPath);
            return JsonConvert.DeserializeObject<List<InstanceInfo>>(json) ?? new List<InstanceInfo>();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task AddAsync(InstanceInfo newInstance)
    {
        await Semaphore.WaitAsync();
        try
        {
            var instances = await GetAllAsync(); 
            if (instances.All(i => i.Id != newInstance.Id))
            {
                instances.Add(newInstance);
                var json = JsonConvert.SerializeObject(instances, Formatting.Indented);
                await File.WriteAllTextAsync(DbPath, json);
            }
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task RemoveAsync(long instanceId)
    {
        await Semaphore.WaitAsync();
        try
        {
            var instances = await GetAllAsync();
            var instanceToRemove = instances.FirstOrDefault(i => i.Id == instanceId);
            if (instanceToRemove != null)
            {
                instances.Remove(instanceToRemove);
                var json = JsonConvert.SerializeObject(instances, Formatting.Indented);
                await File.WriteAllTextAsync(DbPath, json);
            }
        }
        finally
        {
            Semaphore.Release();
        }
    }
}