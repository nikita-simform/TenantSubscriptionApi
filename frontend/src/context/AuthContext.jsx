import { createContext, useContext, useState, useCallback } from "react";
import { api } from "../api/client";
import { getToken, setToken, clearToken } from "../api/client";
import { userFromToken } from "../lib/jwt";

const AuthContext = createContext(null);

function loadUserFromStorage() {
  const token = getToken();
  if (!token) return null;
  try {
    const user = userFromToken(token);
    if (user.expiresAt < Date.now()) {
      clearToken();
      return null;
    }
    return user;
  } catch {
    clearToken();
    return null;
  }
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(loadUserFromStorage);

  const login = useCallback(async (email, password) => {
    const { token } = await api.login({ email, password });
    setToken(token);
    setUser(userFromToken(token));
  }, []);

  const signup = useCallback(async (organizationName, adminEmail, adminPassword) => {
    await api.signup({ organizationName, adminEmail, adminPassword });
    // Signup doesn't return a token (matches the real POST /api/signup contract) —
    // log in right after so the demo flows straight from signup into the app.
    await login(adminEmail, adminPassword);
  }, [login]);

  const logout = useCallback(() => {
    clearToken();
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, login, signup, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
