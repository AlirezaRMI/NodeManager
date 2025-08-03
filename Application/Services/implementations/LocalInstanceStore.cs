using Application.Services.Interfaces;
using Domain.Model;
using Newtonsoft.Json;

namespace Application.Services.implementations;

public class LocalInstanceStore : ILocalInstanceStore
{
    private const string LocalInstanceDbPath = "/var/lib/easyhub-instance-data/instances.json";
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    private async Task<List<InstanceInfo>> ReadInstancesFromFileAsync()
    {
        if (!File.Exists(LocalInstanceDbPath))
        {
            var dir = Path.GetDirectoryName(LocalInstanceDbPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir!);
            }

            await File.WriteAllTextAsync(LocalInstanceDbPath, "[]");
            return new List<InstanceInfo>();
        }

        var json = await File.ReadAllTextAsync(LocalInstanceDbPath);
        return JsonConvert.DeserializeObject<List<InstanceInfo>>(json) ?? new List<InstanceInfo>();
    }

    private async Task WriteInstancesToFileAsync(List<InstanceInfo> instances)
    {
        var json = JsonConvert.SerializeObject(instances, Formatting.Indented);
        await File.WriteAllTextAsync(LocalInstanceDbPath, json);
    }

    public async Task<List<InstanceInfo>> GetAllAsync()
    {
        await Semaphore.WaitAsync();
        try
        {
            return await ReadInstancesFromFileAsync();
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
            var instances = await ReadInstancesFromFileAsync();

            if (instances.All(i => i.Id != newInstance.Id))
            {
                instances.Add(newInstance);
                await WriteInstancesToFileAsync(instances);
                Console.WriteLine($"✅[AddAsync] Instance {newInstance.Id} added successfully.");
            }
            else
            {
                Console.WriteLine($"ℹ️[AddAsync] Instance {newInstance.Id} already exists.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌[AddAsync] Exception: {ex.Message}");
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
            var instances = await ReadInstancesFromFileAsync();
            var instanceToRemove = instances.FirstOrDefault(i => i.Id == instanceId);
            if (instanceToRemove != null)
            {
                instances.Remove(instanceToRemove);
                await WriteInstancesToFileAsync(instances);
                Console.WriteLine($"✅[RemoveAsync] Instance {instanceId} removed successfully.");
            }
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task UpdateAsync(InstanceInfo instanceToUpdate)
    {
        await Semaphore.WaitAsync();
        try
        {
            var instances = await ReadInstancesFromFileAsync();
            var instanceIndex = instances.FindIndex(i => i.Id == instanceToUpdate.Id);

            if (instanceIndex != -1)
            {
                instances[instanceIndex] = instanceToUpdate;
                await WriteInstancesToFileAsync(instances);
            }
        }
        finally
        {
            Semaphore.Release();
        }
    }
}