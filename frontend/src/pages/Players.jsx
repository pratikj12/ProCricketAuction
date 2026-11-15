import { useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import { Download, Plus, Search, SlidersHorizontal, UserRound, X } from 'lucide-react';
import { formatMoney, useAuctionData } from '../context/AuctionDataContext.jsx';
import { resizeImageToDataUrl } from '../utils/resizeImage.js';

const initialForm = {
  firstName: '',
  lastName: '',
  dateOfBirth: '',
  nationality: '',
  email: '',
  city: '',
  role: 'Batter',
  basePrice: '',
  photoDataUrl: ''
};

export default function Players() {
  const { players, teams, dispatch, tournament, workspaceLoading } = useAuctionData();
  const [query, setQuery] = useState('');
  const [category, setCategory] = useState('All categories');
  const [filter, setFilter] = useState('All statuses');
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState(initialForm);
  const filtered = useMemo(() => players.filter((player) => {
    const matchesText = `${player.name} ${player.city} ${player.role} ${player.nationality} ${player.email}`.toLowerCase().includes(query.toLowerCase());
    return matchesText
      && (category === 'All categories' || player.role === category)
      && (filter === 'All statuses' || player.status === filter);
  }), [players, query, category, filter]);

  async function addPlayer(event) {
    event.preventDefault();
    const name = `${form.firstName.trim()} ${form.lastName.trim()}`.trim();
    const basePrice = form.basePrice.trim() ? Number(form.basePrice) : 0;
    if (!form.firstName.trim() || !form.lastName.trim()) return toast.error('Enter the player’s first and last name.');
    if (!Number.isSafeInteger(basePrice) || basePrice < 0) return toast.error('Enter a valid base price.');
    try {
      await dispatch({
        type: 'add-player',
        player: {
          name,
          city: form.city.trim(),
          email: form.email.trim(),
          nationality: form.nationality.trim(),
          dateOfBirth: form.dateOfBirth || null,
          photoDataUrl: form.photoDataUrl,
          role: form.role,
          basePrice
        }
      });
      toast.success(`${name} saved to the player pool.`);
      setShowForm(false);
      setForm(initialForm);
    } catch (error) {
      toast.error(error.response?.data?.detail || error.response?.data?.title || error.message || 'Could not save the player.');
    }
  }

  function exportCsv() {
    const rows = [
      ['Player', 'Category', 'Requested price', 'Assigned price', 'Status', 'Team', 'City', 'Nationality', 'Email'],
      ...filtered.map((player) => [
        player.name,
        player.role,
        player.basePrice,
        player.soldPrice ?? '',
        player.status,
        teams.find((team) => team.id === player.teamId)?.name ?? '',
        player.city,
        player.nationality,
        player.email
      ])
    ];
    const csv = rows.map((row) => row.map((cell) => `"${String(cell ?? '').replaceAll('"', '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${tournament.name || 'tournament'}-players.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  return (
    <main>
      <div className="page-title-row">
        <div><div className="eyebrow"><span className="eyebrow-line" />SEASON ROSTER</div><h1>Player registrations</h1><p className="subheading">{tournament.name} · {tournament.season}. Register profiles, categories, contact details, and base prices.</p></div>
        <div className="toolbar-actions"><button className="button button-ghost" type="button" onClick={exportCsv} disabled={!filtered.length}><Download size={15} /> Export pool</button><button className="button button-primary" disabled={!tournament.id || workspaceLoading} onClick={() => setShowForm(true)}><Plus size={17} /> Add player</button></div>
      </div>
      <div className="summary-strip"><div><UserRound size={17} /><b>{players.length}</b><span>Registered players</span></div><div><span className="summary-status available" /><b>{players.filter((player) => player.status === 'Available').length}</b><span>Available</span></div><div><span className="summary-status sold" /><b>{players.filter((player) => player.status === 'Sold').length}</b><span>Sold</span></div></div>
      <section className="panel table-panel">
        <div className="table-toolbar">
          <div className="search-box"><Search size={17} /><input placeholder="Search players by name, email, role, city..." value={query} onChange={(event) => setQuery(event.target.value)} /></div>
          <div className="toolbar-actions"><label className="select-wrap"><SlidersHorizontal size={15} /><select value={category} onChange={(event) => setCategory(event.target.value)}><option>All categories</option><option>Batter</option><option>Bowler</option><option>All-Rounder</option><option>Wicket-Keeper</option></select></label><label className="select-wrap"><select value={filter} onChange={(event) => setFilter(event.target.value)}><option>All statuses</option><option>Available</option><option>Sold</option><option>Unsold</option></select></label><span className="result-count">{filtered.length} players</span></div>
        </div>
        <div className="table-scroll"><table><thead><tr><th>PLAYER</th><th>CATEGORY</th><th>REQUESTED PRICE</th><th>ASSIGNED PRICE</th><th>STATUS</th><th>TEAM</th><th>REGISTERED DETAILS</th></tr></thead><tbody>{filtered.map((player, index) => {
          const team = teams.find((item) => item.id === player.teamId);
          return <tr key={player.id}>
            <td><div className="player-cell"><div className={`player-avatar avatar-${index % 5}`}>{player.photoDataUrl ? <img src={player.photoDataUrl} alt="" /> : player.name.split(' ').map((part) => part[0]).slice(0, 2).join('')}</div><div><b>{player.name}</b><small>{player.email || player.city || '—'}</small></div></div></td>
            <td><span className="role-pill">{player.role}</span></td>
            <td className="price-cell">{formatMoney(player.basePrice, tournament.currencyCode)}</td>
            <td className="price-cell">{player.soldPrice != null ? formatMoney(player.soldPrice, tournament.currencyCode) : '—'}</td>
            <td><span className={`status-pill ${player.status.toLowerCase()}`}>{player.status}</span></td>
            <td>{team ? <span className="table-team"><i style={{ background: team.color }} />{team.shortName}</span> : <span className="muted">—</span>}</td>
            <td>{[player.city, player.nationality].filter(Boolean).join(' · ') || '—'}</td>
          </tr>;
        })}</tbody></table></div>
        {!filtered.length && <div className="table-empty">{players.length ? 'No players match your search or filters.' : 'No players registered yet. Add your first player to this tournament.'}</div>}
      </section>
      {showForm && <div className="modal-backdrop" onClick={() => setShowForm(false)}><form className="modal-card player-registration-modal" onSubmit={addPlayer} onClick={(event) => event.stopPropagation()}>
        <div className="modal-heading"><div><h2>Add player</h2><p>Player profile information for this tournament.</p></div><button type="button" className="icon-button" onClick={() => setShowForm(false)} aria-label="Close"><X size={18} /></button></div>
        <label className="player-photo-upload">Player photo (optional)<input type="file" accept="image/jpeg,image/png,image/webp" onChange={async (event) => {
          const file = event.target.files?.[0];
          if (!file) return;
          try {
            const photoDataUrl = await resizeImageToDataUrl(file, 512);
            setForm((current) => ({ ...current, photoDataUrl }));
          } catch (error) {
            toast.error(error.message || 'Could not process the player photo.');
          }
        }} /></label>
        {form.photoDataUrl && <img className="player-photo-preview" src={form.photoDataUrl} alt="Player photo preview" />}
        <div className="form-row"><label>First name *<input autoFocus required maxLength="60" value={form.firstName} onChange={(event) => setForm({ ...form, firstName: event.target.value })} placeholder="First name" /></label><label>Last name *<input required maxLength="60" value={form.lastName} onChange={(event) => setForm({ ...form, lastName: event.target.value })} placeholder="Last name" /></label></div>
        <div className="form-row"><label>Date of birth<input type="date" value={form.dateOfBirth} onChange={(event) => setForm({ ...form, dateOfBirth: event.target.value })} /></label><label>Nationality<input maxLength="80" value={form.nationality} onChange={(event) => setForm({ ...form, nationality: event.target.value })} placeholder="e.g. Indian" /></label></div>
        <label>Email<input type="email" maxLength="254" value={form.email} onChange={(event) => setForm({ ...form, email: event.target.value })} placeholder="player@example.com" /><small>Optional contact address for the player.</small></label>
        <div className="form-row"><label>Category *<select value={form.role} onChange={(event) => setForm({ ...form, role: event.target.value })}><option>Batter</option><option>Bowler</option><option>All-Rounder</option><option>Wicket-Keeper</option></select></label><label>Base price<input type="number" min="0" step="any" value={form.basePrice} onChange={(event) => setForm({ ...form, basePrice: event.target.value })} placeholder={`Optional · defaults to ${formatMoney(0, tournament.currencyCode)}`} /></label></div>
        <label>City<input maxLength="120" value={form.city} onChange={(event) => setForm({ ...form, city: event.target.value })} placeholder="City or region" /></label>
        <div className="modal-actions"><button type="button" className="button button-ghost" onClick={() => setShowForm(false)}>Cancel</button><button className="button button-primary" type="submit" disabled={workspaceLoading}><Plus size={16} /> Add player</button></div>
      </form></div>}
    </main>
  );
}
