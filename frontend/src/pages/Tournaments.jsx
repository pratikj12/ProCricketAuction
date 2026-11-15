import { Fragment, useEffect, useState } from 'react';
import { CalendarDays, MapPin, Plus, Shield, Trash2, Trophy } from 'lucide-react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { formatMoney, useAuctionData } from '../context/AuctionDataContext.jsx';
import { currencyOptions } from '../utils/currencyOptions.js';
import tournamentService from '../services/tournamentService.js';

const emptySeason = {
  name: '',
  startDate: '',
  endDate: '',
  venue: '',
  status: 'Upcoming'
};

export default function Tournaments() {
  const location = useLocation();
  const navigate = useNavigate();
  const {
    tournament, seasons, selectedSeasonId, teams, players, dispatch, selectSeason, selectTournament, workspaceLoading, tournaments
  } = useAuctionData();
  const [formType, setFormType] = useState('');
  const [expandedSeasonId, setExpandedSeasonId] = useState(null);
  const [seasonIdToEdit, setSeasonIdToEdit] = useState(null);
  const [rosterSources, setRosterSources] = useState([]);
  const [rosterLoading, setRosterLoading] = useState(false);
  const [rosterError, setRosterError] = useState('');
  const [sourceTournamentId, setSourceTournamentId] = useState('');
  const [sourceSeasonId, setSourceSeasonId] = useState('');
  const [selectedTeamIds, setSelectedTeamIds] = useState([]);
  const [selectedPlayerIds, setSelectedPlayerIds] = useState([]);
  const [seasonForm, setSeasonForm] = useState(emptySeason);
  const [leagueForm, setLeagueForm] = useState({
    name: '',
    venue: '',
    season: String(new Date().getFullYear()),
    currencyCode: 'INR',
    defaultTeamPurse: '100000'
  });

  useEffect(() => {
    if (!location.state?.openNewTournament) return;
    openCreateTournament();
    navigate(location.pathname, { replace: true, state: null });
  }, [location.key, location.pathname, location.state, navigate]);

  async function loadRosterSources(preferredTournamentId, preferredSeasonId) {
    setRosterLoading(true);
    setRosterError('');
    setSelectedTeamIds([]);
    setSelectedPlayerIds([]);
    try {
      const workspaces = await tournamentService.list();
      setRosterSources(workspaces);
      const sourceWorkspace = workspaces.find((workspace) => workspace.tournament.id === preferredTournamentId)
        ?? workspaces.find((workspace) => workspace.teams.length || workspace.players.length)
        ?? workspaces[0];
      const sourceSeason = sourceWorkspace?.seasons.find((season) => season.id === preferredSeasonId)
        ?? sourceWorkspace?.seasons[0];
      setSourceTournamentId(sourceWorkspace?.tournament.id ?? '');
      setSourceSeasonId(sourceSeason?.id ?? '');
    } catch (error) {
      setRosterError(error.response?.data?.detail || error.message || 'Could not load registered teams and players.');
    } finally {
      setRosterLoading(false);
    }
  }

  function openCreateTournament() {
    setFormType('league');
    void loadRosterSources(tournament.id, selectedSeasonId);
  }

  function openCreateSeason() {
    setSeasonIdToEdit(null);
    setSeasonForm({ ...emptySeason, venue: tournament.venue || '' });
    setFormType('season');
    void loadRosterSources(tournament.id, selectedSeasonId);
  }

  function openEditSeason(season) {
    setSeasonIdToEdit(season.id);
    setSeasonForm({
      name: season.name,
      startDate: season.startDate ?? '',
      endDate: season.endDate ?? '',
      venue: season.venue ?? '',
      status: season.status
    });
    setFormType('season');
  }

  const sourceWorkspace = rosterSources.find((workspace) => workspace.tournament.id === sourceTournamentId);
  const sourceTeams = sourceWorkspace?.teams.filter((team) => team.seasonId === sourceSeasonId) ?? [];
  const sourcePlayers = sourceWorkspace?.players.filter((player) => player.seasonId === sourceSeasonId) ?? [];

  function getSelectedRoster() {
    return {
      sourceTournamentId,
      sourceSeasonId,
      teamIds: selectedTeamIds,
      playerIds: selectedPlayerIds
    };
  }

  function toggleSelection(id, selected, setSelected) {
    setSelected((current) => selected ? [...current, id] : current.filter((item) => item !== id));
  }

  function selectAll(items, selected, setSelected) {
    setSelected(selected ? items.map((item) => item.id) : []);
  }

  function renderRosterPicker() {
    const availableSources = rosterSources.filter((workspace) => workspace.seasons.length
      && (workspace.teams.length || workspace.players.length));

    return (
      <section className="roster-copy-picker">
        <div className="roster-copy-heading"><div><b>Reuse existing registrations (optional)</b><small>Choose the teams and players to copy into this roster.</small></div><span className="roster-copy-selection">{selectedTeamIds.length} teams · {selectedPlayerIds.length} players</span></div>
        {rosterLoading ? <p className="roster-copy-message">Loading your registered teams and players…</p>
          : rosterError ? <p className="roster-copy-error" role="alert">{rosterError} You can still create the {formType === 'season' ? 'season' : 'tournament'} without copying.</p>
            : availableSources.length ? <>
              <div className="form-row roster-copy-source"><label>Copy from tournament<select value={sourceTournamentId} onChange={(event) => {
                const selected = rosterSources.find((workspace) => workspace.tournament.id === event.target.value);
                setSourceTournamentId(event.target.value);
                setSourceSeasonId(selected?.seasons[0]?.id ?? '');
                setSelectedTeamIds([]);
                setSelectedPlayerIds([]);
              }}>{availableSources.map((workspace) => <option key={workspace.tournament.id} value={workspace.tournament.id}>{workspace.tournament.name}</option>)}</select></label><label>Source season<select value={sourceSeasonId} onChange={(event) => {
                setSourceSeasonId(event.target.value);
                setSelectedTeamIds([]);
                setSelectedPlayerIds([]);
              }}>{sourceWorkspace?.seasons.map((season) => <option key={season.id} value={season.id}>{season.name}</option>)}</select></label></div>
              <div className="roster-copy-columns">
                <div className="roster-copy-list">
                  <div className="roster-copy-list-heading"><b>Teams <span>{sourceTeams.length}</span></b><label><input type="checkbox" checked={sourceTeams.length > 0 && selectedTeamIds.length === sourceTeams.length} onChange={(event) => selectAll(sourceTeams, event.target.checked, setSelectedTeamIds)} /> Select all</label></div>
                  <div className="roster-copy-scroll">{sourceTeams.map((team) => <label className="roster-copy-option" key={team.id}><input type="checkbox" checked={selectedTeamIds.includes(team.id)} onChange={(event) => toggleSelection(team.id, event.target.checked, setSelectedTeamIds)} /><span className="roster-copy-color" style={{ background: team.color }} /><span><b>{team.name}</b><small>{team.shortName} · {team.captain}</small></span></label>)}{!sourceTeams.length && <p className="roster-copy-message">No teams in this source season.</p>}</div>
                </div>
                <div className="roster-copy-list">
                  <div className="roster-copy-list-heading"><b>Players <span>{sourcePlayers.length}</span></b><label><input type="checkbox" checked={sourcePlayers.length > 0 && selectedPlayerIds.length === sourcePlayers.length} onChange={(event) => selectAll(sourcePlayers, event.target.checked, setSelectedPlayerIds)} /> Select all</label></div>
                  <div className="roster-copy-scroll">{sourcePlayers.map((player) => <label className="roster-copy-option" key={player.id}><input type="checkbox" checked={selectedPlayerIds.includes(player.id)} onChange={(event) => toggleSelection(player.id, event.target.checked, setSelectedPlayerIds)} /><span className="roster-copy-player-mark">{player.name.split(' ').map((part) => part[0]).slice(0, 2).join('')}</span><span><b>{player.name}</b><small>{player.role} · {formatMoney(player.basePrice, sourceWorkspace.tournament.currencyCode)}</small></span></label>)}{!sourcePlayers.length && <p className="roster-copy-message">No players in this source season.</p>}</div>
                </div>
              </div>
              <p className="roster-copy-note">Copied teams start with the full purse set for the destination tournament. Copied players are reset to Available and are not assigned to a team, ready for a fresh auction.</p>
            </> : <p className="roster-copy-message">No other team or player registrations are available to copy yet.</p>}
      </section>
    );
  }

  async function saveLeague(event) {
    event.preventDefault();
    const defaultTeamPurse = Number(leagueForm.defaultTeamPurse);
    if (!Number.isSafeInteger(defaultTeamPurse) || defaultTeamPurse < 1) {
      toast.error(`Enter a starting purse of at least ${formatMoney(1, leagueForm.currencyCode)}.`);
      return;
    }
    try {
      await dispatch({
        type: 'update-tournament',
        tournament: { ...leagueForm, defaultTeamPurse, name: leagueForm.name.trim() },
        roster: getSelectedRoster()
      });
      toast.success(`${leagueForm.name.trim()} tournament created with its first season${selectedTeamIds.length || selectedPlayerIds.length ? ' and selected registrations copied' : ''}.`);
      setFormType('');
    } catch (error) {
      toast.error(error.response?.data?.detail || error.message || 'Could not save the league.');
    }
  }

  async function saveSeason(event) {
    event.preventDefault();
    const action = seasonIdToEdit ? 'update-season' : 'create-season';
    try {
      await dispatch({
        type: action,
        seasonId: seasonIdToEdit,
        season: { ...seasonForm, name: seasonForm.name.trim() },
        roster: action === 'create-season' ? getSelectedRoster() : undefined
      });
      toast.success(seasonIdToEdit ? 'Season details updated.' : `Season created${selectedTeamIds.length || selectedPlayerIds.length ? ' and selected registrations copied' : ''}.`);
      setFormType('');
    } catch (error) {
      toast.error(error.response?.data?.detail || error.response?.data?.title || error.message || 'Could not save the season.');
    }
  }

  async function deleteSeason(season) {
    if (!window.confirm(`Delete ${season.name}? Seasons containing teams or players cannot be deleted.`)) return;
    try {
      await dispatch({ type: 'delete-season', seasonId: season.id });
      toast.success(`${season.name} deleted.`);
    } catch (error) {
      toast.error(error.response?.data?.detail || error.response?.data?.title || error.message || 'Could not delete the season.');
    }
  }

  function toggleSeasonDetails(seasonId) {
    setExpandedSeasonId((current) => current === seasonId ? null : seasonId);
    if (seasonId !== selectedSeasonId) selectSeason(seasonId);
  }

  function openTournament(tournamentId) {
    setExpandedSeasonId(null);
    selectTournament(tournamentId);
  }

  return (
    <main>
      <div className="page-title-row">
        <div>
          <div className="eyebrow"><span className="eyebrow-line" />LEAGUE MANAGEMENT</div>
          <h1>Leagues & seasons</h1>
          <p className="subheading">Create multiple tournaments and keep each season’s squads and player pool together.</p>
        </div>
        <div className="toolbar-actions">
          {tournament.id && <button className="button button-primary" type="button" disabled={workspaceLoading} onClick={openCreateSeason}><Plus size={16} /> Add season</button>}
        </div>
      </div>

      {tournaments.length > 0 && <section className="league-list-section">
        <div className="section-heading"><div><h2>Your tournaments</h2><p>Each tournament has independent seasons, teams, and player registrations.</p></div><span>{tournaments.length} total</span></div>
        <div className="league-card-grid">{tournaments.map((item) => <button type="button" key={item.id} className={`league-card${item.id === tournament.id ? ' is-active' : ''}`} onClick={() => item.id !== tournament.id && openTournament(item.id)} aria-current={item.id === tournament.id ? 'true' : undefined}>
          <span className="league-card-icon"><Trophy size={18} /></span>
          <span className="league-card-copy"><b>{item.name}</b><small>{item.venue || 'Venue not set'}{item.season ? ` · ${item.season}` : ''}</small></span>
          <span className="league-card-state">{item.id === tournament.id ? 'Selected' : 'Open'}</span>
        </button>)}</div>
      </section>}

      {!tournament.id ? (
        <section className="panel tournament-empty">
          <div className="cover-ball"><Trophy size={27} /></div>
          <h2>{tournaments.length ? 'Choose a tournament' : 'No tournaments yet'}</h2>
          <p>{tournaments.length ? 'Select a tournament above to view its seasons, teams, and players.' : 'Create your first tournament to start managing seasons, teams, and player registrations.'}</p>
          {!tournaments.length && <button className="button button-primary" disabled={workspaceLoading} onClick={openCreateTournament}><Plus size={16} /> Create your first tournament</button>}
        </section>
      ) : (
        <>
          <section className="season-league-banner panel">
            <div className="season-league-icon"><Trophy size={21} /></div>
            <div className="season-league-copy">
              <span>ACTIVE LEAGUE</span>
              <h2>{tournament.name}</h2>
              <p>{tournament.venue || 'Venue not set'} <i>·</i> Starting purse {formatMoney(tournament.defaultTeamPurse ?? 100000, tournament.currencyCode)} per team</p>
            </div>
            <div className="season-league-metrics">
              <span><b>{seasons.length}</b> seasons</span>
              <span><b>{tournaments.length}</b> leagues in account</span>
            </div>
          </section>

          <section className="panel season-table-panel">
            <div className="panel-heading">
              <div><h3>Season manager</h3><p>Each season has its own teams and player registrations.</p></div>
              <span className="result-count">{seasons.length} seasons</span>
            </div>
            <div className="table-scroll">
              <table>
                <thead><tr><th>SEASON</th><th>DATES</th><th>VENUE</th><th>TEAMS</th><th>PLAYERS</th><th>STATUS</th><th>ACTIONS</th></tr></thead>
                <tbody>{seasons.map((season) => {
                  const selected = season.id === selectedSeasonId;
                  const dates = [season.startDate, season.endDate].filter(Boolean);
                  return (
                    <Fragment key={season.id}>
                    <tr className={selected ? 'season-row-selected' : ''}>
                      <td><button type="button" className="season-name-button" onClick={() => toggleSeasonDetails(season.id)} aria-expanded={expandedSeasonId === season.id}><CalendarDays size={16} /><span><b>{season.name}</b><small>{expandedSeasonId === season.id ? 'Hide season details' : 'Click to view details'}</small></span></button></td>
                      <td>{dates.length ? dates.join(' — ') : 'Dates not set'}</td>
                      <td><span className="season-venue"><MapPin size={14} />{season.venue || '—'}</span></td>
                      <td>{season.teamCount ?? teams.filter((team) => team.seasonId === season.id).length}</td>
                      <td>{season.playerCount ?? players.filter((player) => player.seasonId === season.id).length}</td>
                      <td><span className={`status-pill ${season.status.toLowerCase()}`}>{season.status}</span></td>
                      <td><div className="season-row-actions"><button type="button" className="button button-ghost" onClick={() => openEditSeason(season)}>Edit</button><button type="button" className="icon-button" onClick={() => deleteSeason(season)} aria-label={`Delete ${season.name}`} title="Delete empty season"><Trash2 size={15} /></button></div></td>
                    </tr>
                    {expandedSeasonId === season.id && <tr key={`${season.id}-details`} className="season-details-row"><td colSpan="7">
                      <section className="season-details-panel" aria-label={`${season.name} details`}>
                        <div className="season-details-heading"><div><span>SEASON DETAILS</span><h3>{season.name}</h3></div><span className={`status-pill ${season.status.toLowerCase()}`}>{season.status}</span></div>
                        <div className="season-detail-facts"><div><small>DATES</small><b>{dates.length ? dates.join(' — ') : 'Not set'}</b></div><div><small>VENUE</small><b>{season.venue || 'Not set'}</b></div><div><small>TEAMS</small><b>{season.teamCount ?? teams.filter((team) => team.seasonId === season.id).length}</b></div><div><small>PLAYERS</small><b>{season.playerCount ?? players.filter((player) => player.seasonId === season.id).length}</b></div><div><small>PLAYERS SOLD</small><b>{season.soldPlayerCount ?? 0}</b></div><div><small>AUCTION SPEND</small><b>{formatMoney(season.totalSpend ?? 0, tournament.currencyCode)}</b></div></div>
                        {selectedSeasonId === season.id ? <>
                          <div className="season-detail-rosters">
                            <div><h4>Teams <span>{teams.length}</span></h4>{teams.length ? <ul>{teams.map((team) => <li key={team.id}><b>{team.name}</b><small>{team.shortName} · {team.captain || 'Captain not set'}</small></li>)}</ul> : <p>No teams in this season yet.</p>}</div>
                            <div><h4>Players <span>{players.length}</span></h4>{players.length ? <ul>{players.slice(0, 8).map((player) => <li key={player.id}><b>{player.name}</b><small>{player.role} · {player.status}</small></li>)}</ul> : <p>No players in this season yet.</p>}</div>
                          </div>
                          {players.length > 8 && <p className="season-details-more">Showing 8 players. Open Players to view the complete roster.</p>}
                          <div className="season-details-links"><Link to="/teams">Manage teams</Link><Link to="/players">Manage players</Link><Link to="/auction/events">Auction events</Link></div>
                        </> : <p className="season-switching-note">Switching to this season’s workspace…</p>}
                      </section>
                    </td></tr>}
                    </Fragment>
                  );
                })}</tbody>
              </table>
            </div>
            {!seasons.length && <div className="table-empty">No seasons are configured for this league.</div>}
          </section>

          <section className="season-scope-note"><Shield size={17} /><span><b>Season-safe rosters.</b> Switching seasons changes the teams and player registrations shown throughout the workspace. Existing season data is retained.</span></section>
        </>
      )}

      {formType === 'league' && <div className="modal-backdrop" onClick={() => setFormType('')}><form className="modal-card modal-card-roster" onSubmit={saveLeague} onClick={(event) => event.stopPropagation()}>
        <div className="modal-heading"><div><h2>Create tournament</h2><p>Each tournament starts with its own first season.</p></div><button type="button" className="icon-button" onClick={() => setFormType('')} aria-label="Close">×</button></div>
        <label>League name *<input autoFocus required minLength="2" maxLength="160" value={leagueForm.name} onChange={(event) => setLeagueForm({ ...leagueForm, name: event.target.value })} placeholder="e.g. City Champions League" /></label>
        <div className="form-row"><label>First season<input required maxLength="80" value={leagueForm.season} onChange={(event) => setLeagueForm({ ...leagueForm, season: event.target.value })} /></label><label>League venue<input maxLength="160" value={leagueForm.venue} onChange={(event) => setLeagueForm({ ...leagueForm, venue: event.target.value })} placeholder="Ground or city" /></label></div>
        <div className="form-row"><label>Currency<select value={leagueForm.currencyCode} onChange={(event) => setLeagueForm({ ...leagueForm, currencyCode: event.target.value })}>{currencyOptions.map(({ code, label }) => <option key={code} value={code}>{label}</option>)}</select></label><label>Starting purse per team<input required type="number" min="1" step="1" value={leagueForm.defaultTeamPurse} onChange={(event) => setLeagueForm({ ...leagueForm, defaultTeamPurse: event.target.value })} /></label></div>
        {renderRosterPicker()}
        <div className="modal-actions"><button type="button" className="button button-ghost" onClick={() => setFormType('')}>Cancel</button><button type="submit" className="button button-primary" disabled={workspaceLoading}>Create tournament</button></div>
      </form></div>}

      {formType === 'season' && <div className="modal-backdrop" onClick={() => setFormType('')}><form className="modal-card modal-card-roster" onSubmit={saveSeason} onClick={(event) => event.stopPropagation()}>
        <div className="modal-heading"><div><h2>{seasonIdToEdit ? 'Edit season' : 'Add season'}</h2><p>Season rosters and auction preparation stay separate.</p></div><button type="button" className="icon-button" onClick={() => setFormType('')} aria-label="Close">×</button></div>
        <label>Season name *<input autoFocus required minLength="2" maxLength="80" value={seasonForm.name} onChange={(event) => setSeasonForm({ ...seasonForm, name: event.target.value })} placeholder="e.g. 2027 season" /></label>
        <div className="form-row"><label>Start date<input type="date" value={seasonForm.startDate} onChange={(event) => setSeasonForm({ ...seasonForm, startDate: event.target.value })} /></label><label>End date<input type="date" min={seasonForm.startDate || undefined} value={seasonForm.endDate} onChange={(event) => setSeasonForm({ ...seasonForm, endDate: event.target.value })} /></label></div>
        <div className="form-row"><label>Venue<input maxLength="160" value={seasonForm.venue} onChange={(event) => setSeasonForm({ ...seasonForm, venue: event.target.value })} /></label><label>Status<select value={seasonForm.status} onChange={(event) => setSeasonForm({ ...seasonForm, status: event.target.value })}><option>Upcoming</option><option>Active</option><option>Completed</option></select></label></div>
        {!seasonIdToEdit && renderRosterPicker()}
        <div className="modal-actions"><button type="button" className="button button-ghost" onClick={() => setFormType('')}>Cancel</button><button type="submit" className="button button-primary" disabled={workspaceLoading}>{seasonIdToEdit ? 'Save changes' : 'Create season'}</button></div>
      </form></div>}
    </main>
  );
}
