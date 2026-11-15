import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import authService from '../services/authService.js';

export const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [initializationError, setInitializationError] = useState(null);

  useEffect(() => {
    let active = true;
    async function loadSession() {
      try {
        await authService.initialize();
        const account = await authService.currentUser();
        if (active) setUser(account);
      } catch (error) {
        if (active && error.response?.status !== 401) {
          setInitializationError('Could not connect to the account server. Make sure the backend is running, then refresh.');
        }
      } finally {
        if (active) setLoading(false);
      }
    }
    loadSession();
    return () => { active = false; };
  }, []);

  async function login(credentials) {
    const account = await authService.login(credentials);
    await authService.initialize();
    setUser(account);
    setInitializationError(null);
    return account;
  }

  async function register(account) {
    const userAccount = await authService.register(account);
    await authService.initialize();
    setUser(userAccount);
    setInitializationError(null);
    return userAccount;
  }

  async function logout() {
    await authService.logout();
    setUser(null);
    try {
      await authService.initialize();
    } catch (error) {
      setInitializationError('You are signed out, but the security token could not be refreshed. Refresh the page before signing in again.');
      throw error;
    }
  }

  async function updateProfile(profile) {
    const updatedAccount = await authService.updateProfile(profile);
    setUser(updatedAccount);
    return updatedAccount;
  }

  async function changePassword(passwords) {
    const updatedAccount = await authService.changePassword(passwords);
    setUser(updatedAccount);
    return updatedAccount;
  }

  const value = useMemo(() => ({
    user,
    loading,
    initializationError,
    login,
    register,
    logout,
    updateProfile,
    changePassword
  }), [user, loading, initializationError]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error('useAuth must be used within AuthProvider');
  return context;
}
