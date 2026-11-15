import { createContext, useCallback, useContext, useEffect, useMemo, useReducer, useRef, useState } from 'react';
import tournamentService from '../services/tournamentService.js';
import teamService from '../services/teamService.js';
import playerService from '../services/playerService.js';

const STORAGE_KEY_PREFIX = 'procricket-auction-demo-v1';
const AuctionDataContext = createContext(null);

export const initialAuctionData = {
  tournament: {
    id: null,
    name: '',
    season: '',
    seasonId: null,
    venue: '',
    currencyCode: 'INR',
    maximumTeams: null,
    maximumPlayers: null
  },
  status: 'Upcoming',
  bidIncrement: 2000,
  timerSeconds: 30,
  currentPlayerId: null,
  currentBid: 0,
  leadingTeamId: null,
  auctionEvents: [],
  players: [],
  teams: [],
  bids: []
};

const SAMPLE_PLAYER_IDS = new Set(['p1', 'p2', 'p3', 'p4', 'p5', 'p6']);
const SAMPLE_TEAM_IDS = new Set(['t1', 't2', 't3', 't4']);

export function removeSampleAuctionData(data) {
  const removedSampleData = (Array.isArray(data.players) ? data.players : []).some((player) => SAMPLE_PLAYER_IDS.has(player.id))
    || (Array.isArray(data.teams) ? data.teams : []).some((team) => SAMPLE_TEAM_IDS.has(team.id));
  const players = (Array.isArray(data.players) ? data.players : []).filter((player) => !SAMPLE_PLAYER_IDS.has(player.id));
  const teams = (Array.isArray(data.teams) ? data.teams : []).filter((team) => !SAMPLE_TEAM_IDS.has(team.id));
  const bids = (Array.isArray(data.bids) ? data.bids : []).filter((bid) =>
    !SAMPLE_PLAYER_IDS.has(bid.playerId) && !SAMPLE_TEAM_IDS.has(bid.teamId));
  const removedCurrentPlayer = SAMPLE_PLAYER_IDS.has(data.currentPlayerId);
  const removedLeadingTeam = SAMPLE_TEAM_IDS.has(data.leadingTeamId);
  const resetAuction = (removedSampleData && data.status === 'Live') || removedCurrentPlayer || removedLeadingTeam;

  return {
    ...initialAuctionData,
    ...data,
    bidIncrement: 2000,
    tournament: data.tournament?.id === 'summer-cup'
      ? initialAuctionData.tournament
      : { ...initialAuctionData.tournament, ...data.tournament },
    players,
    teams,
    bids,
    currentPlayerId: removedCurrentPlayer
      ? players.find((player) => player.status === 'Available')?.id ?? null
      : data.currentPlayerId,
    currentBid: resetAuction ? 0 : data.currentBid,
    leadingTeamId: resetAuction ? null : data.leadingTeamId,
    status: resetAuction
      ? 'Upcoming'
      : data.status ?? initialAuctionData.status
  };
}

function loadData(storageKey) {
  const saved = localStorage.getItem(storageKey);
  if (!saved) return initialAuctionData;
  const parsed = JSON.parse(saved);
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error('Saved auction demo data is invalid. Clear the procricket-auction-demo-v1 browser storage key and reload.');
  }
  return removeSampleAuctionData(parsed);
}

export function auctionDataReducer(state, action) {
  switch (action.type) {
    case 'replace':
      return action.value;
    case 'start':
      return { ...state, status: 'Live' };
    case 'pause':
      return { ...state, status: state.status === 'Live' ? 'Paused' : 'Live' };
    case 'bid': {
      const player = state.players.find((item) => item.id === state.currentPlayerId);
      const team = state.teams.find((item) => item.id === action.teamId);
      const minimumBid = state.currentBid ? state.currentBid + state.bidIncrement : player?.basePrice;
      const amount = action.amount ?? minimumBid;
      if (!player || !team || team.id === state.leadingTeamId || player.status !== 'Available'
        || !Number.isSafeInteger(amount) || amount < minimumBid || team.purse < amount || state.status !== 'Live') return state;
      const bid = { id: crypto.randomUUID(), playerId: player.id, teamId: team.id, amount, createdAt: new Date().toISOString() };
      return { ...state, currentBid: amount, leadingTeamId: team.id, bids: [bid, ...state.bids] };
    }
    case 'finish-player': {
      const player = state.players.find((item) => item.id === state.currentPlayerId);
      if (!player || player.status !== 'Available') return state;
      const sold = action.sold && state.leadingTeamId;
      const teams = sold
        ? state.teams.map((team) => team.id === state.leadingTeamId ? { ...team, purse: team.purse - state.currentBid } : team)
        : state.teams;
      const players = state.players.map((item) => item.id === player.id
        ? { ...item, status: sold ? 'Sold' : 'Unsold', teamId: sold ? state.leadingTeamId : null, soldPrice: sold ? state.currentBid : null }
        : item);
      return { ...state, teams, players, currentPlayerId: null, currentBid: 0, leadingTeamId: null };
    }
    case 'next-player': {
      const next = state.players.find((item) => item.status === 'Available');
      return next
        ? { ...state, currentPlayerId: next.id, currentBid: 0, leadingTeamId: null }
        : { ...state, currentPlayerId: null, status: 'Completed' };
    }
    case 'add-player': {
      const player = { ...action.player, id: crypto.randomUUID(), status: 'Available', teamId: null, soldPrice: null };
      return {
        ...state,
        players: [...state.players, player],
        currentPlayerId: state.currentPlayerId ?? player.id
      };
    }
    case 'add-player-persisted':
      return {
        ...state,
        teams: action.player.status === 'Sold'
          ? state.teams.map((team) => team.id === action.player.teamId
            ? { ...team, purse: team.purse - action.player.soldPrice }
            : team)
          : state.teams,
        players: [...state.players, action.player],
        currentPlayerId: state.currentPlayerId ?? action.player.id
      };
    case 'add-team':
      return { ...state, teams: [...state.teams, { ...action.team, id: crypto.randomUUID() }] };
    case 'add-team-persisted':
      return { ...state, teams: [...state.teams, action.team] };
    case 'delete-team':
      return {
        ...state,
        teams: state.teams.filter((team) => team.id !== action.teamId),
        players: state.players.map((player) => player.teamId === action.teamId
          ? { ...player, status: 'Available', teamId: null, soldPrice: null }
          : player),
        bids: state.bids.filter((bid) => bid.teamId !== action.teamId)
      };
    case 'update-tournament':
      return {
        ...state,
        tournament: { ...state.tournament, ...action.tournament, season: action.tournament.season || state.tournament.season }
      };
    case 'delete-tournament':
      return initialAuctionData;
    case 'reset':
      return initialAuctionData;
    default:
      return state;
  }
}

export function AuctionDataProvider({ children, storageScope = 'signed-out' }) {
  const storageKey = `${STORAGE_KEY_PREFIX}:${storageScope}`;
  const remoteTournamentKey = `${storageKey}:remote-tournament-id`;
  const remoteSeasonKey = `${storageKey}:remote-season-id`;
  const [state, reduce] = useReducer(auctionDataReducer, storageKey, loadData);
  const [tournaments, setTournaments] = useState([]);
  const [seasons, setSeasons] = useState([]);
  const [selectedSeasonId, setSelectedSeasonId] = useState(null);
  const [workspaceLoading, setWorkspaceLoading] = useState(storageScope !== 'signed-out');
  const [workspaceError, setWorkspaceError] = useState('');
  const refreshRequestId = useRef(0);
  const currentSelection = useRef({ tournamentId: state.tournament.id, seasonId: selectedSeasonId });
  currentSelection.current = { tournamentId: state.tournament.id, seasonId: selectedSeasonId };

  const refreshWorkspace = useCallback(async () => {
    if (storageScope === 'signed-out' || !state.tournament.id) return;
    const tournamentId = state.tournament.id;
    const seasonId = selectedSeasonId;
    const requestId = ++refreshRequestId.current;
    try {
      const workspaces = await tournamentService.list();
      const workspace = workspaces.find((item) => item.tournament.id === tournamentId);
      if (!workspace) throw new Error('The selected league could not be loaded.');
      if (requestId !== refreshRequestId.current
        || currentSelection.current.tournamentId !== tournamentId
        || currentSelection.current.seasonId !== seasonId) return;
      setTournaments(workspaces.map((item) => item.tournament));
      setSeasons(workspace.seasons ?? []);
      reduce({ type: 'replace', value: workspaceToAuctionData(workspace, seasonId) });
      setWorkspaceError('');
    } catch (error) {
      if (requestId !== refreshRequestId.current
        || currentSelection.current.tournamentId !== tournamentId
        || currentSelection.current.seasonId !== seasonId) return;
      setWorkspaceError(error.response?.data?.detail
        || error.message
        || 'Could not refresh saved tournament data from the server.');
      throw error;
    }
  }, [selectedSeasonId, state.tournament.id, storageScope]);

  useEffect(() => {
    if (storageScope === 'signed-out') {
      setWorkspaceLoading(false);
      return undefined;
    }

    let active = true;
    async function loadWorkspace() {
      setWorkspaceLoading(true);
      setWorkspaceError('');
      try {
        const legacyData = loadData(storageKey);
        let remoteWorkspaces = await tournamentService.list();

        if (remoteWorkspaces.length === 0 && legacyData.tournament?.name) {
          const legacyTeams = legacyData.teams ?? [];
          const legacyPlayers = legacyData.players ?? [];
          const importedWorkspace = await tournamentService.import({
            tournament: {
              name: legacyData.tournament.name,
              season: legacyData.tournament.season ?? '',
              venue: legacyData.tournament.venue ?? '',
              defaultTeamPurse: legacyTeams[0]?.initialPurse ?? legacyTeams[0]?.purse ?? 100000
            },
            teams: legacyTeams.map((team) => ({
              legacyId: team.id,
              name: team.name,
              shortName: team.shortName,
              displayName: team.displayName ?? '',
              captain: team.captain,
              city: team.city ?? '',
              logoDataUrl: team.logoDataUrl ?? '',
              initialPurse: team.initialPurse ?? team.purse,
              purse: team.purse,
              color: team.color || '#FF6A00'
            })),
            players: legacyPlayers.map((player) => {
              const team = legacyTeams.find((item) => item.id === player.teamId);
              const sold = player.status === 'Sold' && team && Number.isFinite(player.soldPrice);
              return {
                legacyId: player.id,
                name: player.name,
                city: player.city ?? '',
                email: player.email ?? '',
                nationality: player.nationality ?? '',
                dateOfBirth: player.dateOfBirth ?? null,
                photoDataUrl: player.photoDataUrl ?? '',
                role: player.role ?? 'Batter',
                basePrice: player.basePrice ?? 0,
                status: sold || player.status === 'Unsold' ? player.status : 'Available',
                teamLegacyId: sold ? team.id : null,
                soldPrice: sold ? player.soldPrice : null
              };
            })
          });
          localStorage.setItem(remoteTournamentKey, importedWorkspace.tournament.id);
          remoteWorkspaces = await tournamentService.list();
        }

        if (!active) return;
        const savedId = localStorage.getItem(remoteTournamentKey);
        const selectedWorkspace = remoteWorkspaces.find((item) => item.tournament.id === savedId) ?? remoteWorkspaces[0];
        setTournaments(remoteWorkspaces.map((item) => item.tournament));
        if (selectedWorkspace) {
          localStorage.setItem(remoteTournamentKey, selectedWorkspace.tournament.id);
          const savedSeasonId = localStorage.getItem(`${remoteSeasonKey}:${selectedWorkspace.tournament.id}`);
          const selectedSeason = selectedWorkspace.seasons?.find((season) => season.id === savedSeasonId)
            ?? selectedWorkspace.seasons?.[0];
          setSeasons(selectedWorkspace.seasons ?? []);
          setSelectedSeasonId(selectedSeason?.id ?? null);
          if (selectedSeason) {
            localStorage.setItem(`${remoteSeasonKey}:${selectedWorkspace.tournament.id}`, selectedSeason.id);
          }
          reduce({ type: 'replace', value: workspaceToAuctionData(selectedWorkspace, selectedSeason?.id) });
        } else {
          localStorage.removeItem(remoteTournamentKey);
          setSeasons([]);
          setSelectedSeasonId(null);
          reduce({ type: 'replace', value: initialAuctionData });
        }
      } catch (error) {
        if (active) {
          setWorkspaceError(error.response?.data?.detail || 'Could not load your saved tournament data. Check the API connection and try again.');
        }
      } finally {
        if (active) setWorkspaceLoading(false);
      }
    }

    void loadWorkspace();
    return () => { active = false; };
  }, [remoteSeasonKey, remoteTournamentKey, storageKey, storageScope]);

  useEffect(() => {
    if (storageScope === 'signed-out' || !state.tournament.id) return undefined;
    const sync = () => { void refreshWorkspace().catch(() => {}); };
    const interval = window.setInterval(sync, 30000);
    window.addEventListener('focus', sync);
    return () => {
      window.clearInterval(interval);
      window.removeEventListener('focus', sync);
    };
  }, [refreshWorkspace, state.tournament.id, storageScope]);

  useEffect(() => {
    localStorage.setItem(storageKey, JSON.stringify(state));
  }, [state, storageKey]);

  useEffect(() => {
    const refreshAfterPurchase = (event) => {
      const { tournamentId, seasonId } = event.detail ?? {};
      if (!tournamentId || !seasonId || tournamentId !== state.tournament.id
        || seasonId !== selectedSeasonId) return;
      void refreshWorkspace().catch(() => {});
    };
    window.addEventListener('auction-purchase-committed', refreshAfterPurchase);
    return () => window.removeEventListener('auction-purchase-committed', refreshAfterPurchase);
  }, [refreshWorkspace, selectedSeasonId, state.tournament.id]);

  useEffect(() => {
    if (!selectedSeasonId || !state.tournament.id) return;
    const seasonPlayers = state.players.filter((player) => player.seasonId === selectedSeasonId);
    setSeasons((current) => current.map((season) => {
      if (season.id !== selectedSeasonId) return season;
      const soldPlayers = seasonPlayers.filter((player) => player.status === 'Sold');
      return {
        ...season,
        teamCount: state.teams.filter((team) => team.seasonId === selectedSeasonId).length,
        playerCount: seasonPlayers.length,
        soldPlayerCount: soldPlayers.length,
        availablePlayerCount: seasonPlayers.filter((player) => player.status === 'Available').length,
        totalSpend: soldPlayers.reduce((sum, player) => sum + (player.soldPrice || 0), 0)
      };
    }));
  }, [selectedSeasonId, state.players, state.teams, state.tournament.id]);

  useEffect(() => {
    const syncFromOtherTab = (event) => {
      if (event.key === storageKey && event.newValue) {
        window.dispatchEvent(new CustomEvent('auction-storage-sync', { detail: JSON.parse(event.newValue) }));
      }
    };
    window.addEventListener('storage', syncFromOtherTab);
    const sync = (event) => reduce({ type: 'replace', value: event.detail });
    window.addEventListener('auction-storage-sync', sync);
    return () => {
      window.removeEventListener('storage', syncFromOtherTab);
      window.removeEventListener('auction-storage-sync', sync);
    };
  }, [storageKey]);

  const selectTournament = useCallback((tournamentId) => {
    const selected = tournaments.find((item) => item.id === tournamentId);
    if (!selected) return;
    localStorage.setItem(remoteTournamentKey, selected.id);
    const reload = async () => {
      const all = await tournamentService.list();
      const selectedWorkspace = all.find((item) => item.tournament.id === selected.id);
      if (selectedWorkspace) {
        const savedSeasonId = localStorage.getItem(`${remoteSeasonKey}:${selected.id}`);
        const selectedSeason = selectedWorkspace.seasons?.find((season) => season.id === savedSeasonId)
          ?? selectedWorkspace.seasons?.[0];
        setSeasons(selectedWorkspace.seasons ?? []);
        setSelectedSeasonId(selectedSeason?.id ?? null);
        if (selectedSeason) localStorage.setItem(`${remoteSeasonKey}:${selected.id}`, selectedSeason.id);
        reduce({ type: 'replace', value: workspaceToAuctionData(selectedWorkspace, selectedSeason?.id) });
      }
    };
    void reload().catch((error) => setWorkspaceError(error.response?.data?.detail || 'Could not load this tournament.'));
    return undefined;
  }, [remoteSeasonKey, remoteTournamentKey, tournaments]);

  const selectSeason = useCallback((seasonId) => {
    if (!state.tournament.id || !seasons.some((season) => season.id === seasonId)) return;
    const reload = async () => {
      const all = await tournamentService.list();
      const selectedWorkspace = all.find((item) => item.tournament.id === state.tournament.id);
      const selectedSeason = selectedWorkspace?.seasons?.find((season) => season.id === seasonId);
      if (!selectedWorkspace || !selectedSeason) return;
      localStorage.setItem(`${remoteSeasonKey}:${state.tournament.id}`, seasonId);
      setSeasons(selectedWorkspace.seasons ?? []);
      setSelectedSeasonId(seasonId);
      reduce({ type: 'replace', value: workspaceToAuctionData(selectedWorkspace, seasonId) });
    };
    void reload().catch((error) => setWorkspaceError(error.response?.data?.detail || 'Could not load this season.'));
  }, [remoteSeasonKey, seasons, state.tournament.id]);

  const dispatch = useCallback(async (action) => {
    if (storageScope !== 'signed-out' && !workspaceLoading && workspaceError) {
      throw new Error(workspaceError);
    }

    if (action.type === 'update-tournament' && storageScope !== 'signed-out') {
      const created = await tournamentService.create({
        name: action.tournament.name,
        season: action.tournament.season ?? '',
        venue: action.tournament.venue ?? '',
        defaultTeamPurse: Number(action.tournament.defaultTeamPurse) || 100000,
        currencyCode: action.tournament.currencyCode ?? 'INR',
        maximumTeams: action.tournament.maximumTeams ?? null,
        maximumPlayers: action.tournament.maximumPlayers ?? null
      });
      const createdWorkspaces = await tournamentService.list();
      const createdWorkspace = createdWorkspaces.find((item) => item.tournament.id === created.id);
      if (!createdWorkspace) throw new Error('The new tournament was created but its first season could not be loaded.');
      const createdSeason = createdWorkspace.seasons?.[0];
      localStorage.setItem(remoteTournamentKey, created.id);
      setTournaments(createdWorkspaces.map((item) => item.tournament));
      setSeasons(createdWorkspace.seasons ?? []);
      setSelectedSeasonId(createdSeason?.id ?? null);
      if (createdSeason) localStorage.setItem(`${remoteSeasonKey}:${created.id}`, createdSeason.id);
      reduce({ type: 'replace', value: workspaceToAuctionData(createdWorkspace, createdSeason?.id) });
      if (action.roster && (action.roster.teamIds.length || action.roster.playerIds.length)) {
        if (!createdSeason) throw new Error('Tournament created, but it has no first season for the selected roster.');
        try {
          await tournamentService.copyRoster(created.id, createdSeason.id, action.roster);
        } catch (error) {
          throw new Error(`Tournament created, but registrations could not be copied: ${error.response?.data?.detail || error.message}`);
        }
        const workspaces = await tournamentService.list();
        const refreshedWorkspace = workspaces.find((item) => item.tournament.id === created.id);
        if (!refreshedWorkspace) throw new Error('Tournament and roster were created but could not be reloaded.');
        setTournaments(workspaces.map((item) => item.tournament));
        setSeasons(refreshedWorkspace.seasons ?? []);
        reduce({ type: 'replace', value: workspaceToAuctionData(refreshedWorkspace, createdSeason.id) });
      }
      return created;
    }
    if (action.type === 'update-league-settings' && storageScope !== 'signed-out') {
      const updated = await tournamentService.update(state.tournament.id, {
        name: action.tournament.name,
        season: action.tournament.season ?? '',
        venue: action.tournament.venue ?? '',
        defaultTeamPurse: Number(action.tournament.defaultTeamPurse),
        currencyCode: action.tournament.currencyCode,
        maximumTeams: action.tournament.maximumTeams || null,
        maximumPlayers: action.tournament.maximumPlayers || null
      });
      const workspaces = await tournamentService.list();
      const updatedWorkspace = workspaces.find((item) => item.tournament.id === updated.id);
      if (!updatedWorkspace) throw new Error('League settings were saved but the league could not be reloaded.');
      setTournaments(workspaces.map((item) => item.tournament));
      setSeasons(updatedWorkspace.seasons ?? []);
      reduce({ type: 'replace', value: workspaceToAuctionData(updatedWorkspace, selectedSeasonId) });
      return updated;
    }
    if (action.type === 'create-season' && storageScope !== 'signed-out') {
      if (!state.tournament.id) throw new Error('Create a league before adding seasons.');
      const createdSeason = await tournamentService.createSeason(state.tournament.id, action.season);
      let workspaces = await tournamentService.list();
      let updatedWorkspace = workspaces.find((item) => item.tournament.id === state.tournament.id);
      if (!updatedWorkspace) throw new Error('The season was created but the league could not be reloaded.');
      setSeasons(updatedWorkspace.seasons ?? []);
      setSelectedSeasonId(createdSeason.id);
      localStorage.setItem(`${remoteSeasonKey}:${state.tournament.id}`, createdSeason.id);
      reduce({ type: 'replace', value: workspaceToAuctionData(updatedWorkspace, createdSeason.id) });
      if (action.roster && (action.roster.teamIds.length || action.roster.playerIds.length)) {
        try {
          await tournamentService.copyRoster(state.tournament.id, createdSeason.id, action.roster);
        } catch (error) {
          throw new Error(`Season created, but registrations could not be copied: ${error.response?.data?.detail || error.message}`);
        }
        workspaces = await tournamentService.list();
        updatedWorkspace = workspaces.find((item) => item.tournament.id === state.tournament.id);
        if (!updatedWorkspace) throw new Error('Season and roster were created but the league could not be reloaded.');
        setSeasons(updatedWorkspace.seasons ?? []);
        reduce({ type: 'replace', value: workspaceToAuctionData(updatedWorkspace, createdSeason.id) });
      }
      return createdSeason;
    }
    if (action.type === 'copy-roster' && storageScope !== 'signed-out') {
      if (!action.tournamentId || !action.seasonId) throw new Error('Select a destination tournament and season.');
      const result = await tournamentService.copyRoster(action.tournamentId, action.seasonId, action.roster);
      const workspaces = await tournamentService.list();
      const updatedWorkspace = workspaces.find((item) => item.tournament.id === action.tournamentId);
      if (!updatedWorkspace) throw new Error('The roster was copied but the destination tournament could not be reloaded.');
      setTournaments(workspaces.map((item) => item.tournament));
      if (state.tournament.id === action.tournamentId) {
        const selectedSeason = updatedWorkspace.seasons.find((season) => season.id === action.seasonId);
        setSeasons(updatedWorkspace.seasons ?? []);
        if (selectedSeason) {
          setSelectedSeasonId(action.seasonId);
          reduce({ type: 'replace', value: workspaceToAuctionData(updatedWorkspace, action.seasonId) });
        }
      }
      return result;
    }
    if (action.type === 'update-season' && storageScope !== 'signed-out') {
      const updatedSeason = await tournamentService.updateSeason(
        state.tournament.id, action.seasonId, action.season);
      const workspaces = await tournamentService.list();
      const updatedWorkspace = workspaces.find((item) => item.tournament.id === state.tournament.id);
      if (!updatedWorkspace) throw new Error('The season was updated but the league could not be reloaded.');
      setSeasons(updatedWorkspace.seasons ?? []);
      setSelectedSeasonId(updatedSeason.id);
      reduce({ type: 'replace', value: workspaceToAuctionData(updatedWorkspace, updatedSeason.id) });
      return updatedSeason;
    }
    if (action.type === 'delete-season' && storageScope !== 'signed-out') {
      await tournamentService.removeSeason(state.tournament.id, action.seasonId);
      const workspaces = await tournamentService.list();
      const updatedWorkspace = workspaces.find((item) => item.tournament.id === state.tournament.id);
      if (!updatedWorkspace) throw new Error('The season was deleted but the league could not be reloaded.');
      const nextSeason = updatedWorkspace.seasons?.[0];
      setSeasons(updatedWorkspace.seasons ?? []);
      setSelectedSeasonId(nextSeason?.id ?? null);
      if (nextSeason) localStorage.setItem(`${remoteSeasonKey}:${state.tournament.id}`, nextSeason.id);
      reduce({ type: 'replace', value: workspaceToAuctionData(updatedWorkspace, nextSeason?.id) });
      return;
    }
    if (action.type === 'add-team' && storageScope !== 'signed-out') {
      if (!state.tournament.id) throw new Error('Create a tournament before adding teams.');
      const created = await teamService.create(state.tournament.id, {
        ...action.team,
        seasonId: selectedSeasonId
      });
      const team = {
        id: created.id,
        seasonId: created.seasonId,
        name: created.name,
        shortName: created.shortName,
        displayName: created.displayName,
        captain: created.captain,
        city: created.city,
        logoDataUrl: created.logoDataUrl,
        purse: created.purse,
        initialPurse: created.initialPurse,
        color: created.color
      };
      reduce({ type: 'add-team-persisted', team });
      return team;
    }
    if (action.type === 'delete-team' && storageScope !== 'signed-out') {
      if (!state.tournament.id) throw new Error('Create a tournament before deleting teams.');
      await teamService.remove(state.tournament.id, action.teamId);
      reduce(action);
      return;
    }
    if (action.type === 'add-player' && storageScope !== 'signed-out') {
      if (!state.tournament.id) throw new Error('Create a tournament before adding players.');
      const created = await playerService.create(state.tournament.id, {
        ...action.player,
        seasonId: selectedSeasonId
      });
      const player = {
        id: created.id,
        seasonId: created.seasonId,
        name: created.name,
        city: created.city,
        email: created.email,
        nationality: created.nationality,
        dateOfBirth: created.dateOfBirth,
        photoDataUrl: created.photoDataUrl,
        role: created.role,
        basePrice: created.basePrice,
        status: created.status,
        teamId: created.teamId,
        soldPrice: created.soldPrice
      };
      reduce({ type: 'add-player-persisted', player });
      return player;
    }
    if (action.type === 'delete-tournament' && storageScope !== 'signed-out') {
      if (!state.tournament.id) return;
      await tournamentService.remove(state.tournament.id);
      localStorage.removeItem(remoteTournamentKey);
      const remaining = await tournamentService.list();
      setTournaments(remaining.map((item) => item.tournament));
      if (remaining.length) {
        localStorage.setItem(remoteTournamentKey, remaining[0].tournament.id);
        const nextWorkspace = remaining[0];
        const nextSeason = nextWorkspace.seasons?.[0];
        setSeasons(nextWorkspace.seasons ?? []);
        setSelectedSeasonId(nextSeason?.id ?? null);
        if (nextSeason) localStorage.setItem(`${remoteSeasonKey}:${nextWorkspace.tournament.id}`, nextSeason.id);
        reduce({ type: 'replace', value: workspaceToAuctionData(nextWorkspace, nextSeason?.id) });
      } else {
        setSeasons([]);
        setSelectedSeasonId(null);
        reduce({ type: 'delete-tournament' });
      }
      return;
    }
    reduce(action);
  }, [remoteSeasonKey, remoteTournamentKey, selectedSeasonId, state.tournament.id, storageScope, workspaceError, workspaceLoading]);

  const value = useMemo(() => ({
    ...state,
    dispatch,
    tournaments,
    seasons,
    selectedSeasonId,
    selectTournament,
    selectSeason,
    refreshWorkspace,
    workspaceLoading,
    workspaceError
  }), [dispatch, refreshWorkspace, selectSeason, selectTournament, selectedSeasonId, seasons, state, tournaments, workspaceError, workspaceLoading]);
  return <AuctionDataContext.Provider value={value}>{children}</AuctionDataContext.Provider>;
}

export function workspaceToAuctionData(workspace, seasonId = workspace.seasons?.[0]?.id) {
  const tournament = workspace.tournament;
  const selectedSeason = workspace.seasons?.find((season) => season.id === seasonId);
  return {
    ...initialAuctionData,
    tournament: {
      id: tournament.id,
      name: tournament.name,
      season: selectedSeason?.name ?? tournament.season,
      seasonId: selectedSeason?.id ?? null,
      venue: selectedSeason?.venue ?? tournament.venue,
      defaultTeamPurse: tournament.defaultTeamPurse ?? 100000,
      currencyCode: tournament.currencyCode ?? 'INR',
      maximumTeams: tournament.maximumTeams ?? null,
      maximumPlayers: tournament.maximumPlayers ?? null
    },
    status: selectedSeason?.status || tournament.status || 'Upcoming',
    auctionEvents: (workspace.auctionEvents ?? []).filter((event) => !seasonId || event.seasonId === seasonId),
    teams: workspace.teams.filter((team) => !seasonId || team.seasonId === seasonId).map((team) => ({
      id: team.id,
      seasonId: team.seasonId,
      name: team.name,
      shortName: team.shortName,
      displayName: team.displayName,
      captain: team.captain,
      city: team.city,
      logoDataUrl: team.logoDataUrl,
      purse: team.purse,
      initialPurse: team.initialPurse,
      color: team.color
    })),
    players: workspace.players.filter((player) => !seasonId || player.seasonId === seasonId).map((player) => ({
      id: player.id,
      seasonId: player.seasonId,
      name: player.name,
      city: player.city,
      email: player.email,
      nationality: player.nationality,
      dateOfBirth: player.dateOfBirth,
      photoDataUrl: player.photoDataUrl,
      role: player.role,
      basePrice: player.basePrice,
      status: player.status,
      teamId: player.teamId,
      soldPrice: player.soldPrice
    })),
    bids: []
  };
}

export function useAuctionData() {
  const context = useContext(AuctionDataContext);
  if (!context) throw new Error('useAuctionData must be used within AuctionDataProvider');
  return context;
}

export function formatMoney(amount, currency = 'INR') {
  const locales = currency === 'INR' ? 'en-IN' : 'en';
  return new Intl.NumberFormat(locales, { style: 'currency', currency, maximumFractionDigits: 0 }).format(amount ?? 0);
}
