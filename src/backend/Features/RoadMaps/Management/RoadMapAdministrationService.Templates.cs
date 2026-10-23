using backend.Auth;
using backend.Domain.RoadMaps;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NUlid;
using backend.Features.Common;
using static backend.Features.Common.OutcomeErrors;

namespace backend.Features.RoadMaps.Management;

public sealed partial class RoadMapAdministrationService
{
    public async Task<ModuleOutcome<RoadMapTemplateDetailResponse>> CreateTemplate(CreateRoadMapTemplateRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateCreateTemplateRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var productExists = await dbContext.Products.AnyAsync(product => product.Id == request.ProductId, cancellationToken);
        if (!productExists)
        {
            return Missing(new ErrorResponse("product_not_found", "Product was not found."));
        }

        try
        {
            var template = RoadMapTemplate.Create(request.Name!.Trim(), request.ProductId, NormalizeOptional(request.Description));
            dbContext.RoadMapTemplates.Add(template);
            await ModuleAudit.SaveGeneratedIdentityAsync(dbContext, actor.Id, "create", "roadmap_template", () => template.Id.ToString(), () => new { request.Name, request.ProductId }, cancellationToken);

            var response = await BuildTemplateDetail(template.Id).FirstAsync(cancellationToken);
            return ModuleOutcome<RoadMapTemplateDetailResponse>.Success(response);
        }
        catch (ArgumentException ex)
        {
            return Invalid(ToErrorDictionary("request", ex.Message));
        }
    }

    public async Task<ModuleOutcome<RoadMapTemplateDetailResponse>> PatchTemplate(int id,
        PatchRoadMapTemplateRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (request is null || !request.HasChanges())
        {
            return Invalid(ToErrorDictionary("body", "At least one field must be provided for patch."));
        }

        var template = await dbContext.RoadMapTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null)
        {
            return Missing(new ErrorResponse("roadmap_template_not_found", "Roadmap template was not found."));
        }

        if (request.Name is not null)
        {
            template.Update(request.Name.Trim(), request.Description is not null ? NormalizeOptional(request.Description) : template.Description);
        }
        else if (request.Description is not null)
        {
            template.Update(template.Name, NormalizeOptional(request.Description));
        }

        await ModuleAudit.SaveAsync(dbContext, actor.Id, "patch", "roadmap_template", template.Id.ToString(), new { request.Name }, cancellationToken);

        var response = await BuildTemplateDetail(template.Id).FirstAsync(cancellationToken);
        return ModuleOutcome<RoadMapTemplateDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<RoadMapTemplateDetailResponse>> ChangeTemplateStatus(int id,
        ChangeRoadMapTemplateStatusRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (request is null || request.IsActive is null)
        {
            return Invalid(ToErrorDictionary("isActive", "isActive is required."));
        }

        var template = await dbContext.RoadMapTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null)
        {
            return Missing(new ErrorResponse("roadmap_template_not_found", "Roadmap template was not found."));
        }

        if (request.IsActive.Value) template.Activate();
        else template.Deactivate();

        await ModuleAudit.SaveAsync(dbContext, actor.Id, "change_status", "roadmap_template", template.Id.ToString(), new { request.IsActive }, cancellationToken);

        var response = await BuildTemplateDetail(template.Id).FirstAsync(cancellationToken);
        return ModuleOutcome<RoadMapTemplateDetailResponse>.Success(response);
    }

    public async Task<ModuleOutcome<RoadMapTemplateDetailResponse>> AddItem(int id,
        CreateRoadMapItemRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        var errors = ValidateCreateItemRequest(request);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var templateExists = await dbContext.RoadMapTemplates.AnyAsync(x => x.Id == id, cancellationToken);
        if (!templateExists)
        {
            return Missing(new ErrorResponse("roadmap_template_not_found", "Roadmap template was not found."));
        }

        if (!Enum.TryParse<RoadMapItem.RoadMapItemType>(request.Type, true, out var type) || !Enum.IsDefined(type))
        {
            return Invalid(ToErrorDictionary("type", "Type is invalid."));
        }

        try
        {
            var item = RoadMapItem.Create(id, request.Title!.Trim(), type, request.DaysFromStart);
            dbContext.RoadMapItems.Add(item);
            await ModuleAudit.SaveGeneratedIdentityAsync(dbContext, actor.Id, "add_item", "roadmap_template", () => id.ToString(), () => new { item.Id, request.Title, request.Type }, cancellationToken);

            var response = await BuildTemplateDetail(id).FirstAsync(cancellationToken);
            return ModuleOutcome<RoadMapTemplateDetailResponse>.Success(response);
        }
        catch (ArgumentException ex)
        {
            return Invalid(ToErrorDictionary("request", ex.Message));
        }
    }

    public async Task<ModuleOutcome<RoadMapItemResponse>> PatchItem(int id,
        PatchRoadMapItemRequest request,
        CancellationToken cancellationToken, ModuleActor actor)
    {
        if (request is null || !request.HasChanges())
        {
            return Invalid(ToErrorDictionary("body", "At least one field must be provided for patch."));
        }

        var item = await dbContext.RoadMapItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return Missing(new ErrorResponse("roadmap_item_not_found", "Roadmap item was not found."));
        }

        var nextTitle = request.Title?.Trim() ?? item.Title;
        var nextType = item.Type;
        if (request.Type is not null)
        {
            if (!Enum.TryParse<RoadMapItem.RoadMapItemType>(request.Type, true, out nextType))
            {
                return Invalid(ToErrorDictionary("type", "Type is invalid."));
            }
        }

        var nextDays = request.DaysFromStart ?? item.DaysFromStart;

        try
        {
            item.Update(nextTitle, nextType, nextDays);
            await ModuleAudit.SaveAsync(dbContext, actor.Id, "patch", "roadmap_item", item.Id.ToString(), new { request.Title, request.Type, request.DaysFromStart }, cancellationToken);
            return ModuleOutcome<RoadMapItemResponse>.Success(new RoadMapItemResponse(item.Id, item.Title, item.Type.ToString(), item.DaysFromStart));
        }
        catch (ArgumentException ex)
        {
            return Invalid(ToErrorDictionary("request", ex.Message));
        }
    }
}
