import { NavLink, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export function Navbar() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate("/login");
  }

  return (
    <nav className="navbar">
      <div className="navbar-brand">🏢 TenantSubscriptionApi</div>
      <div className="navbar-links">
        {(user.role === "OrgAdmin" || user.role === "OrgMember") && (
          <NavLink to="/resources" className={({ isActive }) => (isActive ? "active" : "")}>
            Resources
          </NavLink>
        )}
        {user.role === "OrgAdmin" && (
          <NavLink to="/members" className={({ isActive }) => (isActive ? "active" : "")}>
            Org Members
          </NavLink>
        )}
        {user.role === "PlatformAdmin" && (
          <NavLink to="/platform" className={({ isActive }) => (isActive ? "active" : "")}>
            Platform Admin
          </NavLink>
        )}
      </div>
      <div className="navbar-user">
        <span className={`badge badge-${user.role}`}>{user.role}</span>
        <button className="secondary" onClick={handleLogout}>Log out</button>
      </div>
    </nav>
  );
}
