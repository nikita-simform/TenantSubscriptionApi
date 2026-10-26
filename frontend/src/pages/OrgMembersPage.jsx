import { useEffect, useState } from "react";
import { api } from "../api/client";
import { useAuth } from "../context/AuthContext";

export function OrgMembersPage() {
  const { user } = useAuth();
  const [members, setMembers] = useState([]);
  const [email, setEmail] = useState("");
  const [role, setRole] = useState("OrgMember");
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  async function loadMembers() {
    setLoading(true);
    try {
      setMembers(await api.listOrgMembers(user.organizationId));
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { loadMembers(); }, []);

  async function handleInvite(e) {
    e.preventDefault();
    setError(null);
    setNotice(null);
    setSubmitting(true);
    try {
      const result = await api.inviteOrgMember(user.organizationId, { email, role });
      setNotice(`Invited ${email} — temporary password: ${result.temporaryPassword}`);
      setEmail("");
      await loadMembers();
    } catch (err) {
      setError(err.message);
    } finally {
      setSubmitting(false);
    }
  }

  async function handleRemove(memberId) {
    setError(null);
    try {
      await api.removeOrgMember(user.organizationId, memberId);
      await loadMembers();
    } catch (err) {
      setError(err.message);
    }
  }

  async function handleRoleChange(memberId, newRole) {
    setError(null);
    try {
      await api.changeOrgMemberRole(user.organizationId, memberId, { role: newRole });
      await loadMembers();
    } catch (err) {
      setError(err.message);
    }
  }

  return (
    <div className="page">
      <h1>Org Members</h1>
      <p className="page-subtitle">
        Inviting past your plan's seat limit is rejected atomically (409) — try it after filling
        every seat. Removing/editing another organization's user by ID is structurally impossible
        here (the backend scopes every lookup to your own JWT, never the URL).
      </p>

      {error && <div className="error-banner">{error}</div>}
      {notice && <div className="card" style={{ background: "#ecfdf5", borderColor: "#a7f3d0" }}>{notice}</div>}

      <div className="card">
        <h2>Invite a member</h2>
        <form onSubmit={handleInvite}>
          <div className="form-row">
            <label>Email</label>
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </div>
          <div className="form-row">
            <label>Role</label>
            <select value={role} onChange={(e) => setRole(e.target.value)}>
              <option value="OrgMember">OrgMember</option>
              <option value="OrgAdmin">OrgAdmin</option>
            </select>
          </div>
          <div className="form-actions">
            <button type="submit" disabled={submitting}>{submitting ? "Inviting…" : "Invite"}</button>
          </div>
        </form>
      </div>

      <div className="card">
        <h2>Members</h2>
        {loading ? (
          <p className="empty-state">Loading…</p>
        ) : members.length === 0 ? (
          <p className="empty-state">No members yet.</p>
        ) : (
          <table>
            <thead>
              <tr><th>Email</th><th>Role</th><th>Joined</th><th></th></tr>
            </thead>
            <tbody>
              {members.map((m) => (
                <tr key={m.id}>
                  <td>{m.email}</td>
                  <td><span className={`badge badge-${m.role}`}>{m.role}</span></td>
                  <td>{new Date(m.createdAt).toLocaleDateString()}</td>
                  <td>
                    <div className="row-actions">
                      <button
                        className="secondary"
                        onClick={() => handleRoleChange(m.id, m.role === "OrgAdmin" ? "OrgMember" : "OrgAdmin")}
                      >
                        Make {m.role === "OrgAdmin" ? "Member" : "Admin"}
                      </button>
                      <button className="danger" onClick={() => handleRemove(m.id)}>Remove</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
