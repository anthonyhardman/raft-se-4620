namespace Raft.Shop;

public class InventoryService
{
    private StorageService _storageService;

    public InventoryService(StorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<List<StoreItem>> GetItems()
    {
        var items = new List<StoreItem>{
            new StoreItem { Id = "1", Name = "Blue Marble", Price = 1, Stock = 0 },
            new StoreItem { Id = "2", Name = "Red Marble", Price = 1, Stock = 0 },
            new StoreItem { Id = "3", Name = "Green Marble", Price = 1, Stock = 0 },
            new StoreItem { Id = "4", Name = "Yellow Marble", Price = 1, Stock = 0 },
            new StoreItem { Id = "5", Name = "Purple Marble", Price = 1, Stock = 0 },
        };

        foreach (var item in items)
        {
            item.Stock = await _storageService.GetItemStock(item);
        }

        return items;
    }

    public async Task UpdateItemStock(StoreItem item)
    {
        item.Stock = await _storageService.GetItemStock(item);
    }

    public async Task<bool> UpdateStock(StoreItem item, int quantity)
    {
        return await _storageService.UpdateStock(item, quantity);
    }
}
