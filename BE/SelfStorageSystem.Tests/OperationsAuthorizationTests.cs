using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Admin;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Operations;
using SelfStorageSystem.Controllers;
using SelfStorageSystem.Domain.Constants;

namespace SelfStorageSystem.Tests;

public class OperationsAuthorizationTests : IClassFixture<OperationsAuthorizationFixture>
{
    private readonly OperationsAuthorizationFixture _fixture;

    public OperationsAuthorizationTests(OperationsAuthorizationFixture fixture) => _fixture = fixture;

    public static IEnumerable<object[]> Routes()
    {
        yield return new object[] { "GET", "/api/manager/facilities/1/pricing/rates", "", nameof(IBusinessOperationsService.GetRatesAsync) };
        yield return new object[] { "PUT", "/api/manager/facilities/1/pricing/rates",
            """{"facilityId":1,"unitTypeId":1,"monthlyRate":1000000,"validFrom":"2026-01-01"}""",
            nameof(IBusinessOperationsService.UpsertRateAsync) };
        yield return new object[] { "GET", "/api/manager/pricing/fees", "", nameof(IBusinessOperationsService.GetFeesAsync) };
        yield return new object[] { "GET", "/api/manager/promotions", "", nameof(IBusinessOperationsService.GetPromotionsAsync) };
        yield return new object[] { "POST", "/api/manager/promotions",
            """{"code":"SALE","name":"Sale","discountType":"fixed","discountValue":100000,"validFrom":"2026-01-01T00:00:00Z","validTo":"2026-12-31T00:00:00Z"}""",
            nameof(IBusinessOperationsService.CreatePromotionAsync) };
        yield return new object[] { "PUT", "/api/manager/promotions/7/status", """{"isActive":false}""",
            nameof(IBusinessOperationsService.UpdatePromotionStatusAsync) };
    }

    public static IEnumerable<object[]> AnonymousRoutes() => Routes().Select(route => route.Take(3).ToArray());

    public static IEnumerable<object[]> ForbiddenRoutes() => Routes().SelectMany(route =>
        new[] { RoleConstants.Customer, RoleConstants.FacilityStaff }
            .Select(role => route.Take(3).Concat(new object[] { role }).ToArray()));

    public static IEnumerable<object[]> AllowedRoutes() => Routes().SelectMany(route =>
        new[] { RoleConstants.FacilityManager, RoleConstants.SystemAdministrator }
            .Select(role => route.Concat(new object[] { role }).ToArray()));

    [Theory]
    [MemberData(nameof(AnonymousRoutes))]
    public async Task NoIdentity_IsUnauthorizedWithoutCallingService(
        string method, string path, string body)
    {
        var callsBefore = _fixture.Service.Invocations.Count;
        using var response = await SendAsync(method, path, body, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(callsBefore, _fixture.Service.Invocations.Count);
    }

    [Theory]
    [MemberData(nameof(ForbiddenRoutes))]
    public async Task OtherRoles_AreForbiddenWithoutCallingService(
        string method, string path, string body, string role)
    {
        var callsBefore = _fixture.Service.Invocations.Count;
        using var response = await SendAsync(method, path, body, role);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(callsBefore, _fixture.Service.Invocations.Count);
    }

    [Theory]
    [MemberData(nameof(AllowedRoutes))]
    public async Task ManagerAndAdministrator_CanReachEveryRoute(
        string method, string path, string body, string serviceMethod, string role)
    {
        var callsBefore = _fixture.Service.Invocations.Count;
        using var response = await SendAsync(method, path, body, role);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invocation = Assert.Single(_fixture.Service.Invocations.Skip(callsBefore));
        Assert.Equal(serviceMethod, invocation.Method.Name);
        if (method != "GET")
            Assert.Contains(OperationsAuthorizationFixture.UserId, invocation.Arguments.OfType<long>());
    }

    private async Task<HttpResponseMessage> SendAsync(string method, string path, string body, string? role)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body.Length > 0)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        if (role != null)
            request.Headers.Add(OperationsAuthorizationFixture.RoleHeader, role);
        return await _fixture.Client.SendAsync(request);
    }
}

public sealed class OperationsAuthorizationFixture : IDisposable
{
    public const long UserId = 1001;
    public const string RoleHeader = "X-Test-Role";
    private readonly TestServer _server;
    public Mock<IBusinessOperationsService> Service { get; } = new(MockBehavior.Strict);
    public Mock<IFacilityManagerService> ManagerService { get; } = new(MockBehavior.Strict);
    public Mock<IAdminManagementService> AdminService { get; } = new(MockBehavior.Strict);
    public HttpClient Client { get; }

    public OperationsAuthorizationFixture()
    {
        Service.Setup(s => s.GetRatesAsync(It.IsAny<OperationsRateQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<OperationsRateDto>());
        Service.Setup(s => s.UpsertRateAsync(It.IsAny<OperationsUpsertRateRequest>(), UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationsRateDto());
        Service.Setup(s => s.GetFeesAsync(It.IsAny<OperationsFeeQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<OperationsFeeDto>());
        Service.Setup(s => s.UpsertFeeAsync(It.IsAny<OperationsUpsertFeeRequest>(), UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationsFeeDto());
        Service.Setup(s => s.GetPromotionsAsync(It.IsAny<OperationsPromotionQueryParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<OperationsPromotionDto>());
        Service.Setup(s => s.CreatePromotionAsync(It.IsAny<OperationsCreatePromotionRequest>(), UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationsPromotionDto());
        Service.Setup(s => s.UpdatePromotionStatusAsync(7, It.IsAny<OperationsUpdatePromotionStatusRequest>(), UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationsPromotionDto());

        ManagerService.Setup(m => m.ValidateFacilityAccessAsync(It.IsAny<long>(), UserId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Exercise MVC routing and authorization middleware without connecting to a database.
        _server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddSingleton(Service.Object);
                services.AddSingleton(ManagerService.Object);
                services.AddSingleton(AdminService.Object);
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, RoleAuthenticationHandler>("Test", _ => { });
                services.AddAuthorization();
                services.AddControllers().ConfigureApplicationPartManager(parts =>
                {
                    parts.ApplicationParts.Clear();
                    parts.ApplicationParts.Add(new ControllerPart(typeof(ManagerController)));
                    parts.ApplicationParts.Add(new ControllerPart(typeof(AdminController)));
                });
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));
        Client = _server.CreateClient();
    }

    public void Dispose()
    {
        Client.Dispose();
        _server.Dispose();
    }

    private sealed class ControllerPart : ApplicationPart, IApplicationPartTypeProvider
    {
        private readonly Type _controllerType;
        public ControllerPart(Type controllerType) => _controllerType = controllerType;
        public override string Name => _controllerType.Name;
        public IEnumerable<TypeInfo> Types => new[] { _controllerType.GetTypeInfo() };
    }

    public sealed class RoleAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public RoleAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RoleHeader, out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            }, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
