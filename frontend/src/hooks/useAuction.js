import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import auctionService from '../services/auctionService.js';
import useSignalR from './useSignalR.js';

function getErrorMessage(error) {
  return error.response?.data?.detail
    || error.response?.data?.title
    || error.message
    || 'The auction request could not be completed.';
}

function hasPurchaseChanges(previous, next) {
  if (!previous) return false;
  const previousTeams = previous.teams.map((team) => `${team.id}:${team.purseRemaining}`).join('|');
  const nextTeams = next.teams.map((team) => `${team.id}:${team.purseRemaining}`).join('|');
  const previousPlayers = previous.players.map((player) =>
    `${player.id}:${player.status}:${player.teamId}:${player.soldPrice}`).join('|');
  const nextPlayers = next.players.map((player) =>
    `${player.id}:${player.status}:${player.teamId}:${player.soldPrice}`).join('|');
  return previousTeams !== nextTeams || previousPlayers !== nextPlayers;
}

export default function useAuction(auctionId, { publicView = false } = {}) {
  const [auction, setAuction] = useState(null);
  const [loading, setLoading] = useState(Boolean(auctionId));
  const [error, setError] = useState('');
  const [pendingAction, setPendingAction] = useState('');
  const previousState = useRef(null);
  const [clock, setClock] = useState(Date.now());

  const applyState = useCallback((incoming) => {
    if (!incoming) return;
    const previous = previousState.current;
    const next = incoming.assignedTeamIds?.length
      ? incoming
      : previous?.assignedTeamIds?.length
        ? { ...incoming, assignedTeamId: previous.assignedTeamId, assignedTeamIds: previous.assignedTeamIds }
        : incoming;
    if (!publicView && hasPurchaseChanges(previous, next)) {
      window.dispatchEvent(new CustomEvent('auction-purchase-committed', {
        detail: { tournamentId: next.tournamentId, seasonId: next.seasonId }
      }));
    }
    previousState.current = next;
    setAuction(next);
    setError('');
  }, [publicView]);

  const refresh = useCallback(async () => {
    if (!auctionId) return null;
    try {
      const state = publicView
        ? await auctionService.getPublicState(auctionId)
        : await auctionService.getState(auctionId);
      applyState(state);
      return state;
    } catch (requestError) {
      setError(getErrorMessage(requestError));
      throw requestError;
    } finally {
      setLoading(false);
    }
  }, [applyState, auctionId, publicView]);

  const { connected, connectionError } = useSignalR(auctionId, applyState, refresh, { publicView });

  useEffect(() => {
    previousState.current = null;
    setAuction(null);
    setLoading(Boolean(auctionId));
    setError('');
    if (auctionId) void refresh().catch(() => {});
  }, [auctionId, refresh]);

  useEffect(() => {
    const timer = window.setInterval(() => setClock(Date.now()), 250);
    return () => window.clearInterval(timer);
  }, []);

  const runAction = useCallback(async (name, action) => {
    setPendingAction(name);
    setError('');
    try {
      const next = await action();
      applyState(next);
      return next;
    } catch (requestError) {
      setError(getErrorMessage(requestError));
      try {
        await refresh();
      } catch {
        // Keep the command failure visible even if a recovery fetch also fails.
      }
      throw requestError;
    } finally {
      setPendingAction('');
    }
  }, [applyState, refresh]);

  const actions = useMemo(() => ({
    start: () => runAction('start', () => auctionService.start(auctionId)),
    pause: () => runAction('pause', () => auctionService.pause(auctionId)),
    resume: () => runAction('resume', () => auctionService.resume(auctionId)),
    presentPlayer: (playerId) => runAction('present', () => auctionService.presentPlayer(auctionId, playerId)),
    nextPlayer: () => runAction('next-player', () => auctionService.nextPlayer(auctionId)),
    skipPlayer: () => runAction('skip', () => auctionService.skipPlayer(auctionId)),
    markSold: (expectedRevision) => runAction('sold', () => auctionService.markSold(auctionId, expectedRevision)),
    markUnsold: () => runAction('unsold', () => auctionService.markUnsold(auctionId)),
    startNextRound: () => runAction('next-round', () => auctionService.startNextRound(auctionId)),
    placeBid: (bid) => runAction('bid', () => auctionService.placeBid(auctionId, bid)),
    reverseSale: (correction) => runAction('reverse-sale', () => auctionService.reverseSale(auctionId, correction))
  }), [auctionId, runAction]);

  const remainingMilliseconds = auction?.bidDeadline
    ? Math.max(0, new Date(auction.bidDeadline).getTime() - clock)
    : Math.max(0, auction?.pausedRemainingMilliseconds ?? 0);

  return {
    auction,
    loading,
    error,
    pendingAction,
    connected,
    connectionError,
    remainingMilliseconds,
    remainingSeconds: Math.ceil(remainingMilliseconds / 1000),
    refresh,
    ...actions
  };
}
