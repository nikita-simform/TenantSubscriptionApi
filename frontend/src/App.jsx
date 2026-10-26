import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { AuthProvider, useAuth } from "./context/AuthContext";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { SignupPage } from "./pages/SignupPage";
import { LoginPage } from "./pages/LoginPage";
import { ResourcesPage } from "./pages/ResourcesPage";
import { OrgMembersPage } from "./pages/OrgMembersPage";
import { PlatformAdminPage } from "./pages/PlatformAdminPage";

function HomeRedirect() {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  if (user.role === "PlatformAdmin") return <Navigate to="/platform" replace />;
  return <Navigate to="/resources" replace />;
}

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/signup" element={<SignupPage />} />
          <Route path="/login" element={<LoginPage />} />

          <Route path="/" element={<HomeRedirect />} />

          <Route element={<ProtectedRoute roles={["OrgAdmin", "OrgMember"]} />}>
            <Route path="/resources" element={<ResourcesPage />} />
          </Route>

          <Route element={<ProtectedRoute roles={["OrgAdmin"]} />}>
            <Route path="/members" element={<OrgMembersPage />} />
          </Route>

          <Route element={<ProtectedRoute roles={["PlatformAdmin"]} />}>
            <Route path="/platform" element={<PlatformAdminPage />} />
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}

export default App;
