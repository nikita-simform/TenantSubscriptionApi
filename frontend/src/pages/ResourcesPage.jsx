import { useEffect, useState } from "react";
import { api } from "../api/client";

export function ResourcesPage() {
  const [resources, setResources] = useState([]);
  const [name, setName] = useState("");
  const [content, setContent] = useState("");
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  async function loadResources() {
    setLoading(true);
    try {
      setResources(await api.listResources());
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { loadResources(); }, []);

  async function handleCreate(e) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await api.createResource({ name, content });
      setName("");
      setContent("");
      await loadResources();
    } catch (err) {
      setError(err.message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="page">
      <h1>Resources</h1>
      <p className="page-subtitle">
        Scoped entirely to your own organization's Postgres schema — log in as a different
        organization's user and this list will never contain what you see here.
      </p>

      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>New resource</h2>
        <form onSubmit={handleCreate}>
          <div className="form-row">
            <label>Name</label>
            <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
          </div>
          <div className="form-row">
            <label>Content</label>
            <input value={content} onChange={(e) => setContent(e.target.value)} required />
          </div>
          <div className="form-actions">
            <button type="submit" disabled={submitting}>{submitting ? "Creating…" : "Create"}</button>
          </div>
        </form>
      </div>

      <div className="card">
        <h2>Your organization's resources</h2>
        {loading ? (
          <p className="empty-state">Loading…</p>
        ) : resources.length === 0 ? (
          <p className="empty-state">No resources yet — create one above.</p>
        ) : (
          <table>
            <thead>
              <tr><th>Name</th><th>Content</th><th>Size</th><th>Created</th></tr>
            </thead>
            <tbody>
              {resources.map((r) => (
                <tr key={r.id}>
                  <td>{r.name}</td>
                  <td>{r.content}</td>
                  <td>{r.sizeBytes} B</td>
                  <td>{new Date(r.createdAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
