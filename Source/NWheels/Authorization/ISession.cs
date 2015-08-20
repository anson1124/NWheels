using System;
using System.Security.Principal;
using NWheels.Endpoints.Core;

namespace NWheels.Authorization
{
    public interface ISession
    {
        string Id { get; }
        IPrincipal UserPrincipal { get; }
        IIdentityInfo UserIdentity { get; }
        IEndpoint OriginatorEndpoint { get; }
        DateTime OpenedAtUtc { get; }
        DateTime? ExpiresAtUtc { get; }
    }
}