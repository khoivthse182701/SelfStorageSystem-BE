using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SelfStorageSystem.Application.Interfaces;
using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Contracts.Customer.Rentals;
using SelfStorageSystem.Contracts.Manager;
using SelfStorageSystem.Contracts.Staff;
using SelfStorageSystem.Domain.Common;
using SelfStorageSystem.Domain.Constants;
using SelfStorageSystem.Domain.Entities;
using SelfStorageSystem.Domain.Errors;
using SelfStorageSystem.Domain.Exceptions;
using SelfStorageSystem.Infrastructure.Persistence;

namespace SelfStorageSystem.Infrastructure.Services;

public partial class FacilityManagerService : IFacilityManagerService
{
    private readonly SelfStorageDbContext _dbContext;
    private readonly ILogger<FacilityManagerService> _logger;
    private readonly IRealtimeNotifier? _realtimeNotifier;

    public FacilityManagerService(
        SelfStorageDbContext dbContext,
        ILogger<FacilityManagerService> logger,
        IRealtimeNotifier? realtimeNotifier = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _realtimeNotifier = realtimeNotifier;
    }
}
