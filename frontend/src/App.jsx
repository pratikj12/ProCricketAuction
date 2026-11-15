import AppRoutes from './routes/AppRoutes.jsx';
import { AuthProvider, useAuth } from './context/AuthContext.jsx';
import { AuctionDataProvider } from './context/AuctionDataContext.jsx';

export default function App() {
  return (
    <AuthProvider>
      <AuthenticatedApp />
    </AuthProvider>
  );
}

function AuthenticatedApp() {
  const { user } = useAuth();
  return (
    <AuctionDataProvider key={user?.id ?? 'signed-out'} storageScope={user?.id ?? 'signed-out'}>
      <AppRoutes />
    </AuctionDataProvider>
  );
}
