import { useEffect, useState } from 'react';
import { KeyRound, LogOut, ShieldCheck, UserRound } from 'lucide-react';
import toast from 'react-hot-toast';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../context/AuthContext.jsx';
import { formatMoney, useAuctionData } from '../context/AuctionDataContext.jsx';
import { currencyOptions } from '../utils/currencyOptions.js';

function getApiError(error, fallback) {
  const errors = error.response?.data?.errors;
  const message = errors && Object.values(errors).flat().find(Boolean);
  return message ?? error.response?.data?.detail ?? fallback;
}

export default function Settings() {
  const { tournament, teams, players, dispatch, workspaceLoading } = useAuctionData();
  const { user, logout, updateProfile, changePassword } = useAuth();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [displayName, setDisplayName] = useState(user?.displayName ?? '');
  const [savingProfile, setSavingProfile] = useState(false);
  const [savingPassword, setSavingPassword] = useState(false);
  const [passwords, setPasswords] = useState({ currentPassword: '', newPassword: '', confirmPassword: '' });
  const [leagueSettings, setLeagueSettings] = useState({
    name: tournament.name,
    season: tournament.season,
    venue: tournament.venue,
    currencyCode: tournament.currencyCode ?? 'INR',
    defaultTeamPurse: String(tournament.defaultTeamPurse ?? 100000),
    maximumTeams: tournament.maximumTeams == null ? '' : String(tournament.maximumTeams),
    maximumPlayers: tournament.maximumPlayers == null ? '' : String(tournament.maximumPlayers)
  });
  const [savingLeague, setSavingLeague] = useState(false);

  useEffect(() => {
    setLeagueSettings({
      name: tournament.name,
      season: tournament.season,
      venue: tournament.venue,
      currencyCode: tournament.currencyCode ?? 'INR',
      defaultTeamPurse: String(tournament.defaultTeamPurse ?? 100000),
      maximumTeams: tournament.maximumTeams == null ? '' : String(tournament.maximumTeams),
      maximumPlayers: tournament.maximumPlayers == null ? '' : String(tournament.maximumPlayers)
    });
  }, [tournament]);

  async function saveLeagueSettings(event) {
    event.preventDefault();
    const defaultTeamPurse = Number(leagueSettings.defaultTeamPurse);
    const maximumTeams = leagueSettings.maximumTeams ? Number(leagueSettings.maximumTeams) : null;
    const maximumPlayers = leagueSettings.maximumPlayers ? Number(leagueSettings.maximumPlayers) : null;
    if (!Number.isSafeInteger(defaultTeamPurse) || defaultTeamPurse < 1
      || maximumTeams !== null && (!Number.isInteger(maximumTeams) || maximumTeams < 1 || maximumTeams > 100)
      || maximumPlayers !== null && (!Number.isInteger(maximumPlayers) || maximumPlayers < 1 || maximumPlayers > 10000)) {
      toast.error('Check the purse and optional team/player limits.');
      return;
    }
    setSavingLeague(true);
    try {
      await dispatch({
        type: 'update-league-settings',
        tournament: { ...leagueSettings, defaultTeamPurse, maximumTeams, maximumPlayers }
      });
      toast.success('League settings saved.');
    } catch (error) {
      toast.error(getApiError(error, 'Could not save league settings.'));
    } finally {
      setSavingLeague(false);
    }
  }

  async function saveProfile(event) {
    event.preventDefault();
    setSavingProfile(true);
    try {
      await updateProfile({ displayName });
      toast.success('Profile updated.');
    } catch (error) {
      toast.error(getApiError(error, 'Could not update your profile.'));
    } finally {
      setSavingProfile(false);
    }
  }

  async function savePassword(event) {
    event.preventDefault();
    if (passwords.newPassword !== passwords.confirmPassword) {
      toast.error('The new password and confirmation do not match.');
      return;
    }
    setSavingPassword(true);
    try {
      await changePassword({ currentPassword: passwords.currentPassword, newPassword: passwords.newPassword });
      setPasswords({ currentPassword: '', newPassword: '', confirmPassword: '' });
      toast.success('Password changed successfully.');
    } catch (error) {
      toast.error(getApiError(error, 'Could not change your password.'));
    } finally {
      setSavingPassword(false);
    }
  }

  async function signOut() {
    try {
      await logout();
      toast.success('You’ve been signed out.');
      navigate('/login', { replace: true });
    } catch {
      toast.error('Could not sign out. Please check your connection and try again.');
    }
  }

  const section = searchParams.get('section');
  useEffect(() => {
    if (section === 'profile' || section === 'password') {
      document.getElementById(section)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  }, [section]);

  return (
    <main>
      <div className="page-title-row"><div><div className="eyebrow"><span className="eyebrow-line" />YOUR ACCOUNT</div><h1>Account settings</h1><p className="subheading">Manage your organizer profile and secure your account.</p></div></div>
      <section className="settings-card"><div className="settings-card-icon"><ShieldCheck size={20} /></div><div><span>ACCOUNT ROLE</span><h2>{user?.roles?.join(', ') || 'Organizer'}</h2><p>Administrator permissions cannot be self-assigned.</p></div></section>
      <section className="settings-card settings-form-card league-settings-card">
        <div className="settings-card-icon"><ShieldCheck size={20} /></div>
        <form onSubmit={saveLeagueSettings}>
          <span>LEAGUE SETTINGS</span><h2>{tournament.name || 'No league selected'}</h2>
          <p>Configure the purse, currency, and optional registration limits for the selected league.</p>
          <label>League name<input required minLength="2" maxLength="160" disabled={!tournament.id} value={leagueSettings.name} onChange={(event) => setLeagueSettings({ ...leagueSettings, name: event.target.value })} /></label>
          <div className="form-row"><label>Default season<input maxLength="40" disabled={!tournament.id} value={leagueSettings.season} onChange={(event) => setLeagueSettings({ ...leagueSettings, season: event.target.value })} /></label><label>League venue<input maxLength="160" disabled={!tournament.id} value={leagueSettings.venue} onChange={(event) => setLeagueSettings({ ...leagueSettings, venue: event.target.value })} /></label></div>
          <div className="form-row"><label>Currency<select disabled={!tournament.id} value={leagueSettings.currencyCode} onChange={(event) => setLeagueSettings({ ...leagueSettings, currencyCode: event.target.value })}>{currencyOptions.map(({ code, label }) => <option key={code} value={code}>{label}</option>)}</select></label><label>Starting team purse<input required type="number" min="1" step="1" disabled={!tournament.id} value={leagueSettings.defaultTeamPurse} onChange={(event) => setLeagueSettings({ ...leagueSettings, defaultTeamPurse: event.target.value })} /></label></div>
          <div className="form-row"><label>Maximum teams per season (optional)<input type="number" min="1" max="100" step="1" disabled={!tournament.id} value={leagueSettings.maximumTeams} onChange={(event) => setLeagueSettings({ ...leagueSettings, maximumTeams: event.target.value })} placeholder="No limit" /><small>{teams.length} teams currently in the selected season.</small></label><label>Maximum players per season (optional)<input type="number" min="1" max="10000" step="1" disabled={!tournament.id} value={leagueSettings.maximumPlayers} onChange={(event) => setLeagueSettings({ ...leagueSettings, maximumPlayers: event.target.value })} placeholder="No limit" /><small>{players.length} players currently in the selected season.</small></label></div>
          <p>Season dates are managed from the Seasons page. Event times use your browser’s local timezone and are stored with their offset.</p>
          <button className="button button-primary" type="submit" disabled={!tournament.id || savingLeague || workspaceLoading}>{savingLeague ? 'Saving…' : `Save league settings · ${formatMoney(Number(leagueSettings.defaultTeamPurse) || 0, leagueSettings.currencyCode)}`}</button>
        </form>
      </section>
      <section className={`settings-card settings-form-card${section === 'profile' ? ' settings-highlight' : ''}`} id="profile">
        <div className="settings-card-icon"><UserRound size={20} /></div>
        <form onSubmit={saveProfile}><span>PROFILE</span><h2>{user?.email}</h2><label>Display name<input required minLength="2" maxLength="100" value={displayName} onChange={(event) => setDisplayName(event.target.value)} /></label><button className="button button-primary" type="submit" disabled={savingProfile}>{savingProfile ? 'Saving…' : 'Save profile'}</button></form>
      </section>
      <section className={`settings-card settings-form-card${section === 'password' ? ' settings-highlight' : ''}`} id="password">
        <div className="settings-card-icon"><KeyRound size={20} /></div>
        <form onSubmit={savePassword}><span>CHANGE PASSWORD</span><h2>Keep your account secure</h2>
          <label>Current password<input required type="password" autoComplete="current-password" value={passwords.currentPassword} onChange={(event) => setPasswords({ ...passwords, currentPassword: event.target.value })} /></label>
          <label>New password<input required type="password" minLength="12" autoComplete="new-password" value={passwords.newPassword} onChange={(event) => setPasswords({ ...passwords, newPassword: event.target.value })} /></label>
          <label>Confirm new password<input required type="password" minLength="12" autoComplete="new-password" value={passwords.confirmPassword} onChange={(event) => setPasswords({ ...passwords, confirmPassword: event.target.value })} /></label>
          <p>Your new password must contain uppercase and lowercase letters, a number, and a symbol.</p>
          <button className="button button-primary" type="submit" disabled={savingPassword}>{savingPassword ? 'Updating…' : 'Change password'}</button>
        </form>
      </section>
      <section className="settings-card"><div className="settings-card-icon"><ShieldCheck size={20} /></div><div><span>SESSION SECURITY</span><h2>Secure browser session</h2><p>Your password is stored as a one-way hash on the server, never in this browser.</p></div><button className="button button-ghost" onClick={signOut}><LogOut size={15} /> Sign out</button></section>
    </main>
  );
}
