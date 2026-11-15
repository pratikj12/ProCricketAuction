import { useEffect, useMemo, useState } from 'react';
import { CalendarDays, Clock3, MapPin, Plus, Radio, Trash2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useAuctionData } from '../context/AuctionDataContext.jsx';
import tournamentService from '../services/tournamentService.js';

const blankEvent = {
  name: '',
  scheduledAt: '',
  venue: '',
  description: '',
  status: 'Scheduled',
  bidIncrement: 2000,
  bidDurationSeconds: 30,
  bidExtensionSeconds: 5,
  maximumSquadSize: 25,
  minimumSquadSize: 0,
  minimumSquadReservePrice: 0,
  maximumRounds: 2
};

function toLocalDateTime(value) {
  const date = new Date(value);
  const offsetDate = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return offsetDate.toISOString().slice(0, 16);
}

export default function AuctionEvents() {
  const { tournament, seasons, selectedSeasonId, workspaceLoading } = useAuctionData();
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(Boolean(tournament.id));
  const [loadError, setLoadError] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [form, setForm] = useState(blankEvent);
  const season = seasons.find((item) => item.id === selectedSeasonId);
  const visibleEvents = useMemo(
    () => events.filter((event) => event.seasonId === selectedSeasonId),
    [events, selectedSeasonId]
  );

  async function refreshEvents() {
    const workspaces = await tournamentService.list();
    const workspace = workspaces.find((item) => item.tournament.id === tournament.id);
    if (!workspace) throw new Error('The selected league could not be loaded.');
    setEvents(workspace.auctionEvents ?? []);
  }

  useEffect(() => {
    let active = true;
    if (!tournament.id) {
      setEvents([]);
      setLoading(false);
      return () => { active = false; };
    }
    setLoading(true);
    setLoadError('');
    tournamentService.list()
      .then((workspaces) => {
        if (!active) return;
        const workspace = workspaces.find((item) => item.tournament.id === tournament.id);
        if (!workspace) throw new Error('The selected league could not be loaded.');
        setEvents(workspace.auctionEvents ?? []);
      })
      .catch((error) => {
        if (active) setLoadError(error.response?.data?.detail || error.message || 'Could not load auction events.');
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [tournament.id]);

  function startCreate() {
    setEditingId(null);
    setForm(blankEvent);
    setShowForm(true);
  }

  function startEdit(event) {
    setEditingId(event.id);
    setForm({
      name: event.name,
      scheduledAt: toLocalDateTime(event.scheduledAt),
      venue: event.venue ?? '',
      description: event.description ?? '',
      status: event.status,
      bidIncrement: event.bidIncrement,
      bidDurationSeconds: event.bidDurationSeconds,
      bidExtensionSeconds: event.bidExtensionSeconds,
      maximumSquadSize: event.maximumSquadSize,
      minimumSquadSize: event.minimumSquadSize,
      minimumSquadReservePrice: event.minimumSquadReservePrice,
      maximumRounds: event.maximumRounds
    });
    setShowForm(true);
  }

  async function saveEvent(event) {
    event.preventDefault();
    if (!selectedSeasonId) {
      toast.error('Create or select a season before scheduling an auction.');
      return;
    }
    try {
      const request = {
        ...form,
        name: form.name.trim(),
        seasonId: selectedSeasonId,
        scheduledAt: new Date(form.scheduledAt).toISOString(),
        bidIncrement: Number(form.bidIncrement),
        bidDurationSeconds: Number(form.bidDurationSeconds),
        bidExtensionSeconds: Number(form.bidExtensionSeconds),
        maximumSquadSize: Number(form.maximumSquadSize),
        minimumSquadSize: Number(form.minimumSquadSize),
        minimumSquadReservePrice: Number(form.minimumSquadReservePrice),
        maximumRounds: Number(form.maximumRounds)
      };
      if (editingId) await tournamentService.updateAuctionEvent(tournament.id, editingId, request);
      else await tournamentService.createAuctionEvent(tournament.id, request);
      await refreshEvents();
      toast.success(editingId ? 'Auction event updated.' : 'Auction event scheduled.');
      setShowForm(false);
    } catch (error) {
      toast.error(error.response?.data?.detail || error.response?.data?.title || error.message || 'Could not save the auction event.');
    }
  }

  async function deleteEvent(event) {
    if (!window.confirm(`Delete the "${event.name}" event?`)) return;
    try {
      await tournamentService.removeAuctionEvent(tournament.id, event.id);
      await refreshEvents();
      toast.success('Auction event deleted.');
    } catch (error) {
      toast.error(error.response?.data?.detail || error.response?.data?.title || error.message || 'Could not delete the event.');
    }
  }

  return (
    <main>
      <div className="page-title-row">
        <div><div className="eyebrow"><span className="eyebrow-line" />SEASON SCHEDULE</div><h1>Auction events</h1><p className="subheading">{tournament.name ? `${tournament.name} · ${season?.name ?? 'Select a season'}` : 'Schedule auction sessions for your league.'}</p></div>
        <button className="button button-primary" disabled={!tournament.id || !selectedSeasonId || workspaceLoading || loading} onClick={startCreate}><Plus size={16} /> Schedule event</button>
      </div>
      {!tournament.id ? <section className="panel table-empty">Create a league before scheduling auction events.</section>
        : loadError ? <section className="panel workspace-error" role="alert">{loadError}</section>
          : loading ? <section className="panel table-empty">Loading saved auction events…</section>
            : visibleEvents.length ? <section className="auction-event-grid">{visibleEvents.map((event) => (
              <article className="panel auction-event-card" key={event.id}>
                <div className="auction-event-card-top"><span className={`status-pill ${event.status.toLowerCase()}`}>{event.status}</span><Radio size={18} /></div>
                <h2>{event.name}</h2>
                <div className="auction-event-meta"><span><CalendarDays size={15} />{new Date(event.scheduledAt).toLocaleDateString()}</span><span><Clock3 size={15} />{new Date(event.scheduledAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span><span><MapPin size={15} />{event.venue || 'Venue not set'}</span></div>
                {event.description && <p className="auction-event-description">{event.description}</p>}
                <div className="auction-event-actions">
                  <Link className="button button-primary" to={`/auction/live/${event.id}`}>Open auction room</Link>
                  <Link className="button button-ghost" to={`/auction/bid/${event.id}`}>Captain desk</Link>
                  <Link className="button button-ghost" target="_blank" to={`/auction/display/${event.id}`}>Projector</Link>
                  <Link className="button button-ghost" to={`/auction/results/${event.id}`}>Results</Link>
                  {['Scheduled', 'Cancelled'].includes(event.status) && <>
                    <button className="button button-ghost" onClick={() => startEdit(event)}>Edit details</button>
                    <button className="icon-button" aria-label={`Delete ${event.name}`} onClick={() => deleteEvent(event)}><Trash2 size={15} /></button>
                  </>}
                </div>
              </article>
            ))}</section>
            : <section className="panel tournament-empty"><CalendarDays size={30} /><h2>No auction events yet</h2><p>Schedule the auction date, venue, and organizer notes for {season?.name ?? 'this season'}.</p><button className="button button-primary" disabled={!selectedSeasonId} onClick={startCreate}><Plus size={15} /> Schedule first event</button></section>}
      <p className="report-disclaimer">Auction events, bidding rules, live state, and purchase results are saved in the league database.</p>
      {showForm && <div className="modal-backdrop" onClick={() => setShowForm(false)}><form className="modal-card auction-event-modal" onSubmit={saveEvent} onClick={(event) => event.stopPropagation()}>
        <div className="modal-heading"><div><h2>{editingId ? 'Edit auction event' : 'Schedule auction event'}</h2><p>Event details are linked to {season?.name ?? 'the selected season'}.</p></div><button type="button" className="icon-button" onClick={() => setShowForm(false)} aria-label="Close">×</button></div>
        <label>Event name *<input autoFocus required minLength="2" maxLength="120" value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} placeholder="e.g. Player auction day" /></label>
        <div className="form-row"><label>Date and time *<input type="datetime-local" required value={form.scheduledAt} onChange={(event) => setForm({ ...form, scheduledAt: event.target.value })} /></label><label>Status<select value={form.status} onChange={(event) => setForm({ ...form, status: event.target.value })}><option>Scheduled</option><option>Cancelled</option></select></label></div>
        <h3>Auction rules</h3>
        <div className="form-row">
          <label>Bid increment<input type="number" min="1" max="9000000000000000" required value={form.bidIncrement} onChange={(event) => setForm({ ...form, bidIncrement: event.target.value })} /></label>
          <label>Bid timer (seconds)<input type="number" min="10" max="300" required value={form.bidDurationSeconds} onChange={(event) => setForm({ ...form, bidDurationSeconds: event.target.value })} /></label>
        </div>
        <div className="form-row">
          <label>Late bid extension (seconds)<input type="number" min="0" max="60" required value={form.bidExtensionSeconds} onChange={(event) => setForm({ ...form, bidExtensionSeconds: event.target.value })} /></label>
          <label>Maximum squad size<input type="number" min="1" max="100" required value={form.maximumSquadSize} onChange={(event) => setForm({ ...form, maximumSquadSize: event.target.value })} /></label>
        </div>
        <div className="form-row">
          <label>Minimum squad size<input type="number" min="0" max="100" required value={form.minimumSquadSize} onChange={(event) => setForm({ ...form, minimumSquadSize: event.target.value })} /></label>
          <label>Reserve per remaining minimum slot<input type="number" min="0" max="9000000000000000" required value={form.minimumSquadReservePrice} onChange={(event) => setForm({ ...form, minimumSquadReservePrice: event.target.value })} /></label>
        </div>
        <label>Maximum auction rounds<input type="number" min="1" max="10" required value={form.maximumRounds} onChange={(event) => setForm({ ...form, maximumRounds: event.target.value })} /></label>
        <label>Venue<input maxLength="160" value={form.venue} onChange={(event) => setForm({ ...form, venue: event.target.value })} placeholder="Ground or online" /></label>
        <label>Notes<textarea maxLength="1000" rows="3" value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} placeholder="Optional organizer notes" /></label>
        <div className="modal-actions"><button type="button" className="button button-ghost" onClick={() => setShowForm(false)}>Cancel</button><button className="button button-primary" type="submit">{editingId ? 'Save event' : 'Schedule event'}</button></div>
      </form></div>}
    </main>
  );
}
