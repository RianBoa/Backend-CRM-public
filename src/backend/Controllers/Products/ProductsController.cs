using backend.Auth;
using backend.Features.Products.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("products")]
public sealed class ProductsController(CatalogService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductListItemResponse>>> GetList([FromQuery] ProductListQuery query,
        CancellationToken cancellationToken)
        => Map(await service.GetList(query, cancellationToken, Actor));

    [HttpGet("select")]
    public async Task<ActionResult<SelectResponse<ProductSelectItemResponse>>> GetSelect([FromQuery] string? search,
        [FromQuery] bool? activeOnly,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
        => Map(await service.GetSelect(search, activeOnly, limit, cancellationToken, Actor));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDetailResponse>> GetById(int id, CancellationToken cancellationToken)
        => Map(await service.GetById(id, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageProducts)]
    [HttpPost]
    public async Task<ActionResult<ProductDetailResponse>> Create([FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
        => Map(await service.Create(request, cancellationToken, Actor), value => CreatedAtAction(nameof(GetById), new { id = value.Id }, value));

    [Authorize(Policy = AuthPolicies.ManageProducts)]
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<ProductDetailResponse>> Patch(int id,
        [FromBody] PatchProductRequest request,
        CancellationToken cancellationToken)
        => Map(await service.Patch(id, request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageProducts)]
    [HttpPost("{id:int}/packages")]
    public async Task<ActionResult<ProductDetailResponse>> AddPackage(int id,
        [FromBody] CreatePackageRequest request,
        CancellationToken cancellationToken)
        => Map(await service.AddPackage(id, request, cancellationToken, Actor));

    [Authorize(Policy = AuthPolicies.ManageProducts)]
    [HttpPatch("packages/{packageId:int}")]
    public async Task<ActionResult<PackageResponse>> PatchPackage(int packageId,
        [FromBody] PatchPackageRequest request,
        CancellationToken cancellationToken)
        => Map(await service.PatchPackage(packageId, request, cancellationToken, Actor));
}
