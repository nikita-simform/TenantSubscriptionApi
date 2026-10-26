const BASE_URL = import.meta.env.VITE_API_BASE_URL;
const TOKEN_KEY = "tenant_subscription_token";

export function getToken() {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token) {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken() {
  localStorage.removeItem(TOKEN_KEY);
}

// Thin fetch wrapper: attaches the JWT, parses JSON, and turns a non-2xx response
// into a thrown Error whose message is the API's own ProblemDetails "detail" (the
// same message AppExceptionHandler puts there server-side) so the UI can just
// display err.message directly.
async function request(path, { method = "GET", body, auth = true } = {}) {
  const headers = { "Content-Type": "application/json" };
  if (auth) {
    const token = getToken();
    if (token) headers["Authorization"] = `Bearer ${token}`;
  }

  const response = await fetch(`${BASE_URL}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body)
  });

  if (response.status === 204) return null;

  const text = await response.text();
  const data = text ? JSON.parse(text) : null;

  if (!response.ok) {
    const message = data?.detail || data?.title || `Request failed (${response.status})`;
    const error = new Error(message);
    error.status = response.status;
    throw error;
  }

  return data;
}

export const api = {
  signup: (body) => request("/api/signup", { method: "POST", body, auth: false }),
  login: (body) => request("/api/auth/login", { method: "POST", body, auth: false }),

  listResources: () => request("/api/resources"),
  createResource: (body) => request("/api/resources", { method: "POST", body }),

  listOrgMembers: (orgId) => request(`/api/orgs/${orgId}/users`),
  inviteOrgMember: (orgId, body) => request(`/api/orgs/${orgId}/users`, { method: "POST", body }),
  removeOrgMember: (orgId, userId) => request(`/api/orgs/${orgId}/users/${userId}`, { method: "DELETE" }),
  changeOrgMemberRole: (orgId, userId, body) =>
    request(`/api/orgs/${orgId}/users/${userId}/role`, { method: "PUT", body }),

  listPlatformOrgs: (page = 1, pageSize = 20) =>
    request(`/api/platform/orgs?page=${page}&pageSize=${pageSize}`)
};
