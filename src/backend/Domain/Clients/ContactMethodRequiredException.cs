namespace backend.Domain.Clients;

public sealed class ContactMethodRequiredException()
    : InvalidOperationException("Client must have at least one contact method: email, phone, telegram, or discord.");
