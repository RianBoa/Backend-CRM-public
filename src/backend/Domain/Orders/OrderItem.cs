namespace backend.Domain.Orders
{
    using Domain.Products;
    using NUlid;
    using System.Text.Json;

    public sealed record OrderItem
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();
        // FKs
        public Order Order { get; private init; }
        public Ulid OrderId { get; private init; }

        public Product Product { get; private init; }
        public int ProductId { get; private init; }

        public Package Package { get; private init; }
        public int PackageId { get; private init; }

        public decimal Price { get; private init; }
        public uint Quantity { get; private init; }
        public JsonDocument? Meta { get; private init; }

        private OrderItem() { } // For EF Core

        private OrderItem( Order order, Ulid orderId, Product product, int productId, Package package, int packageId, decimal price, uint quantity)
        {
            Order = order;
            OrderId = orderId;
            Product = product;
            ProductId = productId;
            Package = package;
            PackageId = packageId;
            Price = price;
            Quantity = quantity;
        }

        // Factory method to create a new OrderItem

        public static OrderItem Create(Order order, Product product, Package package, decimal price, uint quantity)
        {
            if (order == null) throw new ArgumentNullException(nameof(order));
            if (product == null) throw new ArgumentNullException(nameof(product));
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (price < 0) throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
            if (quantity == 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least 1.");
            return new OrderItem(
                order: order,
                orderId: order.Id,
                product: product,
                productId: product.Id,
                package: package,
                packageId: package.Id,
                price: price,
                quantity: quantity
            );
        }
        public static OrderItem Update(
            OrderItem item,
            Product? product = null,
            Package? package = null,
            decimal? price = null,
            uint? quantity = null)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (price is not null && price < 0) throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
            if (quantity is not null && quantity == 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be at least 1.");
            return item with
            {
                Product = product ?? item.Product,
                ProductId = product?.Id ?? item.ProductId,
                Package = package ?? item.Package,
                PackageId = package?.Id ?? item.PackageId,
                Price = price ?? item.Price,
                Quantity = quantity ?? item.Quantity
            };
        }

    }
}
