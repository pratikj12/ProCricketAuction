export const selectCurrentPlayer = (auction) => auction?.currentPlayer ?? null;
export const selectLeadingBid = (auction) => auction?.currentBid ?? null;
export const selectAuctionStatus = (auction) => auction?.status ?? 'Unknown';
