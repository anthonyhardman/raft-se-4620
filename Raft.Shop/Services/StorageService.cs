using Microsoft.AspNetCore.Components.Forms;

namespace Raft.Shop;

public class StorageService
{
    private readonly HttpClient _httpClient;

    public StorageService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("StorageClient");
    }

    public async Task<int> GetUserBalance(string userId)
    {
        var userBalanceKey = userId.Replace(" ", "-") + "-balance";

        try
        {
            return await StrongGet(userBalanceKey);
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Error getting user balance: {e.Message}");
            return 0;
        }
    }

    public async Task UpdateUserBalance(string userId, int amount)
    {
        var userBalanceKey = userId.Replace(" ", "-") + "-balance";
        var userBalance = await GetUserBalance(userBalanceKey);

        try 
        {
            var response = await _httpClient.PostAsync($"api/storage/append?key={userBalanceKey}&value={userBalance + amount}", null);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Error updating user balance: {e.Message}");
        }

    }

    private async Task<int> StrongGet(string userKey)
    {
        var response = await _httpClient.GetAsync($"api/storage/strong?key={userKey}");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return int.Parse(content);
    }


    public async Task<int> GetItemStock(StoreItem item)
    {
        var itemKey = item.Name.Replace(" ", "-") + "-stock";

        try
        {
            return await StrongGet(itemKey);
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Error getting item stock: {e.Message}");
            return 0;
        }
    }

    public async Task<bool> UpdateStock(StoreItem item, int quantity)
    {
        var itemKey = item.Name.Replace(" ", "-") + "-stock";
        var itemStock = await GetItemStock(item);

        try 
        {
            var response = await _httpClient.PostAsync($"api/storage/append?key={itemKey}&value={itemStock + quantity}", null);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Error updating item stock: {e.Message}");
            return false;
        }
    }
}
