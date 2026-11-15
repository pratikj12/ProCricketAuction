import { useEffect, useState } from 'react';
import { ArrowRight, Plus, Shield, Trash2, Users } from 'lucide-react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { formatMoney, useAuctionData } from '../context/AuctionDataContext.jsx';
import { resizeImageToDataUrl } from '../utils/resizeImage.js';
import auctionService from '../services/auctionService.js';

export default function Teams() {
  const { teams, players, auctionEvents, dispatch, tournament, workspaceLoading } = useAuctionData();
  const [showForm, setShowForm] = useState(false);
  const [deletingTeamId, setDeletingTeamId] = useState(null);
  const [auctionBids, setAuctionBids] = useState([]);
  const [bidHistoryError, setBidHistoryError] = useState('');
  const [form, setForm] = useState({
    name: '',
    shortName: '',
    displayName: '',
    city: '',
    captain: '',
    logoDataUrl: ''
  });
  const [expandedTeamId, setExpandedTeamId] = useState(null);

  useEffect(() => {
    let active = true;
    if (!auctionEvents.length) {
      setAuctionBids([]);
      setBidHistoryError('');
      return () => { active = false; };
    }
    const loadBids = async () => {
      try {
        const states = await Promise.all(auctionEvents.map((event) => auctionService.getState(event.id)));
        if (active) {
          setAuctionBids(states.flatMap((state) => state.recentAuctionBids ?? []));
          setBidHistoryError('');
        }
      } catch (error) {
        if (active) setBidHistoryError(error.response?.data?.detail
          || error.message
          || 'Could not load saved auction bid history.');
      }
    };
    void loadBids();
    const interval = window.setInterval(() => { void loadBids(); }, 10000);
    return () => {
      active = false;
      window.clearInterval(interval);
    };
  }, [auctionEvents]);

  async function addTeam(event) {
    event.preventDefault();
    const name = form.name.trim();
    const shortName = form.shortName.trim().toUpperCase();
    const captain = form.captain.trim();
    if (!name || !shortName || !captain) return toast.error('Enter a team name, short code, and captain.');
    if (teams.some((team) => team.name.toLowerCase() === name.toLowerCase()
      || team.shortName.toLowerCase() === shortName.toLowerCase())) {
      return toast.error('A team with that name or short code already exists.');
    }
    try {
      await dispatch({
        type: 'add-team',
        team: {
          name,
          shortName,
          displayName: form.displayName.trim(),
          city: form.city.trim(),
          captain,
          logoDataUrl: form.logoDataUrl
        }
      });
      toast.success(`${name} saved with the league purse and an automatic team color.`);
      setShowForm(false);
      setForm({ name: '', shortName: '', displayName: '', city: '', captain: '', logoDataUrl: '' });
    } catch (error) {
      toast.error(error.response?.data?.detail || error.response?.data?.title || error.message || 'Could not save the team.');
    }
  }

  async function deleteTeam(team) {
    const confirmed = window.confirm(
      `Delete ${team.name}? This permanently removes the team, returns its players to the available pool, and deletes its bids, purchases, and auction activity history. This cannot be undone.`
    );
    if (!confirmed) return;
    setDeletingTeamId(team.id);
    try {
      await dispatch({ type: 'delete-team', teamId: team.id });
      toast.success(`${team.name} was deleted. Its players are available for the auction again.`);
    } catch (error) {
      toast.error(error.response?.data?.detail || error.response?.data?.title || error.message || 'Could not delete the team.');
    } finally {
      setDeletingTeamId(null);
    }
  }

  return (
    <main>
      <div className="page-title-row">
        <div><div className="eyebrow"><span className="eyebrow-line" />THE FRANCHISES</div><h1>Teams & purses</h1><p className="subheading">{tournament.name} · {tournament.season} season roster.</p></div>
        <div className="toolbar-actions"><Link to="/auction/setup" className="button button-ghost">Auction room <ArrowRight size={16} /></Link><button className="button button-primary" disabled={!tournament.id || workspaceLoading} onClick={() => setShowForm(true)}><Plus size={17} /> Add team</button></div>
      </div>
      {teams.length ? <div className="team-card-grid">{teams.map((team, index) => {
        const squad = players.filter((player) => player.teamId === team.id);
        const spent = squad.reduce((sum, player) => sum + (player.soldPrice || 0), 0);
        const initialPurse = team.initialPurse ?? team.purse + spent;
        const pursePercentage = initialPurse > 0 ? Math.min(100, Math.max(0, (team.purse / initialPurse) * 100)) : 0;
        const teamBids = auctionBids.filter((bid) => bid.teamId === team.id);
        const expanded = expandedTeamId === team.id;
        return <article className="team-card" key={team.id}>
          <button className="team-card-summary" type="button" aria-expanded={expanded} onClick={() => {
            setExpandedTeamId(expanded ? null : team.id);
          }}>
            <div className="team-card-top"><div className={`team-crest crest-${(index % 4) + 1}`} style={{ '--team': team.color }}>{team.logoDataUrl ? <img src={team.logoDataUrl} alt="" /> : team.shortName.slice(0, 1)}</div><span className="team-chip"><i style={{ background: team.color }} />ACTIVE</span></div>
            <div className="team-card-name">{team.name}</div><div className="captain-line"><Shield size={14} /> Captain <b>{team.captain}</b></div>
            <div className="team-budget"><div><span>REMAINING PURSE</span><strong>{formatMoney(team.purse, tournament.currencyCode)}</strong></div><div className="budget-ring" style={{ '--progress': `${pursePercentage}%`, '--team': team.color }}><b>{Math.round(pursePercentage)}<small>%</small></b></div></div>
            <div className="budget-track"><i style={{ width: `${pursePercentage}%`, background: team.color }} /></div>
            <div className="team-card-footer"><span><Users size={15} /> {squad.length} players</span><span>Spent <b>{formatMoney(spent, tournament.currencyCode)}</b></span></div>
            <span className="team-card-hint">{expanded ? 'Click to hide squad details' : 'Click for players and bid details'}</span>
          </button>
          <div className="team-card-actions">
            <button
              className="button button-danger team-delete-button"
              type="button"
              disabled={workspaceLoading || deletingTeamId === team.id}
              onClick={() => { void deleteTeam(team); }}
              aria-label={`Delete ${team.name}`}
            >
              <Trash2 size={14} /> {deletingTeamId === team.id ? 'Deleting…' : 'Delete team'}
            </button>
          </div>
          {expanded && <section className="team-detail-panel">
            <div className="team-detail-heading"><div><h3>{team.name} squad</h3><p>Player registrations, purchase prices, and auction activity.</p></div><span>{squad.length} players</span></div>
            {squad.length ? <div className="team-roster-list">{squad.map((player) => {
              const playerBids = auctionBids.filter((bid) => bid.playerId === player.id);
              return <article className="team-roster-player" key={player.id}>
                <div className="team-roster-player-heading">
                  <div><b>{player.name}</b><span>{player.role}{player.city ? ` · ${player.city}` : ''}</span></div>
                  <span className={`status-pill ${player.status.toLowerCase()}`}>{player.status}</span>
                </div>
                <div className="team-roster-player-meta"><span>Base {formatMoney(player.basePrice, tournament.currencyCode)}</span><span>Purchase {formatMoney(player.soldPrice, tournament.currencyCode)}</span></div>
                <div className="team-player-bids">
                  <b>Bid history</b>
                  {playerBids.length ? playerBids.map((bid) => (
                    <span key={bid.id}>{formatMoney(bid.amount, tournament.currencyCode)} <small>{new Date(bid.createdAt).toLocaleString()}</small></span>
                  )) : <span className="muted">{bidHistoryError ? 'Saved bid history is unavailable.' : 'No accepted bids are recorded for this player.'}</span>}
                </div>
              </article>;
            })}</div> : <div className="team-detail-empty">No players have been sold to this team yet. Players are assigned to a squad when the auctioneer confirms a sale.</div>}
            <div className="team-activity-heading"><h4>Team bid activity</h4><span>{teamBids.length} bids</span></div>
            {bidHistoryError && <p className="workspace-error" role="status">{bidHistoryError}</p>}
            {teamBids.length ? <div className="team-bid-list">{teamBids.map((bid) => {
              const player = players.find((item) => item.id === bid.playerId);
              return <div key={bid.id}><span>{player?.name ?? 'Player'}</span><b>{formatMoney(bid.amount, tournament.currencyCode)}</b><small>{new Date(bid.createdAt).toLocaleString()}</small></div>;
            })}</div> : <p className="team-detail-empty">{bidHistoryError ? 'Saved bids could not be loaded.' : 'No accepted bids from this team are recorded in the saved auction events.'}</p>}
          </section>}
        </article>;
      })}</div> : <section className="panel table-empty">No teams yet. Add your first team to prepare the tournament squads.</section>}
      <div className="team-note"><span className="note-icon"><Shield size={17} /></span><span><b>Team purses and confirmed player purchases are saved to your account.</b> Bid history is loaded from the saved auction events.</span></div>
      {showForm && <div className="modal-backdrop" onClick={() => setShowForm(false)}><form className="modal-card" onSubmit={addTeam} onClick={(event) => event.stopPropagation()}>
        <div className="modal-heading"><div><h2>Add team</h2><p>Register a franchise for this tournament.</p></div><button type="button" className="icon-button" onClick={() => setShowForm(false)} aria-label="Close">×</button></div>
        <label>Team name *<input autoFocus required value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} placeholder="e.g. Mumbai Indians" /></label>
        <div className="form-row"><label>Short code *<input required minLength="2" maxLength="8" value={form.shortName} onChange={(event) => setForm({ ...form, shortName: event.target.value.toUpperCase() })} placeholder="e.g. MI" /></label><label>Display name in auction<input maxLength="100" value={form.displayName} onChange={(event) => setForm({ ...form, displayName: event.target.value })} placeholder="Optional" /></label></div>
        <small className="form-help">Shown instead of the team name on live auction screens. Leave empty to use the full team name.</small>
        <label>Team logo<input type="file" accept="image/jpeg,image/png,image/webp" onChange={async (event) => {
          const file = event.target.files?.[0];
          if (!file) return;
          try {
            const logoDataUrl = await resizeImageToDataUrl(file, 256);
            setForm((current) => ({ ...current, logoDataUrl }));
          } catch (error) {
            toast.error(error.message || 'Could not process the team logo.');
          }
        }} /></label>
        {form.logoDataUrl && <img className="team-logo-preview" src={form.logoDataUrl} alt="Team logo preview" />}
        <div className="form-row"><label>City<input maxLength="120" value={form.city} onChange={(event) => setForm({ ...form, city: event.target.value })} placeholder="Home city" /></label><label>Captain / owner<input required value={form.captain} onChange={(event) => setForm({ ...form, captain: event.target.value })} placeholder="Team representative" /></label></div>
        <p className="team-form-note">Starting purse: <b>{formatMoney(tournament.defaultTeamPurse ?? 100000, tournament.currencyCode)}</b> · Team color is assigned automatically.</p>
        <div className="modal-actions"><button type="button" className="button button-ghost" onClick={() => setShowForm(false)}>Cancel</button><button className="button button-primary" type="submit" disabled={workspaceLoading}>Add team</button></div>
      </form></div>}
    </main>
  );
}
