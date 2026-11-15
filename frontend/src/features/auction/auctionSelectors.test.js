import { describe, expect, it } from 'vitest';
import {
  selectAuctionStatus,
  selectCurrentPlayer,
  selectLeadingBid
} from './auctionSelectors.js';

describe('auction selectors', () => {
  it('returns the current auction values', () => {
    const player = { id: 'player-1' };
    const bid = { amount: 100 };
    const auction = { status: 'Live', currentPlayer: player, currentBid: bid };

    expect(selectCurrentPlayer(auction)).toBe(player);
    expect(selectLeadingBid(auction)).toBe(bid);
    expect(selectAuctionStatus(auction)).toBe('Live');
  });

  it('provides safe defaults before auction data is loaded', () => {
    expect(selectCurrentPlayer(null)).toBeNull();
    expect(selectLeadingBid(undefined)).toBeNull();
    expect(selectAuctionStatus(null)).toBe('Unknown');
  });
});
