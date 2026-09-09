using Identity.Api.Administration;
using Identity.Api.Hosting;
using Identity.Api.Validation;
using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Contracts.Administration;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Api.Endpoints;

internal static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin").RequireAuthorization(AdministrationPolicy.Name);
        group.MapGet("/organization-units", Search);
        group.MapGet("/organizations/{organizationId:long}/units/{unitId:long}", Get);
        group.MapPost("/organizations", Create).AddEndpointFilter<RequestValidationFilter<CreateOrganizationRequest>>();
        group.MapPost("/organizations/{organizationId:long}/units", CreateChild).AddEndpointFilter<RequestValidationFilter<CreateOrganizationUnitRequest>>();
        group.MapPut("/organizations/{organizationId:long}/units/{unitId:long}", Update).AddEndpointFilter<RequestValidationFilter<UpdateOrganizationUnitRequest>>();
        group.MapPut("/organizations/{organizationId:long}/units/{unitId:long}/active", SetActive).AddEndpointFilter<RequestValidationFilter<SetOrganizationUnitActiveRequest>>();
        return endpoints;
    }
    private static async Task<IResult> Search(long? organizationId, string? unitType, long? parentOrganizationUnitId,
     bool? isActive, string? search, int? skip, int? take, HttpContext context, IRequestDispatcher dispatcher, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new SearchOrganizationUnitsQuery(organizationId, unitType is null ? null : ParseType(unitType),
         parentOrganizationUnitId, isActive, search, skip ?? 0, take ?? 20, RequestContextFactory.Administration(context)), cancellationToken);
        return Results.Ok(new PagedOrganizationUnitsResponse(result.Skip, result.Take, result.TotalCount, result.Items.Select(Map).ToArray()));
    }
    private static async Task<IResult> Get(long organizationId, long unitId, HttpContext context, IRequestDispatcher dispatcher, CancellationToken cancellationToken) =>
     Results.Ok(Map(await dispatcher.Send(new GetOrganizationUnitQuery(organizationId, unitId, RequestContextFactory.Administration(context)), cancellationToken)));
    private static async Task<IResult> Create(CreateOrganizationRequest request, HttpContext context, IRequestDispatcher dispatcher, CancellationToken cancellationToken) =>
     Created(await dispatcher.Send(new CreateOrganizationCommand(request.UnitCode, request.UnitName, RequestContextFactory.Administration(context),
      request.Description, Address(request.Address)), cancellationToken));
    private static async Task<IResult> CreateChild(long organizationId, CreateOrganizationUnitRequest request, HttpContext context, IRequestDispatcher dispatcher, CancellationToken cancellationToken) =>
     Created(await dispatcher.Send(new CreateOrganizationUnitCommand(organizationId, request.ParentOrganizationUnitId, ParseType(request.UnitType),
      request.UnitCode, request.UnitName, RequestContextFactory.Administration(context), request.Description, Address(request.Address)), cancellationToken));
    private static async Task<IResult> Update(long organizationId, long unitId, UpdateOrganizationUnitRequest request, HttpContext context, IRequestDispatcher dispatcher, CancellationToken cancellationToken) =>
     Results.Ok(Map(await dispatcher.Send(new UpdateOrganizationUnitCommand(organizationId, unitId, request.UnitCode, request.UnitName, request.Description,
      Address(request.Address), request.RowVersion, RequestContextFactory.Administration(context)), cancellationToken)));
    private static async Task<IResult> SetActive(long organizationId, long unitId, SetOrganizationUnitActiveRequest request, HttpContext context, IRequestDispatcher dispatcher, CancellationToken cancellationToken) =>
     Results.Ok(Map(await dispatcher.Send(new SetOrganizationUnitActiveCommand(organizationId, unitId, request.IsActive!.Value, request.RowVersion,
      RequestContextFactory.Administration(context)), cancellationToken)));
    private static IResult Created(OrganizationHierarchyResult result) => Results.Created(
     $"/api/v1/admin/organizations/{result.OrganizationId}/units/{result.OrganizationUnitId}",
     new OrganizationCreatedResponse(result.OrganizationId, result.OrganizationUnitId, result.UnitType.ToString(), result.HierarchyPath, result.RowVersion));
    private static OrganizationUnitType ParseType(string value)
    {
        if (!Enum.TryParse<OrganizationUnitType>(value, out var type) || !Enum.IsDefined(type) || type.ToString() != value)
            throw new ArgumentException("A named organization unit type is required.");
        return type;
    }
    private static OrganizationUnitAddress Address(OrganizationAddressData? address) => address is null ? new() : new(
     address.AddressLine1, address.AddressLine2, address.AddressLine3, address.City, address.District, address.StateName,
     address.PostalCode, address.CountryCode, address.Latitude, address.Longitude);
    private static OrganizationUnitResponse Map(OrganizationUnitDetails unit) => new(unit.OrganizationId, unit.OrganizationUnitId,
     unit.ParentOrganizationUnitId, unit.UnitType.ToString(), unit.UnitCode, unit.UnitName, unit.Description,
     new OrganizationAddressData(unit.Address.AddressLine1, unit.Address.AddressLine2, unit.Address.AddressLine3, unit.Address.City,
      unit.Address.District, unit.Address.StateName, unit.Address.PostalCode, unit.Address.CountryCode, unit.Address.Latitude, unit.Address.Longitude),
     unit.HierarchyPath, unit.IsActive, DateTime.SpecifyKind(unit.CreatedAt, DateTimeKind.Utc),
     unit.UpdatedAt.HasValue ? DateTime.SpecifyKind(unit.UpdatedAt.Value, DateTimeKind.Utc) : null, unit.RowVersion);
}
