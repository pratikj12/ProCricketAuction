import { useEffect, useMemo, useState } from 'react';
import { ArrowRight, CalendarDays, CircleDollarSign, Clock3, Radio, ShieldCheck, Users, UserRound } from 'lucide-react';
import { Link } from 'react-router-dom';
import { formatMoney, useAuctionData } from '../context/AuctionDataContext.jsx';
import tournamentService from '../services/tournamentService.js';

export default function AuctionSetup() {
  const { tournament, teams, players, selectedSeasonId, seasons } = useAuctionData();
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const season = seasons.find((item) => item.id === selectedSeasonId);
  const currentEvents = useMemo(() => events.filter((event) => event.seasonId === selectedSeasonId), [events, selectedSeasonId]);

  useEffect(() => {
    if (!tournament.id) {
      setEvents([]);
      return undefined;
    }
    let active = true;
    setLoading(true);
    setError('');
    tournamentService.list()
      .then((workspaces) => {
        if (!active) return;
        const workspace = workspaces.find((item) => item.tournament.id === tournament.id);
        if (!workspace) throw new Error('The selected tournament could not be loaded.');
        setEvents(workspace.auctionEvents ?? []);
      })
      .catch((requestError) => {
        if (active) setError(requestError.response?.data?.detail || requestError.message || 'Could not load auction events.');
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [tournament.id]);

  return (
    <main>
      <div className="page-title-row">
        <div><div className="eyebrow"><span className="eyebrow-line" />CONTROL ROOM</div><h1>Auction room</h1><p className="subheading">{tournament.name || 'Select a tournament'} · {season?.name ?? 'Select a season'}</p></div>
        <Link to="/auction/events" className="button button-primary"><CalendarDays size={15} /> Manage auction events</Link>
      </div>
      <section className="setup-banner">
        <div className="setup-banner-icon"><Radio size={23} /></div>
        <div><span>LIVE AUCTION WORKSPACE</span><h2>{tournament.name || 'Your tournament'}</h2><p>{season?.name ?? 'Season not selected'} <i /> {teams.length} teams <i /> {players.length} players</p></div>
        <Link to="/auction/events" className="button button-ghost">View events <ArrowRight size={15} /></Link>
      </section>
      <div className="setup-grid">
        <div className="setup-main">
          <div className="section-heading setup-heading"><div><h2>Scheduled auction events</h2><p>Each room has isolated persisted bidding, purse, and round state.</p></div></div>
          {error && <div className="workspace-error" role="alert">{error}</div>}
          {loading ? <div className="panel table-empty">Loading saved auction events…</div>
            : currentEvents.length ? <div className="auction-event-grid">{currentEvents.map((event) => (
              <article className="panel auction-event-card" key={event.id}>
                <div className="auction-event-card-top"><span className={`status-pill ${event.status.toLowerCase()}`}>{event.status}</span><Radio size={18} /></div>
                <h2>{event.name}</h2>
                <div className="auction-event-meta"><span><CalendarDays size={15} />{new Date(event.scheduledAt).toLocaleString()}</span></div>
                <p className="auction-event-description">{formatMoney(event.bidIncrement, tournament.currencyCode)} increment · {event.bidDurationSeconds}s timer · {event.maximumSquadSize} squad slots</p>
                <div className="auction-event-actions">
                  <Link to={`/auction/live/${event.id}`} className="button button-primary">Open control room</Link>
                  <Link to={`/auction/display/${event.id}`} target="_blank" className="button button-ghost">Projector</Link>
                </div>
              </article>
            ))}</div> : <section className="panel tournament-empty"><CalendarDays size={30} /><h2>No auction event is scheduled</h2><p>Create an event with its bid increment, timer, squad size, and extension rules to initialize a live room.</p><Link to="/auction/events" className="button button-primary">Schedule an auction event</Link></section>}
        </div>
        <aside className="setup-aside">
          <h3>SERVER-AUTHORITATIVE FORMAT</h3>
          <div className="format-row"><span><CircleDollarSign size={17} /></span><div><b>Team purse</b><small>{formatMoney(tournament.defaultTeamPurse ?? 100000, tournament.currencyCode)} starting budget per franchise</small></div></div>
          <div className="format-row"><span><Users size={17} /></span><div><b>Team eligibility</b><small>Captains can bid only for teams assigned to their accounts.</small></div></div>
          <div className="format-row"><span><UserRound size={17} /></span><div><b>Player pool</b><small>{players.filter((player) => player.status === 'Available').length} available · {players.filter((player) => player.status === 'Sold').length} sold</small></div></div>
          <div className="format-row"><span><Clock3 size={17} /></span><div><b>Server timer</b><small>Deadline, pause, resume, and late-bid extension are persisted.</small></div></div>
          <div className="format-note"><ShieldCheck size={16} /><span>Only confirmed sales deduct purse. A failed or rejected command never creates a sale.</span></div>
        </aside>
      </div>
    </main>
  );
}
