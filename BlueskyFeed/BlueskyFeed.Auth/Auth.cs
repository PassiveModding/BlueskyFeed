using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BlueskyFeed.Auth;

public static class Extensions
{
    public static async Task<string> VerifyJwt(this DidResolver didResolver, string jwtStr, string? audienceDid)
    {
        var jwt = new JwtSecurityToken(jwtStr);

        if (jwt.ValidTo < DateTime.UtcNow)
        {
            throw new ArgumentException("JWT expired", nameof(jwtStr));
        }

        // check if audience matches
        if (audienceDid != null && jwt.Audiences.FirstOrDefault() != audienceDid)
        {
            throw new ArgumentException("JWT audience mismatch", nameof(jwtStr));
        }

        // GetSigningKey
        var issuer = jwt.Issuer;
        var signingKey = await didResolver.ResolveAtProtoKey(issuer);

        var msg = jwt.RawHeader + "." + jwt.RawPayload;
        var msgBytes = Encoding.UTF8.GetBytes(msg);

        var sig = jwt.RawSignature;
        var sigBytes = Base64UrlEncoder.DecodeBytes(sig);

        // verify signature
        var result = Crypto.VerifySignature(signingKey, msgBytes, sigBytes);
        if (!result)
        {
            throw new ArgumentException("JWT signature invalid", nameof(jwtStr));
        }

        return jwt.Issuer;
    }
}