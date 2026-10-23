using backend.Domain.Clients;
using backend.Domain.Orders;
using backend.Domain.Subscriptions;
using backend.Domain.LearningStructure;
using backend.Domain.Cases;
using backend.Domain.Users;
using backend.Domain.Tasks;
using backend.Domain.CuratorNote;
using backend.Domain.Applications;
using backend.Infrastructure.Converters;
using Microsoft.EntityFrameworkCore;
using backend.Domain.RoadMaps;
using backend.Domain.Products;
using backend.Domain.Security;
using backend.Domain.Auditing;

using NUlid;

namespace backend.Infrastructure
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        // Client related entities
        public DbSet<Client> Clients => Set<Client>();
        public DbSet<ClientEmail> ClientEmails => Set<ClientEmail>();
        public DbSet<ExternalAccount> ExternalAccounts => Set<ExternalAccount>();
        public DbSet<Domain.Integrations.SalesOrderLink> SalesOrderLinks => Set<Domain.Integrations.SalesOrderLink>();
        public DbSet<Domain.Integrations.SalesPaymentLink> SalesPaymentLinks => Set<Domain.Integrations.SalesPaymentLink>();
        public DbSet<Domain.Integrations.SalesImportReceipt> SalesImportReceipts => Set<Domain.Integrations.SalesImportReceipt>();
        public DbSet<Domain.Messaging.DiscordGrantRequest> DiscordGrantRequests => Set<Domain.Messaging.DiscordGrantRequest>();
        public DbSet<Domain.Messaging.OutboxMessage> IntegrationOutbox => Set<Domain.Messaging.OutboxMessage>();
        public DbSet<Domain.Messaging.InboxReceipt> IntegrationInbox => Set<Domain.Messaging.InboxReceipt>();
        // Order related entities
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<AppliedBenefit> AppliedBenefits => Set<AppliedBenefit>();
        public DbSet<PromoBenefitRule> PromoBenefitRules => Set<PromoBenefitRule>();
        public DbSet<ConditionRule> ConditionRules => Set<ConditionRule>();

        // Product and Subscription related entities
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Package> Packages => Set<Package>();
        public DbSet<Subscription> Subscriptions => Set<Subscription>();
        public DbSet<SubscriptionAdjustment> SubscriptionAdjustments => Set<SubscriptionAdjustment>();
        public DbSet<DiscordAccess> DiscordAccesses => Set<DiscordAccess>();
        public DbSet<PlatformAccess> PlatformAccesses => Set<PlatformAccess>();

        // Learning Structure related entities
        public DbSet<Cohort> Cohorts => Set<Cohort>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<Enrollment> Enrollments => Set<Enrollment>();
        public DbSet<Case> Cases => Set<Case>();
        public DbSet<RoadMapTemplate> RoadMapTemplates => Set<RoadMapTemplate>();
        public DbSet<RoadMapItem> RoadMapItems => Set<RoadMapItem>();
        public DbSet<RoadMapAssignment> RoadMapAssignments => Set<RoadMapAssignment>();
        public DbSet<CuratorNote> CuratorNotes => Set<CuratorNote>();
        public DbSet<NoteType> NoteTypes => Set<NoteType>();

        // User and Task related entities
        public DbSet<User> Users => Set<User>();
        public DbSet<WorkflowTask> Tasks => Set<WorkflowTask>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        // Application related entities
        public DbSet<Application> Applications => Set<Application>();
        public DbSet<ApplicationReply> ApplicationReplies => Set<ApplicationReply>();
        public DbSet<ApplicationType> ApplicationTypes => Set<ApplicationType>();


        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder
                .Properties<Ulid>()
                .HaveConversion<UlidToStringConverter>()
                .HaveMaxLength(26);

            configurationBuilder
                .Properties<Ulid?>()
                .HaveConversion<NullableUlidToStringConverter>()
                .HaveMaxLength(26);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Ignore<Domain.Subscriptions.SubscriptionStatus>();
            modelBuilder.Ignore<backend.Domain.Orders.OrderStatus>();

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }

        private void AdvanceEnrollmentVersions()
        {
            foreach (var entry in ChangeTracker.Entries<Enrollment>().Where(x => x.State == EntityState.Modified))
                entry.Property(x => x.Version).CurrentValue = Guid.NewGuid();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            AdvanceEnrollmentVersions();
            try { return base.SaveChanges(acceptAllChangesOnSuccess); }
            catch (DbUpdateConcurrencyException) { ChangeTracker.Clear(); throw; }
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            AdvanceEnrollmentVersions();
            try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
            catch (DbUpdateConcurrencyException) { ChangeTracker.Clear(); throw; }
        }
    }
}
