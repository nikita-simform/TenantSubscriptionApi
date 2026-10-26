import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

export function SignupPage() {
  const { signup } = useAuth();
  const navigate = useNavigate();
  const [organizationName, setOrganizationName] = useState("");
  const [adminEmail, setAdminEmail] = useState("");
  const [adminPassword, setAdminPassword] = useState("");
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await signup(organizationName, adminEmail, adminPassword);
      navigate("/");
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="auth-page">
      <h1>Create your organization</h1>
      <p className="page-subtitle">Self-service onboarding — your own isolated schema is provisioned instantly.</p>
      {error && <div className="error-banner">{error}</div>}
      <form onSubmit={handleSubmit}>
        <div className="form-row">
          <label>Organization name</label>
          <input value={organizationName} onChange={(e) => setOrganizationName(e.target.value)} required />
        </div>
        <div className="form-row">
          <label>Admin email</label>
          <input type="email" value={adminEmail} onChange={(e) => setAdminEmail(e.target.value)} required />
        </div>
        <div className="form-row">
          <label>Admin password</label>
          <input
            type="password"
            value={adminPassword}
            onChange={(e) => setAdminPassword(e.target.value)}
            minLength={8}
            required
          />
        </div>
        <div className="form-actions">
          <button type="submit" disabled={loading}>{loading ? "Creating…" : "Create organization"}</button>
        </div>
      </form>
      <p className="hint">Already have an account? <Link to="/login">Log in</Link></p>
    </div>
  );
}
