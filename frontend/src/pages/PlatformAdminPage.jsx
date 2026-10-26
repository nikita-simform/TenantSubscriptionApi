import { useEffect, useState } from "react";
import { api } from "../api/client";

export function PlatformAdminPage() {
  const [result, setResult] = useState(null);
  const [page, setPage] = useState(1);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const pageSize = 10;

  async function load(targetPage) {
    setLoading(true);
    try {
      setResult(await api.listPlatformOrgs(targetPage, pageSize));
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(page); }, [page]);

  const totalPages = result ? Math.max(1, Math.ceil(result.totalCount / pageSize)) : 1;

  return (
    <div className="page">
      <h1>Platform Admin</h1>
      <p className="page-subtitle">
        Plan and usage metadata only — notice there is no field here that could ever expose an
        organization's actual content. This page's own service (PlatformAdminService) doesn't
        even have a dependency capable of reaching tenant data.
      </p>

      {error && <div className="error-banner">{error}</div>}

      <div className="card">
        <h2>Organizations ({result?.totalCount ?? "…"})</h2>
        {loading ? (
          <p className="empty-state">Loading…</p>
        ) : (
          <>
            <table>
              <thead>
                <tr><th>Name</th><th>Plan</th><th>Seats</th><th>Storage</th></tr>
              </thead>
              <tbody>
                {result.items.map((org) => {
                  const seatPct = Math.round((org.userCount / org.maxUsers) * 100);
                  const nearLimit = seatPct >= 80;
                  return (
                    <tr key={org.id}>
                      <td>{org.name}</td>
                      <td>{org.planName}</td>
                      <td>
                        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                          <div className="progress-bar">
                            <div
                              className={`progress-bar-fill${nearLimit ? " danger" : ""}`}
                              style={{ width: `${seatPct}%` }}
                            />
                          </div>
                          <span>{org.userCount}/{org.maxUsers}</span>
                        </div>
                      </td>
                      <td>{org.storageUsedMB}/{org.maxStorageMB} MB</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            <div className="form-actions" style={{ justifyContent: "center", marginTop: 16 }}>
              <button className="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                Previous
              </button>
              <span style={{ alignSelf: "center", fontSize: 13, color: "var(--text-muted)" }}>
                Page {page} of {totalPages}
              </span>
              <button className="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                Next
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
