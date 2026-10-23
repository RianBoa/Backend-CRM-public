using System.Text.Json;
using backend.Domain.Auditing;
using backend.Features.Clients.Profile;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using TeamRequest = backend.Domain.Applications.Application;

namespace backend.Features.Applications;

public sealed class ApplicationWorkService(ApplicationDbContext db, ClientProfileService profiles, TimeProvider clock)
{
    public async Task<ApplicationProcessingView?> GetProcessingAsync(Ulid id, CancellationToken cancellationToken)
    {
        var application = await db.Applications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return application is null ? null : ToProcessing(application,
            await db.ApplicationReplies.CountAsync(x => x.ApplicationId == id, cancellationToken));
    }

    public async Task<PagedResponse<ReplyView>?> GetRepliesAsync(Ulid id, ApplicationPageQuery page, CancellationToken cancellationToken)
    {
        if (!await db.Applications.AnyAsync(x => x.Id == id, cancellationToken)) return null;
        var query = db.ApplicationReplies.AsNoTracking().Where(x => x.ApplicationId == id);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page.Page - 1) * page.PageSize).Take(page.PageSize)
            .Select(x => new { x.Id, x.AuthorId, AuthorName = x.Author.FullName, x.Body, x.CreatedAt })
            .ToListAsync(cancellationToken);
        return new(rows.Select(x => new ReplyView(x.Id.ToString(), x.AuthorId.ToString(), x.AuthorName, x.Body, x.CreatedAt)).ToList(),
            total, page.Page, page.PageSize);
    }

    public async Task<WorkResult<ReplyReceipt>> ReplyAsync(
        Ulid id, Ulid actorId, string body, TeamRequest.ApplicationStatus? status, CancellationToken cancellationToken)
    {
        var actor = await db.Users.FirstOrDefaultAsync(x => x.Id == actorId && x.IsActive, cancellationToken);
        if (actor is null) return WorkResult<ReplyReceipt>.Forbidden();
        var application = await db.Applications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (application is null) return WorkResult<ReplyReceipt>.Missing();
        var now = clock.GetUtcNow().UtcDateTime;
        if (status is not null && !Enum.IsDefined(status.Value))
            return WorkResult<ReplyReceipt>.Conflict("Unknown application status.");
        // Validate the transition before adding a reply; a rejected command must have no partial effects.
        if (status is not null && (status == TeamRequest.ApplicationStatus.New && application.Status != status))
            return WorkResult<ReplyReceipt>.Conflict("An application cannot return to New.");
        backend.Domain.Applications.ApplicationReply reply;
        try
        {
            reply = application.AddReply(actorId, body, now);
            if (status is not null) application.ChangeStatus(status.Value, actorId, now);
        }
        catch (InvalidOperationException ex) { return WorkResult<ReplyReceipt>.Conflict(ex.Message); }
        Audit(application, actorId, "reply", new { replyId = reply.Id.ToString(), status = application.Status.ToString() });
        if (!await SaveAsync(cancellationToken)) return WorkResult<ReplyReceipt>.Conflict("Application changed concurrently. Reload and retry.");
        var count = await db.ApplicationReplies.CountAsync(x => x.ApplicationId == id, cancellationToken);
        return new(new(new(reply.Id.ToString(), actorId.ToString(), actor.FullName, reply.Body, reply.CreatedAt),
            ToProcessing(application, count)), null);
    }

    public async Task<WorkResult<ApplicationProcessingView>> ChangeStatusAsync(
        Ulid id, Ulid actorId, TeamRequest.ApplicationStatus status, CancellationToken cancellationToken)
    {
        if (!await ActiveActorAsync(actorId, cancellationToken)) return WorkResult<ApplicationProcessingView>.Forbidden();
        var application = await db.Applications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (application is null) return WorkResult<ApplicationProcessingView>.Missing();
        var previous = application.Status;
        try { application.ChangeStatus(status, actorId, clock.GetUtcNow().UtcDateTime); }
        catch (InvalidOperationException ex) { return WorkResult<ApplicationProcessingView>.Conflict(ex.Message); }
        if (previous != status)
        {
            Audit(application, actorId, "change_status", new { before = previous.ToString(), after = status.ToString() });
            if (!await SaveAsync(cancellationToken)) return WorkResult<ApplicationProcessingView>.Conflict("Application changed concurrently. Reload and retry.");
        }
        return new(ToProcessing(application, await db.ApplicationReplies.CountAsync(x => x.ApplicationId == id, cancellationToken)), null);
    }

    public async Task<WorkResult<ClientProfile>> UpdateClientProfileAsync(
        Ulid id, Ulid actorId, ClientProfile profile, CancellationToken cancellationToken)
    {
        if (!await ActiveActorAsync(actorId, cancellationToken)) return WorkResult<ClientProfile>.Forbidden();
        var application = await db.Applications.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (application?.ClientId is not { } clientId || !await db.Clients.AnyAsync(x => x.Id == clientId, cancellationToken))
            return WorkResult<ClientProfile>.Missing();
        try { application.RecordClientChange(); }
        catch (InvalidOperationException ex) { return WorkResult<ClientProfile>.Conflict(ex.Message); }
        Audit(application, actorId, "update_client_profile", new { clientId = clientId.ToString() });
        try
        {
            // Profile service shares this DbContext: both audit events, the revision and client save atomically.
            var value = await profiles.ReplaceAsync(clientId, profile, actorId, cancellationToken, id);
            return value is null ? WorkResult<ClientProfile>.Missing() : new(value, null);
        }
        catch (DbUpdateConcurrencyException) { return WorkResult<ClientProfile>.Conflict("Application changed concurrently. Reload and retry."); }
    }

    public async Task<PagedResponse<ApplicationActivity>?> GetActivityAsync(Ulid id, ApplicationPageQuery page, CancellationToken cancellationToken)
    {
        if (!await db.Applications.AnyAsync(x => x.Id == id, cancellationToken)) return null;
        var entityId = id.ToString();
        var query = db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "application" && x.EntityId == entityId);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page.Page - 1) * page.PageSize).Take(page.PageSize)
            .Select(x => new { x.Id, x.UserId, x.Action, x.CreatedAtUtc }).ToListAsync(cancellationToken);
        return new(rows.Select(x => new ApplicationActivity(x.Id.ToString(), x.UserId?.ToString(), x.Action, x.CreatedAtUtc)).ToList(),
            total, page.Page, page.PageSize);
    }

    private Task<bool> ActiveActorAsync(Ulid id, CancellationToken cancellationToken)
        => db.Users.AnyAsync(x => x.Id == id && x.IsActive, cancellationToken);

    private void Audit(TeamRequest application, Ulid actorId, string action, object metadata)
        => db.AuditLogs.Add(AuditLog.Create(actorId, action, "application", application.Id.ToString(), JsonSerializer.Serialize(metadata)));

    private async Task<bool> SaveAsync(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    private static ApplicationProcessingView ToProcessing(TeamRequest x, int count)
        => new(x.Id.ToString(), x.Status.ToString(), x.ClientId?.ToString(), x.ProcessedById?.ToString(), x.ProcessedAt, count);
}
