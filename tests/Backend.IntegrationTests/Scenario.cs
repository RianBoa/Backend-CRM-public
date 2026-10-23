using backend.Auth;
using backend.Domain.Clients;
using backend.Domain.LearningStructure;
using backend.Domain.Orders;
using backend.Domain.Products;
using backend.Domain.Users;

namespace Backend.IntegrationTests;

internal sealed record Scenario(User Admin, Client Student, Product Product, Package Package, Order Order, OrderItem Purchase)
{
    public const string Password = "integration-test-password";

    public static async Task<Scenario> SeedAsync(PostgresDatabase database)
    {
        await using var db = database.CreateContext();
        var admin = User.Create("admin@example.test", "@admin", new UserPasswordService().HashPassword(Password), User.UserRole.Admin, "Admin");
        var student = Client.Create("Student", Email.From("student@example.test"),
            ContactInfo.Create("123456789", "@student", null, null), new Financials(100, 200));
        var product = Product.Create("ffa-core", "FFA Core", "Course", true);
        db.AddRange(admin, student, product);
        await db.SaveChangesAsync();
        var package = Package.Create("mentorship", "Mentorship", product, 30, true, 30);
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        var order = Order.Create(student.Id, OrderSource.Manual);
        var purchase = OrderItem.Create(order, product, package, 100, 1);
        db.OrderItems.Add(purchase);
        await db.SaveChangesAsync();
        return new(admin, student, product, package, order, purchase);
    }
}
