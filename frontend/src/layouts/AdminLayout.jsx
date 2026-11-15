import { useEffect, useRef, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Activity, Bell, CalendarDays, ChevronDown, CircleHelp, Command, FileBarChart2, Info, KeyRound, LayoutDashboard, LogOut, Radio, Settings as SettingsIcon, Trophy, UserRound, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import { useAuctionData } from '../context/AuctionDataContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { Link } from 'react-router-dom';
import DeleteTournamentAction from '../components/common/DeleteTournamentAction.jsx';

export default function AdminLayout() {
  const { tournament, bids, players, teams, seasons, selectedSeasonId, selectSeason } = useAuctionData();
  const { tournaments, selectTournament, workspaceLoading, workspaceError } = useAuctionData();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [workspaceMenu, setWorkspaceMenu] = useState(false);
  const workspaceRef = useRef(null);
  const location = useLocation();
  const [notificationMenu, setNotificationMenu] = useState(false);
  const notificationRef = useRef(null);
  const [profileMenu, setProfileMenu] = useState(false);
  const profileRef = useRef(null);
  const links = [
    { to: '/dashboard', label: 'Overview', icon: LayoutDashboard },
    { to: '/tournaments', label: 'Seasons', icon: CalendarDays },
    { to: '/players', label: 'Players', icon: UserRound },
    { to: '/teams', label: 'Teams', icon: Users },
    { to: '/auction/events', label: 'Auction events', icon: CalendarDays },
    { to: '/members', label: 'Members', icon: Users },
    { to: '/auction/setup', label: 'Auction room', icon: Radio },
    { to: '/reports', label: 'Reports', icon: FileBarChart2 }
  ];
  const pageLabel = links.find(({ to }) => location.pathname === to || location.pathname.startsWith(`${to}/`))?.label
    ?? (location.pathname.startsWith('/auction/display/') ? 'Projector display'
      : location.pathname === '/settings' ? 'Settings'
        : location.pathname === '/help' ? 'Help & support'
          : location.pathname === '/about' ? 'About'
            : 'Workspace');
  const notifications = [
    ...bids.slice(0, 3).map((bid) => ({
      id: bid.id,
      title: `${teams.find((team) => team.id === bid.teamId)?.name ?? 'A team'} placed a bid`,
      description: `${players.find((player) => player.id === bid.playerId)?.name ?? 'A player'} · ${new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(bid.amount)}`
    })),
    ...players.filter((player) => player.status === 'Sold').slice(-2).map((player) => ({
      id: `sold-${player.id}`,
      title: `${player.name} was sold`,
      description: `${teams.find((team) => team.id === player.teamId)?.name ?? 'Winning team'} · ${new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(player.soldPrice)}`
    }))
  ].slice(0, 4);

  useEffect(() => {
    if (!workspaceMenu) return undefined;
    function closeOnOutsideClick(event) {
      if (!workspaceRef.current?.contains(event.target)) setWorkspaceMenu(false);
    }
    function closeOnEscape(event) {
      if (event.key === 'Escape') setWorkspaceMenu(false);
    }
    document.addEventListener('pointerdown', closeOnOutsideClick);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsideClick);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [workspaceMenu]);

  useEffect(() => {
    setWorkspaceMenu(false);
    setNotificationMenu(false);
    setProfileMenu(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!notificationMenu) return undefined;
    function closeOnOutsideClick(event) {
      if (!notificationRef.current?.contains(event.target)) setNotificationMenu(false);
    }
    function closeOnEscape(event) {
      if (event.key === 'Escape') setNotificationMenu(false);
    }
    document.addEventListener('pointerdown', closeOnOutsideClick);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsideClick);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [notificationMenu]);

  useEffect(() => {
    if (!profileMenu) return undefined;
    function closeOnOutsideClick(event) {
      if (!profileRef.current?.contains(event.target)) setProfileMenu(false);
    }
    function closeOnEscape(event) {
      if (event.key === 'Escape') setProfileMenu(false);
    }
    document.addEventListener('pointerdown', closeOnOutsideClick);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsideClick);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [profileMenu]);

  async function signOut() {
    try {
      await logout();
      toast.success('You’ve been signed out.');
      navigate('/login', { replace: true });
    } catch {
      toast.error('Could not sign out. Check your connection and try again.');
    }
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <NavLink to="/dashboard" className="brand">
          <span className="brand-mark"><Command size={21} /></span>
          <span>ProCricket<span className="brand-dot">.</span><small>AUCTION MANAGER</small></span>
        </NavLink>
        <div className="workspace-label">WORKSPACE</div>
        <div className="workspace-switch-wrap" ref={workspaceRef}>
          <button className="workspace-switcher" aria-expanded={workspaceMenu} onClick={() => setWorkspaceMenu(!workspaceMenu)}>
            <span className="workspace-avatar">{(user?.displayName ?? 'SP').slice(0, 2).toUpperCase()}</span>
            <span><b>{tournament.name || 'No league selected'}</b><small>{user?.displayName ?? 'Organizer'}</small></span>
            <span className="chevron">⌄</span>
          </button>
          {workspaceMenu && <div className="workspace-menu"><span>YOUR TOURNAMENTS</span>{tournaments.map((item) => <button type="button" className="workspace-option" key={item.id} onClick={() => { selectTournament(item.id); setWorkspaceMenu(false); }}><Trophy size={15} /><span><b>{item.name}</b><small>{item.season ? `Season ${item.season}` : 'Tournament workspace'}</small></span>{item.id === tournament.id && <i>✓</i>}</button>)}{!tournaments.length && <p className="workspace-empty">No tournaments yet. Create one to get started.</p>}<Link to="/tournaments" onClick={() => setWorkspaceMenu(false)}><span className="workspace-add">+</span><span><b>Manage tournaments</b><small>Create a tournament</small></span></Link><DeleteTournamentAction /></div>}
        </div>
        {tournament.id && seasons.length > 0 && <label className="season-switcher"><CalendarDays size={15} /><span>SEASON</span><select aria-label="Select season" value={selectedSeasonId ?? ''} onChange={(event) => selectSeason(event.target.value)}>{seasons.map((season) => <option key={season.id} value={season.id}>{season.name}</option>)}</select></label>}
        <div className="nav-label">MENU</div>
        <nav className="side-nav">
          {links.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to} onClick={() => setWorkspaceMenu(false)} className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`}>
              <Icon size={18} strokeWidth={1.8} /><span>{label}</span>
              {label === 'Auction room' && <span className="live-pip" />}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <NavLink to="/auction/events" onClick={() => setWorkspaceMenu(false)} className="nav-link"><Radio size={18} /><span>Auction rooms</span></NavLink>
          <NavLink to="/settings" onClick={() => setWorkspaceMenu(false)} className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`}><SettingsIcon size={18} /><span>Settings</span></NavLink>
          <Link to="/help" onClick={() => setWorkspaceMenu(false)} className="sidebar-help"><CircleHelp size={17} /><span>Need a hand?<small>Visit our help center</small></span></Link>
          <button className="user-card" onClick={signOut} title="Click to sign out" aria-label="Sign out"><div className="user-avatar">{(user?.displayName ?? 'OR').split(' ').map((part) => part[0]).slice(0, 2).join('').toUpperCase()}</div><span><b>{user?.displayName ?? 'Organizer'}</b><small>{user?.roles?.[0] ?? 'Organizer'}</small></span><LogOut size={16} className="user-shield" /></button>
        </div>
      </aside>
      <section className="main-column">
        <header className="topbar">
          <nav className="breadcrumb" aria-label="Breadcrumb">
            <Link to="/dashboard">Overview</Link>
            <span className="crumb-slash">/</span>
            <Link to="/tournaments">{tournament.name || 'Tournaments'}{tournament.season ? ` · ${tournament.season}` : ''}</Link>
            <span className="crumb-slash">/</span>
            <span aria-current="page">{pageLabel}</span>
          </nav>
          <div className="topbar-actions">
            <span className="connection"><i /> API connected</span>
            <button className="icon-button" aria-label="Activity" title="View auction activity" onClick={() => navigate('/auction/events')}><Activity size={18} /></button>
            <div className="notification-wrap" ref={notificationRef}>
              <button className="icon-button notification" aria-label="Notifications" title="Auction notifications" aria-expanded={notificationMenu} onClick={() => setNotificationMenu(!notificationMenu)}>
                <Bell size={18} />{notifications.length > 0 && <i />}
              </button>
              {notificationMenu && <div className="notification-menu">
                <div className="notification-heading"><span>RECENT ACTIVITY</span><Link to="/auction/events" onClick={() => setNotificationMenu(false)}>View all</Link></div>
                {notifications.length ? notifications.map((notification) => <Link key={notification.id} to="/auction/events" onClick={() => setNotificationMenu(false)}>
                  <span className="notification-mark"><Activity size={14} /></span>
                  <span><b>{notification.title}</b><small>{notification.description}</small></span>
                </Link>) : <p>No auction updates yet. Activity will appear here when bidding begins.</p>}
              </div>}
            </div>
            <div className="profile-menu-wrap" ref={profileRef}>
              <button className="profile-trigger" aria-label="Open profile menu" aria-expanded={profileMenu} onClick={() => setProfileMenu(!profileMenu)}>
                <span className="top-avatar">{(user?.displayName ?? 'OR').split(' ').map((part) => part[0]).slice(0, 2).join('').toUpperCase()}</span><ChevronDown size={14} />
              </button>
              {profileMenu && <div className="profile-menu">
                <div className="profile-menu-header"><b>{user?.displayName ?? 'Organizer'}</b><small>{user?.email}</small></div>
                <Link to="/settings?section=profile" onClick={() => setProfileMenu(false)}><UserRound size={17} />My Profile</Link>
                <Link to="/settings" onClick={() => setProfileMenu(false)}><SettingsIcon size={17} />Account Settings</Link>
                <Link to="/settings?section=password" onClick={() => setProfileMenu(false)}><KeyRound size={17} />Change Password</Link>
                <Link to="/about" onClick={() => setProfileMenu(false)}><Info size={17} />About</Link>
                <Link to="/help" onClick={() => setProfileMenu(false)}><CircleHelp size={17} />Help &amp; Support</Link>
                <button onClick={() => { setProfileMenu(false); void signOut(); }}><LogOut size={17} />Logout</button>
              </div>}
            </div>
          </div>
        </header>
        <div className="page-content">{workspaceError && <div className="workspace-error" role="alert">{workspaceError} Your existing browser copy has been retained. Reload after the server is available.</div>}{workspaceLoading && <div className="workspace-loading">Loading your saved tournament data…</div>}<Outlet /></div>
        <footer className="app-footer">© 2026 ProCricket Auction <span>SECURE ORGANIZER WORKSPACE</span></footer>
      </section>
    </div>
  );
}
