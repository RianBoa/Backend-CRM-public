using System;
using System.Collections.Generic;
using System.Linq;
using backend.Domain.Orders;
using backend.Domain.Subscriptions;
using backend.Domain.Cases;
using NUlid;

namespace backend.Domain.Clients
{
    public sealed record Client
    {
        //Identity
        public Ulid Id { get; private init; } = Ulid.NewUlid();
        public string ShortId => Id.ToString()[..10];

        // Basic Info
        public string Name { get; private set; } = default!;
        public ICollection<ClientEmail> Emails { get; private init; } = new List<ClientEmail>();
        public Email? PrimaryEmail => Emails.FirstOrDefault(x => x.IsPrimary)?.Email;

        // Contact & Additional Info
        public ContactInfo Contacts { get; private set; } = ContactInfo.Empty;
        public Financials Financials { get; private set; } = Financials.Empty;
        public AdditionalInfo? AdditionalInfo { get; private set; }

        //Metadata
        public DateTimeOffset CreatedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
        public string? CreatedBy { get; private set; }

        // Relations
        public ICollection<Order> Orders { get; private init; } = new List<Order>();
        public ICollection<Subscription> Subscriptions { get; private init; } = new List<Subscription>();
        public ICollection<Case> Cases { get; private init; } = new List<Case>();

        // Computed
        public int ActiveOrdersCount => Orders.Count(o => o.Status == OrderStatus.Active);
        public int CompletedOrdersCount => Orders.Count(o => o.Status == OrderStatus.Graduated);

        //Constructors
        private Client() { }

        private Client(string name, DateTimeOffset createdAtUtc, string? createdBy)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Full name is required.", nameof(name));

            Name = name.Trim();
            CreatedAtUtc = createdAtUtc;
            CreatedBy = createdBy;
        }

        //  Factory
        public static Client Create(
            string name,
            Email? email,
            ContactInfo? contacts = null,
            Financials? financials = null,
            AdditionalInfo? additionalInfo = null,
            DateTimeOffset? nowUtc = null,
            string? createdBy = null)
        {
            var client = new Client(name, nowUtc ?? DateTimeOffset.UtcNow, createdBy);

            if (contacts is not null) client.SetContacts(contacts);
            if (financials is not null) client.SetFinancials(financials);
            if (additionalInfo is not null) client.SetAdditionalInfo(additionalInfo);
            if (email is not null) client.AddEmail(email, isPrimary: true);

            client.EnsureAtLeastOneContact();
            return client;
        }

        // Mutators
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Full name is required.", nameof(name));

            Name = name.Trim();
        }

        public void ChangeEmail(Email? email)
        {
            if (email is null)
            {
                RemovePrimaryEmail();
            }
            else
            {
                ReplacePrimaryEmail(email);
            }

            EnsureAtLeastOneContact();
        }

        public void AddEmail(Email email, bool isPrimary = false)
        {
            ArgumentNullException.ThrowIfNull(email);

            if (Emails.Any(x => x.Email.Value == email.Value))
                return;

            var shouldBePrimary = isPrimary || Emails.Count == 0;

            if (shouldBePrimary)
                ClearPrimaryEmail();

            Emails.Add(ClientEmail.Create(Id, email, shouldBePrimary));
            EnsureAtLeastOneContact();
        }

        public void RemoveEmail(string rawEmail)
        {
            if (string.IsNullOrWhiteSpace(rawEmail))
                return;

            var normalized = Email.From(rawEmail).Value;
            var existing = Emails.FirstOrDefault(x => x.Email.Value == normalized);
            if (existing is null)
                return;

            var wasPrimary = existing.IsPrimary;
            Emails.Remove(existing);

            if (wasPrimary && Emails.Count > 0)
                Emails.First().SetPrimary();

            EnsureAtLeastOneContact();
        }

        public void SetPrimaryEmail(string rawEmail)
        {
            if (string.IsNullOrWhiteSpace(rawEmail))
                throw new ArgumentException("Email is required.", nameof(rawEmail));

            var normalized = Email.From(rawEmail).Value;
            var existing = Emails.FirstOrDefault(x => x.Email.Value == normalized)
                ?? throw new InvalidOperationException("Client does not have this email.");

            ClearPrimaryEmail();
            existing.SetPrimary();
        }

        public void SetContacts(ContactInfo contacts)
        {
            var next = contacts ?? ContactInfo.Empty;
            EnsureAtLeastOneContact(next);
            Contacts = next;
        }

        public void SetPhone(string? phone)
        {
            Contacts = Contacts with { PhoneNumber = NormalizeOrNull(phone) };
            EnsureAtLeastOneContact();
        }

        public void LinkTelegram(string? handleOrId)
        {
            Contacts = Contacts with { Telegram = NormalizeOrNull(handleOrId) };
            EnsureAtLeastOneContact();
        }

        public void LinkDiscord(string? userIdOrTag)
        {
            Contacts = Contacts with { Discord = NormalizeOrNull(userIdOrTag) };
            EnsureAtLeastOneContact();
        }

        public void SetFinancials(Financials financials)
        {
            Financials = financials ?? Financials.Empty;
        }

        public void AddEarnings(decimal amount)
        {
            Financials = Financials.AddEarnings(amount);
        }

        public void SetAdditionalInfo(AdditionalInfo? info)
        {
            AdditionalInfo = info;
        }

        //Helpers
        private static string? NormalizeOrNull(string? v)
            => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        private void ReplacePrimaryEmail(Email email)
        {
            var existing = Emails.FirstOrDefault(x => x.Email.Value == email.Value);
            ClearPrimaryEmail();

            if (existing is not null)
            {
                existing.SetPrimary();
                return;
            }

            Emails.Add(ClientEmail.Create(Id, email, true));
        }

        private void RemovePrimaryEmail()
        {
            var primary = Emails.FirstOrDefault(x => x.IsPrimary);
            if (primary is null)
                return;

            Emails.Remove(primary);
            if (Emails.Count > 0)
                Emails.First().SetPrimary();
        }

        private void ClearPrimaryEmail()
        {
            foreach (var clientEmail in Emails.Where(x => x.IsPrimary))
                clientEmail.UnsetPrimary();
        }

        //Business Rules
        private void EnsureAtLeastOneContact(ContactInfo? proposedContacts = null)
        {
            var contacts = proposedContacts ?? Contacts;
            if (!Emails.Any() &&
                string.IsNullOrWhiteSpace(contacts.PhoneNumber) &&
                string.IsNullOrWhiteSpace(contacts.Telegram) &&
                string.IsNullOrWhiteSpace(contacts.Discord))
            {
                throw new ContactMethodRequiredException();
            }
        }
    }
}
