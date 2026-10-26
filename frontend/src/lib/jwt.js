// Decodes a JWT's payload CLIENT-SIDE, purely so the UI knows which role/org to
// render for — this is NOT a security check. The signature is never verified here;
// the backend re-validates the token on every single request regardless of what
// this function returns, so nothing the UI decides based on this ever grants
// real access.
export function decodeJwtPayload(token) {
  const payload = token.split(".")[1];
  const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=");
  const json = atob(padded);
  return JSON.parse(json);
}

const ROLE_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

export function userFromToken(token) {
  const claims = decodeJwtPayload(token);
  return {
    userId: claims.sub,
    role: claims[ROLE_CLAIM],
    organizationId: claims.organizationId ?? null,
    schemaName: claims.schemaName ?? null,
    expiresAt: claims.exp * 1000
  };
}
