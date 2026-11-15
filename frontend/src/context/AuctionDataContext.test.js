import { describe, expect, it } from 'vitest';
import { auctionDataReducer, initialAuctionData, removeSampleAuctionData, workspaceToAuctionData } from './AuctionDataContext.jsx';

const auctionData = {
  ...initialAuctionData,
  tournament: { id: 'test-tournament', name: 'Test Cup', season: '2026', venue: 'Test Ground' },
  currentPlayerId: 'player-1',
  players: [{ id: 'player-1', name: 'Test Batter', basePrice: 10000, status: 'Available', teamId: null, soldPrice: null }],
  teams: [
    { id: 'team-1', name: 'Test Team One', shortName: 'ONE', captain: 'Captain One', purse: 100000, color: '#ff6f00' },
    { id: 'team-2', name: 'Test Team Two', shortName: 'TWO', captain: 'Captain Two', purse: 100000, color: '#60a5fa' }
  ]
};

describe('auction demo state transitions', () => {
  it('loads sale totals, purse balances, and event status from the saved workspace only', () => {
    const workspace = {
      tournament: { id: 'tournament-1', name: 'Saved Cup', season: '2026', currencyCode: 'INR' },
      seasons: [{ id: 'season-1', name: '2026', status: 'Upcoming' }],
      teams: [{
        id: 'team-1',
        seasonId: 'season-1',
        name: 'Saved Team',
        shortName: 'SAV',
        purse: 150000,
        initialPurse: 200000,
        color: '#123456'
      }],
      players: [{
        id: 'player-1',
        seasonId: 'season-1',
        name: 'Sold Player',
        status: 'Sold',
        teamId: 'team-1',
        soldPrice: 50000
      }],
      auctionEvents: [{
        id: 'auction-1',
        seasonId: 'season-1',
        name: 'Final Auction',
        status: 'Completed'
      }]
    };

    const state = workspaceToAuctionData(workspace, 'season-1');

    expect(state.players).toMatchObject([{ id: 'player-1', status: 'Sold', soldPrice: 50000 }]);
    expect(state.teams).toMatchObject([{ id: 'team-1', purse: 150000, initialPurse: 200000 }]);
    expect(state.auctionEvents).toEqual(workspace.auctionEvents);
    expect(state.bids).toEqual([]);
  });

  it('accepts increasing bids from different teams and sells once', () => {
    const live = auctionDataReducer(auctionData, { type: 'start' });
    const firstBid = auctionDataReducer(live, { type: 'bid', teamId: 'team-1' });
    const sameTeamBid = auctionDataReducer(firstBid, { type: 'bid', teamId: 'team-1' });
    const nextBid = auctionDataReducer(sameTeamBid, { type: 'bid', teamId: 'team-2' });
    const sold = auctionDataReducer(nextBid, { type: 'finish-player', sold: true });
    const repeatedSale = auctionDataReducer(sold, { type: 'finish-player', sold: true });

    expect(firstBid.currentBid).toBe(10000);
    expect(sameTeamBid).toBe(firstBid);
    expect(nextBid.currentBid).toBe(12000);
    expect(sold.players[0]).toMatchObject({ status: 'Sold', teamId: 'team-2', soldPrice: 12000 });
    expect(sold.teams.find((team) => team.id === 'team-2').purse).toBe(88000);
    expect(repeatedSale).toBe(sold);
  });

  it('allows a captain to jump above the minimum bid', () => {
    const live = auctionDataReducer(auctionDataReducer(auctionData, { type: 'start' }), { type: 'bid', teamId: 'team-1' });
    const jumpBid = auctionDataReducer(live, { type: 'bid', teamId: 'team-2', amount: 50000 });
    const belowMinimum = auctionDataReducer(live, { type: 'bid', teamId: 'team-2', amount: 11999 });

    expect(jumpBid.currentBid).toBe(50000);
    expect(jumpBid.leadingTeamId).toBe('team-2');
    expect(belowMinimum).toBe(live);
  });

  it('accepts a direct 20000 bid after a 4000 opening bid', () => {
    const lowerBasePrice = {
      ...auctionData,
      players: [{ ...auctionData.players[0], basePrice: 4000 }]
    };
    const firstBid = auctionDataReducer(
      auctionDataReducer(lowerBasePrice, { type: 'start' }),
      { type: 'bid', teamId: 'team-1' }
    );
    const directBid = auctionDataReducer(firstBid, { type: 'bid', teamId: 'team-2', amount: 20000 });

    expect(firstBid.currentBid).toBe(4000);
    expect(directBid.currentBid).toBe(20000);
    expect(directBid.leadingTeamId).toBe('team-2');
  });

  it('rejects bids when auction is not live or purse is insufficient', () => {
    expect(auctionDataReducer(auctionData, { type: 'bid', teamId: 'team-1' })).toBe(auctionData);

    const live = auctionDataReducer(auctionData, { type: 'start' });
    const emptyPurse = {
      ...live,
      teams: live.teams.map((team) => team.id === 'team-1' ? { ...team, purse: 0 } : team)
    };

    expect(auctionDataReducer(emptyPurse, { type: 'bid', teamId: 'team-1' })).toBe(emptyPurse);
  });

  it('adds a team with the supplied squad and purse details', () => {
    const team = { name: 'Pune Panthers', shortName: 'PUN', captain: 'Aarav Patel', purse: 75000, color: '#60a5fa' };
    const result = auctionDataReducer(auctionData, { type: 'add-team', team });

    expect(result.teams).toHaveLength(auctionData.teams.length + 1);
    expect(result.teams.at(-1)).toMatchObject(team);
    expect(result.teams.at(-1).id).toEqual(expect.any(String));
  });

  it('deducts a persisted squad purchase from the assigned team purse', () => {
    const player = {
      id: 'player-2',
      name: 'Purchased Player',
      status: 'Sold',
      teamId: 'team-1',
      soldPrice: 25000
    };
    const result = auctionDataReducer(auctionData, { type: 'add-player-persisted', player });

    expect(result.players.at(-1)).toEqual(player);
    expect(result.teams.find((team) => team.id === 'team-1').purse).toBe(75000);
    expect(result.teams.find((team) => team.id === 'team-2').purse).toBe(100000);
  });

  it('starts without sample tournament, team, or player records', () => {
    expect(initialAuctionData.tournament.name).toBe('');
    expect(initialAuctionData.players).toEqual([]);
    expect(initialAuctionData.teams).toEqual([]);
  });

  it('removes only built-in sample records and keeps organizer-created data', () => {
    const migrated = removeSampleAuctionData({
      ...auctionData,
      bidIncrement: 5000,
      players: [...auctionData.players, { id: 'p1', name: 'Sample Batter' }],
      teams: [...auctionData.teams, { id: 't1', name: 'Sample Team' }],
      bids: [{ id: 'sample-bid', playerId: 'p1', teamId: 't1' }]
    });

    expect(migrated.players.map((player) => player.id)).toEqual(['player-1']);
    expect(migrated.teams.map((team) => team.id)).toEqual(['team-1', 'team-2']);
    expect(migrated.bids).toEqual([]);
    expect(migrated.bidIncrement).toBe(2000);
    expect(migrated.tournament).toMatchObject({
      ...auctionData.tournament,
      currencyCode: 'INR',
      maximumTeams: null,
      maximumPlayers: null,
      seasonId: null
    });
  });

  it('clears the built-in sample tournament during migration', () => {
    const migrated = removeSampleAuctionData({
      ...initialAuctionData,
      tournament: { id: 'summer-cup', name: 'Summer Premier League', season: '2026', venue: 'The Oval Ground' },
      players: [{ id: 'p1', name: 'Sample Batter' }],
      teams: [{ id: 't1', name: 'Sample Team' }]
    });

    expect(migrated.tournament).toEqual(initialAuctionData.tournament);
    expect(migrated.players).toEqual([]);
    expect(migrated.teams).toEqual([]);
  });

  it('assigns the first newly added player as the auction selection', () => {
    const result = auctionDataReducer({ ...initialAuctionData }, {
      type: 'add-player',
      player: { name: 'New Batter', basePrice: 10000 }
    });

    expect(result.currentPlayerId).toBe(result.players[0].id);
  });

  it('deletes the active tournament and its associated demo data', () => {
    const liveAuction = {
      ...auctionData,
      status: 'Paused',
      currentBid: 15000,
      leadingTeamId: 'team-1',
      bids: [{ id: 'bid-1', playerId: 'player-1', teamId: 'team-1', amount: 15000 }]
    };

    expect(auctionDataReducer(liveAuction, { type: 'delete-tournament' })).toEqual(initialAuctionData);
  });
});
