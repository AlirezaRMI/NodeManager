using Application.Services.Interfaces;
using Domain.DTOs.Instance;
using Newtonsoft.Json;

namespace Application.Services.implementations;

public class LocalInstanceStore : ILocalInstanceStore
{
    private const string LocalInstanceDbPath = "/var/lib/easyhub-instance-data/instances.json";
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public async Task<List<InstanceInfo>> GetAllAsync()
    {
        await Semaphore.WaitAsync();
        try
        {
            if (!File.Exists(LocalInstanceDbPath))
            {
                await File.WriteAllTextAsync(LocalInstanceDbPath, "[]");
                return new List<InstanceInfo>();
            }

            var json = await File.ReadAllTextAsync(LocalInstanceDbPath);
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
            var directory = Path.GetDirectoryName(LocalInstanceDbPath);
            Console.WriteLine($"📁[AddAsync] Directory: {directory}");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
                Console.WriteLine($"📂[AddAsync] Directory created.");
            }

            var instances = await GetAllAsync();
            Console.WriteLine($"📄[AddAsync] Existing instance count: {instances.Count}");

            if (instances.All(i => i.Id != newInstance.Id))
            {
                instances.Add(newInstance);
                Console.WriteLine($"➕[AddAsync] Adding instance with ID {newInstance.Id}");

                var json = JsonConvert.SerializeObject(instances, Formatting.Indented);
                await File.WriteAllTextAsync(LocalInstanceDbPath, json);

                Console.WriteLine($"✅[AddAsync] Successfully wrote to {LocalInstanceDbPath}");
            }
            else
            {
                Console.WriteLine($"⚠️[AddAsync] Instance ID {newInstance.Id} already exists.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌[AddAsync] Failed to write instance JSON: {ex.Message}");
            throw;
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
            var directory = Path.GetDirectoryName(LocalInstanceDbPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }
            var instances = await GetAllAsync();
            var instanceToRemove = instances.FirstOrDefault(i => i.Id == instanceId);
            if (instanceToRemove != null)
            {
                instances.Remove(instanceToRemove);
                var json = JsonConvert.SerializeObject(instances, Formatting.Indented);
                if (!Directory.Exists(directory) || !new DirectoryInfo(directory!).Attributes.HasFlag(FileAttributes.Directory))
                {
                    throw new Exception($"Directory {directory} is not accessible or not a directory");
                }

                await File.WriteAllTextAsync(LocalInstanceDbPath, json);
            }
        }
        finally
        {
            Semaphore.Release();
        }
    }
}