import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext.jsx';
import AdminLayout from '../layouts/AdminLayout.jsx';
import AuctionLayout from '../layouts/AuctionLayout.jsx';
import Register from '../pages/Register.jsx';
import Settings from '../pages/Settings.jsx';
import AuctionSetup from '../pages/AuctionSetup.jsx';
import CaptainBidding from '../pages/CaptainBidding.jsx';
import Dashboard from '../pages/Dashboard.jsx';
import Login from '../pages/Login.jsx';
import LiveAuction from '../pages/LiveAuction.jsx';
import Players from '../pages/Players.jsx';
import Reports from '../pages/Reports.jsx';
import Results from '../pages/Results.jsx';
import Help from '../pages/Help.jsx';
import Teams from '../pages/Teams.jsx';
import Tournaments from '../pages/Tournaments.jsx';
import AuctionDisplay from '../pages/AuctionDisplay.jsx';
import About from '../pages/About.jsx';
import AuctionEvents from '../pages/AuctionEvents.jsx';
import Members from '../pages/Members.jsx';

export default function AppRoutes() {
  const { user, loading, initializationError } = useAuth();
  const location = useLocation();
  const isPublicAuctionRoute = /^\/auction\/watch\/[^/]+\/?$/.test(location.pathname);
  if (loading && !isPublicAuctionRoute) return <div className="auth-loading"><span className="loading-spinner" /> Connecting to your secure account…</div>;
  return (
    <Routes>
      <Route path="/auction/watch/:auctionId" element={<AuctionDisplay publicView />} />
      <Route path="/login" element={user ? <Navigate to="/dashboard" replace /> : <Login serverError={initializationError} />} />
      <Route path="/register" element={user ? <Navigate to="/dashboard" replace /> : <Register serverError={initializationError} />} />
      <Route path="/auction/display/:auctionId" element={user ? <AuctionLayout><AuctionDisplay /></AuctionLayout> : <Navigate to="/login" replace />} />
      <Route path="/auction/bid/:auctionId" element={user ? <AuctionLayout><CaptainBidding /></AuctionLayout> : <Navigate to="/login" replace />} />
      <Route path="/" element={user ? <AdminLayout /> : <Navigate to="/login" replace />}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<Dashboard />} />
        <Route path="tournaments" element={<Tournaments />} />
        <Route path="players" element={<Players />} />
        <Route path="teams" element={<Teams />} />
        <Route path="auction/events" element={<AuctionEvents />} />
        <Route path="members" element={<Members />} />
        <Route path="auction/setup" element={<AuctionSetup />} />
        <Route path="auction/live/:auctionId" element={<LiveAuction />} />
        <Route path="auction/results/:auctionId" element={<Results />} />
        <Route path="reports" element={<Reports />} />
        <Route path="settings" element={<Settings />} />
        <Route path="about" element={<About />} />
        <Route path="help" element={<Help />} />
      </Route>
      <Route path="*" element={<Navigate to={user ? '/dashboard' : '/login'} replace />} />
    </Routes>
  );
}
