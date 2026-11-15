import { useEffect, useMemo, useRef, useState } from 'react';
import { ArrowDownRight, ArrowRight, ArrowUpRight, CalendarDays, ChevronDown, CircleDollarSign, Clock3, Plus, Radio, Users, UserRound, Zap } from 'lucide-react';
import { Link } from 'react-router-dom';
import { formatMoney, useAuctionData } from '../context/AuctionDataContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import DeleteTournamentAction from '../components/common/DeleteTournamentAction.jsx';
import auctionService from '../services/auctionService.js';

function StatCard({ icon: Icon, label, value, note, trend, tone = 'lime', to }) {
  return (
    <Link to={to} className="stat-card stat-card-link">
      <div className={`stat-icon ${tone}`}><Icon size={19} /></div>
      <div className="stat-label">{label}</div>
      <div className="stat-value">{value}</div>
      <div className="stat-note">{trend && <span className="trend-up"><ArrowUpRight size={13} />{trend}</span>}{note}</div>
    </Link>
  );
}

export default function Dashboard() {
  const {
    tournament,
    players,
    teams,
    auctionEvents,
    seasons,
    selectedSeasonId,
    refreshWorkspace,
    workspaceError
  } = useAuctionData();
  const { user } = useAuth();
  const [period, setPeriod] = useState('This season');
  const [periodMenu, setPeriodMenu] = useState(false);
  const [auctionSummary, setAuctionSummary] = useState({ events: [], error: '' });
  const periodMenuRef = useRef(null);
  const eventIds = useMemo(
    () => auctionEvents.map((event) => event.id).join(','),
    [auctionEvents]
  );

  useEffect(() => {
    if (!eventIds) {
      setAuctionSummary({ events: [], error: '' });
      return undefined;
    }
    let active = true;
    const loadAuctionSummary = async () => {
      try {
        const events = await Promise.all(auctionEvents.map(async (event) => ({
          ...event,
          state: await auctionService.getState(event.id)
        })));
        if (active) setAuctionSummary({ events, error: '' });
      } catch (error) {
        if (active) setAuctionSummary({
          events: [],
          error: error.response?.data?.detail
            || error.message
            || 'Could not load saved auction activity.'
        });
      }
    };
    void loadAuctionSummary();
    const interval = window.setInterval(() => { void loadAuctionSummary(); }, 10000);
    return () => {
      active = false;
      window.clearInterval(interval);
    };
  }, [auctionEvents, eventIds]);

  useEffect(() => {
    void refreshWorkspace().catch(() => {});
  }, [refreshWorkspace]);
  useEffect(() => {
    if (!periodMenu) return undefined;
    function closeOnOutsideClick(event) {
      if (!periodMenuRef.current?.contains(event.target)) setPeriodMenu(false);
    }
    function closeOnEscape(event) {
      if (event.key === 'Escape') setPeriodMenu(false);
    }
    document.addEventListener('pointerdown', closeOnOutsideClick);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsideClick);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [periodMenu]);
  const sold = players.filter((player) => player.status === 'Sold');
  const available = players.filter((player) => player.status === 'Available');
  const selectedSeason = seasons.find((season) => season.id === selectedSeasonId);
  const allSeasonTotals = seasons.reduce((totals, season) => ({
    spend: totals.spend + (season.totalSpend || 0),
    sold: totals.sold + (season.soldPlayerCount || 0),
    available: totals.available + (season.availablePlayerCount || 0),
    players: totals.players + (season.playerCount || 0),
    teams: totals.teams + (season.teamCount || 0)
  }), { spend: 0, sold: 0, available: 0, players: 0, teams: 0 });
  const spend = period === 'All seasons'
    ? allSeasonTotals.spend
    : sold.reduce((sum, player) => sum + (player.soldPrice || 0), 0);
  const soldCount = period === 'All seasons' ? allSeasonTotals.sold : sold.length;
  const availableCount = period === 'All seasons' ? allSeasonTotals.available : available.length;
  const playerCount = period === 'All seasons' ? allSeasonTotals.players : players.length;
  const teamCount = period === 'All seasons' ? allSeasonTotals.teams : teams.length;
  const latestEvent = auctionSummary.events
    .slice()
    .sort((first, second) => new Date(second.scheduledAt) - new Date(first.scheduledAt))[0];
  const recent = auctionSummary.events.flatMap((event) => (event.state.recentAuctionBids ?? []).map((bid) => ({
    ...bid,
    auctionName: event.name
  })))
    .sort((first, second) => new Date(second.createdAt) - new Date(first.createdAt))
    .slice(0, 4);
  const auctionStatus = latestEvent?.state.status ?? (auctionEvents.length ? auctionEvents[auctionEvents.length - 1].status : null);
  const auctionStatusLabel = auctionStatus === 'Live' ? 'In progress'
    : auctionStatus === 'AwaitingDecision' ? 'Awaiting result'
      : auctionStatus === 'Paused' ? 'Paused'
        : auctionStatus === 'Completed' ? 'Completed'
          : auctionStatus === 'Cancelled' ? 'Cancelled'
            : auctionEvents.length ? 'Upcoming' : 'Not scheduled';
  const today = new Intl.DateTimeFormat('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' }).format(new Date());

  return (
    <main>
      <div className="welcome-row">
        <div><div className="eyebrow"><span className="eyebrow-line" />{today.toUpperCase()}</div><h1>Hello, {user?.displayName?.split(' ')[0] ?? 'Organizer'} <span className="wave">✳</span></h1><p className="subheading">{tournament.name ? `${tournament.name} · ${selectedSeason?.name ?? 'Season overview'}` : 'Create a league to start managing seasons, teams, players, and auctions.'}</p></div>
        <div className="toolbar-actions">
          <Link to="/tournaments" state={{ openNewTournament: true }} className="button button-ghost"><Plus size={17} /> New tournament</Link>
          {tournament.name && <Link to="/tournaments" className="button button-primary"><CalendarDays size={16} /> Manage seasons</Link>}
        </div>
      </div>
      {(workspaceError || auctionSummary.error) && <div className="panel workspace-error" role="status">{workspaceError || auctionSummary.error}</div>}
      <section className="active-event">
        <div className="event-left"><div className="event-kicker"><span className="status-dot" /> {tournament.name ? 'LEAGUE OVERVIEW' : 'GET STARTED'}</div><h2>{tournament.name || 'No league created'}<span className="season-pill">{tournament.season ? `${tournament.season} SEASON` : 'CREATE YOUR FIRST'}</span></h2><div className="event-meta"><span><CalendarDays size={15} /> {tournament.season ? `Season ${tournament.season}` : 'Create a league to get started'}</span><span><Users size={15} /> {teams.length} teams</span><span><Radio size={15} /> {tournament.venue || 'Venue not set'}</span></div><div className="event-actions"><Link to={tournament.name ? '/auction/setup' : '/tournaments'} state={tournament.name ? undefined : { openNewTournament: true }} className="button button-green"><Zap size={15} /> {tournament.name ? 'Open auction room' : 'Create tournament'}</Link><Link to="/tournaments" className="text-link">Season manager <ArrowRight size={15} /></Link>{tournament.name && <DeleteTournamentAction variant="button" />}</div></div>
        <div className="event-visual"><div className="visual-orbit orbit-one" /><div className="visual-orbit orbit-two" /><div className="visual-ball">🏏</div><div className="visual-label"><span>AUCTION READINESS</span><b>{tournament.name ? 'Ready to play' : 'Create tournament'} <i>✓</i></b></div></div>
      </section>
      <div className="section-heading"><div><p>{period === 'This season' ? <h2>Current season performance</h2> : 'Combined totals across the seasons in this league.'}</p></div><div className="period-select-wrap" ref={periodMenuRef}><button className="period-select" aria-expanded={periodMenu} onClick={() => setPeriodMenu(!periodMenu)}>{period} <ChevronDown size={14} /></button>{periodMenu && <div className="period-menu"><span>REPORTING SCOPE</span>{['This season', 'All seasons'].map((option) => <button className={period === option ? 'selected' : ''} key={option} onClick={() => { setPeriod(option); setPeriodMenu(false); }}><span>{option}</span>{period === option && <i>✓</i>}</button>)}<small>Choose the season reporting scope.</small></div>}</div></div>
      <section className="stats-grid">
        <StatCard to="/reports" icon={CircleDollarSign} label="TOTAL SPENT" value={formatMoney(spend, tournament.currencyCode)} note="Based on recorded sold players" trend={soldCount ? `${soldCount} sold` : null} />
        <StatCard to="/players" icon={UserRound} label="PLAYERS SOLD" value={`${soldCount} / ${playerCount}`} note={`${availableCount} players available`} tone="blue" />
        <StatCard to="/teams" icon={Users} label="ACTIVE TEAMS" value={teamCount} note={period === 'All seasons' ? 'Across all league seasons' : 'Current season roster'} tone="purple" />
        <StatCard to="/auction/events" icon={Clock3} label="AUCTION STATUS" value={auctionStatusLabel} note={latestEvent?.name ?? 'Saved auction event status'} tone="orange" />
      </section>
      <section className="quick-actions-section">
        <div className="section-heading"><div><h2>Quick actions</h2><p>Jump to the next step in your season setup.</p></div></div>
        <div className="quick-actions-grid">
          <Link to="/tournaments" className="quick-action-card"><CalendarDays size={18} /><span><b>Manage seasons</b><small>Create or update seasons</small></span><ArrowRight size={15} /></Link>
          <Link to="/teams" className="quick-action-card"><Users size={18} /><span><b>Register teams</b><small>{teams.length} teams in this season</small></span><ArrowRight size={15} /></Link>
          <Link to="/players" className="quick-action-card"><UserRound size={18} /><span><b>Player registrations</b><small>{players.length} players in the pool</small></span><ArrowRight size={15} /></Link>
          <Link to="/auction/setup" className="quick-action-card"><Radio size={18} /><span><b>Prepare auction</b><small>Review bidding setup</small></span><ArrowRight size={15} /></Link>
        </div>
      </section>
      <section className="dashboard-grid">
        <article className="panel activity-panel">
          <div className="panel-heading"><div><h3>Recent activity</h3><p>Latest accepted bids from saved auction events.</p></div><Link to="/auction/events" className="small-link">View auctions <ArrowRight size={14} /></Link></div>
          {recent.length ? <div className="activity-list">{recent.map((bid) => {
            const team = teams.find((item) => item.id === bid.teamId);
            const player = players.find((item) => item.id === bid.playerId);
            return <div className="activity-row" key={bid.id}><div className="activity-avatar" style={{ color: team?.color }}>{team?.shortName?.slice(0, 2)}</div><div className="activity-description"><b>{bid.teamName || team?.name}</b><span>Bid on {player?.name ?? 'auction player'} · {bid.auctionName}</span></div><div className="activity-price">{formatMoney(bid.amount, tournament.currencyCode)}<small>{new Date(bid.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</small></div><ArrowDownRight size={16} className="activity-arrow" /></div>;
          })}</div> : <div className="empty-activity"><span className="empty-icon"><Radio size={20} /></span><b>{auctionSummary.error ? 'Auction activity unavailable' : 'No accepted bids yet'}</b><span>{auctionSummary.error ? 'Refresh or open the auction room to check the connection.' : 'Accepted bids from your saved auction events will appear here.'}</span><Link to="/auction/events">View auction events <ArrowRight size={14} /></Link></div>}
        </article>
        <article className="panel teams-panel">
          <div className="panel-heading"><div><h3>Team purse</h3><p>Available budgets across all teams.</p></div><Link to="/teams" className="icon-link"><ArrowRight size={16} /></Link></div>
          <div className="team-purse-list">{teams.slice(0, 4).map((team, index) => {
            const teamSpend = sold.filter((player) => player.teamId === team.id).reduce((sum, player) => sum + (player.soldPrice || 0), 0);
            const initialPurse = team.initialPurse ?? team.purse + teamSpend;
            const pursePercentage = initialPurse > 0 ? Math.min(100, Math.max(0, team.purse / initialPurse * 100)) : 0;
            return <div className="purse-row" key={team.id}><span className={`team-mark team-mark-${index + 1}`} style={{ '--team': team.color }}>{team.shortName}</span><div className="purse-info"><div><b>{team.name}</b><span>{formatMoney(team.purse, tournament.currencyCode)}</span></div><div className="purse-track"><i style={{ width: `${pursePercentage}%`, background: team.color }} /></div></div></div>;
          })}</div>
        </article>
      </section>
      <div className="dashboard-bottom"><span><i className="tiny-green-dot" /> Tournament, team, player & sale data saved</span><span>Dashboard refreshes with server-backed auction data</span><Link to="/reports">All reports <ArrowRight size={14} /></Link></div>
    </main>
  );
}
