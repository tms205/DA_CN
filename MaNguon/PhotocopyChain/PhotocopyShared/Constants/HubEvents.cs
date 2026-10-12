namespace PhotocopyShared.Constants;

/// <summary>Tên sự kiện SignalR (dùng từ Tuần 2).</summary>
public static class HubEvents
{
    public const string OrderCreated = "OrderCreated";
    public const string OrderStatusChanged = "OrderStatusChanged";
    public const string PaymentConfirmed = "PaymentConfirmed";
    public const string DeliveryAvailable = "DeliveryAvailable";
    public const string CatalogChanged = "CatalogChanged";
    public const string StockAlert = "StockAlert";
}
