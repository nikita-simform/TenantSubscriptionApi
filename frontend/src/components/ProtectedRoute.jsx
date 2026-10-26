import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { Navbar } from "./Navbar";

// Mirrors the backend's [Authorize(Roles = ...)] — but this is a UI convenience
// ONLY. A user could edit this check out of the bundle entirely and it would
// change nothing: every API call still carries the same JWT, and the server
// re-checks the role itself on every request (see OrgUsersController,
// ResourcesController, PlatformController). This just avoids showing a page
// whose every API call would 403 anyway.
export function ProtectedRoute({ roles }) {
  const { user } = useAuth();

  if (!user) return <Navigate to="/login" replace />;
  if (roles && !roles.includes(user.role)) return <Navigate to="/" replace />;

  return (
    <>
      <Navbar />
      <Outlet />
    </>
  );
}
