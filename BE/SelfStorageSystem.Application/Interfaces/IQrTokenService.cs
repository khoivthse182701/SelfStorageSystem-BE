namespace SelfStorageSystem.Application.Interfaces;

public sealed record QrTokenPayload(
    string Typ,
    long? RsvId,
    long? AgrId,
    long CustId,
    long FacId,
    long Iat,
    long Exp);

public interface IQrTokenService
{
    string IssueToken(string typ, long? rsvId, long? agrId, long customerId, long facilityId, TimeSpan? ttl = null);
    bool TryVerifyToken(string? token, string expectedType, out QrTokenPayload? payload);
}
